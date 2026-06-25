// 燃料處理 · The FUEL FACTORY room. Fabricate / validate / load / discharge signed fuel assemblies.
import { el, panel, button } from "../ui.js";

export function FuelHandling(ctx) {
  const { send, t, pick } = ctx;
  const root = el("div");
  root.style.gridTemplateColumns = "1fr";
  root.style.gridAutoRows = "min-content";

  // banner
  const banner = el("div", "fuel-banner norun", t("noRun"));

  // fabricate / validate panel
  const top = panel("fabricate", t);
  const fabRow = el("div", "row");
  const enLab = el("div", "ctl-label");
  enLab.appendChild(el("span", null, t("enrichment")));
  const enVal = el("span", null, "4.2 %"); enLab.appendChild(enVal);
  const enS = el("input"); enS.type = "range"; enS.min = 3; enS.max = 5; enS.step = 0.1; enS.value = 4.2;
  enS.style.width = "180px";
  enS.oninput = () => enVal.textContent = parseFloat(enS.value).toFixed(1) + " %";
  const massBox = el("input"); massBox.type = "number"; massBox.value = 523; massBox.min = 1; massBox.max = 1000;
  const massLab = el("span", "muted", t("mass"));
  const fabBtn = button(t("fabricate"), null, () => {
    send.fuel("fabricate", { enrichment: parseFloat(enS.value), mass: parseFloat(massBox.value) || 523 });
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

  // three columns
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

  root.appendChild(banner);
  root.appendChild(top);
  root.appendChild(lists);

  function rowFor(a, kind) {
    const r = el("div", "fuel-row");
    const left = el("div", "fid", a.id);
    const meta = el("div", "meta",
      `${a.enrichmentPct.toFixed(1)}% · ${a.massKgHM.toFixed(0)}kg · ${t("burnup")} ${a.burnupMwdPerTonne.toFixed(0)} MWd/t · ${a.status}`);
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
    r.appendChild(left); r.appendChild(act); r.appendChild(meta); r.appendChild(sig);
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

  function onFuel(r) {
    if (r.op === "listFuel") render(ctx.store.fuel);
    else if (r.op === "validate" && r.validation) {
      result.className = "fuel-result " + (r.validation.valid ? "ok" : "bad");
      result.textContent = `[${r.validation.reason}] ` + pick(r.validation.en, r.validation.zh);
    } else if (r.msgEn) {
      result.className = "fuel-result " + (r.ok ? "ok" : "bad");
      result.textContent = pick(r.msgEn, r.msgZh);
    }
    if (r.op !== "validate") render(ctx.store.fuel);
  }
  function onLang() {
    banner.textContent = ctx.store.fuel.canRun ? t("canRun") : t("noRun");
    [colFresh, colLoaded, colSpent].forEach(c => c.h.textContent = t(c.h._key));
    fabBtn.textContent = t("fabricate"); discBtn.textContent = t("discharge");
    explain.textContent = t("fuelExplain");
    render(ctx.store.fuel);
  }
  function draw() {}
  // initial paint from any cached fuel state
  setTimeout(() => render(ctx.store.fuel), 100);
  return { el: root, onFuel, onLang, draw };
}
