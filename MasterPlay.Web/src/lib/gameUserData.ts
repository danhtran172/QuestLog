import type { GameDetail, UserDataPatch } from "@/lib/api/domain";

/**
 * Endpoint userdata hien tai nhan mot snapshot cua nhom du lieu nguoi dung.
 * Moi quick edit phai gui lai cac field con lai de khong vo tinh xoa du lieu
 * khong nam trong thao tac dang sua.
 */
export function gameUserDataPatch(
  game: GameDetail,
  changes: UserDataPatch,
): UserDataPatch {
  return {
    score: game.score ?? null,
    excited: game.excited ?? null,
    totalPlaytimeSeconds: game.totalPlaytimeSeconds ?? null,
    progressId: game.progress?.id ?? null,
    ownershipId: game.ownership?.id ?? null,
    exeName: game.exeName ?? null,
    launchTarget: game.launchTarget ?? null,
    saveFolderPath: game.saveFolderPath ?? null,
    backupFolderPath: game.backupFolderPath ?? null,
    ownershipInfo: game.ownershipInfo ?? null,
    ...changes,
  };
}
