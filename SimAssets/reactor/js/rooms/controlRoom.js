// 控制室 · The integrating hub: NIS, period/Tavg/pressure gauges, annunciators, CSF/SPDS, RPS,
// and the full reactivity / rod / mode / alarm / scenario control set.
import { el, panel, readout, toggle, slider, button } from "../ui.js";
import { Gauge } from "../widgets/gauge.js";
import { Bargraph } from "../widgets/bargraph.js";
import { Annunciator } from "../widgets/annunciator.js";

const CSF = [
  ["S Subcrit", "S 次臨界"], ["C Cooling", "C 堆芯冷卻"], ["H Heat sink", "H 熱阱"],
  ["P Integrity", "P 完整性"], ["Z Containment", "Z 安全殼"], ["I Inventory", "I 存量"],
];

export function ControlRoom(ctx) {
  const { send, t, pick } = ctx;
  const root = el("div");
  root.style.gridTemplateColumns = "1.1fr 1fr 1fr";

  // ---- column 1: NIS + gauges ----
  const nis = panel("nis", t);
  const srmBars = [
    new Bargraph({ label: "SRM", min: 0, max: 12, w: 50, h: 130, fmt: v => "1e" + v.toFixed(0) }),
    new Bargraph({ label: "IRM", min: 0, max: 12, w: 50, h: 130, fmt: v => "1e" + v.toFixed(0) }),
    new Bargraph({ label: "PRM", min: 0, max: 120, w: 50, h: 130 }),
  ];
  const barRow = el("div", "row");
  srmBars.forEach(b => barRow.appendChild(b.el));
  const periodG = new Gauge({ label: t("period"), unit: "s", min: -100, max: 100, size: 130,
    bands: [{ lo: 0, hi: 18, kind: "danger" }, { lo: 18, hi: 40, kind: "warn" }, { lo: 40, hi: 100, kind: "normal" }] });
  barRow.appendChild(periodG.el);
  nis.appendChild(barRow);
  const nisRead = el("div", "grid-auto");
  const rPow = readout(t("power")), rRate = readout(t("startupRate")), rRho = readout(t("reactivity"));
  [rPow, rRate, rRho].forEach(r => nisRead.appendChild(r));
  nis.appendChild(nisRead);

  const gaugePanel = panel("thx", t);
  const gTavg = new Gauge({ label: "Tavg", unit: "°C", min: 250, max: 340, size: 130,
    bands: [{ lo: 250, hi: 290, kind: "warn" }, { lo: 290, hi: 315, kind: "normal" }, { lo: 315, hi: 340, kind: "warn" }] });
  const gPress = new Gauge({ label: t("pressure"), unit: "MPa", min: 0, max: 20, size: 130,
    bands: [{ lo: 0, hi: 12.9, kind: "danger" }, { lo: 12.9, hi: 16, kind: "normal" }, { lo: 16, hi: 20, kind: "danger" }],
    setpoints: [{ v: 16.55 }] });
  const gPzr = new Gauge({ label: t("pzrLevel"), unit: "%", min: 0, max: 100, size: 130,
    bands: [{ lo: 0, hi: 17, kind: "danger" }, { lo: 17, hi: 70, kind: "normal" }, { lo: 70, hi: 92, kind: "warn" }, { lo: 92, hi: 100, kind: "danger" }] });
  const gElec = new Gauge({ label: t("electric"), unit: "MWe", min: 0, max: 1200, size: 130 });
  const gg = el("div", "row");
  [gTavg, gPress, gPzr, gElec].forEach(g => gg.appendChild(g.el));
  gaugePanel.appendChild(gg);

  // ---- column 2: annunciators + CSF + RPS ----
  const annunP = panel("annun", t);
  const annunHost = el("div");
  annunP.appendChild(annunHost);
  const annun = new Annunciator(annunHost);
  annun.relabel();

  const csfP = panel("csf", t);
  const csfGrid = el("div", "csf-grid");
  const csfCells = CSF.map(([en, zh]) => {
    const c = el("div", "csf s0");
    c._en = en; c._zh = zh;
    c.innerHTML = pick(en, zh);
    csfGrid.appendChild(c);
    return c;
  });
  csfP.appendChild(csfGrid);

  const rpsP = panel("rps", t);
  const perms = el("div", "perm-pills");
  const permEls = {};
  ["p6", "p7", "p8", "p9", "p10"].forEach(p => {
    const e = el("span", "perm", "P-" + p.slice(1));
    perms.appendChild(e); permEls[p] = e;
  });
  rpsP.appendChild(perms);
  const rpsList = el("div");
  rpsP.appendChild(rpsList);
  let rpsRows = [];

  // ---- column 3: controls ----
  const rodP = panel("rodCtl", t);
  // mode selector
  const modeRow = el("div", "btn-grp");
  const modeBtns = [
    button(t("shutdown"), null, () => send.control("setMode", { index: 0 })),
    button(t("startup"), null, () => send.control("setMode", { index: 1 })),
    button(t("run"), null, () => send.control("setMode", { index: 2 })),
  ];
  modeBtns.forEach(b => modeRow.appendChild(b));
  rodP._modeBtns = modeBtns;
  const modeLab = el("div", "ctl-label"); modeLab._key = "mode"; modeLab.textContent = t("mode");
  rodP.appendChild(modeLab); rodP.appendChild(modeRow);

  // rod bank sliders
  const bankSliders = [];
  for (let i = 0; i < 4; i++) {
    const lab = el("div", "ctl-label");
    const span = el("span"); span.textContent = t("rodBank") + " " + "ABCD"[i];
    const val = el("span", null, "100%");
    lab.appendChild(span); lab.appendChild(val);
    const s = slider(0, 100, 0.5, v => send.control("setRod", { index: i, value: v }));
    s.value = 100;
    bankSliders.push({ s, val, span });
    rodP.appendChild(lab); rodP.appendChild(s);
  }
  // all-banks quick controls
  const allRow = el("div", "btn-grp");
  allRow.appendChild(button(t("rodsOut"), null, () => stepAll(-5)));
  allRow.appendChild(button(t("hold"), null, () => {}));
  allRow.appendChild(button(t("rodsIn"), null, () => stepAll(+5)));
  rodP.appendChild(allRow);
  let lastAvg = 100;
  function stepAll(d) { send.control("setAllRods", { value: Math.max(0, Math.min(100, lastAvg + d)) }); }

  const autoTog = toggle(t("autoRods"), on => send.control("autoRods", { flag: on }));
  rodP.appendChild(autoTog);
  const spLab = el("div", "ctl-label"); const spVal = el("span", null, "100%");
  const spName = el("span", null, t("setpoint")); spLab.appendChild(spName); spLab.appendChild(spVal);
  const spS = slider(0, 120, 1, v => { send.control("autoSetpoint", { value: v / 100 }); spVal.textContent = v + "%"; });
  spS.value = 100;
  rodP.appendChild(spLab); rodP.appendChild(spS);

  // boron
  const boronLab = el("div", "ctl-label"); const boronVal = el("span", null, "1200 ppm");
  const boronName = el("span", null, t("boronTarget")); boronLab.appendChild(boronName); boronLab.appendChild(boronVal);
  const boronS = slider(0, 2500, 10, v => { send.control("setBoronTarget", { value: v }); boronVal.textContent = v + " ppm"; });
  boronS.value = 1200;
  rodP.appendChild(boronLab); rodP.appendChild(boronS);

  // alarm + trip + scenario
  const alarmP = panel("alarmCtl", t);
  const trips = el("div", "btn-grp");
  trips.appendChild(button(t("resetTrip"), null, () => send.control("resetTrip")));
  trips.appendChild(button(t("resetAll"), null, () => send.control("reset")));
  alarmP.appendChild(trips);
  const alarmBtns = el("div", "btn-grp");
  alarmBtns.appendChild(button(t("ack"), null, () => annun.ack()));
  let lampOn = false;
  alarmBtns.appendChild(button(t("lamptest"), null, () => { lampOn = !lampOn; annun.lampTest(lampOn); }));
  alarmP.appendChild(alarmBtns);
  const scenLab = el("div", "ctl-label"); scenLab._key = "scenario"; scenLab.textContent = t("scenario");
  const scenSel = el("select");
  [["scNormal", 0], ["scLoca", 1], ["scSbo", 2], ["scLofw", 3], ["scAtws", 4], ["scXenon", 5]].forEach(([k, idx]) => {
    const o = el("option", null, t(k)); o.value = idx; o._key = k; scenSel.appendChild(o);
  });
  scenSel.onchange = () => send.control("scenario", { index: parseInt(scenSel.value) });
  alarmP.appendChild(scenLab); alarmP.appendChild(scenSel);

  root.appendChild(nis);
  root.appendChild(annunP);
  root.appendChild(rodP);
  root.appendChild(gaugePanel);
  root.appendChild(csfP);
  root.appendChild(alarmP);
  root.appendChild(rpsP);

  // ---- state & draw ----
  let st = null;
  function onState(s) {
    st = s;
    // NIS bars: log of cps
    const logc = Math.log10(Math.max(1, s.sourceCps));
    srmBars[0].set(Math.min(8, logc));
    srmBars[1].set(Math.min(11, logc));
    srmBars[2].set(s.power * 100);
    rPow.setVal((s.power * 100).toFixed(1), "%", s.power > 1.09 ? "danger" : s.power > 1 ? "warn" : "good");
    rRate.setVal(s.startupRateDpm.toFixed(2), "DPM");
    rRho.setVal(s.reactivityPcm.toFixed(0), "pcm");
    // gauges
    gTavg.set(s.tavg); gPress.set(s.primaryPressureMPa); gPzr.set(s.pzrLevel); gElec.set(s.electricMW);
    periodG.set(Math.max(-100, Math.min(100, s.periodS > 1e7 ? 100 : s.periodS)));
    // mode buttons
    modeBtns.forEach((b, i) => b.classList.toggle("sel", i === s.mode));
    // rod sliders (only if user not dragging — simple: always reflect)
    let avg = 0;
    for (let i = 0; i < 4; i++) {
      const v = s.rodBank[i];
      avg += v;
      if (document.activeElement !== bankSliders[i].s) bankSliders[i].s.value = v;
      bankSliders[i].val.textContent = v.toFixed(0) + "%";
    }
    lastAvg = avg / 4;
    autoTog.setState(s.autoRods);
    if (document.activeElement !== boronS) { boronS.value = s.targetBoronPpm; boronVal.textContent = s.targetBoronPpm.toFixed(0) + " ppm"; }
    scenSel.value = s.scenario;
    // annunciators
    annun.update(new Set(s.alarms || []));
    // CSF
    const sev = csfSeverity(s);
    csfCells.forEach((c, i) => { c.className = "csf s" + sev[i]; });
    // RPS
    renderRps(s.rps);
    ["p6", "p7", "p8", "p9", "p10"].forEach(p => permEls[p].classList.toggle("on", !!s.rps.perms[p]));
  }

  function renderRps(rps) {
    if (rpsRows.length !== rps.funcs.length) {
      rpsList.innerHTML = ""; rpsRows = [];
      rps.funcs.forEach(f => {
        const row = el("div", "rps-row");
        const nm = el("span", "nm");
        const ch = el("div", "rps-ch");
        const leds = (f.channelTrips || []).map(() => { const l = el("span", "rps-led"); ch.appendChild(l); return l; });
        row.appendChild(nm); row.appendChild(ch);
        rpsList.appendChild(row);
        rpsRows.push({ row, nm, leds });
      });
    }
    rps.funcs.forEach((f, i) => {
      const r = rpsRows[i];
      r.nm.textContent = pick(f.nameEn, f.nameZh);
      r.row.className = "rps-row" + (f.blocked ? " blocked" : "") + (f.tripped ? " fn" : "");
      (f.channelTrips || []).forEach((tr, j) => { if (r.leds[j]) r.leds[j].className = "rps-led" + (tr ? " trip" : ""); });
    });
  }

  function draw(dt) {
    srmBars.forEach(b => b.draw());
    periodG.draw(dt);
    gTavg.draw(dt); gPress.draw(dt); gPzr.draw(dt); gElec.draw(dt);
  }

  function onLang() {
    annun.relabel();
    rodP._modeBtns[0].textContent = t("shutdown");
    rodP._modeBtns[1].textContent = t("startup");
    rodP._modeBtns[2].textContent = t("run");
    csfCells.forEach(c => c.innerHTML = pick(c._en, c._zh));
    [...scenSel.options].forEach(o => o.textContent = t(o._key));
    if (st) onState(st);
  }

  return { el: root, onState, draw, onLang };
}

function csfSeverity(s) {
  const VPL = 17.2;
  return [
    s.scrammed && s.power > 0.02 ? 3 : (s.power > 1.05 ? 2 : 0),
    s.subcoolingC < 0 ? 3 : s.subcoolingC < 15 ? 2 : 0,
    s.sgLevel < 17 ? 3 : s.sgLevel < 30 ? 2 : 0,
    s.primaryPressureMPa > VPL ? 3 : s.primaryPressureMPa > VPL - 1 ? 2 : 0,
    s.meltdown ? 3 : s.damage > 1 ? 2 : 0,
    s.pzrLevel < 17 ? 3 : s.pzrLevel < 30 ? 2 : 0,
  ];
}
