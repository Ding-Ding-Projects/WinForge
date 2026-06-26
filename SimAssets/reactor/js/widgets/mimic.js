// 系統模擬圖 · P&ID mimic patcher: loads the SVG backplate, animates flow with stroke-dashoffset,
// colours pipes by temperature, and binds abstract plant status to the live simulation state.
import { tempColor } from "../render.js";

const STATE_CLASSES = ["state-normal", "state-watch", "state-challenged", "state-trip", "state-unavailable"];
const STATUS_CLASSES = ["state-normal", "state-watch", "state-challenged", "state-trip", "state-unavailable"];
const SCENARIOS_EN = [
  "Normal operations",
  "Primary leak",
  "Station blackout",
  "LOCA",
  "Rod drop",
  "Turbine trip",
  "SG tube leak",
  "Main steam line break",
  "Fuel defect",
  "PZR spray failure",
  "PZR PORV stuck open",
  "Loss of feedwater",
  "SGTR with SBO",
  "Anticipated transient",
  "ECCS recirculation",
  "Xenon restart"
];
const SCENARIOS_ZH = [
  "正常運行",
  "一次側泄漏",
  "全廠失電",
  "冷卻水喪失",
  "控制棒掉落",
  "汽輪機跳閘",
  "蒸汽發生器管漏",
  "主蒸汽管破裂",
  "燃料缺陷",
  "穩壓器噴淋故障",
  "穩壓器閥門卡開",
  "給水喪失",
  "SGTR 加 SBO",
  "預期瞬變",
  "ECCS 再循環",
  "氙毒重啟"
];

function clamp(v, lo, hi) {
  const n = Number.isFinite(v) ? v : lo;
  return Math.max(lo, Math.min(hi, n));
}

function pct(v, digits = 0) {
  return `${(clamp(v, 0, 1) * 100).toFixed(digits)}%`;
}

function pick(state, en, zh) {
  return state?.zhPrimary ? zh : en;
}

export class Mimic {
  constructor(container) {
    this.container = container;
    this.svg = null;
    this.dashOffset = 0;
    this.rotorAngle = 0;
    this.ready = false;
    this._load();
  }

  async _load() {
    for (const file of ["svg/plant-mimic-v2.svg", "svg/mimic-primary.svg"]) {
      try {
        const res = await fetch(file);
        if (!res.ok) continue;
        const txt = await res.text();
        const wrap = document.createElement("div");
        wrap.className = "mimic-wrap";
        wrap.innerHTML = txt;
        this.container.replaceChildren(wrap);
        this.svg = wrap.querySelector("svg");
        this.ready = !!this.svg;
        if (this.ready) return;
      } catch (e) {
        // Offline fallback: try the next local asset.
      }
    }
  }

  _q(id) { return this.svg ? this.svg.getElementById(id) : null; }

  _setText(id, text) {
    const el = this._q(id);
    if (el) el.textContent = text;
  }

  _setVisible(id, on) {
    const el = this._q(id);
    if (!el) return;
    el.style.opacity = on ? 1 : 0;
    el.style.display = on ? "" : "none";
  }

  _setState(id, stateName) {
    const el = this._q(id);
    if (!el) return;
    STATE_CLASSES.forEach(c => el.classList.remove(c));
    el.classList.add(`state-${stateName}`);
  }

  _setStatus(id, stateName) {
    const el = this._q(id);
    if (!el) return;
    STATUS_CLASSES.forEach(c => el.classList.remove(c));
    el.classList.add(`state-${stateName}`);
  }

  _setLevel(id, value) {
    const el = this._q(id);
    if (!el) return;
    const pctValue = clamp(value, 0, 100) / 100;
    const y0 = parseFloat(el.dataset.y0 ?? "40");
    const fullH = parseFloat(el.dataset.h ?? "76");
    const h = fullH * pctValue;
    el.setAttribute("y", (y0 + fullH - h).toFixed(1));
    el.setAttribute("height", h.toFixed(1));
  }

  draw(dt, state) {
    if (!this.ready || !this.svg || !state) return;
    const flow = state.flowFraction ?? 0;
    const steamDrive = (state.steamPressureMPa ?? 0) > 0.35 || (state.turbineRpm ?? 0) > 50;
    const feedDrive = (state.feedwaterFlow ?? 0) > 0.01 || state.auxFeed;
    const ccwDrive = (state.ccwFlow ?? 0) > 0.01 || state.ccwAvailable;
    this.dashOffset -= Math.max(flow, state.feedwaterFlow ?? 0, state.ccwFlow ?? 0, steamDrive ? 0.3 : 0) * dt * 90;
    const flowEls = this.svg.querySelectorAll(".flow");
    const hotCol = tempColor(state.thot ?? 35);
    const coldCol = tempColor(state.tcold ?? 35);
    const steamCol = tempColor((state.steamPressureMPa ?? 0.5) * 40 + 100, 100, 400);
    flowEls.forEach(el => {
      el.style.strokeDashoffset = this.dashOffset;
      const id = el.id;
      let col = hotCol;
      let active = flow > 0.01;
      if (id.includes("cold") || id.includes("feed")) {
        col = coldCol;
        active = id.includes("feed") ? feedDrive : active;
      } else if (id.includes("steam") || id.includes("exhaust") || id.includes("generator")) {
        col = steamCol;
        active = steamDrive;
      } else if (id.includes("ccw")) {
        col = "#6ed6ff";
        active = ccwDrive;
      }
      el.style.stroke = active ? col : "#2b313c";
      el.style.opacity = active ? 1 : 0.2;
    });

    this._setLevel("pzrLevel", state.pzrLevel ?? 55);
    this._setLevel("sgLevel", state.sgLevel ?? 50);

    const cg = this._q("coreGlow");
    if (cg) {
      const p = state.power ?? 0;
      if (state.meltdown) cg.setAttribute("fill", "#ff3b30");
      else if (p > 0.001) cg.setAttribute("fill", tempColor(state.fuelTemp ?? 35, 100, 1200));
      else cg.setAttribute("fill", "#16202e");
      cg.setAttribute("opacity", (0.35 + clamp(p, 0, 1) * 0.6).toFixed(2));
    }

    for (let i = 1; i <= 2; i++) {
      const rcp = this._q("rcp" + i);
      const running = (state.rcpRunning && state.rcpRunning[i - 1]);
      if (rcp) rcp.setAttribute("fill", running ? "#16263f" : "#1a2030");
    }

    const gen = this._q("gen");
    if (gen) gen.setAttribute("fill", state.genBreaker ? "#16332a" : "#1a2030");

    this._drawV2(dt, state);
  }

  _drawV2(dt, state) {
    if (this.svg.id !== "plantMimicV2") return;

    const power = clamp(state.power ?? 0, 0, 1.6);
    const flow = clamp(state.flowFraction ?? 0, 0, 1.4);
    const steam = state.steamPressureMPa ?? 0;
    const pzr = clamp(state.pzrLevel ?? 55, 0, 100);
    const sg = clamp(state.sgLevel ?? 50, 0, 100);
    const feed = clamp(state.feedwaterFlow ?? 0, 0, 1.2);
    const ccw = clamp(state.ccwFlow ?? 0, 0, 1.2);
    const fuel = state.fuelTemp ?? 35;
    const rad = state.radiationLevel ?? 0;
    const secondaryRad = state.secondaryRadiation ?? 0;
    const containmentPressure = state.containmentPressureKpa ?? 0;
    const containmentTemp = state.containmentTempC ?? 35;
    const h2 = state.containmentH2Pct ?? 0;
    const coolantStable = flow > 0.18 || state.eccsInjecting || state.accumInject || power < 0.02;
    const steamSinkAvailable = feed > 0.03 || state.auxFeed || steam > 0.8 || power < 0.02;
    const electricalAvailable = state.acAvailable !== false && !state.stationBlackout;
    const chemistryAlarm = !!state.chemistryAlarm || (state.makeupInSpec === false) || rad > 3 || secondaryRad > 20;

    const coreState = state.meltdown || fuel > 1050 ? "trip"
      : state.scrammed || power > 1.05 || fuel > 820 ? "challenged"
      : power > 0.65 || fuel > 540 ? "watch"
      : "normal";
    const heatState = !coolantStable ? "trip"
      : flow < 0.45 || !steamSinkAvailable ? "challenged"
      : feed < 0.08 && power > 0.15 ? "watch"
      : "normal";
    const containmentState = containmentPressure > 260 || h2 > 5 ? "trip"
      : state.containmentIsolation || state.containmentSpray || containmentPressure > 90 || h2 > 2 ? "watch"
      : "normal";
    const electricalState = state.stationBlackout ? "trip"
      : state.edgPower ? "watch"
      : electricalAvailable ? "normal"
      : "unavailable";
    const chemistryState = chemistryAlarm ? "challenged" : "normal";

    this._setState("coreZone", coreState);
    this._setState("primaryZone", heatState);
    this._setState("secondaryZone", steamSinkAvailable ? "normal" : "watch");
    this._setState("heatSinkZone", ccw > 0.05 || state.ccwAvailable ? "normal" : "watch");
    this._setState("electricalZone", electricalState);
    this._setState("containmentBoundary", containmentState);

    this._setStatus("cardCoreHeat", coreState);
    this._setStatus("cardHeatRemoval", heatState);
    this._setStatus("cardContainment", containmentState);
    this._setStatus("cardElectrical", electricalState);
    this._setStatus("cardChemistry", chemistryState);

    this._setText("txtPower", pct(power, power < 0.01 ? 2 : 1));
    this._setText("txtFlow", pct(flow, 0));
    this._setText("txtPzr", `${pzr.toFixed(0)}%`);
    this._setText("txtSg", `${sg.toFixed(0)}%`);
    this._setText("txtSteam", `${steam.toFixed(1)} MPa`);
    this._setText("txtCcw", pct(ccw, 0));
    this._setText("txtRad", rad > 5 || secondaryRad > 30 ? pick(state, "high", "偏高") : rad > 1 ? pick(state, "watch", "留意") : pick(state, "normal", "正常"));
    this._setText("txtBattery", pct(state.batterySoc ?? 1, 0));
    this._setText("txtContainmentReadout", `${containmentPressure.toFixed(0)} kPa · ${containmentTemp.toFixed(0)} C`);

    const scenarioId = clamp(Math.round(state.scenarioId ?? 0), 0, SCENARIOS_EN.length - 1);
    this._setText("txtScenario", state.zhPrimary ? SCENARIOS_ZH[scenarioId] : SCENARIOS_EN[scenarioId]);
    this._setText("txtMode", this._modeText(state));
    this._setText("txtCoreHeat", this._coreText(state, coreState));
    this._setText("txtHeatRemoval", this._heatText(state, heatState));
    this._setText("txtContainment", this._containmentText(state, containmentState));
    this._setText("txtElectrical", this._electricalText(state, electricalState));
    this._setText("txtChemistry", this._chemistryText(state, chemistryState));

    const rods = Array.isArray(state.rodBank) && state.rodBank.length
      ? state.rodBank.reduce((a, b) => a + b, 0) / state.rodBank.length
      : 100;
    const controlRods = this._q("controlRods");
    if (controlRods) controlRods.setAttribute("transform", `translate(0 ${((100 - clamp(rods, 0, 100)) * 0.42).toFixed(1)})`);

    const rpm = state.turbineRpm ?? 0;
    this.rotorAngle = (this.rotorAngle + rpm * dt * 0.22) % 360;
    const turbineRotor = this._q("turbineRotor");
    const genRotor = this._q("genRotor");
    if (turbineRotor) turbineRotor.setAttribute("transform", `rotate(${this.rotorAngle.toFixed(1)} 673 226)`);
    if (genRotor) genRotor.setAttribute("transform", `rotate(${this.rotorAngle.toFixed(1)} 741 226)`);

    const showInjection = !!state.eccsInjecting || !!state.accumInject;
    const showAuxFeed = !!state.auxFeed;
    const showSpray = !!state.containmentSpray;
    const showRelief = !!state.steamRelief;
    this._setVisible("siPath", showInjection);
    this._setVisible("auxFeedPath", showAuxFeed);
    this._setVisible("sprayHeader", showSpray);
    this._setVisible("steamRelief", showRelief);

    const sprayDots = this.svg.querySelectorAll("#sprayHeader .spray");
    sprayDots.forEach((el, i) => {
      const pulse = showSpray ? 0.35 + 0.5 * Math.abs(Math.sin(this.dashOffset * 0.02 + i)) : 0;
      el.style.opacity = pulse.toFixed(2);
    });

    const ccwPath = this._q("ccwPath");
    if (ccwPath && !ccwDriveFromState(state)) ccwPath.style.opacity = "0.2";
  }

  _modeText(state) {
    if (state.meltdown) return pick(state, "Core damage", "爐心受損");
    if (state.scrammed) return pick(state, "Trip response", "跳閘應對");
    const mode = Math.round(state.mode ?? 0);
    const en = ["Shutdown", "Startup", "Power", "Hot standby", "Cooldown"];
    const zh = ["停機", "啟動", "功率運行", "熱備用", "冷卻"];
    return state.zhPrimary ? (zh[mode] ?? zh[0]) : (en[mode] ?? en[0]);
  }

  _coreText(state, severity) {
    if (severity === "trip") return pick(state, "damage risk", "損傷風險");
    if (state.scrammed) return pick(state, "scrammed", "已跳堆");
    if (severity === "challenged") return pick(state, "challenged", "受挑戰");
    if (severity === "watch") return pick(state, "watch heat", "留意熱量");
    return pick(state, "stable", "穩定");
  }

  _heatText(state, severity) {
    if (severity === "trip") return pick(state, "insufficient", "不足");
    if (state.eccsInjecting || state.accumInject) return pick(state, "injection", "注入中");
    if (state.auxFeed) return pick(state, "aux feed", "輔助給水");
    if (severity === "challenged") return pick(state, "limited", "受限");
    if (severity === "watch") return pick(state, "watch sink", "留意冷源");
    return pick(state, "available", "可用");
  }

  _containmentText(state, severity) {
    if (severity === "trip") return pick(state, "high energy", "高能量");
    if (state.containmentSpray) return pick(state, "spray", "噴淋");
    if (state.containmentIsolation) return pick(state, "isolated", "已隔離");
    if (severity === "watch") return pick(state, "watch", "留意");
    return pick(state, "normal", "正常");
  }

  _electricalText(state, severity) {
    if (state.stationBlackout) return pick(state, "blackout", "失電");
    if (state.edgPower) return pick(state, "EDG power", "柴油機");
    if (severity === "unavailable") return pick(state, "unavailable", "不可用");
    return pick(state, "AC online", "交流可用");
  }

  _chemistryText(state, severity) {
    if (severity !== "normal") return pick(state, "monitor", "監測");
    return pick(state, "in band", "範圍內");
  }
}

function ccwDriveFromState(state) {
  return (state.ccwFlow ?? 0) > 0.01 || !!state.ccwAvailable;
}
