/**
 * Client cho nhom theme + taxonomy theo chu so huu — spec 2026-10-04-theme-groups §5.
 *
 * Dat o `src/lib/` chu KHONG phai `src/lib/api/` (hop dong sinh tu openapi.yaml,
 * backend so huu — AGENTS.md §2). Cung cho voi `adminApi.ts`, `gameDetailApi.ts`.
 *
 * Hai "scope" dung chung mot shape:
 *   - "library": du lieu chung (chi doc) + du lieu ca nhan cua nguoi dung;
 *   - "admin":   du lieu chung, chi admin goi duoc (Admin Console).
 * Quyen sua tung muc lay tu `editable` do server tra — client KHONG tu suy ra.
 */
import type {
  GroupedThemes,
  TaxonomyItem,
  ThemeGroup,
  ThemeGroupWrite,
  ThemeOrderRequest,
} from "@/lib/api/domain";
import { httpRequest } from "./http";

export type TaxonomyScope = "library" | "admin";
export type TaggableKind = "genres" | "themes";

const base = (scope: TaxonomyScope) => (scope === "admin" ? "/api/admin/taxonomy" : "/api/taxonomy");

const query = (q?: string) => {
  const term = q?.trim();
  return term ? `?q=${encodeURIComponent(term)}` : "";
};

export const taxonomyApi = {
  groupedThemes: (scope: TaxonomyScope, q?: string) =>
    httpRequest<GroupedThemes>(`${base(scope)}/themes/grouped${query(q)}`),

  list: (scope: TaxonomyScope, kind: TaggableKind, q?: string) =>
    httpRequest<TaxonomyItem[]>(`${base(scope)}/${kind}${query(q)}`),

  create: (scope: TaxonomyScope, kind: TaggableKind, item: Pick<TaxonomyItem, "name"> & Partial<TaxonomyItem>) =>
    httpRequest<TaxonomyItem>(`${base(scope)}/${kind}`, {
      method: "POST",
      body: JSON.stringify({ id: 0, sortOrder: 0, iconData: null, ...item }),
    }),

  update: (scope: TaxonomyScope, kind: TaggableKind, item: TaxonomyItem) =>
    httpRequest<TaxonomyItem>(`${base(scope)}/${kind}/${item.id}`, {
      method: "PUT",
      body: JSON.stringify(item),
    }),

  remove: (scope: TaxonomyScope, kind: TaggableKind, id: number) =>
    httpRequest<void>(`${base(scope)}/${kind}/${id}`, { method: "DELETE" }),

  orderThemes: (scope: TaxonomyScope, request: ThemeOrderRequest) =>
    httpRequest<void>(`${base(scope)}/themes/order`, {
      method: "PUT",
      body: JSON.stringify(request),
    }),

  orderGenres: (ids: number[]) =>
    httpRequest<void>("/api/admin/taxonomy/genres/order", {
      method: "PUT",
      body: JSON.stringify({ ids }),
    }),

  createGroup: (scope: TaxonomyScope, body: ThemeGroupWrite) =>
    httpRequest<ThemeGroup>(`${base(scope)}/theme-groups`, {
      method: "POST",
      body: JSON.stringify(body),
    }),

  updateGroup: (scope: TaxonomyScope, id: number, body: ThemeGroupWrite) =>
    httpRequest<ThemeGroup>(`${base(scope)}/theme-groups/${id}`, {
      method: "PUT",
      body: JSON.stringify(body),
    }),

  removeGroup: (scope: TaxonomyScope, id: number) =>
    httpRequest<void>(`${base(scope)}/theme-groups/${id}`, { method: "DELETE" }),

  orderGroups: (scope: TaxonomyScope, ids: number[]) =>
    httpRequest<void>(`${base(scope)}/theme-groups/order`, {
      method: "PUT",
      body: JSON.stringify({ ids }),
    }),
};

/** Moi mutation taxonomy invalidate ca tien to ["taxonomy"] — cache cu cua picker genre/series dung chung tien to. */
export const groupedThemesKey = (scope: TaxonomyScope, q = "") => ["taxonomy", "grouped", scope, q] as const;
