import type { paths } from "@/lib/api/types.gen";
import type { GameListItem } from "@/lib/api/domain";
import { httpRequest } from "./http";

type QueueOrderBody = NonNullable<
  paths["/api/queue/order"]["put"]["requestBody"]
>["content"]["application/json"];

export const queueApi = {
  get: () => httpRequest<GameListItem[]>("/api/queue"),
  reorder: (gameIds: QueueOrderBody["gameIds"]) =>
    httpRequest<GameListItem[]>("/api/queue/order", {
      method: "PUT",
      body: JSON.stringify({ gameIds } satisfies QueueOrderBody),
    }),
};
