// 冷卻 / 熱阱 · Steam pressure, SG level, RCP flow bargraphs, condenser strip chart, subcooling.
import { el, panel, slider } from "../ui.js";
import { Bargraph } from "../widgets/bargraph.js";
import { Gauge } from "../widgets/gauge.js";
import { StripChart } from "../widgets/stripchart.js";

export function Cooling(ctx) {
  const { send, t } = ctx;
  const root = el("div");
  root.style.gridTemplateColumns = "1fr 1fr";

  const barsP = panel("cooling", t); barsP._title.textContent = t("flow");
  const bFlow = new Bargraph({ label: "FLOW", min: 0, max: 100, w: 56, h: 160, warn: 0.85, danger: 0.95, invert: true });
  const bSg = new Bargraph({ label: "SG", min: 0, max: 100, w: 56, h: 160, invert: true, warn: 0.5, danger: 0.7 });
  const bFeed = new Bargraph({ label: "FEED", min: 0, max: 100, w: 56, h: 160, invert: true });
  const gSub = new Gauge({ label: t("subcool"), unit: "°C", min: -20, max: 120, size: 140,
    bands: [{ lo: -20, hi: 0, kind: "danger" }, { lo: 0, hi: 15, kind: "warn" }, { lo: 15, hi: 120, kind: "normal" }] });
  const gr = el("div", "row");
  [bFlow, bSg, bFeed].forEach(b => gr.appendChild(b.el)); gr.appendChild(gSub.el);
  barsP.appendChild(gr);
  const chart = new StripChart({ label: t("steamPress") + " MPa", min: 0, max: 9, w: 330, h: 100, color: "#19e0ff" });
  barsP.appendChild(chart.el);

  const ctlP = panel("rodCtl", t); ctlP._title.textContent = t("cooling");
  const feedLab = el("div", "ctl-label"); const feedVal = el("span", null, "0%");
  feedLab.appendChild(el("span", null, t("feedwater"))); feedLab.appendChild(feedVal);
  const feedS = slider(0, 100, 1, v => { send.control("feedwater", { value: v / 100 }); feedVal.textContent = v + "%"; });
  ctlP.appendChild(feedLab); ctlP.appendChild(feedS);
  const flowLab = el("div", "ctl-label"); const flowVal = el("span", null, "0%");
  flowLab.appendChild(el("span", null, t("rcpFlow"))); flowLab.appendChild(flowVal);
  const flowS = slider(0, 100, 1, v => { send.control("rcpFlow", { value: v / 100 }); flowVal.textContent = v + "%"; });
  ctlP.appendChild(flowLab); ctlP.appendChild(flowS);

  root.appendChild(barsP); root.appendChild(ctlP);

  let st = null;
  function onState(s) {
    st = s;
    bFlow.set(s.flowFraction * 100); bSg.set(s.sgLevel); bFeed.set(s.feedwaterFlow * 100);
    gSub.set(s.subcoolingC);
    if (document.activeElement !== feedS) { feedS.value = s.feedwaterFlow * 100; feedVal.textContent = (s.feedwaterFlow * 100).toFixed(0) + "%"; }
    if (document.activeElement !== flowS) { flowS.value = s.rcpFlowDemand * 100; flowVal.textContent = (s.rcpFlowDemand * 100).toFixed(0) + "%"; }
  }
  function draw(dt) {
    bFlow.draw(); bSg.draw(); bFeed.draw(); gSub.draw(dt);
    if (st) chart.feed(dt, st.steamPressureMPa);
    chart.draw();
  }
  function onLang() { ctlP._title.textContent = t("cooling"); barsP._title.textContent = t("flow"); if (st) onState(st); }
  return { el: root, onState, draw, onLang };
}
