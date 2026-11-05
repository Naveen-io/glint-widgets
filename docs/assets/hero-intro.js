// Hero intro: thumbnails rise from below into a straight line, then the line wraps into the ring
// that assets/carousel.js draws, and the real carousel takes over.
//
// Nothing here touches carousel.js. The overlay reproduces the ring's resting layout from the
// numbers that file exports -- RING and geometry(), the same ones tools/carousel-check.mjs uses to
// reproduce render() -- and ring3d always starts at angle 0, so the clones' end state and the real
// carousel's first frame are the same formula at the same angle. That is what makes the hand-off
// exact, and it means the carousel never has to run (or be paused) during the intro.
//
// Phase 3 is driven frame by frame, lerping x, z, rotateY and scale as plain numbers. Nothing
// interpolates a matrix, and no clone ever turns past the angle it ends on.
import { carousel, RING, geometry } from "./carousel.js";

const KEY = "dw-hero-intro";
// The same condition carousel.js uses to pick its flat track over the 3D ring.
const FLAT_MQ = "(max-width: 734px), (hover: none) and (pointer: coarse)";
const CR = 20;              // card corner radius in carousel.js (its CR); not exported
const THUMB = 0.16;
const LINE_GAP = 10;
const MAX_TURN = 88;        // past 90deg a card shows its back, so those are never cloned
const NEAR_LIMIT = 0.85;    // no clone may come within 15% of the camera distance
const RISE_EASE = "cubic-bezier(0.16, 1, 0.3, 1)";

// Every intro duration, in milliseconds, in one place. Multiply with INTRO_SPEED to slow the whole
// sequence down (?introSpeed=2 overrides it); nothing else scales these.
const INTRO_SPEED = 1;
const TIMING = {
  glow: 400,        // phase 0: the glow alone, before the first thumbnail moves
  riseDur: 800,     // each thumbnail's rise
  stagger: 70,      // between thumbnails, in shuffled order
  fade: 250,        // opacity 0 -> 1 over the start of each rise
  hold: 700,        // phase 2, measured from the last thumbnail landing
  wrap: 1200,       // phase 3
  buttons: 500,     // the hero buttons, fading in as phase 3 starts
  resume: 1500,     // the real carousel (with its autoplay) takes over this long after phase 3
  skip: 200,        // how long the rest of the intro takes when someone interrupts it
};

// cubic-bezier(0.65, 0, 0.35, 1) by bisection: phase 3 needs it as a number, not a CSS string.
function bezier(x1, y1, x2, y2) {
  const at = (a, b) => (t) => 3 * t * (1 - t) * (1 - t) * a + 3 * t * t * (1 - t) * b + t * t * t;
  const bx = at(x1, x2), by = at(y1, y2);
  return (t) => {
    if (t <= 0) return 0;
    if (t >= 1) return 1;
    let lo = 0, hi = 1, u = t;
    for (let i = 0; i < 20; i++) {
      u = (lo + hi) / 2;
      if (bx(u) < t) lo = u; else hi = u;
    }
    return by(u);
  };
}
const wrapEase = bezier(0.65, 0, 0.35, 1);
const lerp = (a, b, t) => a + (b - a) * t;

function readSpeed() {
  const q = new URLSearchParams(location.search).get("introSpeed");
  const n = q === null ? NaN : parseFloat(q);
  return Number.isFinite(n) && n > 0 && n <= 20 ? n : INTRO_SPEED;
}

const DEV = /^(localhost|127\.0\.0\.1|\[::1\])$/.test(location.hostname) || location.protocol === "file:" ||
  new URLSearchParams(location.search).has("introDebug");

// The transform the real carousel gives a card, rebuilt from its own numbers. Same functions in
// the same order as render(), so the final frame matches it exactly.
const ringTransform = (x, z, rotY, sx, sy) =>
  "translate3d(-50%,-50%,0) translate3d(" + x.toFixed(1) + "px,0," + z.toFixed(1) + "px) rotateY(" +
  rotY.toFixed(2) + "deg) scale(" + sx.toFixed(3) + "," + sy.toFixed(3) + ")";

// The ring at rest, exactly as carousel.js render() draws it at angle 0, in cylinder terms.
function ringState(width) {
  const g = geometry(width);
  const cardH = RING.widthFor("small");
  const cards = [];
  for (let i = 0; i < RING.CARDS.length; i++) {
    const theta = RING.offsets[i];
    let wrapped = ((theta % 360) + 360) % 360;
    if (wrapped > 180) wrapped -= 360;
    const absDeg = Math.abs(wrapped);
    // Cards past 88deg show their back (the stylesheet hides backfaces), so the real carousel does
    // not show them either: cloning them is what made clones vanish mid-turn.
    if (absDeg > MAX_TURN) continue;
    const rad = (theta * Math.PI) / 180;
    const t = Math.pow(absDeg / 90, RING.CURVE);
    cards.push({
      index: i,
      src: RING.CARDS[i].src,
      w: Math.round(RING.widthFor(RING.CARDS[i].size) * g.sc),
      h: Math.round(cardH * g.sc),
      radius: Math.round(CR * g.sc),
      endX: -g.r * Math.sin(rad),
      endZ: -g.r * Math.cos(rad) + g.zoff,
      endRotY: wrapped,
      endSX: 1 + RING.SX * t,
      endSY: 1 + RING.SY * t,
      endOpacity: Math.max(0.85, 1 - 0.15 * Math.min(absDeg / 150, 1)),
    });
  }
  return { persp: g.persp, cards };
}

// The straight line, in the cards' final left-to-right order, so no clone ever crosses another.
function lineUp(cards) {
  cards.sort((a, b) => a.endX - b.endX);
  let width = LINE_GAP * (cards.length - 1);
  for (const c of cards) width += c.w * THUMB;
  let x = -width / 2;
  for (const c of cards) {
    c.lineX = x + (c.w * THUMB) / 2;
    x += c.w * THUMB + LINE_GAP;
  }
}

function shuffled(items) {
  const a = items.slice();
  for (let i = a.length - 1; i > 0; i--) {
    const j = Math.floor(Math.random() * (i + 1));
    [a[i], a[j]] = [a[j], a[i]];
  }
  return a;
}

const twoFrames = () => new Promise((done) =>
  requestAnimationFrame(() => requestAnimationFrame(() => done())));

// Decoded, not merely created: a clone whose PNG has not decoded yet rises as an empty box.
function decoded(images) {
  return Promise.all(images.map((img) => {
    if (img.decode) return img.decode().catch(() => {});
    if (img.complete) return Promise.resolve();
    return new Promise((done) => { img.onload = img.onerror = () => done(); });
  }));
}

export function heroIntro(opts) {
  const { stage, scene, wrapper, basePath } = opts;
  const host = wrapper.closest(".hero") || wrapper;
  wrapper.classList.add("lens-glow");     // the glow fades in on its own: phase 0

  const mount = () => carousel(scene, basePath);
  const params = new URLSearchParams(location.search);
  const forced = params.get("intro") === "1";
  let played = false;
  try { played = Boolean(sessionStorage.getItem(KEY)); } catch (e) { /* no storage */ }

  // No intro for a repeat visit, for reduced motion, or in the flat (phone) mode, whose resting
  // layout is CSS flexbox rather than the ring this reproduces.
  if ((played && !forced) || window.matchMedia("(prefers-reduced-motion: reduce)").matches ||
      window.matchMedia(FLAT_MQ).matches || !document.body.animate) {
    mount();
    return { intro: false };
  }
  if (!forced) { try { sessionStorage.setItem(KEY, "1"); } catch (e) { /* no storage */ } }

  const speed = readSpeed();
  const T = {};
  for (const k of Object.keys(TIMING)) T[k] = TIMING[k] * speed;

  const { persp, cards } = ringState(stage.clientWidth || window.innerWidth);
  if (!cards.length) { mount(); return { intro: false }; }
  lineUp(cards);
  const maxZ = NEAR_LIMIT * persp;

  // The overlay carries the same classes as the real ring, so it inherits the same CSS. It is
  // built now but animated only once everything it needs is ready, so the stage shows the empty
  // glow until the timeline actually starts.
  const overlay = document.createElement("div");
  overlay.className = "carousel-scene hero-overlay";
  overlay.setAttribute("aria-hidden", "true");
  const ring = document.createElement("div");
  ring.className = "carousel-ring";
  ring.style.perspective = persp.toFixed(1) + "px";
  overlay.appendChild(ring);

  const below = stage.clientHeight / 2;
  const order = shuffled(cards);
  const images = [];
  for (const card of cards) {
    const el = document.createElement("div");
    el.className = "carousel-card";
    el.style.width = card.w + "px";
    el.style.height = card.h + "px";
    el.style.borderRadius = card.radius + "px";
    el.style.opacity = "0";
    // Visible backfaces on the clones: with the turn capped at 88deg a front always faces the
    // camera, and this way a rounding error can never blank a card mid-animation.
    el.style.backfaceVisibility = "visible";
    el.style.webkitBackfaceVisibility = "visible";
    const img = document.createElement("img");
    img.src = basePath + card.src;
    img.alt = "";
    img.draggable = false;
    el.appendChild(img);
    ring.appendChild(el);
    images.push(img);
    card.el = el;
    card.delay = T.glow + order.indexOf(card) * T.stagger;
    card.startX = card.lineX + (Math.random() - 0.5) * 120;
    card.startY = below + 40 + Math.random() * 80;
    card.line = ringTransform(card.lineX, 0, 0, THUMB, THUMB);
    card.end = ringTransform(card.endX, card.endZ, card.endRotY, card.endSX, card.endSY);
    el.style.zIndex = String(Math.round(1000));
  }
  stage.appendChild(overlay);

  // Phase 1 ends when the last thumbnail lands, so the rest follows from the stagger.
  const riseEnd = T.glow + (cards.length - 1) * T.stagger + T.riseDur;
  const wrapStart = riseEnd + T.hold;

  host.classList.add("lens-intro");       // holds the buttons back
  host.style.setProperty("--intro-buttons", Math.round(T.buttons) + "ms");

  let handed = false, t0 = 0, raf = 0;
  const rises = [];
  const timers = [];

  // One frame of phase 3: plain number lerps, one transform string, always the same functions.
  function frame(p) {
    for (const card of cards) {
      const x = lerp(card.lineX, card.endX, p);
      const z = Math.min(lerp(0, card.endZ, p), maxZ);
      const rotY = lerp(0, card.endRotY, p);         // never past its own end angle
      const sx = lerp(THUMB, card.endSX, p);
      const sy = lerp(THUMB, card.endSY, p);
      card.el.style.transform = p >= 1 ? card.end : ringTransform(x, z, rotY, sx, sy);
      card.el.style.opacity = String(lerp(1, card.endOpacity, p));
      card.el.style.zIndex = String(Math.round(1000 - z));
    }
  }

  let wrapT0 = 0, wrapDur = 0, wrapFrom = 0;
  function startWrap(duration, fromProgress) {
    cancelAnimationFrame(raf);
    // WAAPI animations outrank inline styles, so the rise has to let go before this can drive the
    // transform itself. The line value is written back first, so nothing moves at the switch.
    for (const a of rises) a.cancel();
    for (const card of cards) {
      card.el.style.transform = card.line;
      card.el.style.opacity = "1";
    }
    wrapT0 = performance.now();
    wrapDur = Math.max(duration, 16);
    wrapFrom = fromProgress || 0;
    const tick = () => {
      const linear = Math.min(1, wrapFrom + (1 - wrapFrom) * ((performance.now() - wrapT0) / wrapDur));
      frame(wrapEase(linear));
      if (linear < 1) { raf = requestAnimationFrame(tick); return; }
      raf = 0;
      if (DEV) console.log("[intro] phase 3 end: " + Math.round(performance.now() - t0) +
        "ms (config " + Math.round(wrapStart + T.wrap) + "ms)");
      timers.push(setTimeout(handOff, T.resume));
    };
    if (DEV) console.log("[intro] phase 3 start: " + Math.round(performance.now() - t0) +
      "ms (config " + Math.round(wrapStart) + "ms)");
    tick();
  }

  // Dev only: the rebuilt transforms against the real cards, once both are on screen.
  function checkAgainstReal() {
    const realRing = scene.querySelector(".carousel-ring");
    if (!realRing) return;
    const samples = [cards[Math.floor(cards.length / 2)], cards[Math.floor(cards.length / 4)], cards[0]];
    for (const card of samples) {
      const real = realRing.children[card.index];
      if (!real) continue;
      const a = card.el.getBoundingClientRect(), b = real.getBoundingClientRect();
      const dx = Math.abs(a.left - b.left), dy = Math.abs(a.top - b.top);
      const dw = Math.abs(a.width - b.width), dh = Math.abs(a.height - b.height);
      const line = "card " + card.index + " (" + card.endRotY.toFixed(0) + "deg): dx " + dx.toFixed(2) +
        " dy " + dy.toFixed(2) + " dw " + dw.toFixed(2) + " dh " + dh.toFixed(2) + "px";
      if (Math.max(dx, dy, dw, dh) > 1) console.warn("[intro] hand-off " + line + "  <-- over 1px");
      else console.log("[intro] hand-off " + line);
    }
  }

  function handOff() {
    if (handed) return;
    handed = true;
    cancelAnimationFrame(raf);
    for (const id of timers) clearTimeout(id);
    host.classList.remove("lens-intro");
    // Mount the real carousel under the clones, let it paint, then drop the overlay. Both draw
    // the ring at angle 0 from the same numbers, so the frames they overlap are identical.
    mount();
    requestAnimationFrame(() => requestAnimationFrame(() => {
      if (DEV) checkAgainstReal();
      overlay.remove();
    }));
    if (DEV) console.log("[intro] hand-off: " + Math.round(performance.now() - t0) + "ms");
  }

  // Any deliberate input runs the rest of the intro out in T.skip, then hands over.
  function skip() {
    if (handed) return;
    if (raf) {                                      // already wrapping: finish it faster
      const linear = Math.min(1, wrapFrom + (1 - wrapFrom) * ((performance.now() - wrapT0) / wrapDur));
      startWrap(T.skip, linear);
      return;
    }
    for (const id of timers) clearTimeout(id);
    timers.length = 0;
    startWrap(T.skip, 0);
  }
  stage.addEventListener("pointerdown", skip, { once: true });
  stage.addEventListener("wheel", skip, { once: true, passive: true });
  wrapper.addEventListener("keydown", (e) => {
    if (e.key === "ArrowLeft" || e.key === "ArrowRight" || e.key === " " || e.key === "Enter") skip();
  });
  // No resize handler on purpose: a scrollbar appearing during load fires resize, and skipping on
  // that made the whole intro run out in 200ms. A resize mid-intro just means the real ring mounts
  // at the new width when it takes over.

  // (a) the clones' art is decoded, (b) fonts are settled, (c) two frames later. Every time below
  // is measured from that moment, never from page load.
  decoded(images)
    .then(() => (document.fonts ? document.fonts.ready.catch(() => {}) : null))
    .then(twoFrames)
    .then(() => {
      if (handed) return;
      t0 = performance.now();
      for (const card of cards) {
        card.el.style.opacity = "";
        const start = "translate3d(-50%,-50%,0) translate3d(" + card.startX.toFixed(1) + "px," +
          card.startY.toFixed(1) + "px,0px) rotateY(0.00deg) scale(" + THUMB + "," + THUMB + ")";
        // Phase 1 stays on the Web Animations API: both keyframes are the same function list with
        // no rotation and no mirroring, so there is nothing for the browser to decompose.
        rises.push(card.el.animate([{ transform: start }, { transform: card.line }],
          { delay: card.delay, duration: T.riseDur, easing: RISE_EASE, fill: "both" }));
        rises.push(card.el.animate([{ opacity: 0 }, { opacity: 1 }],
          { delay: card.delay, duration: T.fade, fill: "both" }));
      }
      timers.push(setTimeout(() => host.classList.remove("lens-intro"), wrapStart));
      timers.push(setTimeout(() => startWrap(T.wrap, 0), wrapStart));

      if (DEV) {
        console.log("[intro] start (art decoded, fonts ready, +2 frames). speed " + speed + ", " +
          cards.length + " cards. phase 1 " + Math.round(T.glow) + "-" + Math.round(riseEnd) +
          "ms, phase 2 to " + Math.round(wrapStart) + "ms, phase 3 to " +
          Math.round(wrapStart + T.wrap) + "ms, hand-off " + Math.round(wrapStart + T.wrap + T.resume) + "ms");
        const last = order[order.length - 1];
        rises[0].finished.then(() => console.log("[intro] phase 1 first card end: " +
          Math.round(performance.now() - t0) + "ms (config " + Math.round(order[0].delay + T.riseDur) + "ms)"));
        last.el.getAnimations()[0].finished.then(() => console.log("[intro] phase 1 last card end: " +
          Math.round(performance.now() - t0) + "ms (config " + Math.round(riseEnd) + "ms)"));
      }
    });

  return { intro: true };
}
