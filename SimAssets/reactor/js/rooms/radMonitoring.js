// 輻射監測 · Synthetic area/process/effluent monitors from power/damage/meltdown/ECCS, with
// alert/alarm/trip colour states, dose-rate trend, containment-isolation lamp. Read-only.
import { el, panel, readout, toggle } from "../ui.js";
import { StripChart } from "../widgets/stripchart.js";

export function RadMonitoring(ctx) {
  const { t } = ctx;
  const root = el("div");
  root.style.gridTemplateColumns = "1fr 1fr";

  const monP = panel("radmon", t);
  const grid = el("div", "grid-auto");
  const rArea = readout(t("area")), rProc = readout(t("process")), rEff = readout(t("effluent")), rDose = readout(t("doseRate"));
  [rArea, rProc, rEff, rDose].forEach(r => grid.appendChild(r));
  monP.appendChild(grid);
  const isoT = toggle(t("containIso"), () => {}); isoT.style.pointerEvents = "none";
  monP.appendChild(isoT);

  const trendP = panel("doseRate", t);
  const chart = new StripChart({ label: "µSv/h (log)", min: 0, max: 8, w: 340, h: 130, color: "#ffb300", period: 0.3 });
  trendP.appendChild(chart.el);

  root.appendChild(monP); root.appendChild(trendP);

  function sev(v, alert, alarm) { return v >= alarm ? "danger" : v >= alert ? "warn" : "good"; }

  let st = null; let doseLog = 0;
  function onState(s) {
    st = s;
    // synthetic dose rates (µSv/h) — background + power leakage + big jumps on damage/meltdown/ECCS
    const bg = 0.1;
    const area = bg + s.power * 2 + s.damage * 50 + (s.meltdown ? 50000 : 0) + (s.eccsInjecting ? 30 : 0);
    const proc = bg + s.power * 0.5 + s.damage * 200 + (s.meltdown ? 200000 : 0);
    const eff = bg + s.damage * 5 + (s.meltdown ? 5000 : 0) + (s.reliefValve ? s.power * 3 : 0);
    rArea.setVal(fmt(area), "µSv/h", sev(area, 10, 1000));
    rProc.setVal(fmt(proc), "µSv/h", sev(proc, 50, 5000));
    rEff.setVal(fmt(eff), "µSv/h", sev(eff, 5, 500));
    rDose.setVal(fmt(area), "µSv/h", sev(area, 10, 1000));
    doseLog = Math.log10(Math.max(0.1, area));
    const iso = s.meltdown || s.damage > 1 || area > 1000;
    isoT.setState(iso, !s.meltdown);
    isoT._lab.textContent = t("containIso") + (iso ? " — ISOLATED" : "");
  }
  function fmt(v) { return v >= 1000 ? (v / 1000).toFixed(1) + "k" : v.toFixed(1); }
  function draw(dt) {
    if (st) chart.feed(dt, doseLog);
    chart.draw();
  }
  function onLang() { if (st) onState(st); }
  return { el: root, onState, draw, onLang };
}
