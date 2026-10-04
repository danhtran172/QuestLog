import type {
  FinishGameRequest,
  GameDetail,
  GameMetadataPreview,
  UserDataPatch,
  UpdateFinishTimeRequest,
} from "@/lib/api/domain";
import { httpRequest } from "./http";

export type ExternalLinks = {
  hltbUrl?: string;
  metacriticUrl?: string;
};

export type SyncSource = "metadata" | "card-cover" | "cover" | "portrait-cover";
export type FilePickerFilter = "exe-or-shortcut" | "database" | "json";

type SelectedPath = { path?: string | null };
type BackupFolder = { backupFolderPath: string };

export const gameDetailApi = {
  get: (id: string) => httpRequest<GameDetail>(`/api/games/${encodeURIComponent(id)}`),
  create: (input: GameDetail) =>
    httpRequest<GameDetail>("/api/games", {
      method: "POST",
      body: JSON.stringify(input),
    }),
  update: (id: string, input: GameDetail) =>
    httpRequest<GameDetail>(`/api/games/${encodeURIComponent(id)}`, {
      method: "PUT",
      body: JSON.stringify(input),
    }),
  updateUserData: (id: string, input: UserDataPatch) =>
    httpRequest<GameDetail>(`/api/games/${encodeURIComponent(id)}/userdata`, {
      method: "PATCH",
      body: JSON.stringify(input),
    }),
  finish: (id: string, input: FinishGameRequest) =>
    httpRequest<GameDetail>(`/api/games/${encodeURIComponent(id)}/finishes`, {
      method: "POST",
      body: JSON.stringify(input),
    }),
  updateFinishTime: (gameId: string, finishId: number, input: UpdateFinishTimeRequest) =>
    httpRequest<GameDetail>(
      `/api/games/${encodeURIComponent(gameId)}/finishes/${finishId}`,
      {
        method: "PUT",
        body: JSON.stringify(input),
      },
    ),
  deleteFinish: (gameId: string, finishId: number) =>
    httpRequest<GameDetail>(
      `/api/games/${encodeURIComponent(gameId)}/finishes/${finishId}`,
      { method: "DELETE" },
    ),
  externalLinks: (id: string) =>
    httpRequest<ExternalLinks>(`/api/games/${encodeURIComponent(id)}/external-links`),
  revealFolder: (path: string) =>
    httpRequest<void>("/api/fs/reveal", {
      method: "POST",
      body: JSON.stringify({ path }),
    }),
  pickFolder: () =>
    httpRequest<SelectedPath>("/api/fs/pick-folder", { method: "POST" }),
  pickFile: (filter: FilePickerFilter) =>
    httpRequest<SelectedPath>("/api/fs/pick-file", {
      method: "POST",
      body: JSON.stringify({ filter }),
    }),
  generateBackupFolder: (id: string) =>
    httpRequest<BackupFolder>(`/api/games/${encodeURIComponent(id)}/backup-folder/generate`, {
      method: "POST",
    }),
  adjustPlaytime: (id: string, deltaMinutes: number) =>
    httpRequest<GameDetail>(`/api/games/${encodeURIComponent(id)}/playtime`, {
      method: "POST",
      body: JSON.stringify({ deltaMinutes }),
    }),
  /**
   * Preview metadata tu provider ngoai theo TEN — chi de dien draft, KHONG ghi DB.
   * Dung duoc ca khi them game moi (chua co id). Nguoi dung phai bam Save moi luu.
   */
  previewMetadata: (name: string, source: SyncSource) =>
    httpRequest<GameMetadataPreview>("/api/game-metadata/preview", {
      method: "POST",
      body: JSON.stringify({ name, source }),
    }),
  disable: (id: string) =>
    httpRequest<void>(`/api/games/${encodeURIComponent(id)}/disable`, { method: "POST" }),
  launch: (id: string) =>
    httpRequest<void>(`/api/games/${encodeURIComponent(id)}/launch`, { method: "POST" }),
};
