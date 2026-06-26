// 啟動程序清單 · Live approach-to-criticality checklist, fed by the C# simulator.
import { el, panel, readout } from "../ui.js";

const MODE_NAMES = [
  ["Shutdown", "停機"], ["Startup", "啟動"], ["Run", "運轉"],
  ["Tripped", "已跳機"], ["Meltdown", "熔毀"],
];

export function StartupChecklist(ctx) {
  const { store, t, pick } = ctx;
  const root = el("div");
  root.style.gridTemplateColumns = "1.35fr .8fr";

  const listP = panel("startupChecklist", t);
  const progress = el("div", "startup-progress");
  const rowsHost = el("div", "startup-list");
  listP.appendChild(progress);
  listP.appendChild(rowsHost);

  const controlsP = panel("startupControls", t);
  const meters = el("div", "grid-auto");
  const rMode = readout(t("mode"));
  const rFlow = readout(t("flow"));
  const rPress = readout(t("pressure"));
  const rOneM = readout("1/M");
  [rMode, rFlow, rPress, rOneM].forEach(r => meters.appendChild(r));
  controlsP.appendChild(meters);
  const controlsHost = el("div", "startup-control-list");
  controlsP.appendChild(controlsHost);

  root.appendChild(listP);
  root.appendChild(controlsP);

  let checklist = store.startupChecklist || { steps: [] };
  let latestState = store.latest;

  function renderChecklist() {
    const steps = checklist.steps || [];
    rowsHost.innerHTML = "";
    controlsHost.innerHTML = "";

    if (steps.length === 0) {
      progress.textContent = t("startupWaiting");
      rowsHost.appendChild(el("div", "muted", t("startupWaiting")));
      return;
    }

    const done = steps.filter(s => !!s.ok).length;
    progress.textContent = `${t("startupProgress")}: ${done}/${steps.length}`;

    steps.forEach(step => {
      const row = el("div", "startup-step" + (step.ok ? " done" : ""));
      const mark = el("div", "startup-check", step.ok ? "✓" : "○");
      const body = el("div", "startup-step-body");
      const title = el("div", "startup-step-title", `${step.index}. ${pick(step.en, step.zh)}`);
      const ctl = el("div", "startup-step-control", pick(`Use: ${step.controlEn}`, `使用：${step.controlZh}`));
      body.appendChild(title);
      body.appendChild(ctl);
      row.appendChild(mark);
      row.appendChild(body);
      rowsHost.appendChild(row);

      const controlRow = el("div", "startup-control-row");
      controlRow.appendChild(el("span", "startup-control-num", String(step.index)));
      controlRow.appendChild(el("span", null, pick(step.controlEn, step.controlZh)));
      controlsHost.appendChild(controlRow);
    });
  }

  function onStartupChecklist(data) {
    checklist = data || { steps: [] };
    renderChecklist();
  }

  function onState(s) {
    latestState = s;
    const mode = MODE_NAMES[s.mode] || ["—", "—"];
    rMode.setVal(pick(mode[0], mode[1]), "");
    rFlow.setVal((s.flowFraction * 100).toFixed(0), "%", s.flowFraction > 0.85 ? "good" : "warn");
    rPress.setVal(s.primaryPressureMPa.toFixed(2), "MPa", s.primaryPressureMPa > 14.5 ? "good" : "warn");
    rOneM.setVal(s.oneOverM.toFixed(2), "", s.oneOverM < 0.25 ? "good" : "");
  }

  function onLang() {
    listP._title.textContent = t("startupChecklist");
    controlsP._title.textContent = t("startupControls");
    rMode.setLabel(t("mode"));
    rFlow.setLabel(t("flow"));
    rPress.setLabel(t("pressure"));
    renderChecklist();
    if (latestState) onState(latestState);
  }

  renderChecklist();
  if (latestState) onState(latestState);
  return { el: root, onStartupChecklist, onState, onLang };
}
