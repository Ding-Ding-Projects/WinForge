// 汽輪機轉子 · Rotating turbine rotor, angular speed ∝ RPM.
import { fitCanvas } from "../render.js";

export class Turbine {
  constructor(opts) {
    this.w = opts.w ?? 200; this.h = opts.h ?? 200;
    this.canvas = document.createElement("canvas");
    this.ctx = fitCanvas(this.canvas, this.w, this.h);
    this.rpm = 0; this.angle = 0; this.blades = opts.blades ?? 16;
  }
  get el() { return this.canvas; }
  set(rpm) { this.rpm = rpm; }
  draw(dt) {
    // visual speed scaled down from real rpm
    this.angle += (this.rpm / 1800) * dt * 6.2832 * 1.5;
    const ctx = this.ctx, w = this.w, h = this.h, cx = w / 2, cy = h / 2, r = w * 0.4;
    ctx.clearRect(0, 0, w, h);
    ctx.save(); ctx.translate(cx, cy); ctx.rotate(this.angle);
    // hub
    ctx.fillStyle = "#2a3340"; ctx.beginPath(); ctx.arc(0, 0, r * 0.22, 0, Math.PI * 2); ctx.fill();
    ctx.strokeStyle = "#4cc2ff"; ctx.lineWidth = 2; ctx.stroke();
    // blades
    for (let i = 0; i < this.blades; i++) {
      const a = (i / this.blades) * Math.PI * 2;
      ctx.save(); ctx.rotate(a);
      ctx.fillStyle = "#3a4656";
      ctx.beginPath();
      ctx.moveTo(0, -r * 0.22);
      ctx.lineTo(-r * 0.07, -r);
      ctx.lineTo(r * 0.07, -r);
      ctx.closePath(); ctx.fill();
      ctx.strokeStyle = "#5a6b80"; ctx.lineWidth = 0.5; ctx.stroke();
      ctx.restore();
    }
    ctx.restore();
    // casing
    ctx.strokeStyle = "#2b313c"; ctx.lineWidth = 3;
    ctx.beginPath(); ctx.arc(cx, cy, r + 4, 0, Math.PI * 2); ctx.stroke();
    // rpm text
    ctx.fillStyle = "#e6edf5"; ctx.font = "bold 14px Consolas"; ctx.textAlign = "center";
    ctx.fillText(this.rpm.toFixed(0) + " rpm", cx, cy + r + 22);
    // sync indicator
    const synced = Math.abs(this.rpm - 1800) < 30;
    ctx.fillStyle = synced ? "#35d07f" : "#93a2b5"; ctx.font = "10px 'Segoe UI'";
    ctx.fillText(synced ? "SYNCHRONOUS 1800" : "—", cx, cy + r + 36);
  }
}
