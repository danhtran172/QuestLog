/**
 * Am thanh giao dien cho console session.
 *
 * Tong hop bang Web Audio ngay trong app: khong file am thanh, khong goi mang.
 * Moi cue la vai not ngan (sine/triangle) qua mot master gain + lowpass nhe de
 * nghe mem, khong choi tai khi giu phim di chuyen.
 */
export type ConsoleSoundCue =
  | "move"
  | "page"
  | "blocked"
  | "tab"
  | "confirm"
  | "back"
  | "launch"
  | "promptsOpen"
  | "promptsClose"
  | "enterConsole"
  | "exitConsole";

type Note = Readonly<{
  /** Giay tinh tu luc cue bat dau. */
  at: number;
  /** Do dai not, giay. */
  duration: number;
  frequency: number;
  /** Tan so cuoi not (glide). Bo trong = giu nguyen. */
  frequencyEnd?: number;
  type: OscillatorType;
  gain: number;
}>;

export const CONSOLE_SOUND_CUES: Readonly<Record<ConsoleSoundCue, readonly Note[]>> = {
  move: [{ at: 0, duration: 0.045, frequency: 1320, type: "sine", gain: 0.33 }],
  page: [
    { at: 0, duration: 0.05, frequency: 1100, type: "sine", gain: 0.3 },
    { at: 0.055, duration: 0.06, frequency: 1480, type: "sine", gain: 0.3 },
  ],
  blocked: [{ at: 0, duration: 0.09, frequency: 190, frequencyEnd: 130, type: "triangle", gain: 0.45 }],
  tab: [{ at: 0, duration: 0.11, frequency: 620, frequencyEnd: 990, type: "triangle", gain: 0.26 }],
  confirm: [
    { at: 0, duration: 0.08, frequency: 880, type: "sine", gain: 0.3 },
    { at: 0.05, duration: 0.12, frequency: 1320, type: "sine", gain: 0.26 },
  ],
  back: [
    { at: 0, duration: 0.08, frequency: 990, type: "sine", gain: 0.26 },
    { at: 0.05, duration: 0.12, frequency: 620, type: "sine", gain: 0.26 },
  ],
  launch: [
    { at: 0, duration: 0.1, frequency: 523.25, type: "triangle", gain: 0.28 },
    { at: 0.065, duration: 0.1, frequency: 659.25, type: "triangle", gain: 0.28 },
    { at: 0.13, duration: 0.1, frequency: 783.99, type: "triangle", gain: 0.28 },
    { at: 0.195, duration: 0.32, frequency: 1046.5, type: "sine", gain: 0.32 },
  ],
  promptsOpen: [{ at: 0, duration: 0.06, frequency: 760, frequencyEnd: 1140, type: "sine", gain: 0.2 }],
  promptsClose: [{ at: 0, duration: 0.06, frequency: 1140, frequencyEnd: 760, type: "sine", gain: 0.2 }],
  enterConsole: [
    { at: 0, duration: 0.42, frequency: 392, type: "sine", gain: 0.22 },
    { at: 0.08, duration: 0.42, frequency: 587.33, type: "sine", gain: 0.2 },
    { at: 0.16, duration: 0.5, frequency: 783.99, type: "sine", gain: 0.2 },
    { at: 0.16, duration: 0.5, frequency: 1174.66, type: "triangle", gain: 0.08 },
  ],
  exitConsole: [
    { at: 0, duration: 0.3, frequency: 783.99, type: "sine", gain: 0.2 },
    { at: 0.08, duration: 0.3, frequency: 587.33, type: "sine", gain: 0.2 },
    { at: 0.16, duration: 0.4, frequency: 392, type: "sine", gain: 0.22 },
  ],
};

const MASTER_GAIN = 0.35;
const ATTACK_SECONDS = 0.004;

type AudioGraph = Readonly<{ context: AudioContext; input: AudioNode }>;

let graph: AudioGraph | null = null;
let unlockListenersAttached = false;

function createGraph(): AudioGraph | null {
  if (graph) return graph;
  const AudioContextCtor = typeof window === "undefined" ? undefined : window.AudioContext;
  if (!AudioContextCtor) return null;
  try {
    const context = new AudioContextCtor();
    const master = context.createGain();
    master.gain.value = MASTER_GAIN;
    const lowpass = context.createBiquadFilter();
    lowpass.type = "lowpass";
    lowpass.frequency.value = 5200;
    master.connect(lowpass).connect(context.destination);
    graph = { context, input: master };
    return graph;
  } catch {
    // Trinh duyet khong cho tao AudioContext: app van chay, chi khong co tieng.
    return null;
  }
}

/*
 * Trinh duyet chi cho AudioContext chay sau mot cu chi nguoi dung (click/phim).
 * Tay cam khong luon duoc tinh la cu chi, nen mo khoa o cu chi dau tien bat ky.
 * Vo desktop (WebView2) tat autoplay policy nen khong can buoc nay.
 */
function attachUnlockListeners() {
  if (unlockListenersAttached || typeof window === "undefined") return;
  unlockListenersAttached = true;
  const unlock = () => {
    const current = createGraph();
    if (!current || current.context.state !== "suspended") {
      window.removeEventListener("pointerdown", unlock, true);
      window.removeEventListener("keydown", unlock, true);
      return;
    }
    void current.context.resume().catch(() => undefined);
  };
  window.addEventListener("pointerdown", unlock, true);
  window.addEventListener("keydown", unlock, true);
}

function scheduleNote(context: AudioContext, destination: AudioNode, startAt: number, note: Note) {
  const begin = startAt + note.at;
  const end = begin + note.duration;
  const oscillator = context.createOscillator();
  oscillator.type = note.type;
  oscillator.frequency.setValueAtTime(note.frequency, begin);
  if (note.frequencyEnd) oscillator.frequency.exponentialRampToValueAtTime(note.frequencyEnd, end);

  const envelope = context.createGain();
  envelope.gain.setValueAtTime(0.0001, begin);
  envelope.gain.exponentialRampToValueAtTime(note.gain, begin + ATTACK_SECONDS);
  envelope.gain.exponentialRampToValueAtTime(0.0001, end);

  oscillator.connect(envelope).connect(destination);
  oscillator.start(begin);
  oscillator.stop(end + 0.02);
  oscillator.onended = () => {
    oscillator.disconnect();
    envelope.disconnect();
  };
}

export function playConsoleSound(cue: ConsoleSoundCue) {
  attachUnlockListeners();
  const current = createGraph();
  if (!current) return;
  const { context, input } = current;
  const play = () => {
    const startAt = context.currentTime + 0.005;
    for (const note of CONSOLE_SOUND_CUES[cue]) scheduleNote(context, input, startAt, note);
  };
  if (context.state === "running") {
    play();
    return;
  }
  // Dang suspended: thu resume; neu trinh duyet chan thi bo qua cue nay, khong bao loi.
  void context.resume().then(() => {
    if (context.state === "running") play();
  }).catch(() => undefined);
}
