// 趨勢記錄儀 · Strip chart with a Float32Array ring buffer, full polyline redraw.
import { fitCanvas, clamp } from "../render.js";

export class StripChart {
  constructor(opts) {
    this.w = opts.w ?? 320; this.h = opts.h ?? 110;
    this.min = opts.min ?? 0; this.max = opts.max ?? 100;
    this.color = opts.color ?? "#4cc2ff";
    this.label = opts.label ?? "";
    this.n = opts.n ?? 240;
    this.buf = new Float32Array(this.n).fill(this.min);
    this.head = 0;
    this.canvas = document.createElement("canvas");
    this.ctx = fitCanvas(this.canvas, this.w, this.h);
    this._acc = 0; this._period = opts.period ?? 0.25; // s between samples
  }
  get el() { return this.canvas; }
  push(v) {
    this.buf[this.head] = clamp(v, this.min, this.max);
    this.head = (this.head + 1) % this.n;
  }
  feed(dt, v) { this._acc += dt; if (this._acc >= this._period) { this._acc = 0; this.push(v); } }
  draw() {
    const ctx = this.ctx, w = this.w, h = this.h;
    ctx.clearRect(0, 0, w, h);
    ctx.fillStyle = "#0c0f14"; ctx.fillRect(0, 0, w, h);
    ctx.strokeStyle = "#1c222b"; ctx.lineWidth = 1;
    for (let i = 1; i < 4; i++) { const y = (h * i) / 4; ctx.beginPath(); ctx.moveTo(0, y); ctx.lineTo(w, y); ctx.stroke(); }
    ctx.strokeStyle = this.color; ctx.lineWidth = 1.6; ctx.beginPath();
    for (let i = 0; i < this.n; i++) {
      const idx = (this.head + i) % this.n;
      const v = this.buf[idx];
      const f = (v - this.min) / (this.max - this.min);
      const x = (i / (this.n - 1)) * w;
      const y = h - f * h;
      if (i === 0) ctx.moveTo(x, y); else ctx.lineTo(x, y);
    }
    ctx.stroke();
    ctx.fillStyle = "#93a2b5"; ctx.font = "10px 'Segoe UI'"; ctx.textAlign = "left";
    ctx.fillText(this.label, 6, 13);
  }
}
