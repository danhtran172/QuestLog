/**
 * Anh dan tu clipboard (Ctrl+V) vao o anh cua Editor.
 *
 * Anh di qua dung field anh co san duoi dang data URL; hang doi nen cua Host
 * dua no len Cloudinary va thay o do bang link Cloudinary (DOMAIN_RULES §12).
 * Thu nho truoc de chuoi gui di khong nang vai MB — chi doi kich thuoc, khong
 * dong toi noi dung.
 */

/** Lay file anh dau tien trong clipboard; `null` khi nguoi dung dan chu. */
export function clipboardImageFile(data: DataTransfer | null): File | null {
  if (!data) return null;
  for (const item of Array.from(data.items)) {
    if (item.kind === "file" && item.type.startsWith("image/")) return item.getAsFile();
  }
  return null;
}

/** Data URL do nguoi dung dan vao — o input khong in nguyen chuoi base64 ra. */
export function isInlineImage(value: string): boolean {
  return value.startsWith("data:image/");
}

/** Ve lai anh thanh JPEG, rong toi da `maxWidth` (khong phong to anh nho). */
export async function imageFileToDataUrl(file: File, maxWidth: number): Promise<string> {
  const url = URL.createObjectURL(file);
  try {
    const image = await new Promise<HTMLImageElement>((resolve, reject) => {
      const element = new Image();
      element.onload = () => resolve(element);
      element.onerror = () => reject(new Error("decode"));
      element.src = url;
    });
    const scale = Math.min(1, maxWidth / image.naturalWidth);
    const canvas = document.createElement("canvas");
    canvas.width = Math.round(image.naturalWidth * scale);
    canvas.height = Math.round(image.naturalHeight * scale);
    const context = canvas.getContext("2d");
    if (!context) throw new Error("canvas");
    // JPEG khong co kenh alpha: lot nen den de vung trong suot khong thanh mau la.
    context.fillStyle = "#000";
    context.fillRect(0, 0, canvas.width, canvas.height);
    context.drawImage(image, 0, 0, canvas.width, canvas.height);
    return canvas.toDataURL("image/jpeg", 0.9);
  } finally {
    URL.revokeObjectURL(url);
  }
}
