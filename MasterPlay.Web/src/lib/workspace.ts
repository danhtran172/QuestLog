/**
 * WORKSPACE MODE — xem docs/superpowers/specs/2026-09-14-admin-console-mode-design.md §2.
 *
 * `role` (admin | user) va `workspace mode` (library | adminConsole) la HAI KHAI
 * NIEM DOC LAP. Role den tu JWT va bat bien trong phien; mode la state UI cua
 * phien va KHONG persist.
 *
 * Mode la rao chan UX, KHONG phai rao chan bao mat: access token cua admin van du
 * quyen goi endpoint admin ke ca khi UI dang o `library`. Moi endpoint admin phai
 * tu kiem tra role tu JWT.
 *
 * Khong ghi localStorage (AGENTS.md §2) va khong ghi /api/settings: reload hay
 * dang nhap lai deu ve `library`. Mode "dinh" qua reload la cach de nhat de admin
 * vo tinh sua metadata dung chung.
 */

export type WorkspaceMode = "library" | "adminConsole";

/**
 * Section cua Admin Console. Thu tu nay la thu tu hien thi va thu tu Ctrl+1..3.
 *
 * `taxonomy` la bo genre/theme MANUAL dung chung (`system:shared`, co icon) va nhom
 * theme — spec 2026-10-04-theme-groups D2. Van KHONG co cho sua `metadata_genres` /
 * `metadata_themes`: hai bang do do tien trinh sync ghi (spec 09-14 §4.4).
 *
 * `publishing` dat TRUOC `dashboard`: no la mot man hinh GHI, con dashboard chi
 * doc. Cac muc ghi nam lien nhau.
 */
export const ADMIN_SECTIONS = ["games", "series", "taxonomy", "publishing", "integrations", "crawler", "dashboard"] as const;

export type AdminSection = (typeof ADMIN_SECTIONS)[number];

export const DEFAULT_ADMIN_SECTION: AdminSection = "games";

/**
 * Che do xem cua section Games — the hoac danh sach, giong thu vien.
 *
 * Cung ly do khong persist nhu `WorkspaceMode`: day la trang thai UI cua phien,
 * khong phai Settings dung chung voi Tray (AGENTS.md §2). Thu vien cung giu
 * `layout` bang `useState` chu khong ghi dau ca.
 */
export type AdminLayout = "card" | "list";

export const DEFAULT_ADMIN_LAYOUT: AdminLayout = "card";
