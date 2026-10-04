// Toan bo chuoi hien thi cua landing. Them ngon ngu = them mot khoa o day
// va mot thu muc trong src/pages.
//
// Nhan trang thai (Planned, Nextplay...), nhan nut tray va nut tay cam lay
// dung chu cua app (MasterPlay.Web/src/lib/i18n.tsx) de khach thay khop khi cai.

export const locales = ["vi", "en"] as const;
export type Locale = (typeof locales)[number];

export type StopKey = "planned" | "nextplay" | "playing" | "done";

/** Mot dong cua tieu de lon; doan co `accent` duoc to mau. */
export type TitleLine = { text: string; accent?: boolean }[];

export interface Stop {
  key: StopKey;
  status: string;
  title: string;
  body: string;
  tags: string[];
}

export interface Dictionary {
  meta: { title: string; description: string };
  nav: { journey: string; console: string; download: string; switchLang: string; menu: string };
  hero: {
    eyebrow: string;
    title: TitleLine[];
    titlePlain: string;
    subtitle: string;
    cta: string;
    secondary: string;
    note: string;
  };
  tracker: {
    label: string;
    title: string;
    objectives: [string, string, string, string];
    progressLabel: string;
  };
  shots: {
    library: string;
    list: string;
    queue: string;
    filter: string;
    detail: string;
    console: string;
    cardPlaying: string;
    cardDone: string;
  };
  journey: { eyebrow: string; title: string; intro: string; stops: [Stop, Stop, Stop, Stop] };
  tray: { title: string; running: string; items: [string, string, string] };
  quest: { label: string; game: string; meta: string };
  console: {
    eyebrow: string;
    title: string;
    body: string;
    details: string;
    play: string;
    changeTab: string;
    keyboard: string;
  };
  download: { eyebrow: string; title: string; body: string; cta: string; requirements: string };
  footer: { tagline: string; releases: string };
}

export const ui: Record<Locale, Dictionary> = {
  vi: {
    meta: {
      title: "QuestLog — Biến backlog game thành nhật ký nhiệm vụ",
      description:
        "QuestLog gom mọi game bạn có và muốn chơi về một thư viện trên Windows: xếp hàng game chơi tiếp, ghi lại giờ chơi thật và tự động sao lưu save.",
    },
    nav: {
      journey: "Hành trình",
      console: "Console",
      download: "Tải xuống",
      switchLang: "Switch to English",
      menu: "Điều hướng chính",
    },
    hero: {
      eyebrow: "Ứng dụng Windows cho người chơi PC",
      title: [
        [{ text: "Biến backlog" }],
        [{ text: "thành " }, { text: "nhật ký", accent: true }],
        [{ text: "nhiệm vụ", accent: true }, { text: "." }],
      ],
      titlePlain: "Biến backlog thành nhật ký nhiệm vụ.",
      subtitle:
        "QuestLog gom mọi game bạn có và muốn chơi về một thư viện, xếp hàng game chơi tiếp, ghi lại giờ chơi thật và tự động sao lưu save.",
      cta: "Tải cho Windows",
      secondary: "Xem hành trình",
      note: "Windows 10/11 · 64-bit",
    },
    tracker: {
      label: "Nhiệm vụ chính",
      title: "Dọn backlog",
      objectives: [
        "Gom mọi game về một thư viện",
        "Xếp hàng game chơi tiếp",
        "Chơi Assassin's Creed Black Flag Resynced",
        "Đánh dấu Đã xong",
      ],
      progressLabel: "Giờ đã chơi so với thời lượng trung bình",
    },
    shots: {
      library:
        "Thư viện trong QuestLog dạng lưới: ảnh bìa, trạng thái tiến độ, nơi sở hữu, điểm và giờ chơi của từng game",
      list: "Thư viện dạng danh sách với ngày phát hành, tiến độ, mức hào hứng, MetaScore, thời lượng trung bình và nơi sở hữu",
      queue: "Hàng chờ chơi với chín game được xếp theo thứ tự",
      filter: "Bộ lọc theo năm hoàn thành, mức hào hứng, điểm, tổng giờ chơi, độ dài và MetaScore",
      detail:
        "Chi tiết game Assassin's Creed Black Flag Resynced: đang chơi, 40 giờ chơi, MetaScore 84, thể loại và chủ đề",
      console: "Chế độ console với băng chuyền ảnh bìa cỡ lớn và nút Chơi",
      cardPlaying: "Card game Assassin's Creed Black Flag Resynced đang ở trạng thái Playing",
      cardDone: "Card game 007 First Light ở trạng thái Done, hoàn thành tháng 06/2026",
    },
    journey: {
      eyebrow: "Hành trình",
      title: "Một game đi qua backlog của bạn thế nào",
      intro: "Mỗi game trong QuestLog có một trạng thái tiến độ. Đây là việc app làm cho bạn ở từng chặng.",
      stops: [
        {
          key: "planned",
          status: "Planned",
          title: "Mọi game một chỗ, kể cả game chưa ra",
          body: "Tìm game theo tên, QuestLog tự điền ảnh bìa, nhà phát triển, ngày phát hành, MetaScore và thời lượng trung bình. Ghi lại bạn sở hữu game ở đâu: đã mua, được chia sẻ, gói thuê bao hay mới nằm trong wishlist.",
          tags: ["Ảnh bìa tự động", "MetaScore", "Thời lượng trung bình", "Nơi sở hữu"],
        },
        {
          key: "nextplay",
          status: "Nextplay",
          title: "Biết ngay tối nay chơi gì",
          body: "Kéo thả để xếp Hàng chờ chơi theo thứ tự bạn muốn. Lọc theo MetaScore, độ dài game, mức hào hứng hay năm hoàn thành để chọn đúng game cho tâm trạng hôm nay.",
          tags: ["Kéo thả", "Bộ lọc", "Sắp xếp"],
        },
        {
          key: "playing",
          status: "Playing",
          title: "Bấm Chơi, phần còn lại để QuestLog lo",
          body: "Mở game ngay từ thư viện. Ứng dụng khay hệ thống ghi lại thời gian bạn thực sự ở trong game, quét thư mục để tìm game đã cài và tự động sao lưu save.",
          tags: ["Giờ chơi thật", "Quét thư viện", "Sao lưu save"],
        },
        {
          key: "done",
          status: "Done",
          title: "Phá đảo rồi? Ghi lại.",
          body: "Đánh dấu Đã xong kèm tháng hoàn thành, chấm điểm theo cảm nhận của bạn và giữ lại lịch sử mỗi lần chơi hết game.",
          tags: ["Lịch sử hoàn thành", "Điểm của bạn"],
        },
      ],
    },
    tray: {
      title: "Dịch vụ nền",
      running: "Đang chạy",
      items: ["Theo dõi giờ chơi", "Quét thư viện", "Dịch vụ backup"],
    },
    quest: { label: "Nhiệm vụ hoàn thành", game: "007 First Light", meta: "20 giờ · 06/2026" },
    console: {
      eyebrow: "Chế độ console",
      title: "Ngả lưng. Cầm tay cầm.",
      body: "Nhấn F10, hoặc giữ A + LB + RB trên tay cầm, để chuyển sang giao diện toàn màn hình: lướt qua ảnh bìa, mở chi tiết và vào game mà không cần chuột.",
      details: "Chi tiết",
      play: "Chơi",
      changeTab: "Đổi tab",
      keyboard: "Bật / tắt",
    },
    download: {
      eyebrow: "Đích đến",
      title: "Bắt đầu nhiệm vụ đầu tiên",
      body: "Tải bản mới nhất cho Windows rồi thêm game đầu tiên vào thư viện.",
      cta: "Tải cho Windows",
      requirements: "Windows 10 trở lên · 64-bit",
    },
    footer: { tagline: "Làm cho người chơi, bởi người chơi.", releases: "Các bản phát hành" },
  },
  en: {
    meta: {
      title: "QuestLog — Turn your game backlog into a quest log",
      description:
        "QuestLog keeps every game you own or want in one Windows library: line up what to play next, record the hours you actually play and back up your saves automatically.",
    },
    nav: {
      journey: "Journey",
      console: "Console",
      download: "Download",
      switchLang: "Chuyển sang tiếng Việt",
      menu: "Main navigation",
    },
    hero: {
      eyebrow: "A Windows app for PC players",
      title: [
        [{ text: "Turn your" }],
        [{ text: "backlog into" }],
        [{ text: "a " }, { text: "quest log", accent: true }, { text: "." }],
      ],
      titlePlain: "Turn your backlog into a quest log.",
      subtitle:
        "QuestLog keeps every game you own or want in one library, lines up what to play next, records the hours you actually play and backs up your saves automatically.",
      cta: "Download for Windows",
      secondary: "See how it works",
      note: "Windows 10/11 · 64-bit",
    },
    tracker: {
      label: "Main quest",
      title: "Clear the backlog",
      objectives: [
        "Collect every game in one library",
        "Line up what to play next",
        "Play Assassin's Creed Black Flag Resynced",
        "Mark it Done",
      ],
      progressLabel: "Hours played against the average length",
    },
    shots: {
      library:
        "QuestLog's library grid: covers, progress status, ownership, scores and playtime for every game",
      list: "The library as a list with release dates, progress, excitement, MetaScore, average length and ownership",
      queue: "The Play Queue with nine games in order",
      filter: "Filters for finished year, excitement, score, total playtime, game length and MetaScore",
      detail:
        "Game details for Assassin's Creed Black Flag Resynced: playing, 40 hours played, MetaScore 84, genres and themes",
      console: "Console mode with a large cover carousel and a Play button",
      cardPlaying: "Game card for Assassin's Creed Black Flag Resynced with status Playing",
      cardDone: "Game card for 007 First Light with status Done, finished in June 2026",
    },
    journey: {
      eyebrow: "The journey",
      title: "How a game moves through your backlog",
      intro: "Every game in QuestLog has a progress status. Here's what the app does for you at each stop.",
      stops: [
        {
          key: "planned",
          status: "Planned",
          title: "Every game in one place, even unreleased ones",
          body: "Search by name and QuestLog fills in the cover, developer, release date, MetaScore and average length. Note where each game lives: bought, shared, on a subscription or still on your wishlist.",
          tags: ["Automatic covers", "MetaScore", "Average length", "Ownership"],
        },
        {
          key: "nextplay",
          status: "Nextplay",
          title: "Know what to play tonight",
          body: "Drag to order your Play Queue. Filter by MetaScore, game length, how excited you are or the year you finished to pick the right game for tonight's mood.",
          tags: ["Drag to reorder", "Filters", "Sorting"],
        },
        {
          key: "playing",
          status: "Playing",
          title: "Hit Play, QuestLog handles the rest",
          body: "Launch games straight from your library. The tray app records the time you actually spend in game, scans your folders for installed games and backs up your saves automatically.",
          tags: ["Real playtime", "Library scanner", "Save backups"],
        },
        {
          key: "done",
          status: "Done",
          title: "Finished it? Log it.",
          body: "Mark a game Done with the month you finished, score it your way and keep a history of every playthrough.",
          tags: ["Finish history", "Your score"],
        },
      ],
    },
    tray: {
      title: "Tray services",
      running: "Running",
      items: ["Playtime tracking", "Library scanner", "Backup service"],
    },
    quest: { label: "Quest complete", game: "007 First Light", meta: "20 hours · 06/2026" },
    console: {
      eyebrow: "Console mode",
      title: "Sit back. Grab a controller.",
      body: "Press F10, or hold A + LB + RB on a controller, to switch to a full-screen view: flip through covers, open details and launch games without a mouse.",
      details: "Details",
      play: "Play",
      changeTab: "Change tab",
      keyboard: "Toggle",
    },
    download: {
      eyebrow: "Destination",
      title: "Start your first quest",
      body: "Get the latest Windows build and add your first game to the library.",
      cta: "Download for Windows",
      requirements: "Windows 10 or later · 64-bit",
    },
    footer: { tagline: "Made for players, by players.", releases: "All releases" },
  },
};

export const localePath: Record<Locale, string> = { vi: "/", en: "/en/" };
