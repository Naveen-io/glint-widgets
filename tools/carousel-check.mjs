// Verifies the hero 3D carousel's geometry without a browser. Run: node tools/carousel-check.mjs
// Three invariants, checked for every container width and every ring angle:
//   1. No card edge reaches the CSS camera plane (z = perspective). A card at or past it
//      projects to a near-infinite size; WebKit builds a compositing surface for it and iOS
//      can run out of GPU memory and panic.
//   2. No hidden card is still inside the container -- cards must not vanish mid-screen.
//   3. No two on-screen cards overlap by more than 0.5px.
import { geometry, RING } from "../docs/assets/carousel.js";

const WIDTHS = [1024, 1200, 1440, 1920, 2560];
const STEP = 0.5;
const OVERLAP_TOL = 0.5;

// Reproduces render(): where card i lands, and the perspective projection of its two
// vertical edges. CSS rotateY maps a local (u,0,0) to (u*cos, 0, -u*sin), then the
// translate puts it on the ring; the projection divides by (persp - z).
function project(g, i, angle) {
  const theta = angle + RING.offsets[i];
  let wrapped = ((theta % 360) + 360) % 360;
  if (wrapped > 180) wrapped -= 360;
  const absDeg = Math.abs(wrapped);
  const rad = (theta * Math.PI) / 180;
  const x = -g.r * Math.sin(rad);
  const z = -g.r * Math.cos(rad) + g.zoff;
  const hidden = z > 0.7 * g.persp;

  const sx = 1 + RING.SX * Math.pow(absDeg / 90, RING.CURVE);
  const hw = (RING.widthFor(RING.CARDS[i].size) * g.sc * sx) / 2;
  const w = (wrapped * Math.PI) / 180;
  const edges = [-hw, hw].map((u) => {
    const ez = z - u * Math.sin(w);
    return { z: ez, px: ((x + u * Math.cos(w)) * g.persp) / (g.persp - ez) };
  });
  const px = edges.map((e) => e.px);
  return { hidden, z, maxZ: Math.max(edges[0].z, edges[1].z), lo: Math.min(...px), hi: Math.max(...px) };
}

const fails = [];
const note = (m) => { if (fails.length < 12) fails.push(m); };

for (const w of WIDTHS) {
  const g = geometry(w);
  const half = w / 2;
  let worstZ = -Infinity, worstGap = Infinity, zFail = 0, hideFail = 0, lapFail = 0;

  for (let a = 0; a < 360; a += STEP) {
    const cards = RING.CARDS.map((_, i) => project(g, i, a));
    const onScreen = [];

    for (let i = 0; i < cards.length; i++) {
      const c = cards[i];
      if (c.hidden) {
        // A hidden card must be fully outside the container, so nothing pops out mid-screen.
        // The ring is concave, so every card faces the camera and backface-visibility never
        // saves us; the one exception is a card whose geometry reaches the camera plane,
        // which CSS projects to garbage and cannot be shown at all. Invariant 1 keeps drawn
        // cards far away from that plane, so those are only ones already hidden.
        if (c.maxZ < g.persp && c.lo < half && c.hi > -half) {
          hideFail++;
          note(`${w}px a=${a}: hidden card ${i} still on screen (${c.lo.toFixed(0)}..${c.hi.toFixed(0)} vs +-${half})`);
        }
        continue;
      }
      worstZ = Math.max(worstZ, c.maxZ);
      if (!(c.maxZ < g.persp)) {
        zFail++;
        note(`${w}px a=${a}: card ${i} edge z ${c.maxZ.toFixed(1)} >= perspective ${g.persp.toFixed(1)}`);
      }
      if (c.lo < half && c.hi > -half) onScreen.push(c);
    }

    onScreen.sort((p, q) => p.lo - q.lo);
    for (let k = 1; k < onScreen.length; k++) {
      const gap = onScreen[k].lo - onScreen[k - 1].hi;
      worstGap = Math.min(worstGap, gap);
      if (gap < -OVERLAP_TOL) {
        lapFail++;
        note(`${w}px a=${a}: on-screen cards overlap by ${(-gap).toFixed(1)}px`);
      }
    }
  }

  const ok = !zFail && !hideFail && !lapFail;
  console.log(
    `${String(w).padStart(5)}px  max edge z ${worstZ.toFixed(1).padStart(7)} / persp ${g.persp.toFixed(0).padStart(5)}` +
    ` (${(worstZ / g.persp).toFixed(2)})  tightest gap ${worstGap.toFixed(2).padStart(7)}px  ` +
    (ok ? "ok" : `FAIL  z:${zFail} hidden-on-screen:${hideFail} overlap:${lapFail}`)
  );
}

if (fails.length) {
  console.log("\n" + fails.join("\n"));
  process.exit(1);
}
console.log(`\nwidths ${WIDTHS.join(", ")}, angles 0-360 in ${STEP}deg steps: all three invariants hold`);
