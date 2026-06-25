// 主程式 · App shell: builds tabs, mounts rooms, wires header + language + RAF active-room dispatch.
import { store, send } from "./bridge.js";
import { t, pick, isZh, applyLang } from "./i18n.js";
import { setActiveDraw } from "./render.js";

import { ControlRoom } from "./rooms/controlRoom.js";
import { Containment } from "./rooms/reactorContainment.js";
import { TurbineHall } from "./rooms/turbineHall.js";
import { FuelHandling } from "./rooms/fuelHandling.js";
import { CvcsAux } from "./rooms/cvcsAux.js";
import { Electrical } from "./rooms/electrical.js";
import { Cooling } from "./rooms/cooling.js";
import { RadMonitoring } from "./rooms/radMonitoring.js";

const MODE_NAMES = [
  ["SHUTDOWN", "停機"], ["STARTUP", "啟動"], ["RUN", "運轉"],
  ["TRIPPED", "已跳機"], ["MELTDOWN", "熔毀"],
];

const ROOMS = [
  { id: "control",  key: "roomControl", make: ControlRoom },
  { id: "contain",  key: "roomContain", make: Containment },
  { id: "turbine",  key: "roomTurbine", make: TurbineHall },
  { id: "fuel",     key: "roomFuel",    make: FuelHandling },
  { id: "cvcs",     key: "roomCvcs",    make: CvcsAux },
  { id: "elec",     key: "roomElec",    make: Electrical },
  { id: "cooling",  key: "roomCooling", make: Cooling },
  { id: "rad",      key: "roomRad",     make: RadMonitoring },
];

const tabstrip = document.getElementById("tabstrip");
const roomsHost = document.getElementById("rooms");
const rooms = [];
let active = 0;

const ctx = { store, send, t, pick };

for (let i = 0; i < ROOMS.length; i++) {
  const def = ROOMS[i];
  const inst = def.make(ctx);
  inst._def = def;
  inst.el.classList.add("room");
  roomsHost.appendChild(inst.el);
  rooms.push(inst);

  const tab = document.createElement("button");
  tab.className = "tab";
  tab.innerHTML = `<span>${t(def.key)}</span>`;
  tab._key = def.key;
  tab.onclick = () => selectRoom(i);
  tabstrip.appendChild(tab);
  def._tab = tab;
}

function selectRoom(i) {
  active = i;
  rooms.forEach((r, idx) => r.el.classList.toggle("active", idx === i));
  ROOMS.forEach((d, idx) => d._tab.classList.toggle("active", idx === i));
  try { localStorage.setItem("reactor.activeRoom", ROOMS[i].id); } catch (e) {}
}

// restore last room
try {
  const saved = localStorage.getItem("reactor.activeRoom");
  const idx = ROOMS.findIndex(d => d.id === saved);
  selectRoom(idx >= 0 ? idx : 0);
} catch (e) { selectRoom(0); }

// ---- header wiring ----
const statusDot = document.getElementById("statusDot");
const statusText = document.getElementById("statusText");
const modePill = document.getElementById("modePill");
document.getElementById("scramBtn").onclick = () => send.control("scram");
document.getElementById("fullBtn").onclick = () => send.fullscreen();
document.getElementById("langBtn").onclick = () => {
  send.setLanguage(isZh() ? "English" : "Cantonese");
};

const clockReadout = document.getElementById("clockReadout");
const powerReadout = document.getElementById("powerReadout");
const mweReadout = document.getElementById("mweReadout");
const fuelReadout = document.getElementById("fuelReadout");

// ---- state ingest ----
store.on("state", s => {
  // header
  statusText.textContent = pick(s.statusEn, s.statusZh);
  const mode = MODE_NAMES[s.mode] || ["—", "—"];
  modePill.textContent = pick(mode[0], mode[1]);
  let dotCol = "#35d07f";
  if (s.meltdown) dotCol = "#ff1744";
  else if (s.scrammed) dotCol = "#ffb300";
  else if (s.damage > 1) dotCol = "#ff4d4d";
  statusDot.style.color = dotCol; statusDot.style.background = dotCol;
  // footer
  clockReadout.textContent = "t = " + s.clock.toFixed(1) + " s";
  powerReadout.textContent = "P = " + (s.power * 100).toFixed(1) + " %";
  mweReadout.textContent = s.electricMW.toFixed(0) + " MWe";
  fuelReadout.textContent = s.fuelCanRun ? pick("FUEL OK", "燃料正常") : pick("NO FUEL", "無燃料");
  fuelReadout.className = "fuel-pill " + (s.fuelCanRun ? "run" : "norun");
  // tab alarm badges
  rooms.forEach((r, i) => {
    if (r.onState) r.onState(s);
  });
  updateTabAlarms(s);
});

function updateTabAlarms(s) {
  // mark control-room tab if any alarm active
  const ctrl = ROOMS[0]._tab;
  const any = (s.alarms && s.alarms.length > 0);
  ctrl.querySelector("span").innerHTML = t("roomControl") + (any ? ' <span class="tab-alarm">●</span>' : "");
  const fuelTab = ROOMS[3]._tab;
  fuelTab.querySelector("span").innerHTML = t("roomFuel") + (!s.fuelCanRun ? ' <span class="tab-alarm">●</span>' : "");
}

store.on("fuel", r => { rooms.forEach(rm => rm.onFuel && rm.onFuel(r)); });

// language refresh
store.on("lang", () => {
  applyLang(document);
  ROOMS.forEach(d => { d._tab.querySelector("span").textContent = t(d.key); });
  rooms.forEach(r => r.onLang && r.onLang());
});

// ---- RAF: only the active room draws ----
setActiveDraw((dt, time) => {
  const r = rooms[active];
  if (r && r.draw) r.draw(dt, time, store.latest);
});

// request initial fuel list
send.fuel("listFuel");
applyLang(document);
