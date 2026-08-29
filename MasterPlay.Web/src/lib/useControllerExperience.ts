import { useCallback, useEffect, useMemo, useState } from "react";
import { toast } from "sonner";
import type { ControllerPrompt } from "@/components/ControllerPrompts";
import type { GameListItem, Tab } from "./api/domain";
import { TABS } from "./api/domain";
import type { ControllerAction } from "./controllerInput";
import {
  setConsoleFullscreen,
  subscribeConsoleToggle,
  subscribeFullscreenState,
} from "./desktopBridge";
import {
  activateFocusedControl,
  moveSpatialFocus,
  scrollControllerSurface,
  type SpatialDirection,
} from "./spatialNavigation";
import { useGamepadController } from "./useGamepadController";

type ControllerExperienceOptions = Readonly<{
  games: readonly GameListItem[];
  loading: boolean;
  error: string | null;
  tab: Tab;
  page: number;
  totalPages: number;
  layout: "card" | "list";
  hasOverlay: boolean;
  t: (key: string) => string;
  onTabChange: (tab: Tab) => void;
  onPageChange: (page: number) => void;
  onLayoutChange: (layout: "card" | "list") => void;
  onCloseOverlay: () => void;
  onOpenSettings: () => void;
  onOpenGame: (gameId: string) => void;
  onAddGame: () => void;
  onLaunchGame: (gameId: string) => void;
  onRefresh: () => void;
  onLeaveDashboard: () => void;
}>;

export function useControllerExperience({
  games,
  loading,
  error,
  tab,
  page,
  totalPages,
  layout,
  hasOverlay,
  t,
  onTabChange,
  onPageChange,
  onLayoutChange,
  onCloseOverlay,
  onOpenSettings,
  onOpenGame,
  onAddGame,
  onLaunchGame,
  onRefresh,
  onLeaveDashboard,
}: ControllerExperienceOptions) {
  const [inputModality, setInputModality] = useState<"pointer" | "keyboard" | "gamepad">("pointer");
  const [consoleSession, setConsoleSession] = useState(false);
  const [consoleVisible, setConsoleVisible] = useState(true);
  const [fullscreenActive, setFullscreenActive] = useState(false);
  const [selectedGameId, setSelectedGameId] = useState<string | null>(null);
  const [pendingEdge, setPendingEdge] = useState<"first" | "last" | null>(null);
  const showConsole = consoleSession && consoleVisible;
  const selectedGame = games.find((game) => game.id === selectedGameId) ?? games[0] ?? null;

  const toggleConsoleSession = useCallback(() => {
    setConsoleSession((current) => {
      const next = !current;
      if (next) setConsoleVisible(true);
      void setConsoleFullscreen(next).catch((fullscreenError) => {
        toast.error(fullscreenError instanceof Error ? fullscreenError.message : t("controller.fullscreenError"));
      });
      return next;
    });
  }, [t]);

  useEffect(() => subscribeFullscreenState(setFullscreenActive), []);

  useEffect(() => subscribeConsoleToggle(() => {
    setInputModality("keyboard");
    toggleConsoleSession();
  }), [toggleConsoleSession]);

  useEffect(() => {
    const usePointer = () => setInputModality("pointer");
    window.addEventListener("pointerdown", usePointer, { passive: true });
    window.addEventListener("wheel", usePointer, { passive: true });
    return () => {
      window.removeEventListener("pointerdown", usePointer);
      window.removeEventListener("wheel", usePointer);
    };
  }, []);

  useEffect(() => {
    const useKeyboard = (event: KeyboardEvent) => {
      setInputModality("keyboard");
      if (event.key !== "F10") return;
      event.preventDefault();
      toggleConsoleSession();
    };
    window.addEventListener("keydown", useKeyboard);
    return () => window.removeEventListener("keydown", useKeyboard);
  }, [toggleConsoleSession]);

  useEffect(() => {
    if (loading) return;
    if (games.length === 0) {
      setSelectedGameId(null);
      setPendingEdge(null);
      return;
    }
    if (pendingEdge) {
      const game = pendingEdge === "first" ? games[0] : games[games.length - 1];
      setSelectedGameId(game?.id ?? null);
      setPendingEdge(null);
      return;
    }
    if (!games.some((game) => game.id === selectedGameId)) {
      setSelectedGameId(games[0]?.id ?? null);
    }
  }, [games, loading, pendingEdge, selectedGameId]);

  const selectAdjacentTab = useCallback((change: -1 | 1) => {
    const currentIndex = TABS.indexOf(tab);
    const nextTab = TABS[(currentIndex + change + TABS.length) % TABS.length];
    if (!nextTab) return;
    setSelectedGameId(null);
    setPendingEdge("first");
    onTabChange(nextTab);
  }, [onTabChange, tab]);

  const moveConsoleSelection = useCallback((change: -1 | 1) => {
    if (games.length === 0) return;
    const currentIndex = Math.max(0, games.findIndex((game) => game.id === selectedGameId));
    const nextIndex = currentIndex + change;
    if (nextIndex >= 0 && nextIndex < games.length) {
      setSelectedGameId(games[nextIndex]?.id ?? null);
    } else if (tab !== "queue" && change > 0 && page < totalPages) {
      setPendingEdge("first");
      onPageChange(page + 1);
    } else if (tab !== "queue" && change < 0 && page > 1) {
      setPendingEdge("last");
      onPageChange(page - 1);
    }
  }, [games, onPageChange, page, selectedGameId, tab, totalPages]);

  const focusSpatialTarget = useCallback((direction: SpatialDirection) => {
    const target = moveSpatialFocus(direction);
    const gameId = target?.closest<HTMLElement>("[data-game-id]")?.dataset.gameId;
    if (gameId) setSelectedGameId(gameId);
  }, []);

  const handleAction = useCallback((action: ControllerAction) => {
    if (action === "toggleConsole") {
      toggleConsoleSession();
      return;
    }
    if (hasOverlay) {
      if (action === "activate") activateFocusedControl();
      else if (action === "back") onCloseOverlay();
      else if (action === "moveUp") focusSpatialTarget("up");
      else if (action === "moveDown") focusSpatialTarget("down");
      else if (action === "moveLeft") focusSpatialTarget("left");
      else if (action === "moveRight") focusSpatialTarget("right");
      else if (action === "scrollUp") scrollControllerSurface("up");
      else if (action === "scrollDown") scrollControllerSurface("down");
      return;
    }

    if (action === "toggleSurface" && consoleSession) setConsoleVisible((current) => !current);
    else if (action === "openSettings") onOpenSettings();
    else if (action === "previousTab") selectAdjacentTab(-1);
    else if (action === "nextTab") selectAdjacentTab(1);
    else if (action === "moveLeft" && showConsole) moveConsoleSelection(-1);
    else if (action === "moveRight" && showConsole) moveConsoleSelection(1);
    else if (action === "moveUp") focusSpatialTarget("up");
    else if (action === "moveDown") focusSpatialTarget("down");
    else if (action === "moveLeft") focusSpatialTarget("left");
    else if (action === "moveRight") focusSpatialTarget("right");
    else if (action === "activate") {
      const active = document.activeElement instanceof HTMLElement ? document.activeElement : null;
      const primaryGameId = active?.dataset.gamePrimary === "true"
        ? active.closest<HTMLElement>("[data-game-id]")?.dataset.gameId
        : null;
      if (error) onRefresh();
      else if (showConsole && selectedGame) onOpenGame(selectedGame.id);
      else if (primaryGameId) onOpenGame(primaryGameId);
      else activateFocusedControl();
    } else if (action === "back" && showConsole) {
      setConsoleVisible(false);
    } else if (action === "play") {
      const focusedGameId = document.activeElement instanceof HTMLElement
        ? document.activeElement.closest<HTMLElement>("[data-game-id]")?.dataset.gameId
        : null;
      const game = showConsole ? selectedGame : games.find((item) => item.id === focusedGameId);
      if (game?.canLaunch) onLaunchGame(game.id);
    } else if (action === "refresh") onRefresh();
    else if (action === "toggleLayout") {
      onLeaveDashboard();
      if (showConsole) {
        setConsoleVisible(false);
        onLayoutChange("list");
      } else if (tab !== "queue") {
        onLayoutChange(layout === "card" ? "list" : "card");
      }
    } else if (action === "addGame") onAddGame();
    else if (action === "scrollUp") scrollControllerSurface("up");
    else if (action === "scrollDown") scrollControllerSurface("down");
  }, [
    consoleSession,
    error,
    focusSpatialTarget,
    games,
    hasOverlay,
    layout,
    moveConsoleSelection,
    onAddGame,
    onCloseOverlay,
    onLaunchGame,
    onLayoutChange,
    onLeaveDashboard,
    onOpenGame,
    onOpenSettings,
    onRefresh,
    selectAdjacentTab,
    selectedGame,
    showConsole,
    tab,
    toggleConsoleSession,
  ]);

  const gamepad = useGamepadController({
    onAction: handleAction,
    onGamepadInput: () => setInputModality("gamepad"),
  });

  const prompts = useMemo<ControllerPrompt[]>(() => {
    if (hasOverlay) {
      return [
        { button: "A", label: t("controller.select") },
        { button: "B", label: t("controller.back") },
        { button: "RS", label: t("controller.scroll") },
      ];
    }
    if (error) return [{ button: "A", label: t("error.retry") }];
    const next: ControllerPrompt[] = [
      { button: "A", label: t("action.details") },
      { button: "LB/RB", label: t("controller.changeTab") },
      { button: "L3", label: t("controller.changeLayout") },
      { button: "R3", label: t("action.addGame") },
      { button: "View", label: t("settings.title") },
    ];
    if (selectedGame?.canLaunch) next.splice(1, 0, { button: "X", label: t("action.play") });
    if (consoleSession) {
      next.push({ button: "Menu", label: t(showConsole ? "controller.gallery" : "controller.console") });
    }
    return next;
  }, [consoleSession, error, hasOverlay, selectedGame?.canLaunch, showConsole, t]);

  return {
    connected: gamepad.connected,
    consoleSession,
    fullscreenActive,
    inputModality,
    prompts,
    selectedGameId,
    setInputModality,
    setSelectedGameId,
    showConsole,
    toggleConsoleSession,
  };
}
