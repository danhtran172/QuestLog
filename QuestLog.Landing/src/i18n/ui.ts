// Toan bo chuoi hien thi cua landing. Them ngon ngu = them mot khoa o day
// va mot thu muc trong src/pages.
//
// Ten dai diem, nhan tray va nut tay cam lay dung chu cua app
// (MasterPlay.Web/src/lib/i18n.tsx) de khach thay khop khi cai.
// Khong quang cao tinh nang dang tat (backup save: FeatureFlags.GameBackup).

export const locales = ["vi", "en"] as const;
export type Locale = (typeof locales)[number];

/** Ba chang tren dong thoi gian: da choi -> dang choi -> sap choi. */
export type StopKey = "done" | "playing" | "nextplay";

/** Mot dong cua tieu de lon; doan co `accent` duoc to mau. */
export type TitleLine = { text: string; accent?: boolean }[];

export interface Stop {
  key: StopKey;
  label: string;
  title: string;
  body: string;
  tags: string[];
}

export interface OrganizeCard {
  title: string;
  body: string;
}

export interface Band {
  name: string;
  range: string;
}

export interface Dictionary {
  meta: { title: string; description: string };
  nav: { timeline: string; organize: string; scores: string; download: string; switchLang: string; menu: string };
  hero: {
    eyebrow: string;
    title: TitleLine[];
    titlePlain: string;
    subtitle: string;
    cta: string;
    secondary: string;
    note: string;
  };
  glance: {
    label: string;
    finished: string;
    playing: string;
    next: string;
    upcoming: string;
    scoreLabel: string;
    excitedLabel: string;
    playtimeLabel: string;
  };
  shots: {
    library: string;
    dashboard: string;
    detail: string;
    queue: string;
    releases: string;
    filter: string;
    themes: string;
    sortProfile: string;
    console: string;
    scoreCard: string;
  };
  timeline: { eyebrow: string; title: string; intro: string; stops: [Stop, Stop, Stop] };
  tray: { title: string; running: string; items: [string, string] };
  console: {
    eyebrow: string;
    title: string;
    body: string;
    details: string;
    play: string;
    changeTab: string;
    keyboard: string;
  };
  organize: {
    eyebrow: string;
    title: string;
    intro: string;
    filter: OrganizeCard;
    themes: OrganizeCard;
    labels: OrganizeCard;
    sort: OrganizeCard;
  };
  scores: {
    eyebrow: string;
    title: string;
    intro: string;
    score: { title: string; body: string; bands: Band[] };
    excited: { title: string; body: string; bands: Band[] };
  };
  download: { eyebrow: string; title: string; body: string; cta: string; requirements: string; unsigned: string };
  footer: { releases: string };
}

/* Nguong dai diem: LibraryQuery.ScoreMaterialBand (Host) va excitedBandOf (web). */
const SCORE_RANGES = ["0–3", "3–6", "6–8", "8–9", "9–10", "10", "10+"];
const EXCITED_RANGES = ["0–3", "3–6", "6–8", "8–9", "9–10", "10+"];
const SCORE_NAMES = ["Rust", "Copper", "Silver", "Gold", "Platinum", "Diamond", "Opal"];

const bands = (names: string[], ranges: string[]): Band[] => names.map((name, i) => ({ name, range: ranges[i] }));

export const ui: Record<Locale, Dictionary> = {
  vi: {
    meta: {
      title: "QuestLog — Quản lý thư viện game: đã chơi gì, sắp chơi gì",
      description:
        "Ứng dụng Windows quản lý thư viện game: phân loại theo trạng thái, nơi sở hữu, thể loại và chủ đề; chấm điểm cá nhân và điểm háo hức; tự ghi giờ chơi, lịch sử hoàn thành và hàng chờ chơi.",
    },
    nav: {
      timeline: "Theo dõi",
      organize: "Phân loại",
      scores: "Chấm điểm",
      download: "Tải xuống",
      switchLang: "Switch to English",
      menu: "Điều hướng chính",
    },
    hero: {
      eyebrow: "Quản lý thư viện game trên Windows",
      title: [[{ text: "Đã chơi gì." }], [{ text: "Sắp chơi gì.", accent: true }]],
      titlePlain: "Đã chơi gì. Sắp chơi gì.",
      subtitle:
        "QuestLog sắp xếp cả thư viện game của bạn: phân loại theo trạng thái, nơi sở hữu, thể loại và chủ đề; chấm điểm cá nhân và điểm háo hức; tự ghi giờ chơi và lịch sử hoàn thành.",
      cta: "Tải cho Windows",
      secondary: "Xem tính năng",
      note: "Windows 10/11 · 64-bit · tự báo khi có bản mới",
    },
    glance: {
      label: "Thư viện của bạn",
      finished: "Vừa hoàn thành",
      playing: "Đang chơi",
      next: "Chơi tiếp",
      upcoming: "Sắp ra mắt",
      scoreLabel: "Điểm",
      excitedLabel: "Háo hức",
      playtimeLabel: "Tổng giờ chơi",
    },
    shots: {
      library:
        "Thư viện trong QuestLog dạng lưới: ảnh bìa, trạng thái tiến độ, nơi sở hữu, điểm cá nhân, MetaScore và giờ chơi của từng game",
      dashboard:
        "Dashboard của QuestLog: tổng số game, số game hoàn thành, tổng giờ chơi, điểm trung bình và biểu đồ hoàn thành theo năm",
      detail: "Chi tiết một game đang chơi: giờ đã chơi, thời lượng trung bình, điểm háo hức, thể loại và chủ đề",
      queue: "Hàng chờ chơi: game đang chơi đứng đầu, các game tiếp theo xếp theo thứ tự",
      releases: "Thông báo phát hành của hai game sắp ra mắt kèm số ngày còn lại",
      filter: "Bộ lọc kết hợp trạng thái, nơi sở hữu, thể loại, chủ đề, MetaScore, độ dài, điểm và độ háo hức",
      themes: "Chọn chủ đề theo nhóm",
      sortProfile: "Thứ tự sắp xếp mặc định của một tab, kéo thả để đổi",
      console: "Chế độ console với băng chuyền ảnh bìa cỡ lớn và nút Chơi",
      scoreCard: "Card game với điểm cá nhân và MetaScore",
    },
    timeline: {
      eyebrow: "Theo dõi",
      title: "Cả thư viện, theo dòng thời gian của bạn",
      intro: "QuestLog nhớ hộ bạn đã chơi xong gì, đang chơi gì và định chơi gì tiếp.",
      stops: [
        {
          key: "done",
          label: "Đã chơi",
          title: "Nhìn lại mọi game đã phá đảo",
          body: "Mỗi lần chơi hết được ghi lại cùng tháng hoàn thành và điểm của bạn, kể cả khi chơi lại. Dashboard cộng dồn tất cả: số game hoàn thành mỗi năm, tổng giờ chơi, phân bố điểm, và đặt điểm của bạn cạnh MetaScore.",
          tags: ["Lịch sử hoàn thành", "Hoàn thành theo năm", "Điểm của bạn so với MetaScore"],
        },
        {
          key: "playing",
          label: "Đang chơi",
          title: "Giờ chơi thật, tự ghi lại",
          body: "Mở game ngay từ thư viện. Ứng dụng khay hệ thống đếm thời gian bạn thực sự ở trong game và quét thư mục để nhận ra game đã cài; trang chi tiết hiện tổng giờ chơi cùng thời lượng trung bình của game.",
          tags: ["Giờ chơi thật", "Quét thư viện", "Mở game từ thư viện"],
        },
        {
          key: "nextplay",
          label: "Sắp chơi",
          title: "Luôn biết game tiếp theo",
          body: "Kéo thả để xếp Hàng chờ chơi, game đang chơi luôn đứng đầu. Game sắp ra mắt đếm ngược từng ngày và báo cho bạn khi tới ngày phát hành.",
          tags: ["Hàng chờ kéo thả", "Thông báo phát hành", "Ưu tiên game háo hức nhất"],
        },
      ],
    },
    tray: {
      title: "Dịch vụ nền",
      running: "Đang chạy",
      items: ["Theo dõi giờ chơi", "Quét thư viện"],
    },
    console: {
      eyebrow: "Chế độ console",
      title: "Ngả lưng. Cầm tay cầm.",
      body: "Nhấn F10, hoặc giữ A + LB + RB trên tay cầm, để chuyển sang giao diện toàn màn hình: lướt qua ảnh bìa, mở chi tiết và vào game mà không cần chuột.",
      details: "Chi tiết",
      play: "Chơi",
      changeTab: "Đổi tab",
      keyboard: "Bật / tắt",
    },
    organize: {
      eyebrow: "Phân loại",
      title: "Phân loại theo cách của bạn",
      intro: "Mỗi game có trạng thái, nơi sở hữu, thể loại, chủ đề và series. Bộ lọc kết hợp được tất cả cùng lúc.",
      filter: {
        title: "Bộ lọc nhiều tầng",
        body: "Kết hợp trạng thái, nơi sở hữu, thể loại, chủ đề, MetaScore, độ dài, điểm, độ háo hức và năm hoàn thành. Bật lọc ngược để xem phần còn lại.",
      },
      themes: {
        title: "Chủ đề theo nhóm",
        body: "Tạo chủ đề và nhóm của riêng bạn, kéo thả chủ đề vào nhóm nào cũng được.",
      },
      labels: {
        title: "Giao diện trực quan",
        body: "Mỗi card gom đủ ảnh bìa, trạng thái, nơi sở hữu, điểm của bạn và MetaScore — đẹp, gọn, nhìn là biết game đang ở đâu.",
      },
      sort: {
        title: "Mỗi tab một thứ tự",
        body: "Kéo thả các tiêu chí để từng tab tự sắp xếp theo ý bạn.",
      },
    },
    scores: {
      eyebrow: "Chấm điểm",
      title: "Hai thang điểm, đều là của bạn",
      intro:
        "Điểm cá nhân cho game đã chơi, điểm háo hức cho game sắp chơi. Cả hai từ 0 tới 10, bước 0,5; mức 11 là Peak Score, hiện thành 10+.",
      score: {
        title: "Điểm cá nhân",
        body: "Mỗi khoảng điểm mang một chất liệu riêng, từ Rust tới Opal. Dashboard đặt điểm của bạn cạnh MetaScore để thấy mình hợp hay lệch giới phê bình.",
        bands: bands(SCORE_NAMES, SCORE_RANGES),
      },
      excited: {
        title: "Điểm háo hức",
        body: "Càng muốn chơi càng nóng, từ lạnh cực độ tới dung nham. Khi bạn chưa tự xếp, Hàng chờ chơi đưa game nóng nhất lên trước.",
        bands: bands(["Lạnh cực độ", "Lạnh", "Trung tính", "Nóng", "Nóng cực độ", "Dung nham"], EXCITED_RANGES),
      },
    },
    download: {
      eyebrow: "Tải xuống",
      title: "Đã chơi gì, sắp chơi gì, QuestLog nhắc cho bạn",
      body: "Cài một lần cho Windows. Đăng nhập để mang thư viện sang máy khác; app tự kiểm tra và báo khi có bản mới.",
      cta: "Tải cho Windows",
      requirements: "Windows 10 trở lên · 64-bit",
      unsigned: "Bản cài chưa ký số, nên lần đầu Windows SmartScreen có thể cảnh báo: chọn “Thông tin khác” rồi “Vẫn chạy”.",
    },
    footer: { releases: "Các bản phát hành" },
  },
  en: {
    meta: {
      title: "QuestLog — Know what you've played and what's next",
      description:
        "A Windows game library manager: organize by status, ownership, genre and theme; rate games with your own score and excitement; log playtime, finish history and your play queue automatically.",
    },
    nav: {
      timeline: "Tracking",
      organize: "Organize",
      scores: "Scores",
      download: "Download",
      switchLang: "Chuyển sang tiếng Việt",
      menu: "Main navigation",
    },
    hero: {
      eyebrow: "Game library manager for Windows",
      title: [[{ text: "What you played." }], [{ text: "What's next.", accent: true }]],
      titlePlain: "What you played. What's next.",
      subtitle:
        "QuestLog organizes your whole game library: by status, ownership, genre and theme. Rate games with your own score and excitement, and let it log your playtime and every finish.",
      cta: "Download for Windows",
      secondary: "See the features",
      note: "Windows 10/11 · 64-bit · tells you when an update is ready",
    },
    glance: {
      label: "Your library",
      finished: "Just finished",
      playing: "Playing",
      next: "Up next",
      upcoming: "Coming soon",
      scoreLabel: "Score",
      excitedLabel: "Excited",
      playtimeLabel: "Total playtime",
    },
    shots: {
      library:
        "QuestLog's library grid: cover, progress status, ownership, personal score, MetaScore and playtime for every game",
      dashboard:
        "The QuestLog dashboard: total games, finished games, total playtime, average score and finishes per year",
      detail: "Details of a game in progress: hours played, average length, excitement, genres and themes",
      queue: "The Play Queue: the game you're playing on top, the next games in order",
      releases: "Release notifications for two upcoming games with the days left",
      filter: "Filters combining status, ownership, genre, theme, MetaScore, length, score and excitement",
      themes: "Picking themes by group",
      sortProfile: "A tab's default sort order, drag to rearrange",
      console: "Console mode with a large cover carousel and a Play button",
      scoreCard: "A game card with a personal score and MetaScore",
    },
    timeline: {
      eyebrow: "Tracking",
      title: "Your whole library, on your timeline",
      intro: "QuestLog remembers what you've finished, what you're playing and what you plan to play next.",
      stops: [
        {
          key: "done",
          label: "Played",
          title: "Look back on everything you've finished",
          body: "Every finish is logged with the month and your score, replays included. The dashboard adds it all up: finishes per year, total playtime, how your scores spread, and your score next to the MetaScore.",
          tags: ["Finish history", "Finishes per year", "Your score vs MetaScore"],
        },
        {
          key: "playing",
          label: "Playing",
          title: "Real playtime, logged for you",
          body: "Launch games straight from your library. The tray app counts the time you actually spend in game and scans your folders for installed games; the detail view shows your total playtime alongside the game's average length.",
          tags: ["Real playtime", "Library scanner", "Launch from the library"],
        },
        {
          key: "nextplay",
          label: "Up next",
          title: "Always know what's next",
          body: "Drag to order your Play Queue; the game you're playing stays on top. Upcoming games count down the days and notify you on release day.",
          tags: ["Drag-to-order queue", "Release notifications", "Most anticipated first"],
        },
      ],
    },
    tray: {
      title: "Tray services",
      running: "Running",
      items: ["Playtime tracking", "Library scanner"],
    },
    console: {
      eyebrow: "Console mode",
      title: "Sit back. Grab a controller.",
      body: "Press F10, or hold A + LB + RB on a controller, to switch to a full-screen view: flip through covers, open details and launch games without a mouse.",
      details: "Details",
      play: "Play",
      changeTab: "Change tab",
      keyboard: "Toggle",
    },
    organize: {
      eyebrow: "Organize",
      title: "Organize it your way",
      intro: "Every game has a status, ownership, genres, themes and a series. Filters combine all of them at once.",
      filter: {
        title: "Layered filters",
        body: "Combine status, ownership, genre, theme, MetaScore, length, score, excitement and finished year. Flip to a reverse filter to see everything else.",
      },
      themes: {
        title: "Themes in groups",
        body: "Create your own themes and groups, and drag any theme into any group.",
      },
      labels: {
        title: "An intuitive interface",
        body: "Every card brings together the cover, status, ownership, your score and MetaScore — clean and tidy, so you see where each game stands at a glance.",
      },
      sort: {
        title: "A sort order per tab",
        body: "Drag sort fields into order so each tab sorts the way you like.",
      },
    },
    scores: {
      eyebrow: "Scores",
      title: "Two scores, both yours",
      intro:
        "A personal score for what you've played, an excitement score for what's next. Both run from 0 to 10 in half steps; 11 is the Peak Score, shown as 10+.",
      score: {
        title: "Your score",
        body: "Each range gets its own material, from Rust up to Opal. The dashboard sets your score beside the MetaScore, so you see where you and the critics disagree.",
        bands: bands(SCORE_NAMES, SCORE_RANGES),
      },
      excited: {
        title: "Excitement",
        body: "The more you want a game, the hotter it runs, from extreme cold to lava. Until you order it yourself, the Play Queue puts the hottest games first.",
        bands: bands(["Extreme cold", "Cold", "Neutral", "Fire", "Extreme fire", "Lava"], EXCITED_RANGES),
      },
    },
    download: {
      eyebrow: "Download",
      title: "What you played, what's next, QuestLog keeps track for you",
      body: "Install it once on Windows. Sign in to take your library to another PC; the app checks for updates and tells you when one is ready.",
      cta: "Download for Windows",
      requirements: "Windows 10 or later · 64-bit",
      unsigned: "The installer is not code-signed yet, so Windows SmartScreen may warn the first time: choose “More info”, then “Run anyway”.",
    },
    footer: { releases: "All releases" },
  },
};

export const localePath: Record<Locale, string> = { vi: "/", en: "/en/" };
