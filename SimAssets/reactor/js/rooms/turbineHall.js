// 汽輪機房 · Rotating rotor + synchroscope + steam particles + steam/SG gauges + turbine controls.
import { el, panel, readout, toggle, slider } from "../ui.js";
import { Gauge } from "../widgets/gauge.js";
import { Turbine } from "../widgets/turbine.js";
import { Synchroscope } from "../widgets/synchroscope.js";
import { Steam } from "../widgets/steam.js";
import { StripChart } from "../widgets/stripchart.js";

export function TurbineHall(ctx) {
  const { send, t } = ctx;
  const root = el("div");
  root.style.gridTemplateColumns = "1fr 1fr";

  const turbP = panel("turbView", t);
  const turbine = new Turbine({ w: 200, h: 230 });
  const steam = new Steam({ w: 200, h: 120, x: 100, y: 110 });
  const layered = el("div", "layered");
  layered.style.width = "200px"; layered.style.height = "230px";
  layered.style.margin = "0 auto"; layered.style.position = "relative";
  turbine.el.style.position = "absolute"; turbine.el.style.inset = "0";
  steam.el.style.position = "absolute"; steam.el.style.left = "0"; steam.el.style.top = "0";
  layered.appendChild(turbine.el); layered.appendChild(steam.el);
  turbP.appendChild(layered);
  const tRead = el("div", "grid-auto");
  const rRpm = readout(t("rpm")), rMwe = readout(t("electric"));
  tRead.appendChild(rRpm); tRead.appendChild(rMwe);
  turbP.appendChild(tRead);

  const syncP = panel("sync", t);
  const sync = new Synchroscope({ w: 160, h: 160 });
  const syncWrap = el("div"); syncWrap.style.textAlign = "center"; syncWrap.appendChild(sync.el);
  syncP.appendChild(syncWrap);

  const gP = panel("thx", t);
  gP._title.textContent = t("steamPress");
  const gSteam = new Gauge({ label: t("steamPress"), unit: "MPa", min: 0, max: 9, size: 130,
    bands: [{ lo: 0, hi: 6.2, kind: "normal" }, { lo: 6.2, hi: 7.6, kind: "warn" }, { lo: 7.6, hi: 9, kind: "danger" }] });
  const gSg = new Gauge({ label: t("sgLevel"), unit: "%", min: 0, max: 100, size: 130,
    bands: [{ lo: 0, hi: 17, kind: "danger" }, { lo: 17, hi: 75, kind: "normal" }, { lo: 75, hi: 100, kind: "warn" }] });
  const gr = el("div", "row"); gr.appendChild(gSteam.el); gr.appendChild(gSg.el);
  gP.appendChild(gr);
  const chart = new StripChart({ label: t("electric") + " MWe", min: 0, max: 1200, w: 330, h: 100, color: "#35d07f" });
  gP.appendChild(chart.el);

  const ctlP = panel("rodCtl", t); ctlP._title.textContent = t("turbView");
  const loadLab = el("div", "ctl-label"); const loadVal = el("span", null, "0%");
  const loadName = el("span", null, t("turbineLoad")); loadLab.appendChild(loadName); loadLab.appendChild(loadVal);
  const loadS = slider(0, 100, 1, v => { send.control("turbineLoad", { value: v / 100 }); loadVal.textContent = v + "%"; });
  ctlP.appendChild(loadLab); ctlP.appendChild(loadS);
  const feedLab = el("div", "ctl-label"); const feedVal = el("span", null, "0%");
  const feedName = el("span", null, t("feedwater")); feedLab.appendChild(feedName); feedLab.appendChild(feedVal);
  const feedS = slider(0, 100, 1, v => { send.control("feedwater", { value: v / 100 }); feedVal.textContent = v + "%"; });
  ctlP.appendChild(feedLab); ctlP.appendChild(feedS);
  const brkT = toggle(t("genBreaker"), on => send.control("genBreaker", { flag: on }));
  ctlP.appendChild(brkT);

  root.appendChild(turbP); root.appendChild(syncP);
  root.appendChild(gP); root.appendChild(ctlP);

  let st = null;
  function onState(s) {
    st = s;
    turbine.set(s.turbineRpm); sync.set(s.turbineRpm, s.genBreaker);
    steam.set(Math.min(1, (s.steamPressureMPa / 8.5) * (s.turbineLoad + 0.1)));
    gSteam.set(s.steamPressureMPa); gSg.set(s.sgLevel);
    rRpm.setVal(s.turbineRpm.toFixed(0), "rpm");
    rMwe.setVal(s.electricMW.toFixed(0), "MWe", s.genBreaker ? "good" : undefined);
    if (document.activeElement !== loadS) { loadS.value = s.turbineLoad * 100; loadVal.textContent = (s.turbineLoad * 100).toFixed(0) + "%"; }
    if (document.activeElement !== feedS) { feedS.value = s.feedwaterFlow * 100; feedVal.textContent = (s.feedwaterFlow * 100).toFixed(0) + "%"; }
    brkT.setState(s.genBreaker);
  }
  function draw(dt) {
    turbine.draw(dt); sync.draw(dt); steam.draw(dt);
    gSteam.draw(dt); gSg.draw(dt);
    if (st) chart.feed(dt, st.electricMW);
    chart.draw();
  }
  function onLang() { ctlP._title.textContent = t("turbView"); gP._title.textContent = t("steamPress"); if (st) onState(st); }
  return { el: root, onState, draw, onLang };
}
