/**
 * Xem truoc anh trong the sua: anh vua chon se trong the nao o CHO NO THUC SU
 * HIEN — card trong luoi (banner) va Console mode (poster + backdrop).
 *
 * Du lieu la chinh ban nhap cua form (cung kieu `GameDetail` sinh tu openapi),
 * khong phai du lieu gia. Cac field server tinh (band diem, canLaunch...) chua
 * co thi preview cung khong co — dung nhu card that sau khi luu.
 */
import { Gamepad2 } from "lucide-react";
import type { GameDetail } from "@/lib/api/domain";
import { CARD_WIDTH } from "@/lib/api/domain";
import { GameCard } from "./GameCard";

const noop = () => {};

export function CardPreview({ game }: { game: GameDetail }) {
  return (
    <div className="pointer-events-none" style={{ width: CARD_WIDTH }} aria-hidden>
      <GameCard game={game} onOpen={noop} onPlay={noop} />
    </div>
  );
}

/** Thu nho mot khung Console: backdrop mo phia sau, poster o giua, ten ben duoi. */
export function ConsolePreview({ game }: { game: GameDetail }) {
  /* Cung thu tu fallback voi ConsoleLibraryView. */
  const backdrop = game.backdropPath || game.bannerPath || game.posterPath || null;
  const cover = game.posterPath || game.backdropPath || game.bannerPath || null;

  return (
    <div
      aria-hidden
      className="pointer-events-none relative aspect-video w-[420px] overflow-hidden rounded-lg bg-[#171a1f] text-[#f1f3f4]"
    >
      {backdrop ? (
        /* Console that mo 34px tren ca man hinh; khung nho ~1/4 thi mo ~1/4. */
        <div
          className="absolute -inset-4 bg-cover bg-center"
          style={{ backgroundImage: `url(${JSON.stringify(backdrop)})`, filter: "blur(9px) saturate(1.15)" }}
        />
      ) : null}
      <div className="absolute inset-0 bg-[linear-gradient(180deg,rgba(12,14,17,.7),rgba(12,14,17,.2)_42%,rgba(12,14,17,.86))]" />
      <span className="absolute left-3 top-2.5 text-[8px] font-extrabold uppercase tracking-[0.16em] text-white/72">
        QuestLog
      </span>
      <div className="absolute left-1/2 top-[13%] aspect-[2/3] h-[66%] -translate-x-1/2 overflow-hidden rounded-lg border border-primary bg-[#252a30] shadow-2xl">
        {cover ? (
          <img src={cover} alt="" className="h-full w-full object-cover" />
        ) : (
          <span className="grid h-full w-full place-items-center text-[#d9dde0]">
            <Gamepad2 size={22} />
          </span>
        )}
        <span className="absolute inset-0 bg-gradient-to-t from-black/45 via-transparent to-white/8" />
      </div>
      <p className="absolute inset-x-0 bottom-[7%] truncate px-4 text-center text-sm font-extrabold text-white drop-shadow-lg">
        {game.name}
      </p>
    </div>
  );
}
