// 系統模擬圖 · P&ID mimic patcher: loads the SVG backplate, animates flow with stroke-dashoffset,
// colours pipes by temperature, drives pressurizer level and core glow.
import { tempColor } from "../render.js";

export class Mimic {
  constructor(container) {
    this.container = container;
    this.svg = null;
    this.dashOffset = 0;
    this.ready = false;
    this._load();
  }
  async _load() {
    try {
      const res = await fetch("svg/mimic-primary.svg");
      const txt = await res.text();
      const wrap = document.createElement("div");
      wrap.className = "mimic-wrap";
      wrap.innerHTML = txt;
      this.container.appendChild(wrap);
      this.svg = wrap.querySelector("svg");
      this.ready = true;
    } catch (e) { /* offline fallback: leave empty */ }
  }
  _q(id) { return this.svg ? this.svg.getElementById(id) : null; }

  draw(dt, state) {
    if (!this.ready || !this.svg || !state) return;
    const flow = state.flowFraction ?? 0;
    this.dashOffset -= flow * dt * 90;
    const flowEls = this.svg.querySelectorAll(".flow");
    const hotCol = tempColor(state.thot ?? 35);
    const coldCol = tempColor(state.tcold ?? 35);
    const steamCol = tempColor((state.steamPressureMPa ?? 0.5) * 40 + 100, 100, 400);
    flowEls.forEach(el => {
      el.style.strokeDashoffset = this.dashOffset;
      const id = el.id;
      let col = hotCol;
      if (id.includes("cold") || id.includes("feed")) col = coldCol;
      else if (id.includes("steam") || id.includes("exhaust")) col = steamCol;
      el.style.stroke = flow > 0.01 || (state.steamPressureMPa ?? 0) > 2 ? col : "#2b313c";
      el.style.opacity = (flow > 0.01 || id.includes("steam")) ? 1 : 0.2;
    });
    // PZR level
    const pl = this._q("pzrLevel");
    if (pl) {
      const lvl = Math.max(0, Math.min(100, state.pzrLevel ?? 55));
      const fullH = 76, y0 = 40;
      const h = fullH * lvl / 100;
      pl.setAttribute("y", (y0 + fullH - h).toFixed(1));
      pl.setAttribute("height", h.toFixed(1));
    }
    // core glow color
    const cg = this._q("coreGlow");
    if (cg) {
      const p = state.power ?? 0;
      if (state.meltdown) cg.setAttribute("fill", "#ff3b30");
      else if (p > 0.001) cg.setAttribute("fill", tempColor(state.fuelTemp ?? 35, 100, 1200));
      else cg.setAttribute("fill", "#16202e");
    }
    // RCP color
    for (let i = 1; i <= 2; i++) {
      const rcp = this._q("rcp" + i);
      const running = (state.rcpRunning && state.rcpRunning[i - 1]);
      if (rcp) rcp.setAttribute("fill", running ? "#16263f" : "#1a2030");
    }
    // generator color
    const gen = this._q("gen");
    if (gen) gen.setAttribute("fill", state.genBreaker ? "#16332a" : "#1a2030");
  }
}
