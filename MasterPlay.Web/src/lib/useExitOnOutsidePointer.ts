import { useEffect, useRef, type RefObject } from "react";
import { PORTAL_SCROLL_ROOT_SELECTOR } from "./usePortalWheelScroll";

/**
 * Bam ra ngoai mot hang sua tai cho = ket thuc viec sua.
 *
 * Nghe o pha capture cua `document` chu khong dung `onBlur`: mot hang sua tai
 * cho gom nhieu nut roi rac, di tu nut no sang nut kia da lam `blur` no len roi.
 *
 * Popover cam ra `<body>` nen nam NGOAI cay DOM cua hang — bam trong no khong
 * phai la "ra ngoai". Nhan ra chung qua dau `PORTAL_SCROLL_ROOT_SELECTOR`.
 *
 * Con hop thoai modal cua chinh hang thi tat `active` trong luc no mo: hop do
 * phu kin man hinh, bam dau cung khong phai y dinh thoat che do sua.
 *
 * @param active bat khi dang sua va khong co hop thoai nao cua hang dang mo.
 * @param containerRef the bao quanh hang. Dung duoc ca voi `display: contents`
 *   vi `contains` doc cay DOM, khong doc bo cuc.
 * @param onExit goi khi bam ra ngoai. Nhan ban MOI NHAT moi luot ve, khong can
 *   on dinh dong kin.
 */
export function useExitOnOutsidePointer(
  active: boolean,
  containerRef: RefObject<HTMLElement | null>,
  onExit: () => void,
) {
  const exitRef = useRef(onExit);
  exitRef.current = onExit;

  useEffect(() => {
    if (!active) return;

    const onPointerDown = (event: PointerEvent) => {
      const target = event.target as Element | null;
      if (!target) return;
      if (containerRef.current?.contains(target)) return;
      if (target.closest?.(PORTAL_SCROLL_ROOT_SELECTOR)) return;
      exitRef.current();
    };

    document.addEventListener("pointerdown", onPointerDown, true);
    return () => document.removeEventListener("pointerdown", onPointerDown, true);
  }, [active, containerRef]);
}
