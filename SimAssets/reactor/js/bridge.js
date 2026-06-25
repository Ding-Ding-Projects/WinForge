// 橋接 · WebView2 message bridge + state store with snapshot interpolation.
// C#->JS: {type:'state', ...} at 10 Hz, and {type:'fuelResult', ...} on demand.
// JS->C#: {type:'control'|'fuel'|'ui', ...}.

import { setLang } from "./i18n.js";

const listeners = { state: [], fuel: [], lang: [], water: [] };

// Two most-recent snapshots for interpolation, plus arrival timestamps.
let prev = null, curr = null;
let prevT = 0, currT = 0;
const TICK = 0.1; // 10 Hz nominal

export const store = {
  latest: null,           // most-recent raw snapshot
  fuel: { fresh: [], loaded: [], spent: [], canRun: false, lastResult: null },
  waste: { files: [], totalMb: 0, totalGb: 0, count: 0, driveFreeGb: -1, safetyFloorGb: 10,
           capGb: 50, capUsedPct: 0, capReached: false, runbackZone: false,
           storageFull: false, generating: false, progressPct: 0, genTargetMb: 0, genId: "" },
  water: { tankLevelPct: 0, tankLevelL: 0, tankCapacityL: 200000, conductivity: 0, ph: 7,
           o2ppb: 0, silicappb: 0, chloridesppb: 0, intakePumpOn: false, intakeRate: 0.7,
           roOn: false, degasifierOn: false, makeupValveOpen: true, roFouling: 0, resinSaturation: 0,
           regenerating: false, intakeLpm: 0, productLpm: 0, makeupDrawLpm: 0,
           lowTankAlarm: false, offSpecAlarm: false, inSpec: true },

  ingest(data) {
    if (!data || typeof data !== "object") return;
    if (data.type === "state") {
      if (typeof data.zhPrimary === "boolean") {
        setLang(data.zhPrimary);
        listeners.lang.forEach(fn => fn(data.zhPrimary));
      }
      prev = curr; prevT = currT;
      curr = data; currT = performance.now() / 1000;
      this.latest = data;
      listeners.state.forEach(fn => fn(data));
    } else if (data.type === "fuelResult") {
      if (data.op === "listFuel" && data.items) {
        this.fuel.fresh = data.items.fresh || [];
        this.fuel.loaded = data.items.loaded || [];
        this.fuel.spent = data.items.spent || [];
        this.fuel.canRun = !!data.canRun;
      }
      if (data.op === "wasteStatus" && data.waste) {
        this.waste = Object.assign(this.waste, data.waste);
      }
      this.fuel.lastResult = data;
      listeners.fuel.forEach(fn => fn(data));
    } else if (data.type === "water") {
      this.water = Object.assign(this.water, data);
      listeners.water.forEach(fn => fn(this.water));
    }
  },

  // Interpolated numeric field between the last two snapshots (smooth 60 fps from 10 Hz data).
  lerp(field) {
    if (!curr) return 0;
    if (!prev) return curr[field] ?? 0;
    const a = prev[field], b = curr[field];
    if (typeof a !== "number" || typeof b !== "number") return b ?? 0;
    const now = performance.now() / 1000;
    let f = (now - currT) / TICK;          // extrapolate slightly past the latest snapshot
    f = Math.max(0, Math.min(1.2, f));
    return a + (b - a) * Math.min(1, f) ; // clamp interpolation factor to [0,1]
  },

  on(kind, fn) { (listeners[kind] || (listeners[kind] = [])).push(fn); },
};

function post(obj) {
  try { window.chrome?.webview?.postMessage(obj); } catch (e) { /* dev shim */ }
}

export const send = {
  control(action, { index = 0, value = 0, flag = false } = {}) {
    post({ type: "control", action, index, value, flag });
  },
  fuel(op, extra = {}) { post({ type: "fuel", op, ...extra }); },
  setLanguage(lang) { post({ type: "ui", action: "setLanguage", valueStr: lang }); },
  fullscreen() { post({ type: "ui", action: "fullscreen" }); },
};

// Wire the incoming message pipe.
if (window.chrome?.webview) {
  window.chrome.webview.addEventListener("message", e => store.ingest(e.data));
} else {
  // Dev shim: drive a fake idle reactor so the page renders standalone in a browser preview.
  console.warn("[reactor] no WebView2 host — running dev shim");
  let t = 0;
  setInterval(() => {
    t += 0.1;
    store.ingest({
      type: "state", clock: t, zhPrimary: false, mode: 0,
      statusEn: "Cold shutdown (dev shim)", statusZh: "冷停機（開發模擬）",
      power: 1e-6, thermalMW: 0, electricMW: 0, periodS: 1e9, reactivityPcm: -9000,
      rodPcm: -8000, boronPcm: 0, dopplerPcm: 0, modPcm: 0, xenonPcm: 0,
      fuelTemp: 35, tcold: 35, thot: 35, tavg: 35, primaryPressureMPa: 2.5,
      pzrLevel: 55, steamPressureMPa: 0.5, sgLevel: 60, iodine: 0, xenon: 0,
      rodBank: [100, 100, 100, 100], boronPpm: 1200, targetBoronPpm: 1200,
      pzrHeater: false, pzrSpray: false, rcpFlowDemand: 0, rcpRunning: [false, false, false, false],
      feedwaterFlow: 0, turbineLoad: 0, genBreaker: false, reliefValve: false,
      eccsArmed: false, eccsInjecting: false, autoRods: false, autoSetpoint: 1,
      turbineRpm: 0, syncRpm: 1800, flowFraction: 0, decayHeat: 0, subcoolingC: 100,
      sourceCps: 100, oneOverM: 1, startupRateDpm: 0, burnup: 0,
      scrammed: false, damage: 0, meltdown: false, scenario: 0,
      accumInject: false, auxFeed: false, lastTripEn: "", lastTripZh: "",
      alarms: [], rps: { trip: false, latched: false, funcs: [], perms: { p6: true, p7: false, p8: false, p9: false, p10: false } },
      fuelLoaded: false, fuelCanRun: false, fuelGateEn: "", fuelGateZh: "",
      spentFuelFull: false, externalPowerCap: -1, operationBlocked: false,
      makeupAvail: 1, makeupInSpec: true, makeupDemandLpm: 0, lowMakeupAlarm: false,
      chemistryAlarm: false, radiationLevel: 0, counterfeitAlarm: false,
    });
    store.ingest({
      type: "water", tankLevelPct: 70, tankLevelL: 140000, tankCapacityL: 200000,
      conductivity: 0.08, ph: 7, o2ppb: 5, silicappb: 3, chloridesppb: 2,
      intakePumpOn: false, intakeRate: 0.7, roOn: false, degasifierOn: false, makeupValveOpen: true,
      roFouling: 0, resinSaturation: 0, regenerating: false,
      intakeLpm: 0, productLpm: 0, makeupDrawLpm: 0,
      lowTankAlarm: false, offSpecAlarm: false, inSpec: true,
    });
  }, 100);
}
