// 化容 / 輔助 · CVCS: boron gauge + target, charging/letdown, VCT proxy, RHR, accumulator/AFW lamps.
import { el, panel, readout, toggle, slider } from "../ui.js";
import { Gauge } from "../widgets/gauge.js";

export function CvcsAux(ctx) {
  const { send, t } = ctx;
  const root = el("div");
  root.style.gridTemplateColumns = "1fr 1fr";

  const gP = panel("boron", t);
  const gBoron = new Gauge({ label: t("boron"), unit: "ppm", min: 0, max: 2500, size: 140 });
  const gXe = new Gauge({ label: t("xenon"), unit: "%", min: 0, max: 150, size: 140, fmt: v => v.toFixed(0) });
  const gr = el("div", "row"); gr.appendChild(gBoron.el); gr.appendChild(gXe.el);
  gP.appendChild(gr);
  const rd = el("div", "grid-auto");
  const rTgt = readout(t("boronTarget")), rVct = readout("VCT"), rAcc = readout("ACC/AFW");
  [rTgt, rVct, rAcc].forEach(r => rd.appendChild(r));
  gP.appendChild(rd);

  const ctlP = panel("rodCtl", t); ctlP._title.textContent = t("boron");
  const bLab = el("div", "ctl-label"); const bVal = el("span", null, "1200 ppm");
  bLab.appendChild(el("span", null, t("boronTarget"))); bLab.appendChild(bVal);
  const bS = slider(0, 2500, 10, v => { send.control("setBoronTarget", { value: v }); bVal.textContent = v + " ppm"; });
  bS.value = 1200;
  ctlP.appendChild(bLab); ctlP.appendChild(bS);
  const quick = el("div", "btn-grp");
  quick.appendChild(makeBtn(t("borate"), () => { bS.value = Math.min(2500, +bS.value + 200); send.control("setBoronTarget", { value: +bS.value }); }));
  quick.appendChild(makeBtn(t("dilute"), () => { bS.value = Math.max(0, +bS.value - 200); send.control("setBoronTarget", { value: +bS.value }); }));
  ctlP.appendChild(quick);
  const eccsT = toggle(t("eccs"), on => send.control("eccsArm", { flag: on }));
  ctlP.appendChild(eccsT);
  const lamps = el("div", "btn-grp");
  const accLamp = toggle("Accumulator", () => {}); accLamp.style.pointerEvents = "none";
  const afwLamp = toggle("Aux feedwater", () => {}); afwLamp.style.pointerEvents = "none";
  lamps.appendChild(accLamp); lamps.appendChild(afwLamp);
  ctlP.appendChild(lamps);

  function makeBtn(label, fn) { const b = el("button", null, label); b.onclick = fn; return b; }

  root.appendChild(gP); root.appendChild(ctlP);

  let st = null;
  function onState(s) {
    st = s;
    gBoron.set(s.boronPpm); gXe.set(s.xenon * 100);
    rTgt.setVal(s.targetBoronPpm.toFixed(0), "ppm");
    rVct.setVal(s.pzrLevel.toFixed(0), "%");
    rAcc.setVal((s.accumInject ? "INJ" : "—") + "/" + (s.auxFeed ? "ON" : "—"), "");
    if (document.activeElement !== bS) { bS.value = s.targetBoronPpm; bVal.textContent = s.targetBoronPpm.toFixed(0) + " ppm"; }
    eccsT.setState(s.eccsArmed || s.eccsInjecting, s.eccsInjecting);
    accLamp.setState(s.accumInject, true); afwLamp.setState(s.auxFeed, true);
  }
  function draw(dt) { gBoron.draw(dt); gXe.draw(dt); }
  function onLang() { ctlP._title.textContent = t("boron"); if (st) onState(st); }
  return { el: root, onState, draw, onLang };
}
