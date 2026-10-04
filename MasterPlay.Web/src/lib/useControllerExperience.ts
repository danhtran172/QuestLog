import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { toast } from "sonner";
import type { ControllerPrompt } from "@/components/ControllerPrompts";
import type { GameListItem, Tab } from "./api/domain";
import { CONSOLE_TABS, TABS } from "./api/domain";
import { isControllerActionAvailable, type ControllerAction } from "./controllerInput";
import { getConsoleKeyboardAction } from "./keyboardInput";
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
  /**
   * Admin Console dang mo. Console (controller) session va Admin Console la hai
   * fullscreen surface KHONG duoc chong nhau (spec 2026-09-14 §2.1): khi suspended
   * thi F10, chord LB+RB+A va moi action controller deu khong hoat dong.
   *
   * Chi chan DAU VAO. `toggleConsoleSession` van goi duoc de App buoc thoat session
   * dang chay truoc khi vao Admin Console.
   */
  suspended?: boolean;
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
  onEnterConsoleSession: () => void;
  onExitConsoleSession: () => void;
}>;

export type ConsoleExitHoldSource = "keyboard" | "gamepad";
export const CONSOLE_EXIT_HOLD_MS = 900;

function isEditableTarget(target: EventTarget | null): boolean {
  if (!(target instanceof HTMLElement)) return false;
  return target.isContentEditable || ["INPUT", "TEXTAREA", "SELECT"].includes(target.tagName);
}

export function useControllerExperience({
  games,
  loading,
  error,
  tab,
  page,
  totalPages,
  layout,
  hasOverlay,
  suspended = false,
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
  onEnterConsoleSession,
  onExitConsoleSession,
}: ControllerExperienceOptions) {
  const [inputModality, setInputModality] = useState<"pointer" | "keyboard" | "gamepad">("pointer");
  const [consoleSession, setConsoleSession] = useState(false);
  const [fullscreenActive, setFullscreenActive] = useState(false);
  const [selectedGameId, setSelectedGameId] = useState<string | null>(null);
  const [pendingEdge, setPendingEdge] = useState<"first" | "last" | null>(null);
  const [promptsOpen, setPromptsOpen] = useState(false);
  const [exitHoldSource, setExitHoldSource] = useState<ConsoleExitHoldSource | null>(null);
  const exitHoldSourceRef = useRef<ConsoleExitHoldSource | null>(null);
  const exitHoldTimerRef = useRef<ReturnType<typeof setTimeout> | null>(null);
  const consoleSessionRef = useRef(false);
  const suspendedRef = useRef(suspended);
  suspendedRef.current = suspended;
  const showConsole = consoleSession;
  const selectedGame = games.find((game) => game.id === selectedGameId) ?? games[0] ?? null;

  const clearExitHold = useCallback((source?: ConsoleExitHoldSource) => {
    if (source && exitHoldSourceRef.current !== source) return;
    if (exitHoldTimerRef.current) clearTimeout(exitHoldTimerRef.current);
    exitHoldTimerRef.current = null;
    exitHoldSourceRef.current = null;
    setExitHoldSource(null);
  }, []);

  const enterConsoleSession = useCallback(() => {
    if (consoleSessionRef.current) return;
    clearExitHold();
    consoleSessionRef.current = true;
    setConsoleSession(true);
    onEnterConsoleSession();
    void setConsoleFullscreen(true).catch((fullscreenError) => {
      toast.error(fullscreenError instanceof Error ? fullscreenError.message : t("controller.fullscreenError"));
    });
  }, [clearExitHold, onEnterConsoleSession, t]);

  const leaveConsoleSession = useCallback(() => {
    if (!consoleSessionRef.current) return;
    clearExitHold();
    consoleSessionRef.current = false;
    setConsoleSession(false);
    onExitConsoleSession();
    void setConsoleFullscreen(false).catch((fullscreenError) => {
      toast.error(fullscreenError instanceof Error ? fullscreenError.message : t("controller.fullscreenError"));
    });
  }, [clearExitHold, onExitConsoleSession, t]);

  const toggleConsoleSession = useCallback(() => {
    if (consoleSessionRef.current) leaveConsoleSession();
    else enterConsoleSession();
  }, [enterConsoleSession, leaveConsoleSession]);

  const beginExitHold = useCallback((source: ConsoleExitHoldSource) => {
    if (!showConsole || hasOverlay || exitHoldSourceRef.current) return;
    setPromptsOpen(false);
    exitHoldSourceRef.current = source;
    setExitHoldSource(source);
    exitHoldTimerRef.current = setTimeout(() => {
      exitHoldTimerRef.current = null;
      exitHoldSourceRef.current = null;
      setExitHoldSource(null);
      leaveConsoleSession();
    }, CONSOLE_EXIT_HOLD_MS);
  }, [hasOverlay, leaveConsoleSession, showConsole]);

  useEffect(() => () => {
    if (exitHoldTimerRef.current) clearTimeout(exitHoldTimerRef.current);
    exitHoldTimerRef.current = null;
    exitHoldSourceRef.current = null;
  }, []);

  useEffect(() => {
    if (!showConsole || hasOverlay) {
      clearExitHold();
      setPromptsOpen(false);
    }
  }, [clearExitHold, hasOverlay, showConsole]);

  useEffect(() => subscribeFullscreenState(setFullscreenActive), []);

  useEffect(() => subscribeConsoleToggle(() => {
    if (suspendedRef.current) return;
    setInputModality("keyboard");
    toggleConsoleSession();
  }), [toggleConsoleSession]);

  useEffect(() => {
    const usePointer = () => {
      setInputModality("pointer");
      setPromptsOpen(false);
    };
    window.addEventListener("pointerdown", usePointer, { passive: true });
    window.addEventListener("wheel", usePointer, { passive: true });
    return () => {
      window.removeEventListener("pointerdown", usePointer);
      window.removeEventListener("wheel", usePointer);
    };
  }, []);

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
    const tabs = consoleSession ? CONSOLE_TABS : TABS;
    const currentIndex = tabs.indexOf(tab);
    const nextTab = tabs[(currentIndex + change + tabs.length) % tabs.length];
    if (!nextTab) return;
    setSelectedGameId(null);
    setPendingEdge("first");
    onTabChange(nextTab);
  }, [consoleSession, onTabChange, tab]);

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
    if (suspendedRef.current) return;
    if (!isControllerActionAvailable(action, consoleSessionRef.current)) return;
    if (action === "togglePrompts") {
      if (showConsole && !hasOverlay) setPromptsOpen((current) => !current);
      return;
    }
    if (action === "openPrompts") {
      if (showConsole && !hasOverlay) setPromptsOpen(true);
      return;
    }
    if (action === "closePrompts") {
      setPromptsOpen(false);
      return;
    }

    setPromptsOpen(false);
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

    if (action === "openSettings") onOpenSettings();
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
    } else if (action === "play") {
      const focusedGameId = document.activeElement instanceof HTMLElement
        ? document.activeElement.closest<HTMLElement>("[data-game-id]")?.dataset.gameId
        : null;
      const game = showConsole ? selectedGame : games.find((item) => item.id === focusedGameId);
      if (game?.canLaunch) onLaunchGame(game.id);
    } else if (action === "refresh") onRefresh();
    else if (action === "toggleLayout") {
      onLeaveDashboard();
      if (tab !== "queue") {
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
    promptPopupControls: showConsole && !hasOverlay,
    onGamepadInput: () => setInputModality("gamepad"),
    onBackHoldChange: (pressed) => {
      if (pressed) {
        setInputModality("gamepad");
        beginExitHold("gamepad");
      } else {
        clearExitHold("gamepad");
      }
    },
  });

  useEffect(() => {
    const useKeyboard = (event: KeyboardEvent) => {
      if (suspendedRef.current) return;
      setInputModality("keyboard");
      if (event.key === "Escape" && showConsole && !hasOverlay) {
        event.preventDefault();
        if (!event.repeat) beginExitHold("keyboard");
        return;
      }
      const action = getConsoleKeyboardAction({
        key: event.key,
        consoleSession,
        consoleVisible: showConsole,
        overlayOpen: hasOverlay,
        editing: isEditableTarget(event.target),
        repeat: event.repeat,
        altKey: event.altKey,
        ctrlKey: event.ctrlKey,
        metaKey: event.metaKey,
      });
      if (!action) return;
      event.preventDefault();
      handleAction(action);
    };
    const releaseKeyboardHold = (event: KeyboardEvent) => {
      if (event.key === "Escape") clearExitHold("keyboard");
    };
    const cancelKeyboardHold = () => clearExitHold("keyboard");
    window.addEventListener("keydown", useKeyboard);
    window.addEventListener("keyup", releaseKeyboardHold);
    window.addEventListener("blur", cancelKeyboardHold);
    return () => {
      window.removeEventListener("keydown", useKeyboard);
      window.removeEventListener("keyup", releaseKeyboardHold);
      window.removeEventListener("blur", cancelKeyboardHold);
    };
  }, [beginExitHold, clearExitHold, consoleSession, handleAction, hasOverlay, showConsole]);

  const prompts = useMemo<ControllerPrompt[]>(() => {
    if (inputModality === "pointer") return [];
    const keyboard = inputModality === "keyboard";
    if (keyboard && !showConsole) return [];
    if (hasOverlay) {
      return keyboard
        ? [{ button: "Enter", label: t("controller.select") }]
        : [
            { button: "A", label: t("controller.select") },
            { button: "B", label: t("controller.back") },
            { button: "RS", label: t("controller.scroll") },
          ];
    }
    if (error) return [{ button: keyboard ? "Space" : "A", label: t("error.retry") }];
    const next: ControllerPrompt[] = keyboard
      ? [
          { button: "Tab", label: t("action.details") },
          { button: "Q/E", label: t("controller.changeTab") },
          { button: "N", label: t("action.addGame") },
          { button: "V", label: t("settings.title") },
        ]
      : [
          { button: "A", label: t("action.details") },
          { button: "LB/RB", label: t("controller.changeTab") },
          ...(isControllerActionAvailable("toggleLayout", consoleSession) && tab !== "queue"
            ? [{ button: "L3", label: t("controller.changeLayout") }]
            : []),
          { button: "R3", label: t("action.addGame") },
          { button: "View", label: t("settings.title") },
        ];
    if (selectedGame?.canLaunch) {
      next.splice(1, 0, { button: keyboard ? "Enter" : "X", label: t("action.play") });
    }
    return next;
  }, [consoleSession, error, hasOverlay, inputModality, selectedGame?.canLaunch, showConsole, tab, t]);

  return {
    connected: gamepad.connected,
    consoleSession,
    exitHoldDurationMs: CONSOLE_EXIT_HOLD_MS,
    exitHoldSource,
    fullscreenActive,
    inputModality,
    promptPopupAvailable: showConsole && !hasOverlay,
    promptTriggerButton: inputModality === "gamepad" ? "D↑" : "Shift",
    prompts,
    promptsOpen,
    selectedGameId,
    setInputModality,
    setSelectedGameId,
    showConsole,
    toggleConsoleSession,
  };
}
