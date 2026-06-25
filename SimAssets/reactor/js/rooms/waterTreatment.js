// 水處理廠 · Water Treatment Plant — intake→clarifier→filtration→RO→mixed-bed IX→degasifier→tank.
// Listens for the dedicated "water" bridge snapshot (NOT "state"); also consumes the reactor
// "state" snapshot for makeup demand. Robust to a missing snapshot (renders dashes / zeros).
import { el, panel, button, slider, toggle, readout } from "../ui.js";
import { Gauge } from "../widgets/gauge.js";
import { Bargraph } from "../widgets/bargraph.js";
import { fitCanvas, clamp } from "../render.js";

export function WaterTreatment(ctx) {
  const { send, t, pick } = ctx;
  const root = el("div");
  root.style.gridTemplateColumns = "1fr 1fr";

  // Latest snapshots (guarded everywhere; may be null until first message).
  let w = null;   // water snapshot
  let st = null;  // reactor state snapshot

  // ---------------------------------------------------------------- A) train mimic
  const trainP = panel("wtTrain", t);
  const trainCanvas = el("canvas", "mimic");
  trainCanvas.style.width = "100%";
  trainCanvas.style.height = "150px";
  trainP.appendChild(trainCanvas);
  const trainCtx = fitCanvas(trainCanvas, 480, 150);
  let trainW = 480, trainH = 150;
  let flowPhase = 0;

  // stages: key = label i18n key, eq() => "on"|"off"|"warn"|"danger"
  const STAGES = [
    { key: "wtIntake",    short: () => pick("Intake", "進水"),    eq: () => (w && w.intakePumpOn) ? "on" : "off" },
    { key: "wtClarifier", short: () => pick("Clarifier", "澄清"), eq: () => (w && (w.intakeLpm ?? 0) > 0) ? "on" : "off" },
    { key: "wtFilter",    short: () => pick("Filter", "過濾"),    eq: () => (w && (w.intakeLpm ?? 0) > 0) ? "on" : "off" },
    { key: "wtRo",        short: () => pick("RO 2-pass", "反滲透"), eq: () => {
        if (!w) return "off";
        if ((w.roFouling ?? 0) > 0.6) return "danger";
        return w.roOn ? "on" : "off";
      } },
    { key: "wtIx",        short: () => pick("Mixed-bed", "混床"),  eq: () => {
        if (!w) return "off";
        if (w.regenerating || (w.resinSaturation ?? 0) > 0.8) return "warn";
        return (w.productLpm ?? 0) > 0 ? "on" : "off";
      } },
    { key: "wtDegas",     short: () => pick("Degas", "除氣"),      eq: () => (w && w.degasifierOn) ? "on" : "off" },
    { key: "wtTankStage", short: () => pick("Tank", "水箱"),       eq: () => {
        if (!w) return "off";
        if (w.lowTankAlarm) return "danger";
        return (w.tankLevelPct ?? 0) > 0 ? "on" : "off";
      } },
  ];

  function stageColor(kind) {
    return kind === "danger" ? "#ff3b30"
      : kind === "warn" ? "#ffb300"
      : kind === "on" ? "#35d07f"
      : "#46505e";
  }

  function drawTrain(dt) {
    const c = trainCtx;
    // keep canvas sized to its CSS box (cheap, robust to layout changes)
    const cssW = trainCanvas.clientWidth || 480;
    if (Math.abs(cssW - trainW) > 1) {
      trainW = cssW;
      fitCanvas(trainCanvas, trainW, trainH);
    }
    c.clearRect(0, 0, trainW, trainH);

    const n = STAGES.length;
    const margin = 14;
    const usable = trainW - margin * 2;
    const cy = trainH / 2 - 6;
    const boxW = Math.min(54, usable / n - 6);
    const boxH = 34;
    const step = usable / n;
    const centers = [];
    for (let i = 0; i < n; i++) centers.push(margin + step * (i + 0.5));

    // pipes between stages
    const intakeFlow = w ? (w.intakeLpm ?? 0) : 0;
    const productFlow = w ? (w.productLpm ?? 0) : 0;
    c.lineWidth = 4;
    for (let i = 0; i < n - 1; i++) {
      const x0 = centers[i] + boxW / 2;
      const x1 = centers[i + 1] - boxW / 2;
      c.strokeStyle = "#2b313c";
      c.beginPath(); c.moveTo(x0, cy); c.lineTo(x1, cy); c.stroke();
    }

    // flowing dots (intake feeds first half of the train, product the rest)
    flowPhase += dt * 0.6;
    if (flowPhase > 1) flowPhase -= 1;
    const half = Math.floor(n / 2);
    for (let i = 0; i < n - 1; i++) {
      const flow = i < half ? intakeFlow : productFlow;
      if (flow <= 0) continue;
      const x0 = centers[i] + boxW / 2;
      const x1 = centers[i + 1] - boxW / 2;
      const segLen = x1 - x0;
      if (segLen <= 0) continue;
      const dots = 3;
      for (let d = 0; d < dots; d++) {
        let f = (flowPhase + d / dots) % 1;
        const x = x0 + segLen * f;
        c.beginPath(); c.arc(x, cy, 2.4, 0, Math.PI * 2);
        c.fillStyle = "#19e0ff"; c.fill();
      }
    }

    // stage boxes + labels
    c.textAlign = "center";
    for (let i = 0; i < n; i++) {
      const s = STAGES[i];
      let kind = "off";
      try { kind = s.eq(); } catch (e) { kind = "off"; }
      const col = stageColor(kind);
      const x = centers[i] - boxW / 2;
      const y = cy - boxH / 2;
      c.fillStyle = "#0e1218"; c.fillRect(x, y, boxW, boxH);
      c.lineWidth = 2; c.strokeStyle = col; c.strokeRect(x, y, boxW, boxH);
      // status pip
      c.beginPath(); c.arc(centers[i], y + 8, 3, 0, Math.PI * 2);
      c.fillStyle = col; c.fill();
      // short label inside
      c.fillStyle = "#cdd6e0"; c.font = "9px 'Segoe UI'"; c.textBaseline = "alphabetic";
      c.fillText(s.short(), centers[i], y + boxH - 6);
      // stage name below
      c.fillStyle = "#7d8a9a"; c.font = "9px 'Segoe UI'";
      c.fillText(t(s.key), centers[i], cy + boxH / 2 + 14);
    }
  }

  // ---------------------------------------------------------------- B) chemistry
  const chemP = panel("wtChemistry", t);
  const gCond = new Gauge({
    label: t("wtConductivity"), unit: "µS/cm", min: 0, max: 1, size: 132,
    fmt: v => v.toFixed(3),
    bands: [
      { lo: 0, hi: 0.1, kind: "normal" },
      { lo: 0.1, hi: 0.5, kind: "warn" },
      { lo: 0.5, hi: 1, kind: "danger" },
    ],
  });
  const gO2 = new Gauge({
    label: t("wtO2"), unit: "ppb", min: 0, max: 100, size: 132,
    fmt: v => v.toFixed(0),
    bands: [
      { lo: 0, hi: 10, kind: "normal" },
      { lo: 10, hi: 50, kind: "warn" },
      { lo: 50, hi: 100, kind: "danger" },
    ],
  });
  const gPh = new Gauge({
    label: t("wtPh"), unit: "pH", min: 0, max: 14, size: 132,
    fmt: v => v.toFixed(1),
    bands: [
      { lo: 0, hi: 6.5, kind: "warn" },
      { lo: 6.5, hi: 7.5, kind: "normal" },
      { lo: 7.5, hi: 14, kind: "warn" },
    ],
  });
  const bSilica = new Bargraph({ label: "SiO₂", min: 0, max: 20, w: 56, h: 150, warn: 0.5, danger: 0.8, fmt: v => v.toFixed(1) });
  const bCl = new Bargraph({ label: "Cl⁻", min: 0, max: 20, w: 56, h: 150, warn: 0.5, danger: 0.8, fmt: v => v.toFixed(1) });
  const chemRow = el("div", "row");
  chemRow.appendChild(gCond.el); chemRow.appendChild(gO2.el); chemRow.appendChild(gPh.el);
  chemRow.appendChild(bSilica.el); chemRow.appendChild(bCl.el);
  chemP.appendChild(chemRow);
  const silicaLab = el("div", "muted", "");
  const clLab = el("div", "muted", "");
  chemP.appendChild(silicaLab); chemP.appendChild(clLab);

  // ---------------------------------------------------------------- C) storage tank
  const tankP = panel("wtTank", t);
  const tankAlarm = el("div", "fuel-banner norun", "");
  tankAlarm.style.display = "none";
  tankP.appendChild(tankAlarm);

  const tankWrap = el("div", "row");
  const bTank = new Bargraph({ label: "TANK", min: 0, max: 100, w: 64, h: 170, invert: true, warn: 0.5, danger: 0.7, fmt: v => v.toFixed(0) + "%" });
  tankWrap.appendChild(bTank.el);
  const tankReads = el("div");
  const rLevel = readout(t("wtTankLevel"));
  const rIntake = readout(t("wtThroughput"));
  const rProduct = readout(t("wtProduct"));
  const rMakeup = readout(t("wtMakeupDraw"));
  const rDemand = readout(t("wtMakeupDemand"));
  [rLevel, rIntake, rProduct, rMakeup, rDemand].forEach(r => tankReads.appendChild(r));
  tankWrap.appendChild(tankReads);
  tankP.appendChild(tankWrap);
  const specLine = el("div", "muted", "");
  tankP.appendChild(specLine);

  // ---------------------------------------------------------------- D) controls
  const ctlP = panel("wtControls", t);

  const tPump = toggle(t("wtIntakePump"), on => send.fuel("waterIntakePump", { flag: on }));
  const tRo = toggle(t("wtRoOn"), on => send.fuel("waterRo", { flag: on }));
  const tDegas = toggle(t("wtDegasOn"), on => send.fuel("waterDegasifier", { flag: on }));
  const tMakeup = toggle(t("wtMakeupValve"), on => send.fuel("waterMakeupValve", { flag: on }));
  const togRow = el("div", "row");
  [tPump, tRo, tDegas, tMakeup].forEach(b => togRow.appendChild(b));
  ctlP.appendChild(togRow);

  const rateLab = el("div", "ctl-label");
  const rateVal = el("span", null, "0%");
  rateLab.appendChild(el("span", null, t("wtIntakeRate"))); rateLab.appendChild(rateVal);
  const rateS = slider(0, 100, 1, v => { send.fuel("waterIntakeRate", { mass: v / 100 }); rateVal.textContent = v + "%"; });
  rateLab._span = rateLab.firstChild;
  ctlP.appendChild(rateLab); ctlP.appendChild(rateS);

  const actRow = el("div", "row");
  const regenBtn = button(t("wtRegen"), null, () => send.fuel("waterRegenerate"));
  const flushBtn = button(t("wtFlushRo"), "ghost", () => send.fuel("waterFlushRo"));
  actRow.appendChild(regenBtn); actRow.appendChild(flushBtn);
  ctlP.appendChild(actRow);

  const rResin = readout(t("wtResin"));
  const rFoul = readout(t("wtFouling"));
  const readRow = el("div", "row");
  readRow.appendChild(rResin); readRow.appendChild(rFoul);
  ctlP.appendChild(readRow);

  // ---------------------------------------------------------------- assemble
  root.appendChild(trainP);
  root.appendChild(chemP);
  root.appendChild(tankP);
  root.appendChild(ctlP);

  // ---------------------------------------------------------------- water render
  function renderWater(snap) {
    if (snap) w = snap;
    const ww = w || {};

    // chemistry targets (gauges/bargraphs animate in draw)
    gCond.set(ww.conductivity ?? 0);
    gO2.set(ww.o2ppb ?? 0);
    gPh.set(ww.ph ?? 7);
    bSilica.set(ww.silicappb ?? 0);
    bCl.set(ww.chloridesppb ?? 0);
    silicaLab.textContent = `${t("wtSilica")}: ${(ww.silicappb ?? 0).toFixed(2)} ppb`;
    clLab.textContent = `${t("wtChlorides")}: ${(ww.chloridesppb ?? 0).toFixed(2)} ppb`;

    // tank
    const pct = ww.tankLevelPct ?? 0;
    bTank.set(pct);
    const lvl = ww.tankLevelL ?? 0, cap = ww.tankCapacityL ?? 0;
    rLevel.setVal(`${lvl.toFixed(0)} / ${cap.toFixed(0)}`, "L", ww.lowTankAlarm ? "danger" : "");
    rIntake.setVal((ww.intakeLpm ?? 0).toFixed(1), "L/min");
    rProduct.setVal((ww.productLpm ?? 0).toFixed(1), "L/min");
    rMakeup.setVal((ww.makeupDrawLpm ?? 0).toFixed(1), "L/min");

    // low-tank banner
    if (ww.lowTankAlarm) {
      tankAlarm.style.display = "";
      tankAlarm.textContent = t("wtLowTank");
    } else {
      tankAlarm.style.display = "none";
    }

    // spec status line
    const offSpec = !!ww.offSpecAlarm;
    const inSpec = ww.inSpec === undefined ? !offSpec : !!ww.inSpec;
    specLine.textContent = offSpec ? t("wtOffSpec") : (inSpec ? t("wtInSpec") : "—");

    // controls reflect snapshot
    tPump.setState(!!ww.intakePumpOn);
    tRo.setState(!!ww.roOn, (ww.roFouling ?? 0) > 0.6);
    tDegas.setState(!!ww.degasifierOn);
    tMakeup.setState(!!ww.makeupValveOpen);

    const rate = Math.round((ww.intakeRate ?? 0) * 100);
    if (document.activeElement !== rateS) {
      rateS.value = rate;
      rateVal.textContent = rate + "%";
    }

    const resin = (ww.resinSaturation ?? 0) * 100;
    const foul = (ww.roFouling ?? 0) * 100;
    rResin.setVal(resin.toFixed(0), "%", (ww.regenerating ? "warn" : resin > 80 ? "danger" : resin > 60 ? "warn" : ""));
    rFoul.setVal(foul.toFixed(0), "%", foul > 60 ? "danger" : foul > 40 ? "warn" : "");
  }

  // ---------------------------------------------------------------- reactor state
  function onState(s) {
    if (s) st = s;
    const ss = st || {};
    // makeup demand from the reactor side (optional)
    const demand = ss.makeupDemandLpm;
    rDemand.setVal(demand === undefined ? "—" : demand.toFixed(1), "L/min",
      ss.lowMakeupAlarm ? "danger" : "");
    // surface reactor chemistry alarm onto the spec line if present and worse
    if (ss.chemistryAlarm && !(w && w.offSpecAlarm)) {
      specLine.textContent = t("wtOffSpec");
    }
  }

  // bridge fan-out for water snapshots
  if (ctx.store && typeof ctx.store.on === "function") {
    ctx.store.on("water", snap => renderWater(snap));
    if (ctx.store.water) renderWater(ctx.store.water);
  }

  // ---------------------------------------------------------------- frame draw
  function draw(dt) {
    try {
      const d = (typeof dt === "number" && dt > 0) ? Math.min(dt, 0.1) : 0.016;
      drawTrain(d);
      gCond.draw(d); gO2.draw(d); gPh.draw(d);
      bSilica.draw(); bCl.draw(); bTank.draw();
    } catch (e) { /* never throw in draw */ }
  }

  // ---------------------------------------------------------------- language
  function onLang() {
    trainP._title.textContent = t("wtTrain");
    chemP._title.textContent = t("wtChemistry");
    tankP._title.textContent = t("wtTank");
    ctlP._title.textContent = t("wtControls");
    gCond.setLabel(t("wtConductivity")); gO2.setLabel(t("wtO2")); gPh.setLabel(t("wtPh"));
    rLevel.setLabel(t("wtTankLevel"));
    rIntake.setLabel(t("wtThroughput"));
    rProduct.setLabel(t("wtProduct"));
    rMakeup.setLabel(t("wtMakeupDraw"));
    rDemand.setLabel(t("wtMakeupDemand"));
    rResin.setLabel(t("wtResin"));
    rFoul.setLabel(t("wtFouling"));
    tPump._lab.textContent = t("wtIntakePump");
    tRo._lab.textContent = t("wtRoOn");
    tDegas._lab.textContent = t("wtDegasOn");
    tMakeup._lab.textContent = t("wtMakeupValve");
    if (rateLab._span) rateLab._span.textContent = t("wtIntakeRate");
    regenBtn.textContent = t("wtRegen");
    flushBtn.textContent = t("wtFlushRo");
    renderWater();
    onState();
  }

  // initial paint + ask the C# side for a fresh water status
  setTimeout(() => { renderWater(); onState(); send.fuel("waterStatus"); }, 100);
  // keep the snapshot live even if the host pushes infrequently
  const poll = setInterval(() => send.fuel("waterStatus"), 2000);

  return { el: root, onState, draw, onLang, dispose: () => clearInterval(poll) };
}
