namespace MasterPlay.Core
{
    /// <summary>
    /// Cong tat tinh nang dung chung cho Host, Tray va Infrastructure.
    /// Web khong doc truc tiep: Host bao qua GET /api/capabilities.
    /// </summary>
    public static class FeatureFlags
    {
        /// <summary>
        /// Backup save game (BackupService cua Tray, tao thu muc backup, chon
        /// thu muc backup goc). Tam tat; du lieu BackupFolderPath/BackupRootPath
        /// van giu nguyen trong DB/config de bat lai khong mat gi.
        /// static readonly thay vi const de khong sinh canh bao "unreachable code".
        /// </summary>
        public static readonly bool GameBackup = false;
    }
}
