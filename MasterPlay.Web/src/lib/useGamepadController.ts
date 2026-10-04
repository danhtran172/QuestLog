import { useEffect, useRef, useState } from "react";
import {
  INITIAL_CONTROLLER_STATE,
  processControllerReading,
  type ControllerAction,
  type ControllerInputState,
} from "./controllerInput";

type UseGamepadControllerOptions = Readonly<{
  onAction: (action: ControllerAction) => void;
  onGamepadInput: () => void;
  onBackHoldChange: (pressed: boolean) => void;
  promptPopupControls: boolean;
}>;

function findStandardGamepad(): Gamepad | null {
  if (typeof navigator.getGamepads !== "function") return null;
  return Array.from(navigator.getGamepads()).find(
    (gamepad): gamepad is Gamepad => gamepad != null && gamepad.connected && gamepad.mapping === "standard",
  ) ?? null;
}

export function useGamepadController({
  onAction,
  onGamepadInput,
  onBackHoldChange,
  promptPopupControls,
}: UseGamepadControllerOptions): { connected: boolean } {
  const [connected, setConnected] = useState(false);
  const connectedRef = useRef(false);
  const inputStateRef = useRef<ControllerInputState>(INITIAL_CONTROLLER_STATE);
  const actionRef = useRef(onAction);
  const gamepadInputRef = useRef(onGamepadInput);
  const backHoldRef = useRef(onBackHoldChange);
  const promptPopupControlsRef = useRef(promptPopupControls);
  const backPressedRef = useRef(false);

  actionRef.current = onAction;
  gamepadInputRef.current = onGamepadInput;
  backHoldRef.current = onBackHoldChange;
  promptPopupControlsRef.current = promptPopupControls;

  useEffect(() => {
    let frameId = 0;

    const updateConnected = (next: boolean) => {
      if (connectedRef.current === next) return;
      connectedRef.current = next;
      setConnected(next);
    };

    const updateBackPressed = (next: boolean) => {
      if (backPressedRef.current === next) return;
      backPressedRef.current = next;
      backHoldRef.current(next);
    };

    const poll = (now: number) => {
      const canReadInput = document.visibilityState === "visible" && document.hasFocus();
      const gamepad = canReadInput ? findStandardGamepad() : null;
      updateConnected(gamepad != null);

      if (!gamepad) {
        updateBackPressed(false);
        inputStateRef.current = INITIAL_CONTROLLER_STATE;
      } else {
        const result = processControllerReading(
          inputStateRef.current,
          {
            buttons: gamepad.buttons.map((button) => button.pressed || button.value >= 0.5),
            axes: gamepad.axes,
          },
          now,
          { promptPopupControls: promptPopupControlsRef.current },
        );
        updateBackPressed(result.backPressed);
        inputStateRef.current = result.state;
        if (result.actions.length > 0) {
          gamepadInputRef.current();
          result.actions.forEach((action) => actionRef.current(action));
        }
      }

      frameId = window.requestAnimationFrame(poll);
    };

    frameId = window.requestAnimationFrame(poll);
    return () => {
      window.cancelAnimationFrame(frameId);
      backPressedRef.current = false;
    };
  }, []);

  return { connected };
}
