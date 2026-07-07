// 同步指示器 · Synchroscope: pointer rotates at the slip frequency between machine and grid.
import { fitCanvas } from "../render.js";

export class Synchroscope {
  constructor(opts) {
    this.w = opts.w ?? 160; this.h = opts.h ?? 160;
    this.canvas = document.createElement("canvas");
    this.ctx = fitCanvas(this.canvas, this.w, this.h);
    this.angle = 0; this.slip = 0; this.breaker = false;
  }
  get el() { return this.canvas; }
  // rpm vs grid 1800 → slip rotation; breaker locks pointer to 12 o'clock when synced+closed
  set(rpm, breakerClosed) {
    this.slip = (rpm - 1800) / 1800; // fraction
    this.breaker = breakerClosed;
  }
  draw(dt) {
    if (this.breaker && Math.abs(this.slip) < 0.02) this.angle += (0 - this.angle) * Math.min(1, dt * 4);
    else this.angle += this.slip * dt * 6.2832 * 3;
    const ctx = this.ctx, w = this.w, h = this.h, cx = w / 2, cy = h / 2, r = w * 0.4;
    ctx.clearRect(0, 0, w, h);
    ctx.fillStyle = "#0e1218"; ctx.beginPath(); ctx.arc(cx, cy, r + 6, 0, Math.PI * 2); ctx.fill();
    ctx.strokeStyle = "#2b313c"; ctx.lineWidth = 1; ctx.stroke();
    // sync zone at top
    ctx.strokeStyle = "#35d07f"; ctx.lineWidth = 4;
    ctx.beginPath(); ctx.arc(cx, cy, r, -Math.PI / 2 - 0.25, -Math.PI / 2 + 0.25); ctx.stroke();
    // ticks
    ctx.strokeStyle = "#5a6b80"; ctx.lineWidth = 1;
    for (let i = 0; i < 12; i++) {
      const a = (i / 12) * Math.PI * 2;
      ctx.beginPath();
      ctx.moveTo(cx + Math.cos(a) * (r - 6), cy + Math.sin(a) * (r - 6));
      ctx.lineTo(cx + Math.cos(a) * r, cy + Math.sin(a) * r);
      ctx.stroke();
    }
    // FAST / SLOW labels
    ctx.fillStyle = "#93a2b5"; ctx.font = "9px 'Segoe UI'"; ctx.textAlign = "center";
    ctx.fillText("SLOW", cx - r * 0.6, cy); ctx.fillText("FAST", cx + r * 0.6, cy);
    // pointer (0 angle = top)
    const a = -Math.PI / 2 + this.angle;
    ctx.strokeStyle = (Math.abs(this.slip) < 0.02) ? "#35d07f" : "#ffb300";
    ctx.lineWidth = 3; ctx.lineCap = "round";
    ctx.beginPath(); ctx.moveTo(cx - Math.cos(a) * 8, cy - Math.sin(a) * 8);
    ctx.lineTo(cx + Math.cos(a) * (r - 10), cy + Math.sin(a) * (r - 10)); ctx.stroke();
    ctx.fillStyle = "#4cc2ff"; ctx.beginPath(); ctx.arc(cx, cy, 4, 0, Math.PI * 2); ctx.fill();
  }
}
