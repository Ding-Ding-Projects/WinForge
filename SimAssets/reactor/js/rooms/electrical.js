// 電氣 / 配電 · One-line: main/safety bus, generator, breaker, EDG/battery lamps, synchroscope echo.
import { el, panel, readout, toggle } from "../ui.js";
import { Gauge } from "../widgets/gauge.js";
import { Synchroscope } from "../widgets/synchroscope.js";

export function Electrical(ctx) {
  const { send, t } = ctx;
  const root = el("div");
  root.style.gridTemplateColumns = "1fr 1fr";

  const oneP = panel("oneline", t);
  const canvas = document.createElement("canvas");
  canvas.width = 420; canvas.height = 220; canvas.style.width = "100%"; canvas.style.maxWidth = "440px";
  oneP.appendChild(canvas);
  const cctx = canvas.getContext("2d");

  const gP = panel("turbView", t); gP._title.textContent = t("electric");
  const gMwe = new Gauge({ label: t("electric"), unit: "MWe", min: 0, max: 1200, size: 140 });
  const gHz = new Gauge({ label: "Hz", unit: "", min: 55, max: 65, size: 140, fmt: v => v.toFixed(0),
    bands: [{ lo: 55, hi: 59.5, kind: "warn" }, { lo: 59.5, hi: 60.5, kind: "normal" }, { lo: 60.5, hi: 65, kind: "warn" }] });
  const gr = el("div", "row"); gr.appendChild(gMwe.el); gr.appendChild(gHz.el);
  gP.appendChild(gr);
  const sync = new Synchroscope({ w: 140, h: 140 });
  const sw = el("div"); sw.style.textAlign = "center"; sw.appendChild(sync.el);
  gP.appendChild(sw);

  const ctlP = panel("rodCtl", t); ctlP._title.textContent = t("genBreaker");
  const brkT = toggle(t("genBreaker"), on => send.control("genBreaker", { flag: on }));
  ctlP.appendChild(brkT);
  const rd = el("div", "grid-auto");
  const rEdg = readout("EDG"), rBat = readout("Battery"), rBus = readout("Safety bus");
  [rEdg, rBat, rBus].forEach(r => rd.appendChild(r));
  ctlP.appendChild(rd);

  root.appendChild(oneP); root.appendChild(gP); root.appendChild(ctlP);

  let st = null;
  function onState(s) {
    st = s;
    gMwe.set(s.electricMW);
    gHz.set(60 * (s.turbineRpm / 1800));
    sync.set(s.turbineRpm, s.genBreaker);
    brkT.setState(s.genBreaker);
    const sbo = s.scenario === 2;
    rEdg.setVal(sbo ? "RUN" : "STBY", "", sbo ? "warn" : "good");
    rBat.setVal(sbo ? "DISCH" : "FLOAT", "", sbo ? "warn" : "good");
    rBus.setVal(s.genBreaker || sbo ? "ENERGIZED" : "OFF", "");
  }
  function draw(dt) {
    gMwe.draw(dt); gHz.draw(dt); sync.draw(dt);
    drawOneLine(cctx, st);
  }
  function onLang() { ctlP._title.textContent = t("genBreaker"); gP._title.textContent = t("electric"); if (st) onState(st); }
  return { el: root, onState, draw, onLang };
}

function drawOneLine(c, s) {
  c.clearRect(0, 0, 420, 220);
  const live = s && (s.genBreaker || s.scenario === 2);
  const genLive = s && s.genBreaker && s.electricMW > 1;
  function box(x, y, w, h, label, on) {
    c.fillStyle = "#1a2030"; c.strokeStyle = on ? "#35d07f" : "#3a4656"; c.lineWidth = 2;
    c.fillRect(x, y, w, h); c.strokeRect(x, y, w, h);
    c.fillStyle = "#93a2b5"; c.font = "11px Consolas"; c.textAlign = "center";
    c.fillText(label, x + w / 2, y + h / 2 + 4);
  }
  function wire(x0, y0, x1, y1, on) {
    c.strokeStyle = on ? "#35d07f" : "#3a4656"; c.lineWidth = 3;
    c.beginPath(); c.moveTo(x0, y0); c.lineTo(x1, y1); c.stroke();
  }
  box(20, 90, 70, 40, "GEN", genLive);
  wire(90, 110, 160, 110, genLive);
  box(160, 90, 70, 40, "MAIN", genLive);
  wire(230, 110, 300, 110, genLive);
  box(300, 90, 90, 40, "GRID", genLive);
  // safety bus drop
  wire(195, 130, 195, 175, live);
  box(150, 175, 90, 35, "SAFETY", live);
}
