// 燃料處理 · The ULTRA-REALISTIC FUEL FACTORY + nuclear-waste management room.
// Fabricate / validate / load(=consume, file deleted) / discharge signed fuel assemblies, and
// manage the real 100 MB–2000 MB nuclear-waste junk files that burning fuel mandatorily produces.
import { el, panel, button } from "../ui.js";

export function FuelHandling(ctx) {
  const { send, t, pick } = ctx;
  const root = el("div");
  root.style.gridTemplateColumns = "1fr";
  root.style.gridAutoRows = "min-content";

  // banner
  const banner = el("div", "fuel-banner norun", t("noRun"));

  // ---- fabricate / validate panel -------------------------------------------------
  const top = panel("fabricate", t);
  const fabRow = el("div", "row");
  const enLab = el("div", "ctl-label");
  enLab.appendChild(el("span", null, t("enrichment")));
  const enVal = el("span", null, "4.2 %"); enLab.appendChild(enVal);
  const enS = el("input"); enS.type = "range"; enS.min = 3; enS.max = 4.95; enS.step = 0.05; enS.value = 4.2;
  enS.style.width = "180px";
  enS.oninput = () => enVal.textContent = parseFloat(enS.value).toFixed(2) + " %";
  const massBox = el("input"); massBox.type = "number"; massBox.value = 461; massBox.min = 400; massBox.max = 540;
  const massLab = el("span", "muted", t("mass"));
  const fabBtn = button(t("fabricate"), null, () => {
    send.fuel("fabricate", { enrichment: parseFloat(enS.value), mass: parseFloat(massBox.value) || 461 });
  });
  const enWrap = el("div"); enWrap.appendChild(enLab); enWrap.appendChild(enS);
  fabRow.appendChild(enWrap);
  const massWrap = el("div"); massWrap.appendChild(massLab); massWrap.appendChild(el("br")); massWrap.appendChild(massBox);
  fabRow.appendChild(massWrap);
  fabRow.appendChild(fabBtn);
  top.appendChild(fabRow);
  const explain = el("div", "muted", t("fuelExplain"));
  top.appendChild(explain);
  const result = el("div", "fuel-result", t("selectFile"));
  top.appendChild(result);
  const discBtn = button(t("discharge"), null, () => send.fuel("discharge"));
  top.appendChild(discBtn);

  // ---- three fuel columns ---------------------------------------------------------
  const lists = panel("roomFuel", t);
  const cols = el("div", "fuel-cols");
  const colFresh = makeCol("fuelFresh"); const colLoaded = makeCol("fuelLoaded"); const colSpent = makeCol("fuelSpent");
  cols.appendChild(colFresh.wrap); cols.appendChild(colLoaded.wrap); cols.appendChild(colSpent.wrap);
  lists.appendChild(cols);

  function makeCol(key) {
    const wrap = el("div");
    const h = el("h3"); h.textContent = t(key); h._key = key;
    const list = el("div", "fuel-list");
    wrap.appendChild(h); wrap.appendChild(list);
    return { wrap, h, list };
  }

  // ---- nuclear waste panel --------------------------------------------------------
  const wastePanel = panel("roomWaste", t);
  const wExplain = el("div", "muted", t("wasteExplain"));
  const wBanner = el("div", "fuel-banner run", "");
  const wStats = el("div", "muted", "");
  const wProg = el("div", "fuel-result", ""); wProg.style.display = "none";
  const wActs = el("div", "row");
  const floorBox = el("input"); floorBox.type = "number"; floorBox.value = 10; floorBox.min = 0; floorBox.style.width = "70px";
  const floorBtn = button(t("setFloor"), "ghost", () => {
    const gb = parseFloat(floorBox.value) || 10;
    send.fuel("setSafetyFloor", { mass: Math.round(gb * 1024) }); // mass field carries MB
  });
  const disposeAllBtn = button(t("disposeAll"), null, () => {
    if (confirm(t("confirmDisposeAll"))) send.fuel("dispose"); // no path = dispose all
  });
  wActs.appendChild(disposeAllBtn);
  wActs.appendChild(floorBox); wActs.appendChild(el("span", "muted", "GB")); wActs.appendChild(floorBtn);
  const wList = el("div", "fuel-list");
  wastePanel.appendChild(wExplain);
  wastePanel.appendChild(wBanner);
  wastePanel.appendChild(wStats);
  wastePanel.appendChild(wProg);
  wastePanel.appendChild(wActs);
  wastePanel.appendChild(wList);

  root.appendChild(banner);
  root.appendChild(top);
  root.appendChild(lists);
  root.appendChild(wastePanel);

  // ---- fuel rows ------------------------------------------------------------------
  function rowFor(a, kind) {
    const r = el("div", "fuel-row");
    const left = el("div", "fid", a.id);
    const meta = el("div", "meta",
      `${(a.enrichmentPct ?? 0).toFixed(2)}% · ${(a.massKgHM ?? 0).toFixed(0)} kgU · ${a.lattice || "17x17"} ${a.material || "UO2"} · ` +
      `${t("burnup")} ${(a.burnupMwdPerTonne ?? 0).toFixed(0)}/${(a.targetBurnupMwdPerTonne ?? 45000).toFixed(0)} MWd/t · ${a.status}`);
    const meta2 = el("div", "meta",
      `${t("manufacturer")}: ${a.manufacturer || "—"} · ${t("lot")} ${a.lot || "—"}`);
    const sig = el("div", "sig " + (a.signatureValid ? "ok" : "bad"), a.signatureValid ? "✔" : "✘");
    const act = el("div");
    if (kind === "fresh") {
      const b = button(t("loadCore"), null, () => send.fuel("load", { path: a.id }));
      b.disabled = !a.signatureValid; act.appendChild(b);
    } else if (kind === "loaded") {
      const b = button(t("unload"), null, () => send.fuel("unload", { path: a.id }));
      act.appendChild(b);
    }
    const valBtn = button(t("validate"), "ghost", () => send.fuel("validate", { path: a.id }));
    act.appendChild(valBtn);
    // fabrication chain (compact)
    if (a.fabChain && a.fabChain.length) {
      const chainBtn = button(t("fabChain"), "ghost", () => {
        result.className = "fuel-result ok";
        result.textContent = `${a.id} — ` + a.fabChain.join("  →  ");
      });
      act.appendChild(chainBtn);
    }
    r.appendChild(left); r.appendChild(act); r.appendChild(meta); r.appendChild(sig); r.appendChild(meta2);
    return r;
  }

  function render(fuel) {
    fill(colFresh.list, fuel.fresh, "fresh");
    fill(colLoaded.list, fuel.loaded, "loaded");
    fill(colSpent.list, fuel.spent, "spent");
    banner.className = "fuel-banner " + (fuel.canRun ? "run" : "norun");
    banner.textContent = fuel.canRun ? t("canRun") : t("noRun");
  }
  function fill(list, items, kind) {
    list.innerHTML = "";
    if (!items || !items.length) { list.appendChild(el("div", "muted", "—")); return; }
    items.forEach(a => list.appendChild(rowFor(a, kind)));
  }

  // ---- waste rendering ------------------------------------------------------------
  function renderWaste(w) {
    w = w || ctx.store.waste;
    wExplain.textContent = t("wasteExplain");
    wBanner.className = "fuel-banner " + (w.storageFull ? "norun" : "run");
    wBanner.textContent = w.storageFull ? t("wasteFull")
      : `${t("wasteTotal")}: ${(w.totalGb || 0).toFixed(2)} GB (${w.count || 0} ${t("wasteCount")})`;
    const free = (w.driveFreeGb >= 0) ? `${w.driveFreeGb} GB` : "?";
    wStats.textContent =
      `${t("wasteCount")}: ${w.count || 0} · ${t("wasteTotal")}: ${(w.totalMb || 0).toFixed(0)} MB · ` +
      `${t("driveFree")}: ${free} · ${t("safetyFloor")}: ${(w.safetyFloorGb || 10)} GB`;

    if (w.generating) {
      wProg.style.display = "";
      wProg.className = "fuel-result ok";
      wProg.textContent = `${t("wasteGen")} ${(w.progressPct || 0).toFixed(0)}% / ${w.genTargetMb || 0} MB`;
    } else {
      wProg.style.display = "none";
    }

    wList.innerHTML = "";
    if (!w.files || !w.files.length) { wList.appendChild(el("div", "muted", t("noWaste"))); return; }
    w.files.forEach(f => {
      const r = el("div", "fuel-row");
      r.appendChild(el("div", "fid", f.id));
      const act = el("div");
      act.appendChild(button(t("disposeOne"), "ghost", () => send.fuel("dispose", { path: f.id })));
      r.appendChild(act);
      r.appendChild(el("div", "meta", `${(f.mb || 0).toFixed(0)} MB · ${f.createdUtc || ""}`));
      wList.appendChild(r);
    });
  }

  function onFuel(r) {
    if (r.op === "listFuel") render(ctx.store.fuel);
    else if (r.op === "wasteStatus") { renderWaste(r.waste); return; }
    else if (r.op === "validate" && r.validation) {
      result.className = "fuel-result " + (r.validation.valid ? "ok" : "bad");
      result.textContent = `[${r.validation.reason}] ` + pick(r.validation.en, r.validation.zh);
    } else if (r.msgEn) {
      result.className = "fuel-result " + (r.ok ? "ok" : "bad");
      result.textContent = pick(r.msgEn, r.msgZh);
    }
    if (r.op !== "validate" && r.op !== "wasteStatus") render(ctx.store.fuel);
    if (r.op === "dispose" || r.op === "load" || r.op === "discharge") send.fuel("wasteStatus");
  }
  function onLang() {
    banner.textContent = ctx.store.fuel.canRun ? t("canRun") : t("noRun");
    [colFresh, colLoaded, colSpent].forEach(c => c.h.textContent = t(c.h._key));
    fabBtn.textContent = t("fabricate"); discBtn.textContent = t("discharge");
    explain.textContent = t("fuelExplain");
    disposeAllBtn.textContent = t("disposeAll"); floorBtn.textContent = t("setFloor");
    if (wastePanel._title) wastePanel._title.textContent = t("roomWaste");
    render(ctx.store.fuel);
    renderWaste();
  }
  function draw() {}

  // initial paint + ask for fuel & waste status
  setTimeout(() => { render(ctx.store.fuel); renderWaste(); send.fuel("wasteStatus"); }, 100);
  // refresh waste status periodically so progress + inventory stay live
  const poll = setInterval(() => send.fuel("wasteStatus"), 2000);
  return { el: root, onFuel, onLang, draw, dispose: () => clearInterval(poll) };
}
