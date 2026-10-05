// Hanh vi phia client cua landing. Trang van doc duoc day du khi khong co JS:
// cac moc sang san, tuyen duong hien net dut, khong co hieu ung xuat hien.

export function initLanding() {
  setupHeader();
  setupReveal();
  setupRoute();
}

function setupHeader() {
  const header = document.querySelector<HTMLElement>("[data-header]");
  if (!header) return;
  const update = () => header.toggleAttribute("data-scrolled", window.scrollY > 12);
  update();
  window.addEventListener("scroll", update, { passive: true });
}

function setupReveal() {
  if (!("IntersectionObserver" in window)) return;
  const items = document.querySelectorAll<HTMLElement>("[data-reveal]");
  const observer = new IntersectionObserver(
    (entries) => {
      for (const entry of entries) {
        if (!entry.isIntersecting) continue;
        entry.target.classList.add("is-in");
        observer.unobserve(entry.target);
      }
    },
    { rootMargin: "0px 0px -8% 0px", threshold: 0.06 },
  );
  items.forEach((el) => observer.observe(el));
  document.documentElement.classList.add("js");
}

/**
 * Ve tuyen duong qua cac moc [data-route-point] (theo thu tu DOM) va to dan
 * phan "da di qua" theo vi tri cuon. Moi doan la Bezier co diem dieu khien
 * nam giua hai moc theo truc y, nen y tang don dieu theo do dai duong —
 * nho vay tim do dai tuong ung voi mot y bang tim kiem nhi phan.
 */
function setupRoute() {
  const route = document.querySelector<HTMLElement>("[data-route]");
  const svg = route?.querySelector<SVGSVGElement>(".route-svg");
  const ahead = svg?.querySelector<SVGPathElement>(".route-ahead");
  const done = svg?.querySelector<SVGPathElement>(".route-done");
  const head = svg?.querySelector<SVGCircleElement>(".route-head");
  const maskRect = svg?.querySelector<SVGRectElement>(".route-mask-rect");
  if (!route || !svg || !ahead || !done || !head) return;

  const points = [...route.querySelectorAll<HTMLElement | SVGElement>("[data-route-point]")];
  if (points.length < 2) return;

  const STEPS = 480;
  let total = 0;
  let ys: number[] = [];
  let pointYs: number[] = [];
  let frame = 0;

  const layout = () => {
    const box = route.getBoundingClientRect();
    const width = route.clientWidth;
    const height = route.offsetHeight;
    svg.setAttribute("viewBox", `0 0 ${width} ${height}`);
    // Mat na mo dan o cuoi: duoi dong thoi gian la "tuong lai", net dut tan dan.
    maskRect?.setAttribute("width", `${width}`);
    maskRect?.setAttribute("height", `${height}`);

    const pts = points.map((el) => {
      const r = el.getBoundingClientRect();
      return { x: r.left - box.left + r.width / 2, y: r.top - box.top + r.height / 2 };
    });
    pointYs = pts.map((p) => p.y);

    const maxSway = width < 768 ? 9 : 30;
    let d = `M ${pts[0].x} ${pts[0].y}`;
    for (let i = 1; i < pts.length; i++) {
      const a = pts[i - 1];
      const b = pts[i];
      const dy = b.y - a.y;
      const sway = (i % 2 ? 1 : -1) * Math.min(maxSway, Math.abs(dy) * 0.08);
      d += ` C ${a.x + sway} ${a.y + dy * 0.42}, ${b.x - sway} ${b.y - dy * 0.42}, ${b.x} ${b.y}`;
    }
    ahead.setAttribute("d", d);
    done.setAttribute("d", d);

    total = done.getTotalLength();
    done.style.strokeDasharray = `${total} ${total}`;
    ys = Array.from({ length: STEPS + 1 }, (_, i) => done.getPointAtLength((total * i) / STEPS).y);
    update();
  };

  const update = () => {
    frame = 0;
    const traveler = window.innerHeight * 0.62 - route.getBoundingClientRect().top;

    let len = 0;
    if (traveler > ys[0]) {
      let lo = 0;
      let hi = STEPS;
      while (lo < hi) {
        const mid = (lo + hi + 1) >> 1;
        if (ys[mid] <= traveler) lo = mid;
        else hi = mid - 1;
      }
      len = (total * lo) / STEPS;
    }

    done.style.strokeDashoffset = `${total - len}`;
    const p = done.getPointAtLength(len);
    head.setAttribute("cx", `${p.x}`);
    head.setAttribute("cy", `${p.y}`);
    head.style.opacity = len > 0 && len < total - 1 ? "1" : "0";

    points.forEach((el, i) => el.classList.toggle("is-reached", pointYs[i] <= traveler + 1));
  };

  const schedule = () => {
    if (!frame) frame = requestAnimationFrame(update);
  };

  route.classList.add("route-live");
  layout();
  window.addEventListener("scroll", schedule, { passive: true });
  // Anh tai xong / font doi / doi kich thuoc cua so -> chieu cao thay doi -> ve lai.
  new ResizeObserver(() => layout()).observe(route);
}
