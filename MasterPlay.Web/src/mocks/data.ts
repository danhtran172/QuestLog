/**
 * Du lieu mock — CHI dung khi chua co backend (VITE_USE_MOCKS=true).
 *
 * Moi object o day deu duoc kiem kieu theo types.gen.ts (sinh tu openapi.yaml),
 * nen mock KHONG the tu bia field. Xem AGENTS.md muc 1.
 *
 * PHAI XOA khi backend that san sang (Giai doan 3).
 */
import type {
  AvgBand,
  CatalogItem,
  GameListItem,
  MetaBand,
  ScoreMaterialBand,
} from "@/lib/api/domain";

/** Progress — sortOrder la NGUON THU HANG SORT (DOMAIN_RULES §3.2), khong phai id. */
export const PROGRESS: CatalogItem[] = [
  { id: 1, systemKey: "Done", name: "Done", displayName: "Done", colorCode: "#4ade80", iconData: null, sortOrder: 0 },
  { id: 2, systemKey: "Playing", name: "Playing", displayName: "Playing", colorCode: "#60a5fa", iconData: null, sortOrder: 1 },
  { id: 3, systemKey: "NextPlay", name: "Next Play", displayName: "Next Play", colorCode: "#fbbf24", iconData: null, sortOrder: 2 },
  { id: 4, systemKey: "Intended", name: "Intended", displayName: "Intended", colorCode: "#c084fc", iconData: null, sortOrder: 3 },
  { id: 5, systemKey: "Dropped", name: "Dropped", displayName: "Dropped", colorCode: "#f87171", iconData: null, sortOrder: 4 },
  { id: 6, systemKey: "Pending", name: "Pending", displayName: "Pending", colorCode: "#94a3b8", iconData: null, sortOrder: 5 },
];

export const OWNERSHIP: CatalogItem[] = [
  { id: 1, systemKey: "Ready", name: "Ready", displayName: "Ready", colorCode: "#4ade80", iconData: null, sortOrder: 0 },
  { id: 2, systemKey: "Pirated", name: "Pirated", displayName: "Pirated", colorCode: "#fbbf24", iconData: null, sortOrder: 1, infoLayout: "download" },
  { id: 3, systemKey: "GamePass", name: "Game Pass", displayName: "Game Pass", colorCode: "#4ade80", iconData: null, sortOrder: 2 },
  { id: 4, systemKey: "NotOwned", name: "Not owned", displayName: "Not owned", colorCode: "#94a3b8", iconData: null, sortOrder: 3 },
  { id: 5, systemKey: "PSRequired", name: "PS required", displayName: "PS required", colorCode: "#c084fc", iconData: null, sortOrder: 4 },
  { id: 6, systemKey: "SteamOff", name: "Steam offline", displayName: "Steam offline", colorCode: "#a3a3a3", iconData: null, sortOrder: 5, infoLayout: "sharedAccount" },
];

/** Nguong dai — PHAI khop DOMAIN_RULES §5.5. Backend that cung dung bang nay. */
export function avgBandOf(hours: number | null | undefined): AvgBand | null {
  if (hours == null || hours <= 0) return null;
  if (hours >= 80) return "VeryLong";
  if (hours >= 60) return "Long";
  if (hours >= 35) return "Medium";
  if (hours >= 20) return "Short";
  return "VeryShort";
}

/** Nguong MetaScore — ranh gioi Low/VeryLow la 50, KHONG phai 60 (mismatch M16). */
export function metaBandOf(meta: number | null | undefined): MetaBand | null {
  if (meta == null || meta <= 0) return null;
  if (meta >= 90) return "VeryHigh";
  if (meta >= 80) return "High";
  if (meta >= 70) return "Medium";
  if (meta >= 50) return "Low";
  return "VeryLow";
}

const TITLES = [
  ["Elden Ring", "FromSoftware", "Bandai Namco", 96, 2022, 120],
  ["Baldur's Gate 3", "Larian Studios", "Larian Studios", 96, 2023, 95],
  ["Hades II", "Supergiant Games", "Supergiant Games", 92, 2024, 40],
  ["Balatro", "LocalThunk", "Playstack", 90, 2024, 25],
  ["Silent Hill 2", "Bloober Team", "Konami", 86, 2024, 18],
  ["Cyberpunk 2077", "CD Projekt Red", "CD Projekt", 86, 2020, 62],
  ["Hollow Knight", "Team Cherry", "Team Cherry", 90, 2017, 41],
  ["Stardew Valley", "ConcernedApe", "ConcernedApe", 89, 2016, 95],
  ["Dave the Diver", "Mintrocket", "Mintrocket", 89, 2023, 32],
  ["Sea of Stars", "Sabotage Studio", "Sabotage Studio", 87, 2023, 30],
  ["Lies of P", "Round8 Studio", "Neowiz", 80, 2023, 35],
  ["Alan Wake 2", "Remedy", "Epic Games", 89, 2023, 22],
  ["Persona 3 Reload", "Atlus", "Sega", 86, 2024, 88],
  ["Armored Core VI", "FromSoftware", "Bandai Namco", 86, 2023, 28],
  ["Star Rail", "HoYoverse", "HoYoverse", 82, 2023, 210],
  ["Dredge", "Black Salt Games", "Team17", 81, 2023, 15],
  ["Cocoon", "Geometric Interactive", "Annapurna", 87, 2023, 6],
  ["Pizza Tower", "Tour De Pizza", "Tour De Pizza", 89, 2023, 12],
  ["Blasphemous 2", "The Game Kitchen", "Team17", 81, 2023, 24],
  ["Remnant II", "Gunfire Games", "Gearbox", 78, 2023, 45],
  ["Atomic Heart", "Mundfish", "Focus", 72, 2023, 26],
  ["Forspoken", "Luminous", "Square Enix", 64, 2023, 30],
  ["Redfall", "Arkane Austin", "Bethesda", 56, 2023, 14],
  ["Gollum", "Daedalic", "Nacon", 34, 2023, 9],
  ["Skull Island", "IguanaBee", "GameMill", 32, 2023, 5],
] as const;

/** Fixture UI khai bao cap score/band; frontend khong tu tinh nguong. */
const SCORE_FIXTURES: readonly {
  score: number;
  band: ScoreMaterialBand;
}[] = [
  { score: 2.5, band: "Rust" },
  { score: 3, band: "Copper" },
  { score: 5.5, band: "Copper" },
  { score: 6, band: "Silver" },
  { score: 7.5, band: "Silver" },
  { score: 8, band: "Gold" },
  { score: 8.5, band: "Gold" },
  { score: 9, band: "Platinum" },
  { score: 9.5, band: "Platinum" },
  { score: 10, band: "Diamond" },
  { score: 11, band: "Opal" },
] as const;

function pick<T>(arr: readonly T[], i: number): T {
  return arr[i % arr.length]!;
}

/**
 * Sinh danh sach game mock.
 * `excited` co ca gia tri thang 0-10 LAN Peak Score 11 de test dung ban chat du lieu that.
 */
export const MOCK_GAMES: GameListItem[] = Array.from({ length: 53 }, (_, i) => {
  const [name, developer, publisher, meta, year, avgHours] = pick(TITLES, i);
  // Buoc nhay phai NGUYEN TO CUNG NHAU voi do dai mang, neu khong phan bo bi thoai hoa.
  // (i*3 mod 6 chi sinh ra 2 gia tri -> khong game nao o trang thai Done.)
  const progress = pick(PROGRESS, i * 7 + 1);
  const ownership = pick(OWNERSHIP, i * 5);
  const suffix = i >= TITLES.length ? ` ${Math.floor(i / TITLES.length) + 1}` : "";

  // Rai gia tri excited tren toan thang 1-10, cu 11 game thi co 1 Peak Score.
  const excited = i % 11 === 0 ? 11 : ((i % 10) + 1);
  const isFinished = progress.systemKey === "Done";
  const scoreFixture = isFinished ? pick(SCORE_FIXTURES, Math.floor(i / 6)) : null;

  return {
    id: `game-${i + 1}`,
    name: `${name}${suffix}`,
    developer,
    publisher,
    series: null,
    bannerPath: null,
    posterPath: null,
    progress,
    ownership,
    metaScore: meta,
    metaScoreBand: metaBandOf(meta),
    excited,
    score: scoreFixture?.score ?? null,
    scoreMaterialBand: scoreFixture?.band ?? null,
    totalPlaytimeSeconds: (i % 97) * 3600 + (i % 60) * 60,
    avgPlaytimeHours: avgHours,
    avgPlaytimeBand: avgBandOf(avgHours),
    publishDate: `${year}-0${(i % 9) + 1}-1${i % 10}`,
    lastFinishedAt: isFinished
      ? `${2020 + (i % 6)}-${String((i % 12) + 1).padStart(2, "0")}-01T00:00:00`
      : null,
    isDlc: false,
    isUpcoming: false,
    ready: i % 4 !== 0,
    isRunning: i === 3,
    canLaunch: i % 4 !== 0,
    folderWarning: i % 17 === 0 ? "Save folder not found" : null,
  } satisfies GameListItem;
});
