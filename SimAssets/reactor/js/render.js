// 渲染驅動 · Single requestAnimationFrame driver. Only the ACTIVE room's draw callback runs each
// frame; other rooms are paused. dpr is capped for integrated-GPU performance.

export const DPR = Math.min(window.devicePixelRatio || 1, 1.5);

let activeDraw = null;
let lastTs = 0;

export function setActiveDraw(fn) { activeDraw = fn; }

function frame(ts) {
  const dt = lastTs ? (ts - lastTs) / 1000 : 0.016;
  lastTs = ts;
  if (activeDraw) {
    try { activeDraw(Math.min(dt, 0.1), ts / 1000); } catch (e) { /* keep the loop alive */ }
  }
  requestAnimationFrame(frame);
}
requestAnimationFrame(frame);

// ----- canvas helpers -----
export function fitCanvas(canvas, w, h) {
  canvas.style.width = w + "px";
  canvas.style.height = h + "px";
  canvas.width = Math.round(w * DPR);
  canvas.height = Math.round(h * DPR);
  const ctx = canvas.getContext("2d");
  ctx.setTransform(DPR, 0, 0, DPR, 0, 0);
  return ctx;
}

// ----- temperature ramp (256-entry LUT, built once) -----
const RAMP_STOPS = [
  [0.00, [27, 108, 255]],   // blue (cold)
  [0.20, [25, 224, 255]],   // cyan
  [0.40, [53, 208, 127]],   // green
  [0.62, [255, 225, 77]],   // yellow
  [0.80, [255, 154, 60]],   // orange
  [1.00, [255, 59, 48]],    // red (hot)
];
const RAMP = (() => {
  const lut = new Array(256);
  for (let i = 0; i < 256; i++) {
    const f = i / 255;
    let a = RAMP_STOPS[0], b = RAMP_STOPS[RAMP_STOPS.length - 1];
    for (let s = 0; s < RAMP_STOPS.length - 1; s++) {
      if (f >= RAMP_STOPS[s][0] && f <= RAMP_STOPS[s + 1][0]) { a = RAMP_STOPS[s]; b = RAMP_STOPS[s + 1]; break; }
    }
    const span = (b[0] - a[0]) || 1;
    const k = (f - a[0]) / span;
    const r = Math.round(a[1][0] + (b[1][0] - a[1][0]) * k);
    const g = Math.round(a[1][1] + (b[1][1] - a[1][1]) * k);
    const bl = Math.round(a[1][2] + (b[1][2] - a[1][2]) * k);
    lut[i] = `rgb(${r},${g},${bl})`;
  }
  return lut;
})();

// normalize a temperature in °C (≈30..660) to a ramp colour
export function tempColor(t, lo = 30, hi = 660) {
  const f = Math.max(0, Math.min(1, (t - lo) / (hi - lo)));
  return RAMP[Math.round(f * 255)];
}

export function tempColorRGB(t, lo = 30, hi = 660) {
  const f = Math.max(0, Math.min(1, (t - lo) / (hi - lo)));
  const s = RAMP[Math.round(f * 255)];
  const m = s.match(/\d+/g);
  return [+m[0], +m[1], +m[2]];
}

export function clamp(v, lo, hi) { return Math.max(lo, Math.min(hi, v)); }
export function lerp(a, b, f) { return a + (b - a) * f; }
