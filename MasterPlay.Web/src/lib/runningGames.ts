/**
 * Trang thai "game dang chay" day tu Host qua SSE `GET /api/events` (su kien
 * `runningProcesses`, payload `RunningProcessesEvent`).
 *
 * Client KHONG tu suy game nao dang chay (AGENTS.md: khong doan trang thai native):
 * chi chep `gameIds` backend gui vao field `isRunning` cua du lieu da cache.
 */

export type SseEventHandler = (event: string, data: string) => void;

/**
 * Doc tung manh text cua `text/event-stream` (theo spec SSE: dong trong ket thuc
 * mot su kien, `:` la chu thich/ping, nhieu dong `data:` noi bang xuong dong).
 * Tra ve ham nhan manh tiep theo — mot su kien co the bi cat giua hai manh.
 */
export function createSseParser(onEvent: SseEventHandler): (chunk: string) => void {
  let buffer = "";
  let event = "message";
  let data: string[] = [];

  const dispatch = () => {
    if (data.length > 0) onEvent(event, data.join("\n"));
    event = "message";
    data = [];
  };

  return (chunk) => {
    buffer += chunk;
    // Tach theo "\n" roi bo "\r" cuoi dong: CRLF bi cat giua hai manh van chi la mot dong.
    const lines = buffer.split("\n");
    buffer = lines.pop() ?? "";

    for (const rawLine of lines) {
      const line = rawLine.endsWith("\r") ? rawLine.slice(0, -1) : rawLine;
      if (line === "") {
        dispatch();
        continue;
      }
      if (line.startsWith(":")) continue;

      const colon = line.indexOf(":");
      const field = colon === -1 ? line : line.slice(0, colon);
      let value = colon === -1 ? "" : line.slice(colon + 1);
      if (value.startsWith(" ")) value = value.slice(1);

      if (field === "event") event = value;
      else if (field === "data") data.push(value);
    }
  };
}

type RunningFlag = { id: string; isRunning: boolean };

function withRunning<T extends RunningFlag>(game: T, running: ReadonlySet<string>): T {
  const isRunning = running.has(game.id);
  return game.isRunning === isRunning ? game : { ...game, isRunning };
}

function isRunningFlag(value: unknown): value is RunningFlag {
  return typeof value === "object" && value !== null &&
    typeof (value as RunningFlag).id === "string" &&
    typeof (value as RunningFlag).isRunning === "boolean";
}

function withRunningList<T>(list: T[], running: ReadonlySet<string>): T[] {
  let changed = false;
  const next = list.map((item) => {
    if (!isRunningFlag(item)) return item;
    const updated = withRunning(item, running);
    if (updated !== item) changed = true;
    return updated as T;
  });
  return changed ? next : list;
}

/**
 * Chep tap game dang chay vao du lieu cache: `GameListResponse` (`items`), mang game
 * (Play Queue) hoac mot `GameDetail`. Khong co gi doi thi tra lai dung tham chieu cu
 * de React Query khong ve lai vo ich. Hinh dang khac thi giu nguyen.
 */
export function applyRunningGames<T>(data: T, running: ReadonlySet<string>): T {
  if (Array.isArray(data)) return withRunningList(data, running) as T;
  if (isRunningFlag(data)) return withRunning(data, running) as T;

  if (typeof data === "object" && data !== null && Array.isArray((data as { items?: unknown }).items)) {
    const page = data as T & { items: unknown[] };
    const items = withRunningList(page.items, running);
    return items === page.items ? data : { ...page, items };
  }

  return data;
}
