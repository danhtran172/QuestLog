/**
 * Bui la bung ra sau card khi hover.
 *
 * Day la hieu ung jungle DUY NHAT tren luoi game — trang tri tinh dat tren mat
 * card da bo theo yeu cau. Bui nay khong nam tren card ma nam SAU no.
 *
 * Vi sao no khong the la con cua card:
 *   Luc hover, card duoc dat `scale` va `z-index` — CA HAI deu tao ngan xep
 *   moi. Ma theo thu tu ve cua CSS, con co z-index AM duoc ve SAU nen cua
 *   chinh phan tu tao ngan xep. Ket qua: dung luc hover — luc duy nhat la
 *   hien ra — no nhay len TRUOC nen card. `z-index: -1` khong cuu duoc.
 *   Cach duy nhat la dua bui ra lam ANH EM cua card, ve truoc no.
 *
 * Bam theo game.id nen mot game luon co dung mot bo la — luoi khong "doi la"
 * moi lan re-render hay cuon qua.
 */
import { useMemo } from "react";
import type { CSSProperties } from "react";

/**
 * La dung cho bui bung ra khi hover. Deu la la don, khong phai ca khom.
 *
 * Danh sach nay co LAP LAI co y: boc deu tay tren bon loai thi duong xi chiem
 * quá nua so la hien ra, trong khi monstera moi la hinh dac trung cua theme.
 * Lap monstera hai lan de ty le vao khoang 2:1.
 */
const BUSH_LEAVES = [
  { code: "pl-01", ar: 1.0475 },
  { code: "pl-01", ar: 1.0475 },
  { code: "pl-02", ar: 1.0866 },
  { code: "pl-02", ar: 1.0866 },
  { code: "pl-05", ar: 0.6661 },
  { code: "pl-06", ar: 0.962 },
] as const;

/** Bam id thanh mot so nho on dinh. */
function hash(value: string | number): number {
  if (typeof value === "number") return Math.abs(Math.trunc(value));
  let h = 2166136261;
  for (let i = 0; i < value.length; i += 1) {
    h ^= value.charCodeAt(i);
    h = Math.imul(h, 16777619);
  }
  return Math.abs(h);
}

/**
 * PRNG nho gon (mulberry32), cung hat giong luon ra cung day so.
 *
 * Ban truoc dung LCG kieu `s*1103515245+12345`. LCG co bit thap rat yeu, ma o
 * day moi card chi boc 4-6 lan voi cac hat giong gan nhau — do lech lo ngay:
 * danh sach 67% monstera ma thuc te boc ra 82%. mulberry32 tron ky hon nen
 * ty le boc bam sat danh sach.
 */
function rng(seed: number) {
  let s = (seed || 1) >>> 0;
  return () => {
    s = (s + 0x6d2b79f5) >>> 0;
    let t = s;
    t = Math.imul(t ^ (t >>> 15), t | 1);
    t ^= t + Math.imul(t ^ (t >>> 7), t | 61);
    return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
  };
}

type Props = {
  /** Dinh danh game — quyet dinh bo trang tri, giu on dinh giua cac lan render. */
  seed: string | number;
};

/**
 * Bui la sau card. Dat lam ANH EM dung TRUOC .game-card trong DOM: nho vay no
 * duoc ve truoc, va nen card (94% dac) phu len phan nam trong bao card. Chi
 * phan tho ra ngoai vien moi hien — do la thu lam hieu ung doc ra "card duoc
 * loi khoi bui".
 */
export function CardBush({ seed }: Props) {
  const leaves = useMemo(() => {
    const key = hash(seed);
    const r = rng(key * 97 + 13);
    const n = 4 + Math.floor(r() * 3);
    const base = r() * 360;
    const out: { code: string; style: CSSProperties }[] = [];
    for (let i = 0; i < n; i += 1) {
      const deg = base + i * (360 / n) + (r() - 0.5) * 40;
      const rad = (deg * Math.PI) / 180;
      // Phai bung xa hon nua kich thuoc card, neu khong chiec la dung lai ben
      // trong bong card va bi chinh nen card nuot mat.
      // Ba lan do: 96-148px cho 16.4% dien tich la lo ra ngoai card,
      // 100-154px cho 17.9%, 114-174px vot len 28.2%. Noi suy ra muc nay de
      // roi vao khoang 20-21%, tuc nhieu hon ban dau chung mot phan tu.
      const dist = 104 + r() * 57;
      const leaf = BUSH_LEAVES[Math.floor(r() * BUSH_LEAVES.length) % BUSH_LEAVES.length];
      out.push({
        code: leaf.code,
        style: {
          "--dx": `${Math.cos(rad) * dist}px`,
          // Card thap hon rong, nen truc doc nen lai cho quy dao om lay card
          "--dy": `${Math.sin(rad) * dist * 0.72}px`,
          "--w": `${37.5 + r() * 23}%`,
          // Anh goc co ngon la huong len (-Y). Cong 90 do thi ngon chi dung
          // huong bung, va cuong quay ve phia card.
          "--rot": `${deg + 90}deg`,
          "--i": i,
          "--ar": leaf.ar,
        } as CSSProperties,
      });
    }
    return out;
  }, [seed]);

  return (
    <span className="j-bush" aria-hidden="true">
      {leaves.map((leaf, i) => (
        <span key={i} className={`j-bl j-a-${leaf.code}`} style={leaf.style} />
      ))}
    </span>
  );
}
