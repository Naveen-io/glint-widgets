// Hero carousel: a 3D concave ring on desktop, a flat 2D track on phones and tablets.
// Phones get the flat mode because the ring needs a compositing surface per card, and a
// card near the CSS camera plane makes that surface enormous enough to crash iOS Safari.

var CARDS = [
  { src: "calendar-dark.png", size: "medium" },
  { src: "analog-clock-light.png", size: "small" },
  { src: "weather-light.png", size: "medium" },
  { src: "focus-timer-dark.png", size: "small" },
  { src: "now-playing-dark.png", size: "medium" },
  { src: "system-dark.png", size: "small" },
  { src: "github-dark.png", size: "medium" },
  { src: "analog-clock-dark.png", size: "small" },
  { src: "reminders-light.png", size: "medium" },
  { src: "focus-timer-light.png", size: "small" },
  { src: "f1-timing-dark.png", size: "medium" },
  { src: "notes-dark.png", size: "small" },
  { src: "photo-frame.png", size: "medium" },
  { src: "analog-clock-light.png", size: "small" },
  { src: "f1-timing-dark.png", size: "medium" },
  { src: "focus-timer-dark.png", size: "small" },
  { src: "calendar-dark.png", size: "medium" },
  { src: "system-dark.png", size: "small" },
  { src: "weather-light.png", size: "medium" },
  { src: "analog-clock-dark.png", size: "small" },
  { src: "now-playing-dark.png", size: "medium" },
  { src: "focus-timer-light.png", size: "small" },
  { src: "github-dark.png", size: "medium" },
  { src: "notes-dark.png", size: "small" },
  { src: "reminders-light.png", size: "medium" },
  { src: "analog-clock-light.png", size: "small" },
];

var N = CARDS.length;
var FLAT_N = 10;    // CARDS starts repeating itself after the first 10; phones show one set.
var REF_W = 1200;
var R = 629, PERSP = 600, ZOFF = 0;
var SY = 0.5, SX = 0.05, CURVE = 2;
var CH = 90, CR = 20, SPEED = 0.01, DRAG = 0.2;
var FLAT_SPEED = 18;    // px per second
var DEG = 180 / Math.PI;
var FLAT_MQ = "(max-width: 734px), (hover: none) and (pointer: coarse)";

function widthFor(size) {
  return size === "small" ? CH : Math.round(CH * 364 / 170);
}

var offsets = [];
(function () {
  var arcs = [];
  for (var i = 0; i < N; i++) arcs.push((widthFor(CARDS[i].size) / R) * DEG);
  var total = 0;
  for (var i = 0; i < N; i++) total += arcs[i];
  var gap = (360 - total) / N;
  offsets.push(0);
  var pos = 0;
  for (var i = 1; i < N; i++) {
    pos += arcs[i - 1] / 2 + gap + arcs[i] / 2;
    offsets.push(pos);
  }
})();

// Layout for a container width. Exported so tools/carousel-check.mjs can verify the
// z/perspective invariant without a browser.
export function geometry(w) {
  var ratio = w / REF_W;
  var shrink = Math.max(0, 1 - ratio);
  var sc = ratio < 1 ? 0.6 + 0.4 * ratio : ratio;
  var persp = PERSP * Math.max(sc, 0.85);
  // render() hides a card once z > 0.7 * persp, so no drawn card is ever deeper than that.
  // Cap zoff at the same depth: WebKit builds a compositing surface per card and a card at
  // or past the camera plane (z = persp) makes that surface enormous, which can take an
  // iPhone down with it.
  var zoff = Math.min((ZOFF + R * shrink * 1.4) * sc, 0.7 * persp);
  return { sc: sc, r: R * sc, zoff: zoff, persp: persp };
}

// Ring constants, exported so tools/carousel-check.mjs can reproduce render() exactly.
export var RING = { CARDS: CARDS, offsets: offsets, widthFor: widthFor, SX: SX, SY: SY, CURVE: CURVE };

function ring3d(container, basePath, signal) {
  var ring = document.createElement("div");
  ring.className = "carousel-ring";
  var els = [];

  for (var i = 0; i < N; i++) {
    var card = document.createElement("div");
    card.className = "carousel-card";
    var img = document.createElement("img");
    img.src = basePath + CARDS[i].src;
    img.alt = CARDS[i].src.replace(".png", "").replace(/-/g, " ");
    img.draggable = false;
    card.appendChild(img);
    ring.appendChild(card);
    els.push(card);
  }
  container.appendChild(ring);

  var angle = 0, dragging = false, dragStartX = 0, dragAngle = 0, lastTime = 0;
  var paused = window.matchMedia("(prefers-reduced-motion: reduce)").matches;
  var r = 0, zoff = 0, persp = PERSP, lastW = 0, shown = [];

  // Sizes only change with the container, so they are written here and not per frame.
  function layout() {
    var w = container.offsetWidth;
    if (!w || w === lastW) return;
    lastW = w;
    var g = geometry(w);
    r = g.r;
    zoff = g.zoff;
    persp = g.persp;
    ring.style.perspective = g.persp.toFixed(1) + "px";
    var ch = Math.round(CH * g.sc) + "px";
    var cr = Math.round(CR * g.sc) + "px";
    for (var i = 0; i < N; i++) {
      els[i].style.width = Math.round(widthFor(CARDS[i].size) * g.sc) + "px";
      els[i].style.height = ch;
      els[i].style.borderRadius = cr;
    }
  }

  function render() {
    for (var i = 0; i < N; i++) {
      var theta = angle + offsets[i];
      var wrapped = ((theta % 360) + 360) % 360;
      if (wrapped > 180) wrapped -= 360;
      var absDeg = Math.abs(wrapped);
      var el = els[i];
      var rad = theta * Math.PI / 180;
      var x = -r * Math.sin(rad);
      var z = -r * Math.cos(rad) + zoff;
      // Hide by depth, not by angle: an edge-on card at 92deg still projects partly into a
      // container 1000px or wider, so an angle rule made cards vanish mid-screen. Depth is
      // what actually matters -- it keeps every drawn card well in front of the camera.
      if (z > 0.7 * persp) {
        if (shown[i] !== false) { el.style.visibility = "hidden"; shown[i] = false; }
        continue;
      }
      if (shown[i] !== true) { el.style.visibility = ""; shown[i] = true; }
      var opacity = Math.max(0.85, 1 - 0.15 * Math.min(absDeg / 150, 1));
      var t = Math.pow(absDeg / 90, CURVE);
      var sy = 1 + SY * t;
      var sx = 1 + SX * t;
      el.style.transform =
        "translate3d(-50%,-50%,0) translateX(" + x.toFixed(1) + "px) " +
        "translateZ(" + z.toFixed(1) + "px) rotateY(" + wrapped.toFixed(2) + "deg) scale(" + sx.toFixed(3) + "," + sy.toFixed(3) + ")";
      el.style.opacity = opacity.toFixed(3);
      el.style.zIndex = Math.round(1000 - z);
    }
  }

  var running = false, onScreen = false;

  function loop(time) {
    if (!onScreen || document.hidden || signal.aborted) { running = false; return; }
    if (lastTime === 0) lastTime = time;
    var dt = time - lastTime;
    lastTime = time;
    if (!paused && !dragging) angle += SPEED * dt * 0.06;
    render();
    requestAnimationFrame(loop);
  }

  function start() {
    if (running || !onScreen || document.hidden) return;
    running = true;
    lastTime = 0;
    requestAnimationFrame(loop);
  }

  layout();
  render();
  document.addEventListener("visibilitychange", start, { signal: signal });
  var io = new IntersectionObserver(function (es) {
    onScreen = es[0].isIntersecting;
    layout();
    start();
  });
  io.observe(container);
  signal.addEventListener("abort", function () { io.disconnect(); });

  container.addEventListener("mousedown", function (e) {
    dragging = true; dragStartX = e.clientX; dragAngle = angle;
    container.style.cursor = "grabbing"; e.preventDefault();
  }, { signal: signal });
  window.addEventListener("mousemove", function (e) {
    if (dragging) angle = dragAngle - (e.clientX - dragStartX) * DRAG;
  }, { signal: signal });
  window.addEventListener("mouseup", function () {
    if (dragging) { dragging = false; container.style.cursor = ""; }
  }, { signal: signal });
  container.addEventListener("touchstart", function (e) {
    dragging = true; dragStartX = e.touches[0].clientX; dragAngle = angle;
  }, { passive: true, signal: signal });
  window.addEventListener("touchmove", function (e) {
    if (dragging) angle = dragAngle - (e.touches[0].clientX - dragStartX) * DRAG;
  }, { passive: true, signal: signal });
  window.addEventListener("touchend", function () { dragging = false; }, { signal: signal });
  window.addEventListener("resize", function () { layout(); render(); }, { signal: signal });
}

// Flat 2D track: one horizontal flex row with the card list duplicated once for a seamless
// loop, and a single transform per frame on the track. No perspective, no 3D transforms and
// no per-card style writes -- nothing for a phone GPU to choke on.
function flat(container, basePath, signal) {
  var track = document.createElement("div");
  track.className = "carousel-track";
  var els = [];

  for (var pass = 0; pass < 2; pass++) {
    for (var i = 0; i < FLAT_N; i++) {
      var card = document.createElement("div");
      card.className = "carousel-flat-card" + (CARDS[i].size === "small" ? " small" : "");
      var img = document.createElement("img");
      img.src = basePath + CARDS[i].src;
      img.alt = pass ? "" : CARDS[i].src.replace(".png", "").replace(/-/g, " ");
      img.draggable = false;
      card.appendChild(img);
      track.appendChild(card);
      els.push(card);
    }
  }
  container.appendChild(track);

  var x = 0, period = 0, dragging = false, dragStartX = 0, dragX = 0, lastTime = 0;
  var reduced = window.matchMedia("(prefers-reduced-motion: reduce)").matches;

  function render() {
    // Card sizes come from CSS, so the loop period is just where the second copy starts.
    if (period <= 1) period = els[FLAT_N].offsetLeft || 1;
    x = ((x % period) + period) % period;
    track.style.transform = "translate3d(" + (-x).toFixed(1) + "px,0,0)";
  }

  var running = false, onScreen = false;

  function loop(time) {
    if (!onScreen || document.hidden || signal.aborted) { running = false; return; }
    if (lastTime === 0) lastTime = time;
    var dt = time - lastTime;
    lastTime = time;
    if (!dragging) x += FLAT_SPEED * dt / 1000;
    render();
    requestAnimationFrame(loop);
  }

  function start() {
    if (running || reduced || !onScreen || document.hidden) return;
    running = true;
    lastTime = 0;
    requestAnimationFrame(loop);
  }

  render();
  document.addEventListener("visibilitychange", start, { signal: signal });
  var io = new IntersectionObserver(function (es) {
    onScreen = es[0].isIntersecting;
    start();
  });
  io.observe(container);
  signal.addEventListener("abort", function () { io.disconnect(); });

  container.addEventListener("touchstart", function (e) {
    dragging = true; dragStartX = e.touches[0].clientX; dragX = x;
  }, { passive: true, signal: signal });
  window.addEventListener("touchmove", function (e) {
    if (!dragging) return;
    x = dragX - (e.touches[0].clientX - dragStartX);
    if (!running) render();    // reduced motion: no loop to pick the change up
  }, { passive: true, signal: signal });
  window.addEventListener("touchend", function () { dragging = false; }, { signal: signal });
  window.addEventListener("resize", function () { period = 0; render(); }, { signal: signal });
}

export function carousel(container, basePath) {
  var mq = window.matchMedia(FLAT_MQ);
  var ac = null;
  function build() {
    if (ac) ac.abort();
    ac = new AbortController();
    container.innerHTML = "";
    (mq.matches ? flat : ring3d)(container, basePath, ac.signal);
  }
  mq.addEventListener("change", build);
  build();
}
