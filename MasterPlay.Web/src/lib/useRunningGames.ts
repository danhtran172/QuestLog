import { useEffect, useRef } from "react";
import { useQueryClient } from "@tanstack/react-query";
import { ApiRequestError, fetchWithSession } from "./api/client";
import type { components } from "./api/types.gen";
import type { ApiError } from "./api/domain";
import { applyRunningGames, createSseParser } from "./runningGames";

type RunningProcessesEvent = components["schemas"]["RunningProcessesEvent"];

const BASE_URL = import.meta.env.VITE_API_BASE_URL ?? "";
const RETRY_MIN_MS = 2_000;
const RETRY_MAX_MS = 30_000;
/** Hon hai nhip `: ping` (20s) ma khong nhan duoc byte nao. */
const STALE_AFTER_MS = 45_000;

/** Moi cache co game mang `isRunning`: thu vien (ca tab console), Play Queue, chi tiet. */
const RUNNING_QUERY_KEYS = [["games"], ["play-queue"], ["game-detail"]] as const;

async function toRequestError(response: Response): Promise<ApiRequestError> {
  let payload: ApiError | undefined;
  try {
    payload = (await response.json()) as ApiError;
  } catch {
    // Body khong phai JSON — van bao loi theo ma HTTP ben duoi.
  }
  return new ApiRequestError(
    response.status,
    payload?.error?.code ?? "UNKNOWN",
    payload?.error?.message ?? `HTTP ${response.status} ${response.statusText}`,
  );
}

/**
 * Giu mot ket noi SSE toi `GET /api/events` va chep `runningProcesses` vao `isRunning`
 * cua moi danh sach game dang cache — nut Play doc field do de doi sang "Playing".
 *
 * Doc bang fetch (can header Bearer, EventSource khong gui duoc). Rot ket noi thi tu noi
 * lai, gian cach tang dan toi 30s; moi chuoi loi bao `onError` MOT lan (khong nuot loi,
 * cung khong spam toast moi lan thu lai). Ket noi lai thanh cong thi Host gui ngay
 * snapshot moi, nen trang thai cu khong bi ket.
 */
export function useRunningGames(enabled: boolean, onError: (error: Error) => void) {
  const queryClient = useQueryClient();
  const onErrorRef = useRef(onError);
  onErrorRef.current = onError;

  useEffect(() => {
    if (!enabled) return;

    const abort = new AbortController();
    let retryMs = RETRY_MIN_MS;
    let failureReported = false;
    let retryTimer: ReturnType<typeof setTimeout> | undefined;

    const applyRunning = (gameIds: string[]) => {
      const running = new Set(gameIds);
      for (const queryKey of RUNNING_QUERY_KEYS) {
        queryClient.setQueriesData({ queryKey }, (data: unknown) =>
          data === undefined ? data : applyRunningGames(data, running));
      }
    };

    const connect = async () => {
      // Parser moi cho moi ket noi: manh su kien do dang cua ket noi cu khong duoc dinh sang.
      const parse = createSseParser((event, data) => {
        if (event !== "runningProcesses") return;
        const payload = JSON.parse(data) as RunningProcessesEvent;
        applyRunning(payload.gameIds);
      });

      // Host ping 20s mot lan. Im lang lau hon STALE_AFTER_MS la ket noi da chet ma khong
      // ai dong (proxy dev giu socket phia trinh duyet khi Host tat, may ngu day...).
      const connection = new AbortController();
      const stopConnection = () => connection.abort();
      abort.signal.addEventListener("abort", stopConnection);
      let staleTimer: ReturnType<typeof setTimeout> | undefined;
      let stale = false;
      const watchStale = () => {
        clearTimeout(staleTimer);
        staleTimer = setTimeout(() => {
          stale = true;
          connection.abort();
        }, STALE_AFTER_MS);
      };

      try {
        watchStale();
        const response = await fetchWithSession(`${BASE_URL}/api/events`, {
          headers: { Accept: "text/event-stream" },
          signal: connection.signal,
        });
        if (!response.ok || !response.body) throw await toRequestError(response);
        // Host cu (chua co /api/events) hay proxy tra trang HTML voi 200: doc tiep se im
        // lang mai mai, UI ket o "Launching". Phai bao loi de nguoi dung biet.
        if (!response.headers.get("Content-Type")?.startsWith("text/event-stream")) {
          await response.body.cancel();
          throw new Error(`/api/events did not return an event stream (${response.headers.get("Content-Type") ?? "no content type"}). Restart QuestLog after updating the Host.`);
        }

        retryMs = RETRY_MIN_MS;
        failureReported = false;
        const reader = response.body.pipeThrough(new TextDecoderStream()).getReader();
        for (;;) {
          const { value, done } = await reader.read();
          if (done) break;
          watchStale();
          parse(value);
        }
        // Host dong stream (khoi dong lai...) -> noi lai ben duoi, khong phai loi.
      } catch (error) {
        if (abort.signal.aborted) return;
        if (!failureReported) {
          failureReported = true;
          onErrorRef.current(stale
            ? new Error(`No response from /api/events for ${STALE_AFTER_MS / 1000}s.`)
            : error instanceof Error ? error : new Error(String(error)));
        }
      } finally {
        clearTimeout(staleTimer);
        abort.signal.removeEventListener("abort", stopConnection);
      }

      if (abort.signal.aborted) return;
      retryTimer = setTimeout(() => void connect(), retryMs);
      retryMs = Math.min(retryMs * 2, RETRY_MAX_MS);
    };

    void connect();
    return () => {
      abort.abort();
      clearTimeout(retryTimer);
    };
  }, [enabled, queryClient]);
}
