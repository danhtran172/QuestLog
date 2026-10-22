import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { toast } from "sonner";
import type { ControllerPrompt } from "@/components/ControllerPrompts";
import type { GameListItem, Tab } from "./api/domain";
import { CONSOLE_TABS, TABS } from "./api/domain";
import { playConsoleSound, type ConsoleSoundCue } from "./consoleSounds";
import { isControllerActionAvailable, type ControllerAction } from "./controllerInput";
import { getConsoleKeyboardAction } from "./keyboardInput";
import {
  isDesktopShell,
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
  const promptsOpenRef = useRef(promptsOpen);
  promptsOpenRef.current = promptsOpen;
  const showConsole = consoleSession;
  const selectedGame = games.find((game) => game.id === selectedGameId) ?? games[0] ?? null;

  const clearExitHold = useCallback((source?: ConsoleExitHoldSource) => {
    if (source && exitHoldSourceRef.current !== source) return;
    if (exitHoldTimerRef.current) clearTimeout(exitHoldTimerRef.current);
    exitHoldTimerRef.current = null;
    exitHoldSourceRef.current = null;
    setExitHoldSource(null);
  }, []);

  const enterConsoleSession = useCallback((fullscreen = true) => {
    if (consoleSessionRef.current) return;
    clearExitHold();
    consoleSessionRef.current = true;
    setConsoleSession(true);
    playConsoleSound("enterConsole");
    onEnterConsoleSession();
    if (!fullscreen) return;
    void setConsoleFullscreen(true).catch((fullscreenError) => {
      toast.error(fullscreenError instanceof Error ? fullscreenError.message : t("controller.fullscreenError"));
    });
  }, [clearExitHold, onEnterConsoleSession, t]);

  /* Vao lai console session luc mo app (mode cuoi cung = console). Khong co cu chi
     nguoi dung nen trinh duyet se tu choi requestFullscreen — chi vo desktop moi
     phong to cua so; tren web van hien console, khong bao loi. */
  const restoreConsoleSession = useCallback(() => {
    enterConsoleSession(isDesktopShell());
  }, [enterConsoleSession]);

  const leaveConsoleSession = useCallback(() => {
    if (!consoleSessionRef.current) return;
    clearExitHold();
    playConsoleSound("exitConsole");
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

  const moveConsoleSelection = useCallback((change: -1 | 1): ConsoleSoundCue => {
    if (games.length === 0) return "blocked";
    const currentIndex = Math.max(0, games.findIndex((game) => game.id === selectedGameId));
    const nextIndex = currentIndex + change;
    if (nextIndex >= 0 && nextIndex < games.length) {
      setSelectedGameId(games[nextIndex]?.id ?? null);
      return "move";
    }
    if (tab !== "queue" && change > 0 && page < totalPages) {
      setPendingEdge("first");
      onPageChange(page + 1);
      return "page";
    }
    if (tab !== "queue" && change < 0 && page > 1) {
      setPendingEdge("last");
      onPageChange(page - 1);
      return "page";
    }
    return "blocked";
  }, [games, onPageChange, page, selectedGameId, tab, totalPages]);

  const focusSpatialTarget = useCallback((direction: SpatialDirection): ConsoleSoundCue => {
    const target = moveSpatialFocus(direction);
    const gameId = target?.closest<HTMLElement>("[data-game-id]")?.dataset.gameId;
    if (gameId) setSelectedGameId(gameId);
    return target ? "move" : "blocked";
  }, []);

  const handleAction = useCallback((action: ControllerAction) => {
    if (suspendedRef.current) return;
    if (!isControllerActionAvailable(action, consoleSessionRef.current)) return;
    /* Chi phat am trong console session; toggleConsole tu phat qua enter/leave. */
    const sound = (cue: ConsoleSoundCue | null) => {
      if (cue && consoleSessionRef.current) playConsoleSound(cue);
    };
    if (action === "togglePrompts") {
      if (showConsole && !hasOverlay) {
        sound(promptsOpenRef.current ? "promptsClose" : "promptsOpen");
        setPromptsOpen((current) => !current);
      }
      return;
    }
    if (action === "openPrompts") {
      if (showConsole && !hasOverlay) {
        if (!promptsOpenRef.current) sound("promptsOpen");
        setPromptsOpen(true);
      }
      return;
    }
    if (action === "closePrompts") {
      if (promptsOpenRef.current) sound("promptsClose");
      setPromptsOpen(false);
      return;
    }

    setPromptsOpen(false);
    if (action === "toggleConsole") {
      toggleConsoleSession();
      return;
    }
    if (hasOverlay) {
      if (action === "activate") {
        sound("confirm");
        activateFocusedControl();
      } else if (action === "back") {
        sound("back");
        onCloseOverlay();
      } else if (action === "moveUp") sound(focusSpatialTarget("up"));
      else if (action === "moveDown") sound(focusSpatialTarget("down"));
      else if (action === "moveLeft") sound(focusSpatialTarget("left"));
      else if (action === "moveRight") sound(focusSpatialTarget("right"));
      else if (action === "scrollUp") scrollControllerSurface("up");
      else if (action === "scrollDown") scrollControllerSurface("down");
      return;
    }

    if (action === "openSettings") {
      sound("confirm");
      onOpenSettings();
    } else if (action === "previousTab") {
      sound("tab");
      selectAdjacentTab(-1);
    } else if (action === "nextTab") {
      sound("tab");
      selectAdjacentTab(1);
    } else if (action === "moveLeft" && showConsole) sound(moveConsoleSelection(-1));
    else if (action === "moveRight" && showConsole) sound(moveConsoleSelection(1));
    else if (action === "moveUp") sound(focusSpatialTarget("up"));
    else if (action === "moveDown") sound(focusSpatialTarget("down"));
    else if (action === "moveLeft") sound(focusSpatialTarget("left"));
    else if (action === "moveRight") sound(focusSpatialTarget("right"));
    else if (action === "activate") {
      const active = document.activeElement instanceof HTMLElement ? document.activeElement : null;
      const primaryGameId = active?.dataset.gamePrimary === "true"
        ? active.closest<HTMLElement>("[data-game-id]")?.dataset.gameId
        : null;
      sound("confirm");
      if (error) onRefresh();
      else if (showConsole && selectedGame) onOpenGame(selectedGame.id);
      else if (primaryGameId) onOpenGame(primaryGameId);
      else activateFocusedControl();
    } else if (action === "play") {
      const focusedGameId = document.activeElement instanceof HTMLElement
        ? document.activeElement.closest<HTMLElement>("[data-game-id]")?.dataset.gameId
        : null;
      const game = showConsole ? selectedGame : games.find((item) => item.id === focusedGameId);
      // Dang chay (`isRunning` tu backend) thi khong mo them lan nua.
      if (game?.canLaunch && !game.isRunning) {
        sound("launch");
        onLaunchGame(game.id);
      } else {
        sound("blocked");
      }
    } else if (action === "refresh") {
      sound("confirm");
      onRefresh();
    } else if (action === "toggleLayout") {
      onLeaveDashboard();
      if (tab !== "queue") {
        onLayoutChange(layout === "card" ? "list" : "card");
      }
    } else if (action === "addGame") {
      sound("confirm");
      onAddGame();
    } else if (action === "scrollUp") scrollControllerSurface("up");
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
    if (showConsole) next.push({ button: keyboard ? "Esc" : "B", label: t("controller.holdToExit") });
    if (!keyboard && showConsole) next.push({ button: "D↓", label: t("controller.closeControls") });
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
    restoreConsoleSession,
    selectedGameId,
    setInputModality,
    setSelectedGameId,
    showConsole,
    toggleConsoleSession,
  };
}
