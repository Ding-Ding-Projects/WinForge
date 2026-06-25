// 蒸汽粒子 · Steam particle puffs with 'screen' blend at an outlet point.
import { fitCanvas, clamp } from "../render.js";

export class Steam {
  constructor(opts) {
    this.w = opts.w ?? 200; this.h = opts.h ?? 160;
    this.canvas = document.createElement("canvas");
    this.ctx = fitCanvas(this.canvas, this.w, this.h);
    this.parts = [];
    this.x = opts.x ?? this.w / 2; this.y = opts.y ?? this.h - 10;
    this.rate = 0; this._acc = 0;
  }
  get el() { return this.canvas; }
  set(rate) { this.rate = clamp(rate, 0, 1); }
  draw(dt) {
    this._acc += this.rate * dt * 40;
    while (this._acc >= 1) {
      this._acc -= 1;
      this.parts.push({
        x: this.x + (Math.random() - 0.5) * 14, y: this.y,
        vx: (Math.random() - 0.5) * 12, vy: -20 - Math.random() * 30,
        life: 0, max: 1.4 + Math.random(), r: 5 + Math.random() * 6,
      });
    }
    const ctx = this.ctx, w = this.w, h = this.h;
    ctx.clearRect(0, 0, w, h);
    ctx.save(); ctx.globalCompositeOperation = "screen";
    for (let i = this.parts.length - 1; i >= 0; i--) {
      const p = this.parts[i];
      p.life += dt; p.x += p.vx * dt; p.y += p.vy * dt; p.vy *= 0.99; p.r += dt * 14;
      if (p.life > p.max) { this.parts.splice(i, 1); continue; }
      const a = (1 - p.life / p.max) * 0.5;
      const g = ctx.createRadialGradient(p.x, p.y, 0, p.x, p.y, p.r);
      g.addColorStop(0, `rgba(220,235,255,${a})`);
      g.addColorStop(1, "rgba(220,235,255,0)");
      ctx.fillStyle = g;
      ctx.beginPath(); ctx.arc(p.x, p.y, p.r, 0, Math.PI * 2); ctx.fill();
    }
    ctx.restore();
  }
}
