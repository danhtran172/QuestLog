import { useQuery } from "@tanstack/react-query";
import { api } from "./api/client";
import type { SearchScope, SortKey, Tab } from "./api/domain";
import { PAGE_SIZE } from "./api/domain";

export type LibraryQuery = {
  tab: Tab;
  q: string;
  scope: SearchScope;
  sort: SortKey;
  reverseSort: boolean;
  page: number;
  /** Tham so filter da quy doi san (xem FilterDialog.filterToQuery). */
  filter?: Record<string, unknown>;
};

/**
 * Lay danh sach game.
 *
 * Toan bo loc / sort / phan trang CHAY O SERVER — hook nay chi gui query va
 * tra ket qua. Khong duoc them bat ky phep loc/sort nao o day (AGENTS.md).
 */
export function useGames(query: LibraryQuery, enabled = true) {
  return useQuery({
    queryKey: ["games", query],
    queryFn: () =>
      api.games({
        tab: query.tab,
        q: query.q || undefined,
        scope: query.scope,
        sort: query.sort,
        reverseSort: query.reverseSort,
        page: query.page,
        pageSize: PAGE_SIZE,
        ...(query.filter ?? {}),
      }),
    enabled,
    staleTime: 30_000,
  });
}
