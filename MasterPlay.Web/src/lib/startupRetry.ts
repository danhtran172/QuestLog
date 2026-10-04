/**
 * Thu lai cac request luc khoi dong khi Local API chua san sang.
 *
 * Desktop: run-web.cmd khoi dong lai Host moi lan mo app; neu Vite da chay san thi
 * proxy tra 500 rong cho toi khi Host len. Loi mang va 5xx duoc thu lai trong mot
 * khoang ngan; loi 4xx (401, 423...) tra ve ngay vi thu lai khong doi ket qua.
 */
import { isTransientError } from "./googleAuthFlow.ts";

export const STARTUP_RETRY_DELAYS_MS = [400, 800, 1200, 1600, 2000, 2000, 2000, 2000, 2000, 2000, 2000];

export async function withStartupRetry<T>(
  operation: () => Promise<T>,
  wait: (ms: number) => Promise<void> = (ms) => new Promise((resolve) => setTimeout(resolve, ms)),
  delays: readonly number[] = STARTUP_RETRY_DELAYS_MS,
): Promise<T> {
  for (let attempt = 0; ; attempt++) {
    try {
      return await operation();
    } catch (error) {
      if (!isTransientError(error) || attempt >= delays.length) throw error;
      await wait(delays[attempt]);
    }
  }
}
