// 啟動程序清單 · Live approach-to-criticality checklist, fed by the C# simulator.
import { el, panel, readout, button } from "../ui.js";

const MODE_NAMES = [
  ["Shutdown", "停機"], ["Startup", "啟動"], ["Run", "運轉"],
  ["Tripped", "已跳機"], ["Meltdown", "熔毀"],
];

export function StartupChecklist(ctx) {
  const { store, send, t, pick } = ctx;
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

  function goToControl(step) {
    const room = step.room || "control";
    try { location.hash = room; } catch (e) {}
  }

  function renderChecklist() {
    const steps = checklist.steps || [];
    rowsHost.innerHTML = "";
    controlsHost.innerHTML = "";

    if (steps.length === 0) {
      progress.textContent = t("startupWaiting");
      rowsHost.appendChild(el("div", "muted", t("startupWaiting")));
      return;
    }

    const done = Number.isFinite(checklist.done) ? checklist.done : steps.filter(s => !!s.ok).length;
    const easyNote = latestState?.easyStartup ? pick(" · Easy Mode control highlights active", " · 簡易模式控制高亮已啟用") : "";
    progress.textContent = `${t("startupProgress")}: ${done}/${steps.length}${easyNote}`;

    steps.forEach(step => {
      const skipped = !!step.skipped;
      const rowCls = "startup-step" + (step.ok ? " done" : step.active ? " active" : step.blocked ? " blocked" : "") + (skipped ? " skipped" : "");
      const row = el("div", rowCls);
      const mark = el("div", "startup-check", skipped ? "↷" : step.ok ? "✓" : step.active ? "→" : "○");
      const body = el("div", "startup-step-body");
      const title = el("div", "startup-step-title", `${step.index}. ${pick(step.en, step.zh)}`);
      const ctl = el("div", "startup-step-control", pick(`Use: ${step.controlEn}`, `使用：${step.controlZh}`));
      const detailText = pick(step.detailEn || "", step.detailZh || "");
      const detail = detailText ? el("div", "startup-step-detail", detailText) : null;
      const goBtn = button(pick("Control", "控制"), "startup-go", () => goToControl(step));
      const actions = el("div", "startup-actions");
      if (step.index === 4) {
        const psia = Number.isFinite(step.primaryPressurePsia)
          ? step.primaryPressurePsia
          : (latestState?.primaryPressureMPa ?? 0) * 145.038;
        actions.appendChild(el("div", "startup-pressure-pill", `${psia.toFixed(0)} / 2235 psia`));
      }
      if (step.skippable) {
        const skipBtn = button(
          skipped ? pick("Skipped", "已跳過") : pick("Skip step 4", "跳過第 4 步"),
          "startup-go skip",
          () => send.control("skipStartupStep4", { flag: true })
        );
        skipBtn.disabled = !step.canSkip || skipped;
        skipBtn.title = pick("Easy Mode only. This advances the checklist, not the plant trips.", "只限簡易模式。只推進清單，唔會繞過跳脫。");
        actions.appendChild(skipBtn);
      }
      actions.appendChild(goBtn);
      body.appendChild(title);
      body.appendChild(ctl);
      if (detail) body.appendChild(detail);
      if (skipped) body.appendChild(el("div", "startup-step-skipnote", pick("Skipped in Easy Mode; pressure and trips remain live.", "已於簡易模式跳過；壓力同跳脫仍然即時生效。")));
      row.appendChild(mark);
      row.appendChild(body);
      row.appendChild(actions);
      rowsHost.appendChild(row);

      const controlRow = el("div", "startup-control-row");
      if (latestState?.easyStartup && step.active) controlRow.classList.add("active");
      controlRow.appendChild(el("span", "startup-control-num", String(step.index)));
      controlRow.appendChild(el("span", null, pick(step.controlEn, step.controlZh)));
      controlRow.appendChild(button(pick("Open", "開啟"), "startup-go ghost", () => goToControl(step)));
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
