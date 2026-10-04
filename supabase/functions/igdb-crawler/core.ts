export type SourceGame = {
  id: number;
  updated_at: number;
  checksum: string | null;
  name: string;
  developer: string | null;
  publisher: string | null;
  summary: string | null;
  publish_date: string | null;
  /** IGDB Unix seconds; used only for the release-date discovery cursor. */
  release_at: number | null;
  is_dlc: boolean;
  poster_url: string | null;
  /** Steam header CDN URL, derived from IGDB's linked Steam app id. */
  banner_url: string | null;
  parent_id: number | null;
  series: string | null;
  genres: { id: number; name: string }[];
  themes: { id: number; name: string }[];
};
type ObjectValue = Record<string, unknown>;
function object(value: unknown): ObjectValue {
  if (!value || typeof value !== "object" || Array.isArray(value)) {
    throw new Error("IGDB_INVALID_OBJECT");
  }
  return value as ObjectValue;
}
function positive(value: unknown): number {
  if (!Number.isSafeInteger(value) || Number(value) <= 0) {
    throw new Error("IGDB_INVALID_ID");
  }
  return Number(value);
}
function optionalText(value: unknown): string | null {
  if (value == null) return null;
  if (typeof value !== "string") throw new Error("IGDB_INVALID_TEXT");
  return value.trim() || null;
}
function named(value: unknown): { id: number; name: string }[] {
  if (value == null) return [];
  if (!Array.isArray(value)) throw new Error("IGDB_INVALID_RELATION");
  return value.map((v) => {
    const item = object(v);
    const name = optionalText(item.name);
    if (!name) throw new Error("IGDB_MISSING_RELATION_NAME");
    return { id: positive(item.id), name };
  }).sort((a, b) => a.id - b.id);
}

export const fields =
  "fields id,name,updated_at,checksum,summary,first_release_date," +
  "game_type.type,parent_game,cover.image_id,collections.name,genres.name,themes.name," +
  "external_games.category,external_games.uid,external_games.external_game_source.name," +
  "involved_companies.developer,involved_companies.publisher,involved_companies.company.name;";

export function normalize(value: unknown): SourceGame {
  const v = object(value);
  const name = optionalText(v.name);
  if (!name || name.length > 300) throw new Error("IGDB_INVALID_NAME");
  const type = object(v.game_type).type;
  if (typeof type !== "string") throw new Error("IGDB_MISSING_GAME_TYPE");
  const companies = v.involved_companies ?? [];
  if (!Array.isArray(companies)) throw new Error("IGDB_INVALID_COMPANIES");
  const companyNames = (role: string) =>
    [
      ...new Set(
        companies.filter((c) => object(c)[role] === true)
          .map((c) => optionalText(object(object(c).company).name)).filter((
            n,
          ): n is string => n !== null),
      ),
    ]
      .sort().join(", ") || null;
  const imageId = v.cover == null
    ? null
    : optionalText(object(v.cover).image_id);
  if (imageId && !/^[a-zA-Z0-9_-]+$/.test(imageId)) {
    throw new Error("IGDB_INVALID_IMAGE_ID");
  }
  const date = v.first_release_date == null
    ? null
    : Number(v.first_release_date);
  if (
    date !== null &&
    (!Number.isSafeInteger(date) || Math.abs(date) > 253402300799)
  ) {
    throw new Error("IGDB_INVALID_DATE");
  }
  const externalGames = v.external_games ?? [];
  if (!Array.isArray(externalGames)) throw new Error("IGDB_INVALID_EXTERNAL_GAMES");
  const steamAppId = externalGames
    .map((value) => object(value))
    .map((item) => {
      const legacyCategory = item.category;
      const source = item.external_game_source == null
        ? null
        : optionalText(object(item.external_game_source).name)?.toLowerCase();
      if (legacyCategory !== 1 && source !== "steam") return null;
      const uid = optionalText(item.uid);
      return uid && /^\d+$/.test(uid) && Number.isSafeInteger(Number(uid)) && Number(uid) > 0
        ? Number(uid)
        : null;
    })
    .filter((value): value is number => value !== null)
    .sort((a, b) => a - b)[0] ?? null;
  return {
    id: positive(v.id),
    updated_at: positive(v.updated_at),
    checksum: optionalText(v.checksum),
    name,
    developer: companyNames("developer"),
    publisher: companyNames("publisher"),
    summary: optionalText(v.summary),
    publish_date: date == null
      ? null
      : new Date(date * 1000).toISOString().slice(0, 10),
    release_at: date,
    is_dlc: [
      "dlc_addon",
      "dlc / addon",
      "dlc",
      "expansion",
      "episode",
      "season",
      "pack",
    ].includes(type.toLowerCase()),
    poster_url: imageId
      ? `https://images.igdb.com/igdb/image/upload/t_cover_big_2x/${imageId}.jpg`
      : null,
    // The crawler runs in Supabase, not on the user's machine. It consumes the
    // Steam app id from IGDB and calls the public asset CDN directly; it never
    // needs the geo/ISP-sensitive Steam Store search page.
    banner_url: steamAppId
      ? `https://cdn.akamai.steamstatic.com/steam/apps/${steamAppId}/header.jpg`
      : null,
    parent_id: v.parent_game == null ? null : positive(v.parent_game),
    series: named(v.collections)[0]?.name ?? null,
    genres: named(v.genres),
    themes: named(v.themes),
  };
}

export type Rpc = <T>(name: string, body?: ObjectValue) => Promise<T>;
type Targets = {
  ids: number[];
  discovery_id: number;
  release_cursor_at: number;
  skip?: string;
};
type Discovery = {
  inserted: number;
  cursor: number;
  releaseCursorAt: number;
  end: boolean;
  paused?: boolean;
};
export interface Provider {
  games(query: string): Promise<ProviderBatch>;
}
export type ProviderBatch = {
  games: SourceGame[];
  scannedThrough: number | null;
  scannedReleaseAt: number | null;
  rejectedIds: number[];
};

// All state transitions live in the database; the function can disappear at any await.
export async function runCrawler(
  rpc: Rpc,
  provider: Provider,
  dryRun: boolean,
  runId: string,
) {
  const targets = await rpc<Targets>(
    dryRun ? "crawler_targets" : "crawler_claim",
    dryRun ? {} : { p_run: runId },
  );
  if (targets.skip) return { skipped: targets.skip };
  const report = {
    checked: 0,
    observed: 0,
    inserted: 0,
    refreshed: 0,
    pending: 0,
    errors: 0,
    sourceRejected: 0,
  };
  let databaseErrors = 0;
  const mergeConsume = (
    value: { refreshed: number; pending: number; errors: number },
  ) => {
    report.refreshed = value.refreshed;
    report.pending = value.pending;
    databaseErrors = value.errors;
    report.errors = databaseErrors + report.sourceRejected;
  };
  try {
    // Drain already observed changes even when IGDB is temporarily unavailable.
    if (!dryRun) {
      mergeConsume(await rpc("crawler_consume", { p_run: runId }));
    }
    if (targets.ids.length) {
      const ids = targets.ids.map(positive);
      const batch = await provider.games(
        `${fields} where id = (${ids.join(",")}); limit 500;`,
      );
      report.checked = ids.length;
      report.observed = batch.games.length;
      report.sourceRejected += batch.rejectedIds.length;
      report.errors = databaseErrors + report.sourceRejected;
      if (!dryRun) {
        await rpc("crawler_observe", {
          p_run: runId,
          p_ids: ids.filter((id) => !batch.rejectedIds.includes(id)),
          p_games: batch.games,
        });
        mergeConsume(await rpc("crawler_consume", { p_run: runId }));
      }
    }
    let cursor = targets.discovery_id;
    let releaseCursorAt = targets.release_cursor_at;
    const discoveryEndAt = Math.floor(Date.now() / 1000) + 180 * 24 * 60 * 60;
    for (
      let page = 0;
      page < (dryRun ? 1 : 5) && report.inserted < 100;
      page++
    ) {
      const batch = await provider.games(
        `${fields} where first_release_date <= ${discoveryEndAt} & ` +
          `first_release_date > ${releaseCursorAt} | ` +
          `first_release_date = ${releaseCursorAt} & id > ${cursor}; ` +
          `sort first_release_date asc; limit ${
          Math.min(20, 100 - report.inserted)
          };`,
      );
      report.sourceRejected += batch.rejectedIds.length;
      report.errors = databaseErrors + report.sourceRejected;
      if (dryRun) {
        return {
          dryRun: true,
          checked: report.checked,
          observed: report.observed,
          discoverySample: batch.games.length,
          sourceRejected: report.sourceRejected,
        };
      }
      const result = await rpc<Discovery>("crawler_discover_release", {
        p_run: runId,
        p_games: batch.games,
        p_cursor_at: releaseCursorAt,
        p_cursor_id: cursor,
        p_scanned_through_at: batch.scannedReleaseAt,
        p_scanned_through_id: batch.scannedThrough,
      });
      report.inserted = result.inserted;
      cursor = result.cursor;
      releaseCursorAt = result.releaseCursorAt;
      if (result.end || result.paused) break;
    }
    return await rpc("crawler_finish", {
      p_run: runId,
      p_status: report.errors ? "partial" : "completed",
      p_report: report,
    });
  } catch (error) {
    // Failed commits never advance a client-side cursor. No further IGDB work.
    if (!dryRun) {
      try {
        await rpc("crawler_finish", {
          p_run: runId,
          p_status: "failed",
          p_report: { error: safeError(error) },
        });
      } catch {
        /* DB may be read-only or the lease expired. Edge log remains the fallback. */
      }
    }
    throw error;
  }
}

export function safeError(error: unknown): string {
  const code = error instanceof Error ? error.message : "UNKNOWN_ERROR";
  // Never pass arbitrary upstream text, response bodies, or secrets into logs.
  return /^[A-Z][A-Z0-9_]{0,80}$/.test(code) ? code : "CRAWLER_REQUEST_FAILED";
}

export async function authorized(
  supplied: string | null,
  secret: string,
): Promise<boolean> {
  if (secret.length < 32 || !supplied) return false;
  const digest = async (s: string) =>
    new Uint8Array(
      await crypto.subtle.digest("SHA-256", new TextEncoder().encode(s)),
    );
  const [a, b] = await Promise.all([digest(supplied), digest(secret)]);
  let difference = 0;
  for (let i = 0; i < a.length; i++) difference |= a[i] ^ b[i];
  return difference === 0;
}
