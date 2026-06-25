// 條形圖 · Vertical LED bargraph with peak-hold.
import { fitCanvas, clamp } from "../render.js";

export class Bargraph {
  constructor(opts) {
    this.min = opts.min ?? 0; this.max = opts.max ?? 100;
    this.label = opts.label ?? ""; this.unit = opts.unit ?? "";
    this.w = opts.w ?? 56; this.h = opts.h ?? 150;
    this.warn = opts.warn ?? 0.85; this.danger = opts.danger ?? 0.95;
    this.invert = opts.invert ?? false; // danger at low end
    this.fmt = opts.fmt ?? (v => v.toFixed(0));
    this.value = this.min; this.peak = this.min;
    this.canvas = document.createElement("canvas");
    this.ctx = fitCanvas(this.canvas, this.w, this.h);
  }
  get el() { return this.canvas; }
  set(v) {
    this.value = clamp(v, this.min, this.max);
    if (this.value > this.peak) this.peak = this.value;
    this.peak += (this.value - this.peak) * 0.004; // slow peak decay
  }
  draw() {
    const ctx = this.ctx, w = this.w, h = this.h;
    ctx.clearRect(0, 0, w, h);
    const barW = w - 18, x = 4, top = 14, bot = h - 14, span = bot - top;
    ctx.fillStyle = "#0e1218"; ctx.fillRect(x, top, barW, span);
    ctx.strokeStyle = "#2b313c"; ctx.strokeRect(x, top, barW, span);
    const segs = 16, gap = 2, segH = (span - gap * (segs - 1)) / segs;
    const f = clamp((this.value - this.min) / (this.max - this.min), 0, 1);
    const lit = Math.round(f * segs);
    for (let i = 0; i < segs; i++) {
      const sf = (i + 0.5) / segs;
      let on = i < lit;
      let col;
      const sev = this.invert ? 1 - sf : sf;
      if (sev >= this.danger) col = "#ff3b30";
      else if (sev >= this.warn) col = "#ffb300";
      else col = "#35d07f";
      ctx.fillStyle = on ? col : "#1a1f27";
      ctx.globalAlpha = on ? 1 : 0.6;
      const yy = bot - segH - i * (segH + gap);
      ctx.fillRect(x + 2, yy, barW - 4, segH);
    }
    ctx.globalAlpha = 1;
    // peak-hold marker
    const pf = clamp((this.peak - this.min) / (this.max - this.min), 0, 1);
    const py = bot - pf * span;
    ctx.strokeStyle = "#4cc2ff"; ctx.lineWidth = 2;
    ctx.beginPath(); ctx.moveTo(x, py); ctx.lineTo(x + barW, py); ctx.stroke();
    // labels
    ctx.fillStyle = "#93a2b5"; ctx.font = "9px 'Segoe UI'"; ctx.textAlign = "center";
    ctx.fillText(this.label, w / 2, 10);
    ctx.fillStyle = "#e6edf5"; ctx.font = "bold 10px Consolas";
    ctx.fillText(this.fmt(this.value), w / 2, h - 2);
  }
}
