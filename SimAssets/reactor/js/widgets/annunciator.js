// 報警盤 · Annunciator matrix. ISA-18.2-ish state machine per tile:
// normal → new (blink, fast) → ack (steady) → cleared. First-out highlight on the first new alarm.
import { t, pick } from "../i18n.js";

// alarm enum index → bilingual label + severity
export const ALARMS = [
  ["High power", "高功率", "warn"],
  ["High fuel temp", "燃料高溫", "danger"],
  ["High coolant temp", "冷卻劑高溫", "danger"],
  ["High pressure", "高壓", "danger"],
  ["Low pressure", "低壓", "warn"],
  ["Low flow", "低流量", "danger"],
  ["Low PZR level", "穩壓器低水位", "warn"],
  ["High PZR level", "穩壓器高水位", "warn"],
  ["Short period", "週期過短", "danger"],
  ["SCRAM", "緊急停堆", "danger"],
  ["High neutron flux", "高中子通量", "danger"],
  ["Steam press high", "蒸汽高壓", "warn"],
  ["CORE DAMAGE", "堆芯損傷", "danger"],
  ["ECCS active", "應急冷卻啟動", "warn"],
  ["Turbine trip", "汽輪機跳脫", "warn"],
  ["Low subcooling", "過冷裕度低", "danger"],
  ["Decay heat high", "衰變熱高", "warn"],
  ["ATWS active", "未停堆暫態", "danger"],
  ["Accumulator inject", "安注箱注入", "warn"],
  ["Aux feedwater", "輔助給水", "warn"],
  ["Natural circ", "自然循環", "warn"],
];

export class Annunciator {
  constructor(container) {
    this.container = container;
    this.container.className = "annun-grid";
    this.tiles = ALARMS.map(([en, zh, sev], i) => {
      const el = document.createElement("div");
      el.className = "tile";
      el.dataset.idx = i;
      this.container.appendChild(el);
      return { el, en, zh, sev, state: "normal" };
    });
    this.firstOut = -1;
  }
  relabel() {
    for (const tl of this.tiles) {
      tl.el.innerHTML = `${pick(tl.en, tl.zh)}<br><small>${pick(tl.zh, tl.en)}</small>`;
    }
  }
  update(activeSet) {
    for (let i = 0; i < this.tiles.length; i++) {
      const tl = this.tiles[i];
      const on = activeSet.has(i);
      if (on && tl.state === "normal") {
        tl.state = "new";
        if (this.firstOut < 0) this.firstOut = i;
      } else if (!on) {
        tl.state = "normal";
        if (this.firstOut === i) this.firstOut = -1;
      }
      let cls = "tile";
      if (tl.state !== "normal") {
        cls += " active" + (tl.sev === "warn" ? " warn" : "");
        if (tl.state === "new") cls += " new";
        if (this.firstOut === i) cls += " first";
      }
      tl.el.className = cls;
    }
  }
  ack() { for (const tl of this.tiles) if (tl.state === "new") tl.state = "ack"; this.firstOut = -1; }
  lampTest(on) {
    for (const tl of this.tiles) tl.el.className = on ? "tile active" : "tile";
    if (!on) this.relabel();
  }
}
