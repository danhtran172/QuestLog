// Cau hinh dung chung cho landing. Gia tri that dat qua bien moi truong
// (xem .env.example) de doi link ma khong can sua code.

// Ban cai phat hanh qua repo public questlog-releases (tools/release.ps1, Velopack).
export const RELEASES_URL = "https://github.com/danhtran172/questlog-releases/releases";

// Bo cai cho chon thu muc (MasterPlay.Installer, boc Setup.exe cua Velopack). Ten file
// giong nhau o moi ban nen /latest/download/<file> luon tai thang ban moi nhat: bam
// mot lan la tai, khong phai lac vao trang Releases cua GitHub.
export const SETUP_FILE = "QuestLog-Setup.exe";

export const DOWNLOAD_URL = import.meta.env.PUBLIC_DOWNLOAD_URL ?? `${RELEASES_URL}/latest/download/${SETUP_FILE}`;
