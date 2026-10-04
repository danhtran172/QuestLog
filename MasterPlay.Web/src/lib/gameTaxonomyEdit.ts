/**
 * Phep tinh thuan cho viec sua Genre/Theme CUA MOT GAME ngay tren the chi tiet.
 *
 * Tach khoi component de kiem thu duoc: ba quyet dinh o day (them, bo, va co
 * duoc tao gia tri moi khong) deu de sai am tham neu chi nhin bang mat.
 */
import type { TaxonomyItem } from "./api/domain";

/** So ten khong phan biet hoa thuong va khoang trang thua — giong backend. */
export function normalizeTaxonomyName(value: string): string {
  return value.trim().toLowerCase();
}

/**
 * Them mot gia tri vao game. Trung id thi giu nguyen — bam hai lan khong duoc
 * sinh ra hai dong giong nhau roi day len PUT.
 */
export function addTaxonomyValue(
  current: readonly TaxonomyItem[],
  item: TaxonomyItem,
): TaxonomyItem[] {
  return current.some((value) => value.id === item.id) ? [...current] : [...current, item];
}

/** Bo mot gia tri khoi game. KHONG dung toi bang taxonomy dung chung. */
export function removeTaxonomyValue(
  current: readonly TaxonomyItem[],
  id: number,
): TaxonomyItem[] {
  return current.filter((value) => value.id !== id);
}

/**
 * Bat/tat mot gia tri trong BAN NHAP cua popover chon nhieu.
 *
 * Chi dung cho ban nhap — bo mot gia tri DA LUU phai di qua buoc hoi lai, con
 * bo thu vua tick trong cung mot lan mo popover thi khong can hoi ai.
 */
export function toggleTaxonomyValue(
  current: readonly TaxonomyItem[],
  item: TaxonomyItem,
): TaxonomyItem[] {
  return current.some((value) => value.id === item.id)
    ? removeTaxonomyValue(current, item.id)
    : addTaxonomyValue(current, item);
}

/** Gop ca ban nhap vao hang hien co — mot lan PUT cho ca lo vua tick. */
export function addTaxonomyValues(
  current: readonly TaxonomyItem[],
  items: readonly TaxonomyItem[],
): TaxonomyItem[] {
  return items.reduce<TaxonomyItem[]>((merged, item) => addTaxonomyValue(merged, item), [...current]);
}

/**
 * Chi cho tao gia tri moi khi ca hai dieu dung:
 *
 * 1. Ket qua dang hien la cua DUNG tu khoa vua go (`query === search`), chu
 *    khong phai ket qua cu con lai trong luc cho debounce — neu khong, nut
 *    "Tao" se hien dua tren mot danh sach khong lien quan.
 * 2. Trong ket qua chua co ten trung. Backend tra 409 TAXONOMY_NAME_EXISTS cho
 *    ten trung, nen chan tu day de nguoi dung khong bam vao mot loi biet truoc.
 */
export function canCreateTaxonomyName(
  search: string,
  query: string,
  results: readonly TaxonomyItem[] | undefined,
): boolean {
  const name = search.trim();
  if (!name || !results) return false;
  if (query !== name) return false;
  return !results.some((item) => normalizeTaxonomyName(item.name) === normalizeTaxonomyName(name));
}
