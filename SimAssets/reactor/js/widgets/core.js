// 切連科夫輝光 · Cherenkov core glow: stacked additive radial gradients, power-scaled flicker.
import { fitCanvas, clamp } from "../render.js";

export class CoreGlow {
  constructor(opts) {
    this.w = opts.w ?? 240; this.h = opts.h ?? 240;
    this.canvas = document.createElement("canvas");
    this.ctx = fitCanvas(this.canvas, this.w, this.h);
    this.power = 0; this.meltdown = false; this._t = 0;
  }
  get el() { return this.canvas; }
  set(power, meltdown) { this.power = power; this.meltdown = meltdown; }
  draw(dt, time) {
    this._t += dt;
    const ctx = this.ctx, w = this.w, h = this.h, cx = w / 2, cy = h / 2;
    ctx.clearRect(0, 0, w, h);
    const p = clamp(this.power, 0, 1.2);
    // vessel ring
    ctx.strokeStyle = this.meltdown ? "#ff3b30" : "#3a4a66";
    ctx.lineWidth = 3; ctx.beginPath(); ctx.arc(cx, cy, w * 0.42, 0, Math.PI * 2); ctx.stroke();
    if (p <= 0.0001 && !this.meltdown) {
      ctx.fillStyle = "#16202e"; ctx.beginPath(); ctx.arc(cx, cy, w * 0.38, 0, Math.PI * 2); ctx.fill();
      return;
    }
    const flicker = 1 + 0.08 * Math.sin(time * 23) + 0.05 * Math.sin(time * 53.7);
    const baseR = w * 0.40 * (0.45 + 0.55 * Math.min(1, p)) * flicker;
    ctx.save();
    ctx.globalCompositeOperation = "lighter";
    const layers = this.meltdown
      ? [[1.0, "255,70,40"], [0.6, "255,140,40"], [0.35, "255,210,80"]]
      : [[1.0, "30,150,255"], [0.6, "60,200,255"], [0.35, "150,240,255"]];
    for (const [scale, rgb] of layers) {
      const r = baseR * scale;
      const g = ctx.createRadialGradient(cx, cy, 0, cx, cy, r);
      const a = clamp(p, 0, 1) * (this.meltdown ? 0.9 : 0.7);
      g.addColorStop(0, `rgba(${rgb},${a})`);
      g.addColorStop(0.5, `rgba(${rgb},${a * 0.4})`);
      g.addColorStop(1, `rgba(${rgb},0)`);
      ctx.fillStyle = g;
      ctx.beginPath(); ctx.arc(cx, cy, r, 0, Math.PI * 2); ctx.fill();
    }
    ctx.restore();
    // fuel-rod lattice hint
    ctx.globalAlpha = 0.25; ctx.strokeStyle = this.meltdown ? "#ffcc88" : "#bfe6ff"; ctx.lineWidth = 1;
    for (let i = -3; i <= 3; i++) {
      const off = i * w * 0.05;
      ctx.beginPath(); ctx.moveTo(cx + off, cy - baseR * 0.6); ctx.lineTo(cx + off, cy + baseR * 0.6); ctx.stroke();
    }
    ctx.globalAlpha = 1;
  }
}
