// 類比錶 · Analog gauge: offscreen pre-rendered face + scale, spring-damped needle.
import { DPR, fitCanvas, clamp } from "../render.js";

export class Gauge {
  constructor(opts) {
    this.min = opts.min ?? 0;
    this.max = opts.max ?? 100;
    this.label = opts.label ?? "";
    this.unit = opts.unit ?? "";
    this.bands = opts.bands ?? [];        // [{lo,hi,kind}]
    this.setpoints = opts.setpoints ?? []; // [{v,label}]
    this.size = opts.size ?? 150;
    this.fmt = opts.fmt ?? (v => v.toFixed(0));
    this.value = this.min; this.target = this.min; this.vel = 0;

    this.canvas = document.createElement("canvas");
    this.canvas.className = "gauge";
    this.ctx = fitCanvas(this.canvas, this.size, this.size);
    this.face = document.createElement("canvas");
    this._renderFace();
  }

  get el() { return this.canvas; }

  _bandColor(kind) {
    return kind === "danger" ? "#ff3b30" : kind === "warn" ? "#ffb300" : "#35d07f";
  }

  _renderFace() {
    const s = this.size, r = s / 2 - 8, cx = s / 2, cy = s / 2 + 6;
    this.face.width = s * DPR; this.face.height = s * DPR;
    const c = this.face.getContext("2d");
    c.setTransform(DPR, 0, 0, DPR, 0, 0);
    c.clearRect(0, 0, s, s);
    const a0 = Math.PI * 0.78, a1 = Math.PI * 2.22; // sweep ~260°
    // bezel
    c.beginPath(); c.arc(cx, cy, r + 6, 0, Math.PI * 2);
    c.fillStyle = "#0e1218"; c.fill();
    c.lineWidth = 1; c.strokeStyle = "#2b313c"; c.stroke();
    // band arcs
    for (const b of this.bands) {
      const f0 = clamp((b.lo - this.min) / (this.max - this.min), 0, 1);
      const f1 = clamp((b.hi - this.min) / (this.max - this.min), 0, 1);
      c.beginPath();
      c.arc(cx, cy, r - 4, a0 + (a1 - a0) * f0, a0 + (a1 - a0) * f1);
      c.lineWidth = 6; c.strokeStyle = this._bandColor(b.kind); c.globalAlpha = 0.85; c.stroke();
      c.globalAlpha = 1;
    }
    // ticks
    const ticks = 8;
    c.strokeStyle = "#6b7787"; c.fillStyle = "#93a2b5"; c.font = "9px Consolas";
    c.textAlign = "center"; c.textBaseline = "middle";
    for (let i = 0; i <= ticks; i++) {
      const f = i / ticks, a = a0 + (a1 - a0) * f;
      const x0 = cx + Math.cos(a) * (r - 10), y0 = cy + Math.sin(a) * (r - 10);
      const x1 = cx + Math.cos(a) * (r - 2), y1 = cy + Math.sin(a) * (r - 2);
      c.lineWidth = 1.5; c.beginPath(); c.moveTo(x0, y0); c.lineTo(x1, y1); c.stroke();
      const tx = cx + Math.cos(a) * (r - 20), ty = cy + Math.sin(a) * (r - 20);
      const val = this.min + (this.max - this.min) * f;
      c.fillText(this.fmt(val), tx, ty);
    }
    // setpoint markers
    for (const sp of this.setpoints) {
      const f = clamp((sp.v - this.min) / (this.max - this.min), 0, 1);
      const a = a0 + (a1 - a0) * f;
      c.strokeStyle = "#ff4d4d"; c.lineWidth = 2;
      c.beginPath();
      c.moveTo(cx + Math.cos(a) * (r - 4), cy + Math.sin(a) * (r - 4));
      c.lineTo(cx + Math.cos(a) * (r + 4), cy + Math.sin(a) * (r + 4));
      c.stroke();
    }
    // label
    c.fillStyle = "#93a2b5"; c.font = "11px 'Segoe UI'"; c.textBaseline = "alphabetic";
    c.fillText(this.label, cx, cy + r - 4);
    this._a0 = a0; this._a1 = a1; this._cx = cx; this._cy = cy; this._r = r;
  }

  setLabel(label, unit) {
    if (label !== undefined) this.label = label;
    if (unit !== undefined) this.unit = unit;
    this._renderFace();
  }

  set(v) { this.target = clamp(v, this.min, this.max); }

  draw(dt) {
    // spring-damped needle
    const k = 60, damp = 12;
    const a = k * (this.target - this.value) - damp * this.vel;
    this.vel += a * dt; this.value += this.vel * dt;
    if (Math.abs(this.target - this.value) < 0.001 && Math.abs(this.vel) < 0.001) {
      this.value = this.target; this.vel = 0;
    }

    const s = this.size, ctx = this.ctx;
    ctx.clearRect(0, 0, s, s);
    ctx.drawImage(this.face, 0, 0, s, s);
    const f = clamp((this.value - this.min) / (this.max - this.min), 0, 1);
    const ang = this._a0 + (this._a1 - this._a0) * f;
    const cx = this._cx, cy = this._cy, r = this._r;
    // needle
    ctx.strokeStyle = "#e6edf5"; ctx.lineWidth = 2.5; ctx.lineCap = "round";
    ctx.beginPath();
    ctx.moveTo(cx - Math.cos(ang) * 10, cy - Math.sin(ang) * 10);
    ctx.lineTo(cx + Math.cos(ang) * (r - 12), cy + Math.sin(ang) * (r - 12));
    ctx.stroke();
    ctx.beginPath(); ctx.arc(cx, cy, 4, 0, Math.PI * 2);
    ctx.fillStyle = "#4cc2ff"; ctx.fill();
    // digital readout
    ctx.fillStyle = "#e6edf5"; ctx.font = "bold 15px Consolas"; ctx.textAlign = "center";
    ctx.fillText(this.fmt(this.value) + (this.unit ? " " + this.unit : ""), cx, cy - r * 0.42);
  }
}
