import {
  authorized,
  normalize,
  type ProviderBatch,
  runCrawler,
  safeError,
  type SourceGame,
} from "./core.ts";

let cachedToken: { token: string; until: number; revision: number } | undefined;
const delay = (ms: number) => new Promise((resolve) => setTimeout(resolve, ms));

function required(name: string): string {
  const value = Deno.env.get(name)?.trim();
  if (!value) throw new Error("CRAWLER_SECRET_MISSING");
  return value;
}

export function createProvider(
  clientId: string,
  clientSecret: string,
  deadline: number,
  credentialRevision: number,
) {
  let requests = 0;
  let lastRequest = 0;
  const boundedFetch = async (url: string, init: RequestInit) => {
    if (requests >= 20) throw new Error("CRAWLER_REQUEST_BUDGET");
    const wait = Math.max(0, lastRequest + 1000 - Date.now());
    if (Date.now() + wait + 1000 >= deadline) {
      throw new Error("CRAWLER_TIME_BUDGET");
    }
    await delay(wait);
    lastRequest = Date.now();
    requests++;
    return fetch(url, {
      ...init,
      signal: AbortSignal.timeout(Math.min(12000, deadline - Date.now())),
    });
  };
  const token = async () => {
    if (
      cachedToken &&
      cachedToken.revision === credentialRevision &&
      cachedToken.until > Date.now()
    ) return cachedToken.token;
    const response = await boundedFetch("https://id.twitch.tv/oauth2/token", {
      method: "POST",
      body: new URLSearchParams({
        client_id: clientId,
        client_secret: clientSecret,
        grant_type: "client_credentials",
      }),
    });
    if (!response.ok) {
      await response.body?.cancel();
      throw new Error(`TWITCH_HTTP_${response.status}`);
    }
    const value = await response.json();
    if (
      typeof value.access_token !== "string" ||
      typeof value.expires_in !== "number"
    ) throw new Error("TWITCH_INVALID_TOKEN");
    cachedToken = {
      token: value.access_token,
      until: Date.now() + Math.max(0, value.expires_in - 300) * 1000,
      revision: credentialRevision,
    };
    return value.access_token;
  };
  return {
    async games(query: string): Promise<ProviderBatch> {
      for (let attempt = 0; attempt < 3; attempt++) {
        const accessToken = await token();
        const response = await boundedFetch("https://api.igdb.com/v4/games", {
          method: "POST",
          headers: {
            "Client-ID": clientId,
            Authorization: `Bearer ${accessToken}`,
            "Content-Type": "text/plain",
          },
          body: query,
        });
        if (response.ok) {
          const raw = await response.json();
          if (!Array.isArray(raw)) throw new Error("IGDB_INVALID_RESPONSE");
          const games: SourceGame[] = [];
          const rejectedIds: number[] = [];
          let scannedThrough: number | null = null;
          let scannedReleaseAt: number | null = null;
          for (const value of raw) {
            const candidateId = value && typeof value === "object" &&
                Number.isSafeInteger((value as Record<string, unknown>).id) &&
                Number((value as Record<string, unknown>).id) > 0
              ? Number((value as Record<string, unknown>).id)
              : null;
            if (candidateId === null) {
              throw new Error("IGDB_INVALID_RESPONSE_ID");
            }
            const releaseAt = (value as Record<string, unknown>).first_release_date;
            if (Number.isSafeInteger(releaseAt) && Number(releaseAt) >= 0) {
              const releaseSeconds = Number(releaseAt);
              if (
                scannedReleaseAt === null || releaseSeconds > scannedReleaseAt ||
                (releaseSeconds === scannedReleaseAt && candidateId > (scannedThrough ?? 0))
              ) {
                scannedReleaseAt = releaseSeconds;
                scannedThrough = candidateId;
              }
            } else if (scannedReleaseAt === null) {
              scannedThrough = Math.max(scannedThrough ?? 0, candidateId);
            }
            try {
              games.push(normalize(value));
            } catch {
              rejectedIds.push(candidateId);
            }
          }
          return { games, scannedThrough, scannedReleaseAt, rejectedIds };
        }
        await response.body?.cancel();
        if (response.status === 401 && attempt === 0) {
          cachedToken = undefined;
          continue;
        }
        if (
          (response.status === 429 || response.status >= 500) && attempt < 2
        ) {
          const header = response.headers.get("Retry-After");
          const parsed = header
            ? (Number.isFinite(Number(header))
              ? Number(header) * 1000
              : Date.parse(header) - Date.now())
            : 0;
          const wait = Math.max(
            Number.isFinite(parsed) ? parsed : 0,
            1000 * 2 ** attempt + Math.random() * 250,
          );
          if (wait > 15000 || Date.now() + wait >= deadline) {
            throw new Error("IGDB_RETRY_DEFERRED");
          }
          await delay(wait);
          continue;
        }
        throw new Error(`IGDB_HTTP_${response.status}`);
      }
      throw new Error("IGDB_RETRIES_EXHAUSTED");
    },
  };
}

export async function handler(request: Request): Promise<Response> {
  if (request.method !== "POST") {
    return new Response("Method not allowed", { status: 405 });
  }
  if (
    !await authorized(
      request.headers.get("x-crawler-secret"),
      Deno.env.get("CRAWLER_SECRET") ?? "",
    )
  ) {
    return new Response("Unauthorized", { status: 401 });
  }
  const runId = crypto.randomUUID();
  try {
    const bodyText = await request.text();
    if (bodyText.length > 1024) {
      return new Response("Payload too large", { status: 413 });
    }
    let body;
    try {
      body = JSON.parse(bodyText || "{}");
    } catch {
      return new Response("Invalid JSON", { status: 400 });
    }
    if (
      !body || typeof body !== "object" || Array.isArray(body) ||
      Object.keys(body).some((k) => k !== "dryRun") ||
      (body.dryRun !== undefined && typeof body.dryRun !== "boolean")
    ) {
      return new Response("Expected {dryRun?: boolean}", { status: 400 });
    }
    const deadline = Date.now() + 100000;
    const url = required("SUPABASE_URL");
    const key = required("SUPABASE_SERVICE_ROLE_KEY");
    const rpc = async <T>(
      name: string,
      payload: Record<string, unknown> = {},
    ): Promise<T> => {
      const remaining = deadline - Date.now();
      if (remaining <= 0) throw new Error("CRAWLER_TIME_BUDGET");
      const response = await fetch(`${url}/rest/v1/rpc/${name}`, {
        method: "POST",
        headers: {
          apikey: key,
          Authorization: `Bearer ${key}`,
          "Content-Type": "application/json",
        },
        body: JSON.stringify(payload),
        signal: AbortSignal.timeout(Math.min(15000, remaining)),
      });
      if (!response.ok) {
        // Keep diagnostics secret-safe: PostgREST's SQLSTATE identifies the
        // failing class without persisting response details or payload data.
        const failure = await response.json().catch(() => null) as
          | { code?: unknown; message?: unknown }
          | null;
        const sqlState = typeof failure?.code === "string" &&
            /^[0-9A-Z]{5}$/.test(failure.code)
          ? `_${failure.code}`
          : "";
        const reason = typeof failure?.message === "string" &&
            failure.message.includes("cannot affect row a second time")
          ? "_DUPLICATE_UPSERT"
          : typeof failure?.message === "string" &&
              failure.message.includes("more than one row")
          ? "_MULTIPLE_ROWS"
          : "";
        const databaseMarker = typeof failure?.message === "string"
          ? failure.message.match(/CRAWLER_[A-Z0-9_]{1,60}/)?.[0]
          : undefined;
        const operation = `_${name.toUpperCase()}`;
        throw new Error(
          databaseMarker ??
            `SUPABASE_HTTP_${response.status}${sqlState}${reason}${operation}`,
        );
      }
      const text = await response.text();
      // PostgREST returns an empty body for the void observation RPC.
      return JSON.parse(text || "null");
    };
    let credentials: {
      revision?: unknown;
      values?: Record<string, unknown>;
    };
    try {
      credentials = await rpc("crawler_igdb_credentials");
    } catch (error) {
      /*
       * Existing deployments kept IGDB credentials as Edge secrets before the
       * optional Integration Console migration existed. Keep that deployment
       * working; a real credential/RPC error must still fail closed.
       */
      if (safeError(error) !== "SUPABASE_HTTP_404_CRAWLER_IGDB_CREDENTIALS") throw error;
      credentials = {
        revision: 1,
        values: {
          clientId: required("IGDB_CLIENT_ID"),
          clientSecret: required("IGDB_CLIENT_SECRET"),
        },
      };
    }
    // The integration console may be installed before its IGDB fields have
    // been configured. In that state, preserve the legacy Edge-secret path so
    // enabling the console cannot interrupt the scheduled crawler.
    const dbCredentialsAreComplete =
      typeof credentials?.revision === "number" &&
      Number.isSafeInteger(credentials.revision) &&
      credentials.revision > 0 &&
      typeof credentials?.values?.clientId === "string" &&
      !!credentials.values.clientId.trim() &&
      typeof credentials?.values?.clientSecret === "string" &&
      !!credentials.values.clientSecret.trim();
    if (!dbCredentialsAreComplete) {
      credentials = {
        revision: 1,
        values: {
          clientId: required("IGDB_CLIENT_ID"),
          clientSecret: required("IGDB_CLIENT_SECRET"),
        },
      };
    }
    const revision = credentials.revision;
    const clientId = credentials.values?.clientId;
    const clientSecret = credentials.values?.clientSecret;
    if (
      typeof revision !== "number" || !Number.isSafeInteger(revision) || revision <= 0 ||
      typeof clientId !== "string" || !clientId.trim() ||
      typeof clientSecret !== "string" || !clientSecret.trim()
    ) {
      throw new Error("CRAWLER_IGDB_NOT_CONFIGURED");
    }
    const result = await runCrawler(
      rpc,
      createProvider(clientId, clientSecret, deadline, revision),
      body.dryRun === true,
      runId,
    );
    console.log(JSON.stringify({ runId, result }));
    return Response.json(result);
  } catch (error) {
    const code = safeError(error);
    console.error(JSON.stringify({ runId, code }));
    return Response.json({ runId, error: code }, { status: 503 });
  }
}
