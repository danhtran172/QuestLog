import { useEffect, useMemo, useState } from "react";
import { AnimatePresence, motion, useReducedMotion } from "motion/react";
import { Gamepad2, RotateCcw } from "lucide-react";
import type { GameListItem } from "@/lib/api/domain";
import { cn } from "@/lib/utils";

function ConsoleCover({
  game,
  offset,
  selected,
  onSelect,
  onOpen,
}: {
  game: GameListItem;
  offset: number;
  selected: boolean;
  onSelect: () => void;
  onOpen: () => void;
}) {
  const [imageFailed, setImageFailed] = useState(false);

  useEffect(() => setImageFailed(false), [game.imagePath]);

  return (
    <button
      type="button"
      data-offset={offset}
      data-controller-selected={selected ? "true" : undefined}
      aria-current={selected ? "true" : undefined}
      aria-label={game.name}
      onClick={() => selected ? onOpen() : onSelect()}
      className={cn(
        "console-cover absolute left-1/2 top-[15%] aspect-[2/3] overflow-hidden rounded-2xl outline-none",
        "border bg-[#252a30] shadow-2xl focus-visible:ring-4 focus-visible:ring-primary/80",
        selected ? "border-primary" : "border-white/14",
      )}
    >
      {game.imagePath && !imageFailed ? (
        <img
          src={game.imagePath}
          alt={game.name}
          draggable={false}
          onError={() => setImageFailed(true)}
          className="h-full w-full object-cover"
        />
      ) : (
        <span className="flex h-full w-full flex-col items-center justify-center gap-4 bg-[#252a30] p-5 text-center text-[#d9dde0]">
          <Gamepad2 aria-hidden size={42} />
          <span className="line-clamp-3 text-sm font-bold">{game.name}</span>
        </span>
      )}
      <span className="pointer-events-none absolute inset-0 bg-gradient-to-t from-black/45 via-transparent to-white/8" />
    </button>
  );
}

export function ConsoleLibraryView({
  games,
  selectedGameId,
  tabLabel,
  isLoading,
  error,
  emptyTitle,
  emptyDescription,
  retryLabel,
  onSelectGame,
  onOpenGame,
  onRetry,
}: {
  games: readonly GameListItem[];
  selectedGameId: string | null;
  tabLabel: string;
  isLoading: boolean;
  error: string | null;
  emptyTitle: string;
  emptyDescription: string;
  retryLabel: string;
  onSelectGame: (gameId: string) => void;
  onOpenGame: (gameId: string) => void;
  onRetry: () => void;
}) {
  const reduceMotion = useReducedMotion();
  const selectedIndex = Math.max(0, games.findIndex((game) => game.id === selectedGameId));
  const selectedGame = games[selectedIndex] ?? null;
  const backdrop = selectedGame?.gImagePath || selectedGame?.imagePath || null;
  const visibleGames = useMemo(
    () => games
      .map((game, index) => ({ game, offset: index - selectedIndex }))
      .filter(({ offset }) => Math.abs(offset) <= 3),
    [games, selectedIndex],
  );

  useEffect(() => {
    for (let index = Math.max(0, selectedIndex - 2); index <= Math.min(games.length - 1, selectedIndex + 2); index += 1) {
      const game = games[index];
      const sources = game ? [game.imagePath, game.gImagePath] : [];
      sources.forEach((source) => {
        if (!source) return;
        const image = new Image();
        image.src = source;
      });
    }
  }, [games, selectedIndex]);

  return (
    <main className="console-library fixed inset-0 z-30 min-h-[100dvh] overflow-hidden bg-[#171a1f] text-[#f1f3f4]">
      <AnimatePresence initial={false}>
        {backdrop ? (
          <motion.div
            key={backdrop}
            aria-hidden
            initial={reduceMotion ? false : { opacity: 0 }}
            animate={{ opacity: 1 }}
            exit={reduceMotion ? undefined : { opacity: 0 }}
            transition={{ duration: reduceMotion ? 0 : 0.32 }}
            className="console-backdrop absolute -inset-12 bg-cover bg-center"
            style={{ backgroundImage: `url(${JSON.stringify(backdrop)})` }}
          />
        ) : null}
      </AnimatePresence>
      <div aria-hidden className="absolute inset-0 bg-[linear-gradient(180deg,rgba(12,14,17,.7),rgba(12,14,17,.2)_42%,rgba(12,14,17,.86))]" />

      <header className="absolute inset-x-0 top-0 z-10 flex items-center justify-between px-[clamp(1.5rem,5vw,5rem)] py-[clamp(1.25rem,4vh,3rem)]">
        <span className="text-sm font-extrabold uppercase tracking-[0.16em] text-white/72">QuestLog</span>
        <span className="rounded-lg border border-white/14 bg-black/25 px-3 py-1.5 text-sm font-bold backdrop-blur-md">{tabLabel}</span>
      </header>

      {error ? (
        <section className="relative z-10 flex min-h-[100dvh] flex-col items-center justify-center gap-4 px-6 text-center">
          <Gamepad2 aria-hidden size={46} className="text-white/65" />
          <p className="max-w-lg text-base text-white/75">{error}</p>
          <button type="button" onClick={onRetry} className="inline-flex items-center gap-2 rounded-xl bg-primary px-4 py-2.5 font-bold text-primary-fg outline-none focus-visible:ring-4 focus-visible:ring-white/70">
            <RotateCcw aria-hidden size={18} />
            {retryLabel}
          </button>
        </section>
      ) : isLoading ? (
        <section aria-label={tabLabel} aria-busy="true" className="relative z-10 flex min-h-[100dvh] items-center justify-center gap-[clamp(1rem,3vw,2.5rem)]">
          {[0, 1, 2].map((index) => (
            <div key={index} className={cn("aspect-[2/3] animate-pulse rounded-2xl bg-white/10", index === 1 ? "w-[clamp(13rem,20vw,22rem)]" : "w-[clamp(8rem,12vw,13rem)] opacity-55")} />
          ))}
        </section>
      ) : games.length === 0 ? (
        <section className="relative z-10 flex min-h-[100dvh] flex-col items-center justify-center gap-3 px-6 text-center">
          <Gamepad2 aria-hidden size={48} className="text-white/55" />
          <h1 className="text-2xl font-extrabold">{emptyTitle}</h1>
          <p className="max-w-md text-sm text-white/65">{emptyDescription}</p>
        </section>
      ) : (
        <>
          <section aria-label={tabLabel} className="relative z-10 min-h-[100dvh]">
            {visibleGames.map(({ game, offset }) => (
              <ConsoleCover
                key={game.id}
                game={game}
                offset={offset}
                selected={offset === 0}
                onSelect={() => onSelectGame(game.id)}
                onOpen={() => onOpenGame(game.id)}
              />
            ))}
          </section>
          {selectedGame ? (
            <div className="pointer-events-none absolute inset-x-0 bottom-[11%] z-20 px-6 text-center">
              <h1 className="mx-auto max-w-4xl truncate text-[clamp(1.35rem,3vw,2.5rem)] font-extrabold tracking-tight text-white drop-shadow-lg">{selectedGame.name}</h1>
              <div className="mt-3 flex flex-wrap justify-center gap-2 text-xs font-bold">
                {selectedGame.progress ? (
                  <span className="rounded-lg border border-white/15 bg-black/35 px-2.5 py-1.5 backdrop-blur-md" style={{ color: selectedGame.progress.colorCode }}>
                    {selectedGame.progress.displayName}
                  </span>
                ) : null}
                {selectedGame.ownership ? (
                  <span className="rounded-lg border border-white/15 bg-black/35 px-2.5 py-1.5 backdrop-blur-md" style={{ color: selectedGame.ownership.colorCode }}>
                    {selectedGame.ownership.displayName}
                  </span>
                ) : null}
              </div>
            </div>
          ) : null}
        </>
      )}
    </main>
  );
}
