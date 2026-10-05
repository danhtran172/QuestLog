// A deliberately independent worker: it enriches shared metadata candidates,
// never user library rows or local paths.
const code = (value: unknown) => value instanceof Error && /^[A-Z][A-Z0-9_]{0,80}$/.test(value.message)
  ? value.message : "GEMINI_EXE_REQUEST_FAILED";

function executableName(value: unknown): string | null {
  if (typeof value !== "string") return null;
  const name = value.trim();
  if (!/^[^\\/:*?"<>|]+\.exe$/i.test(name)) return null;
  return name.slice(0, -4);
}

async function handler(request: Request): Promise<Response> {
  if (request.method !== "POST") return new Response("Method not allowed", { status: 405 });
  const secret = Deno.env.get("GEMINI_EXE_SECRET") ?? "";
  if (!secret || request.headers.get("x-gemini-exe-secret") !== secret) return new Response("Unauthorized", { status: 401 });
  const url = Deno.env.get("SUPABASE_URL") ?? "";
  const serviceKey = Deno.env.get("SUPABASE_SERVICE_ROLE_KEY") ?? "";
  if (!url || !serviceKey) return Response.json({ error: "GEMINI_EXE_SECRET_MISSING" }, { status: 503 });
  const rpc = async <T>(name: string, body: Record<string, unknown> = {}) => {
    const response = await fetch(`${url}/rest/v1/rpc/${name}`, { method: "POST", headers: { apikey: serviceKey, Authorization: `Bearer ${serviceKey}`, "Content-Type": "application/json" }, body: JSON.stringify(body), signal: AbortSignal.timeout(10_000) });
    if (!response.ok) throw Error(`SUPABASE_HTTP_${response.status}`);
    return await response.json() as T;
  };
  const runId = crypto.randomUUID();
  try {
    const claimed = await rpc<{ skip?: string; runId?: string; games?: { id: number; name: string }[] }>("gemini_exe_claim", { p_run: runId });
    if (claimed.skip) return Response.json({ skipped: claimed.skip });
    const credential = await rpc<{ revision?: number; values?: { apiKey?: string } }>("gemini_exe_credentials");
    const apiKey = credential.values?.apiKey?.trim();
    if (!apiKey) throw Error("GEMINI_EXE_NOT_CONFIGURED");
    let errors = 0;
    for (const game of claimed.games ?? []) {
      try {
        const response = await fetch(`https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash:generateContent?key=${encodeURIComponent(apiKey)}`, {
          method: "POST", headers: { "Content-Type": "application/json" }, signal: AbortSignal.timeout(8_000),
          body: JSON.stringify({ contents: [{ parts: [{ text: `Return the primary Windows executable filename for the PC game '${game.name}'. Do not guess. Exclude launchers, installers and bootstrap executables. Return JSON only.` }] }], generationConfig: { responseMimeType: "application/json", responseSchema: { type: "OBJECT", properties: { executableName: { type: "STRING" } }, required: ["executableName"] } } }),
        });
        if (!response.ok) throw Error(`GEMINI_HTTP_${response.status}`);
        const body = await response.json() as { candidates?: { content?: { parts?: { text?: string }[] } }[] };
        const text = body.candidates?.[0]?.content?.parts?.[0]?.text;
        let candidate: string | null = null;
        try { candidate = executableName(JSON.parse(text ?? "{}").executableName); } catch { /* terminal no-candidate */ }
        await rpc("gemini_exe_record", { p_run: runId, p_game: game.id, p_file_name: candidate, p_model: "gemini-2.5-flash", p_error_code: null });
      } catch (error) {
        errors++;
        await rpc("gemini_exe_record", { p_run: runId, p_game: game.id, p_file_name: null, p_model: "gemini-2.5-flash", p_error_code: code(error) });
      }
    }
    return Response.json(await rpc("gemini_exe_finish", { p_run: runId, p_status: errors ? "partial" : "completed", p_report: { processed: (claimed.games ?? []).length } }));
  } catch (error) {
    try { await rpc("gemini_exe_finish", { p_run: runId, p_status: "failed", p_report: { error: code(error) } }); } catch { /* claim may not exist */ }
    return Response.json({ runId, error: code(error) }, { status: 503 });
  }
}

Deno.serve(handler);
