// 反應堆 / 安全殼 · Live P&ID mimic + Cherenkov core glow + thermal gauges + primary-side controls.
import { el, panel, readout, toggle, slider, button } from "../ui.js";
import { Gauge } from "../widgets/gauge.js";
import { CoreGlow } from "../widgets/core.js";
import { Mimic } from "../widgets/mimic.js";

export function Containment(ctx) {
  const { send, t } = ctx;
  const root = el("div");
  root.style.gridTemplateColumns = "1.4fr 1fr";

  const mimicP = panel("mimic", t);
  const mimicHost = el("div");
  mimicP.appendChild(mimicHost);
  const mimic = new Mimic(mimicHost);

  const coreP = panel("coreView", t);
  const glow = new CoreGlow({ w: 240, h: 240 });
  const coreWrap = el("div"); coreWrap.style.textAlign = "center";
  coreWrap.appendChild(glow.el);
  coreP.appendChild(coreWrap);
  const coreRead = el("div", "grid-auto");
  const rFuel = readout(t("fuelTemp")), rDmg = readout(t("damage")), rDecay = readout(t("decay"));
  [rFuel, rDmg, rDecay].forEach(r => coreRead.appendChild(r));
  coreP.appendChild(coreRead);

  const gP = panel("thx", t);
  const gFuel = new Gauge({ label: t("fuelTemp"), unit: "°C", min: 0, max: 3000, size: 120,
    bands: [{ lo: 0, hi: 1000, kind: "normal" }, { lo: 1000, hi: 1204, kind: "warn" }, { lo: 1204, hi: 3000, kind: "danger" }],
    setpoints: [{ v: 1204 }] });
  const gThot = new Gauge({ label: "Thot", unit: "°C", min: 250, max: 360, size: 120,
    bands: [{ lo: 250, hi: 320, kind: "normal" }, { lo: 320, hi: 345, kind: "warn" }, { lo: 345, hi: 360, kind: "danger" }] });
  const gSub = new Gauge({ label: t("subcool"), unit: "°C", min: -20, max: 120, size: 120,
    bands: [{ lo: -20, hi: 0, kind: "danger" }, { lo: 0, hi: 15, kind: "warn" }, { lo: 15, hi: 120, kind: "normal" }],
    setpoints: [{ v: 0 }] });
  const gDecay = new Gauge({ label: t("decay"), unit: "%", min: 0, max: 10, size: 120, fmt: v => v.toFixed(1) });
  const gr = el("div", "row");
  [gFuel, gThot, gSub, gDecay].forEach(g => gr.appendChild(g.el));
  gP.appendChild(gr);

  // controls
  const ctlP = panel("rodCtl", t);
  ctlP._title.textContent = t("thx");
  const rcpRow = el("div", "btn-grp");
  const rcpTogs = [];
  for (let i = 0; i < 4; i++) {
    const tog = toggle(t("rcp") + " " + (i + 1), on => send.control(on ? "rcpStart" : "rcpStop", { index: i }));
    rcpTogs.push(tog); rcpRow.appendChild(tog);
  }
  ctlP.appendChild(rcpRow);
  const flowLab = el("div", "ctl-label"); const flowVal = el("span", null, "0%");
  const flowName = el("span", null, t("rcpFlow")); flowLab.appendChild(flowName); flowLab.appendChild(flowVal);
  const flowS = slider(0, 100, 1, v => { send.control("rcpFlow", { value: v / 100 }); flowVal.textContent = v + "%"; });
  ctlP.appendChild(flowLab); ctlP.appendChild(flowS);

  const togs = el("div", "btn-grp");
  const heaterT = toggle(t("pzrHeater"), on => send.control("pzrHeater", { flag: on }));
  const sprayT = toggle(t("pzrSpray"), on => send.control("pzrSpray", { flag: on }));
  const porvT = toggle(t("porv"), on => send.control("reliefValve", { flag: on }));
  const eccsT = toggle(t("eccs"), on => send.control("eccsArm", { flag: on }));
  [heaterT, sprayT, porvT, eccsT].forEach(x => togs.appendChild(x));
  ctlP.appendChild(togs);

  root.appendChild(mimicP);
  root.appendChild(coreP);
  root.appendChild(gP);
  root.appendChild(ctlP);

  let st = null;
  function onState(s) {
    st = s;
    glow.set(s.power, s.meltdown);
    gFuel.set(s.fuelTemp); gThot.set(s.thot); gSub.set(s.subcoolingC); gDecay.set(s.decayHeat * 100);
    rFuel.setVal(s.fuelTemp.toFixed(0), "°C", s.fuelTemp > 1204 ? "danger" : s.fuelTemp > 1000 ? "warn" : "good");
    rDmg.setVal(s.damage.toFixed(1), "%", s.damage > 1 ? "danger" : "good");
    rDecay.setVal((s.decayHeat * 100).toFixed(1), "%");
    rcpTogs.forEach((tg, i) => tg.setState(s.rcpRunning[i]));
    if (document.activeElement !== flowS) { flowS.value = s.rcpFlowDemand * 100; flowVal.textContent = (s.rcpFlowDemand * 100).toFixed(0) + "%"; }
    heaterT.setState(s.pzrHeater); sprayT.setState(s.pzrSpray);
    porvT.setState(s.reliefValve, true); eccsT.setState(s.eccsArmed || s.eccsInjecting, s.eccsInjecting);
  }
  function draw(dt, time, state) {
    glow.draw(dt, time);
    gFuel.draw(dt); gThot.draw(dt); gSub.draw(dt); gDecay.draw(dt);
    mimic.draw(dt, state || st);
  }
  function onLang() { ctlP._title.textContent = t("thx"); if (st) onState(st); }
  return { el: root, onState, draw, onLang };
}
