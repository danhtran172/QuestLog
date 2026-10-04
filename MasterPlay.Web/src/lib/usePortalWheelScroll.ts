import { useEffect } from "react";

/**
 * Danh dau khung tu cuon nam trong mot portal bi khoa cuon. Gan thang vao the:
 * `<div {...PORTAL_SCROLLER_PROPS} className="overflow-y-auto">`.
 */
export const PORTAL_SCROLLER_PROPS = { "data-portal-scroller": "" } as const;

/**
 * Danh dau CA khung noi dung cam ra portal (vi du `PopoverContent`). Co hai tac
 * dung: lan chuot o bat cu dau trong do cung cuon khung cuon ben trong — ke ca
 * khi con tro dang o tren o tim kiem hay phan le — va cac cho khac nhan ra
 * "diem nay van nam trong popover" du no khong nam trong cay DOM cua minh.
 */
export const PORTAL_SCROLL_ROOT_PROPS = { "data-portal-scroll-root": "" } as const;

export const PORTAL_SCROLL_ROOT_SELECTOR = "[data-portal-scroll-root]";

const PORTAL_SCROLLER_SELECTOR = "[data-portal-scroller]";

/**
 * Tra lai con lan chuot cho khung cuon nam trong portal, khi mot Dialog cua
 * Radix dang mo.
 *
 * Dialog khoa cuon bang `react-remove-scroll`: no nghe `wheel` o tang document
 * (capture, non-passive) va `preventDefault` moi su kien khong nam trong
 * `DialogContent`. Popover / danh sach ket qua lai duoc portal ra thang `<body>`,
 * tuc la NGOAI vung duoc phep — nen lan chuot trong do khong an gi.
 *
 * Khong sua bang cach doi noi cam portal vao trong dialog: `DialogContent` co
 * `-translate-x-1/2`, ma mot phan tu co `transform` tao ra containing block moi
 * cho con chau `position: fixed`. Doi cho cam la danh cuoc vao cach dat vi tri
 * cua popper — dat hon nhieu so voi viec tu cuon.
 *
 * Nghe o DOCUMENT chu khong phai o chinh khung cuon, va tim khung cuon LUC CO
 * SU KIEN chu khong luu san vao ref. Hai diem do tranh hai cach hong:
 *   - ref con `null` luc effect chay (noi dung portal mount qua Portal +
 *     Presence, khong chac xong trong cung mot commit) -> khong bao gio gan
 *     duoc listener;
 *   - mot listener khac goi `stopPropagation` trong pha capture -> listener gan
 *     tren phan tu se khong bao gio chay, con listener cung dat o document thi
 *     van chay (chi `stopImmediatePropagation` moi chan duoc).
 *
 * Phai la `addEventListener` voi `passive: false`: `onWheel` cua React duoc
 * dang ky o tang root duoi dang passive, `preventDefault` trong do vo hieu.
 *
 * @param active bat khi portal dang mo; tat thi khong gan listener nao.
 */
export function usePortalWheelScroll(active: boolean) {
  useEffect(() => {
    if (!active) return;

    const onWheel = (event: WheelEvent) => {
      const target = event.target as Element | null;
      /* Con tro o ngay tren khung cuon la xong. Neu khong, con tro van co the
         dang o cho khac trong cung popover (o tim kiem, nut, phan le) — luc do
         cuon khung cuon duy nhat ben trong, thay vi de su kien roi ra ngoai. */
      const scroller = (target?.closest?.(PORTAL_SCROLLER_SELECTOR)
        ?? target?.closest?.(PORTAL_SCROLL_ROOT_SELECTOR)?.querySelector(PORTAL_SCROLLER_SELECTOR)
        ?? null) as HTMLElement | null;
      if (!scroller) return;

      const max = scroller.scrollHeight - scroller.clientHeight;
      if (max <= 0) return;

      event.preventDefault();
      scroller.scrollTop = Math.min(max, Math.max(0, scroller.scrollTop + event.deltaY));
    };

    document.addEventListener("wheel", onWheel, { capture: true, passive: false });
    return () => document.removeEventListener("wheel", onWheel, { capture: true });
  }, [active]);
}
