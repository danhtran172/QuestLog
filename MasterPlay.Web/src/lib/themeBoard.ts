/**
 * Trang thai bang keo tha nhom theme — ham thuan, khong React.
 * Spec 2026-10-04-theme-groups §6.1.
 *
 * KHONG sap xep / loc du lieu domain: thu tu ban dau la thu tu server tra. O day
 * chi ap mot thao tac keo tha nguoi dung vua lam, roi dung lai request gui len.
 */

export type BoardTheme = { id: number; editable?: boolean };
export type BoardBucket<T extends BoardTheme = BoardTheme> = {
  group: { id: number } | null;
  themes: T[];
};

/** Khoa on dinh cua mot nhom tren bang; "none" = chua phan nhom. */
export const bucketKey = (bucket: BoardBucket) => (bucket.group ? `g${bucket.group.id}` : "none");

export const groupIdOfKey = (key: string): number | null => (key === "none" ? null : Number(key.slice(1)));

export function findBucketKey(board: BoardBucket[], themeId: number): string | null {
  const bucket = board.find((candidate) => candidate.themes.some((theme) => theme.id === themeId));
  return bucket ? bucketKey(bucket) : null;
}

/**
 * Dua theme `themeId` vao nhom `toKey`, dung TRUOC theme `beforeId` (null = cuoi
 * nhom). Tra ve bang moi; bang cu khong bi sua. Theme khong sua duoc thi giu nguyen.
 */
export function moveTheme<B extends BoardBucket>(
  board: B[],
  themeId: number,
  toKey: string,
  beforeId: number | null,
): B[] {
  const fromKey = findBucketKey(board, themeId);
  if (!fromKey || !board.some((bucket) => bucketKey(bucket) === toKey)) return board;
  const theme = board.flatMap((bucket) => bucket.themes).find((candidate) => candidate.id === themeId)!;
  if (theme.editable === false) return board;

  return board.map((bucket) => {
    const key = bucketKey(bucket);
    let themes = key === fromKey ? bucket.themes.filter((candidate) => candidate.id !== themeId) : bucket.themes;
    if (key === toKey) {
      const index = beforeId === null ? -1 : themes.findIndex((candidate) => candidate.id === beforeId);
      themes = index < 0 ? [...themes, theme] : [...themes.slice(0, index), theme, ...themes.slice(index)];
    }
    return themes === bucket.themes ? bucket : { ...bucket, themes };
  });
}

/**
 * Request `PUT …/themes/order` cho nhom `key`: CHI theme sua duoc, dung thu tu
 * dang hien. Theme chung nam xen giua khong vao `ids` va khong bi dong toi.
 */
export function orderRequest(board: BoardBucket[], key: string) {
  const bucket = board.find((candidate) => bucketKey(candidate) === key);
  return {
    groupId: groupIdOfKey(key),
    ids: (bucket?.themes ?? []).filter((theme) => theme.editable !== false).map((theme) => theme.id),
  };
}

/** Doi cho nhom `activeId` toi vi tri cua nhom `overId` (chi trong cac nhom sua duoc). */
export function moveGroup<G extends { id: number; editable?: boolean }>(groups: G[], activeId: number, overId: number): G[] {
  const from = groups.findIndex((group) => group.id === activeId);
  const to = groups.findIndex((group) => group.id === overId);
  if (from < 0 || to < 0 || from === to) return groups;
  if (groups[from].editable === false || groups[to].editable === false) return groups;
  const next = [...groups];
  const [moved] = next.splice(from, 1);
  next.splice(to, 0, moved);
  return next;
}
