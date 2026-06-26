// 雙語字串表 · Bilingual string table (EN + 繁體中文/粵語). t(key) returns the primary-language
// string; the C# side pushes the primary language each tick via state.zhPrimary.

let zhPrimary = false;

const S = {
  appTitle:   ["Reactor Control Room", "反應堆控制室"],
  appSub:     ["OPEN100-class PWR · full-scope simulation", "OPEN100 級壓水堆 · 全範圍模擬"],
  offline:    ["Self-contained · no network", "自包含 · 無網絡"],

  // rooms
  roomControl:    ["Control Room", "控制室"],
  roomContain:    ["Reactor / Containment", "反應堆 / 安全殼"],
  roomTurbine:    ["Turbine Hall", "汽輪機房"],
  roomFuel:       ["Fuel Handling", "燃料處理"],
  roomCvcs:       ["CVCS / Aux", "化容 / 輔助"],
  roomElec:       ["Electrical", "電氣"],
  roomCooling:    ["Cooling", "冷卻"],
  roomRad:        ["Rad Monitoring", "輻射監測"],
  roomStartup:    ["Startup Checklist", "啟動程序清單"],

  // common
  power:      ["Power", "功率"],
  thermal:    ["Thermal", "熱功率"],
  electric:   ["Electric", "電功率"],
  period:     ["Period", "週期"],
  reactivity: ["Reactivity", "反應性"],
  fuelTemp:   ["Fuel temp", "燃料溫度"],
  tavg:       ["Tavg", "平均溫"],
  thot:       ["Thot", "熱腿溫"],
  tcold:      ["Tcold", "冷腿溫"],
  pressure:   ["Primary press", "一迴路壓力"],
  pzrLevel:   ["PZR level", "穩壓器水位"],
  steamPress: ["Steam press", "蒸汽壓力"],
  sgLevel:    ["SG level", "蒸發器水位"],
  subcool:    ["Subcooling", "過冷裕度"],
  decay:      ["Decay heat", "衰變熱"],
  flow:       ["RCS flow", "冷卻劑流量"],
  turbine:    ["Turbine", "汽輪機"],
  rpm:        ["Turbine RPM", "汽輪機轉速"],
  boron:      ["Boron", "硼濃度"],
  xenon:      ["Xenon", "氙毒"],
  startupRate:["Startup rate", "啟動率"],
  burnup:     ["Burnup", "燃耗"],
  damage:     ["Core damage", "堆芯損傷"],
  srm:        ["Source range", "起動範圍"],

  // controls
  mode:       ["Mode", "模式"],
  shutdown:   ["Shutdown", "停機"],
  startup:    ["Startup", "啟動"],
  run:        ["Run", "運轉"],
  rods:       ["Control rods", "控制棒"],
  rodBank:    ["Bank", "棒組"],
  allRods:    ["All banks", "全部棒組"],
  rodsOut:    ["WITHDRAW", "提棒"],
  rodsIn:     ["INSERT", "插棒"],
  hold:       ["HOLD", "保持"],
  autoRods:   ["Auto rods", "自動控棒"],
  setpoint:   ["Power setpoint", "功率設定"],
  borate:     ["Borate", "加硼"],
  dilute:     ["Dilute", "稀釋"],
  boronTarget:["Boron target", "硼目標"],
  resetTrip:  ["Reset trip", "復位跳脫"],
  resetAll:   ["Reset plant", "重置全廠"],
  ack:        ["ACK", "確認"],
  silence:    ["SILENCE", "消聲"],
  lamptest:   ["LAMP TEST", "燈測"],
  scenario:   ["Scenario", "事故情景"],
  pzrHeater:  ["PZR heater", "穩壓器加熱"],
  pzrSpray:   ["PZR spray", "穩壓器噴淋"],
  porv:       ["Relief valve (PORV)", "釋壓閥"],
  eccs:       ["ECCS arm", "應急堆芯冷卻"],
  rcp:        ["RCP", "主泵"],
  rcpFlow:    ["RCP flow demand", "主泵流量需求"],
  start:      ["START", "啟動"],
  stop:       ["STOP", "停止"],
  feedwater:  ["Feedwater", "給水"],
  turbineLoad:["Turbine load", "汽輪機負荷"],
  genBreaker: ["Generator breaker", "發電機開關"],
  closed:     ["CLOSED", "合閘"],
  open:       ["OPEN", "分閘"],

  // scenarios
  scNormal:   ["Normal", "正常"],
  scLoca:     ["LOCA", "失水事故"],
  scSbo:      ["Blackout", "全廠斷電"],
  scLofw:     ["Loss of feed", "喪失給水"],
  scAtws:     ["ATWS", "未停堆暫態"],
  scXenon:    ["Xenon restart", "氙毒重啟"],

  // panels
  nis:        ["Nuclear instrumentation", "核儀表"],
  annun:      ["Annunciators", "報警盤"],
  csf:        ["Critical safety functions", "關鍵安全功能"],
  rps:        ["Reactor protection system", "反應堆保護系統"],
  startupChecklist:["Startup checklist", "啟動程序清單"],
  startupProgress:["Checklist progress", "程序進度"],
  startupControls:["Controls to use", "要使用的控制"],
  startupWaiting:["Waiting for checklist data…", "等待程序清單資料…"],
  thx:        ["Primary loop", "一迴路"],
  rodCtl:     ["Rod & reactivity control", "控棒及反應性控制"],
  alarmCtl:   ["Alarm control", "報警控制"],
  mimic:      ["Plant mimic", "系統模擬圖"],
  coreView:   ["Core", "堆芯"],
  turbView:   ["Turbine & generator", "汽輪發電機"],
  sync:       ["Synchroscope", "同步指示器"],
  oneline:    ["One-line diagram", "單線圖"],
  radmon:     ["Radiation monitors", "輻射監測儀"],

  // fuel
  fuelFresh:  ["Fresh", "新燃料"],
  fuelLoaded: ["In core", "在堆"],
  fuelSpent:  ["Spent", "乏燃料"],
  fabricate:  ["Fabricate assembly", "製造組件"],
  enrichment: ["Enrichment U-235", "鈾-235 濃度"],
  mass:       ["Mass (kg HM)", "質量（公斤重金屬）"],
  validate:   ["Validate file", "驗證檔案"],
  loadCore:   ["Load into core", "裝入堆芯"],
  unload:     ["Unload", "卸料"],
  discharge:  ["Discharge all", "全部退役"],
  canRun:     ["REACTOR CAN RUN — fuel loaded", "反應堆可運轉 — 已裝燃料"],
  noRun:      ["NO FUEL LOADED — reactor cannot start", "未裝燃料 — 反應堆無法啟動"],
  signValid:  ["signature", "簽章"],
  selectFile: ["Select an assembly to validate", "選擇要驗證的組件"],
  fuelExplain:["HMAC-SHA256 signed · loading CONSUMES the fuel (file deleted) · forged / tampered / depleted fuel is rejected",
               "HMAC-SHA256 簽章 · 入料即消耗燃料（檔案刪除）· 偽造／竄改／耗盡燃料會被拒絕"],

  // ultra-realistic fabrication
  fabChain:   ["Fabrication chain", "製造流程"],
  lattice:    ["Lattice", "柵格"],
  material:   ["Material", "材料"],
  manufacturer:["Manufacturer", "製造商"],
  lot:        ["Lot", "批號"],
  targetBurnup:["Target burnup", "目標燃耗"],
  fabDate:    ["Fabricated", "製造日期"],

  // waste
  roomWaste:  ["Nuclear waste management", "核廢料管理"],
  wasteTitle: ["Nuclear waste (junk files on disk)", "核廢料（磁碟垃圾檔）"],
  wasteExplain:["Burning fuel MANDATORILY generates real 100 MB–2000 MB incompressible waste files. " +
                "Disposal sends them to a deep geological repository (deletes them).",
                "燃燒燃料必定產生真實的 100 MB–2000 MB 不可壓縮廢料檔。" +
                "處置即送往深地質處置庫（刪除檔案）。"],
  wasteTotal: ["Total on-disk waste", "磁碟廢料總量"],
  wasteCount: ["Waste files", "廢料檔數"],
  driveFree:  ["Drive free", "磁碟剩餘"],
  safetyFloor:["Safety floor", "安全下限"],
  wasteFull:  ["WASTE STORAGE FULL — dispose of waste", "廢料倉已滿 — 請處置核廢料"],
  wasteGen:   ["Generating waste…", "正在產生核廢料…"],
  dispose:    ["Dispose (deep geological repository)", "處置（深地質處置庫）"],
  disposeAll: ["Dispose ALL waste", "處置全部廢料"],
  disposeOne: ["Dispose", "處置"],
  noWaste:    ["No waste on disk", "磁碟無廢料"],
  confirmDisposeAll:["Dispose ALL nuclear waste files? This permanently deletes them.",
                     "確定處置全部核廢料檔？此操作會永久刪除。"],
  setFloor:   ["Set floor (GB)", "設定下限 (GB)"],

  // waste cap
  wasteCap:   ["Storage cap", "貯存上限"],
  wasteCapUsed:["Cap usage", "上限使用"],
  setCap:     ["Set cap (GB)", "設定上限 (GB)"],
  capFull:    ["SPENT FUEL STORAGE FULL · 乏燃料貯存已滿 — mandate shutdown; load & startup BLOCKED until waste disposed",
               "乏燃料貯存已滿 — 強制停堆；處置廢料前禁止入料及啟動"],
  capRunback: ["Approaching cap — controlled power runback in effect", "接近上限 — 已執行受控功率回降"],

  // water treatment plant
  roomWater:  ["Water Treatment · 水處理廠", "水處理廠 · Water Treatment"],
  wtTrain:    ["Treatment train", "處理流程"],
  wtChemistry:["Water chemistry", "水化學"],
  wtTank:     ["Ultrapure storage tank", "超純水貯存箱"],
  wtControls: ["Plant controls", "水廠控制"],
  wtIntake:   ["Intake", "取水"],
  wtClarifier:["Clarifier", "澄清池"],
  wtFilter:   ["Filtration", "過濾"],
  wtRo:       ["RO 2-pass", "兩級反滲透"],
  wtIx:       ["Mixed-bed IX", "混床除鹽"],
  wtDegas:    ["Degasifier", "除氣器"],
  wtTankStage:["Tank", "水箱"],
  wtIntakePump:["Intake pump", "取水泵"],
  wtIntakeRate:["Intake rate", "取水流率"],
  wtRoOn:     ["Reverse osmosis", "反滲透"],
  wtDegasOn:  ["Vacuum degasifier", "真空除氣器"],
  wtMakeupValve:["Makeup-to-reactor valve", "補水至反應堆閥"],
  wtRegen:    ["Regenerate demineraliser", "再生除鹽器"],
  wtFlushRo:  ["Flush RO membranes", "沖洗 RO 膜"],
  wtConductivity:["Conductivity", "電導率"],
  wtPh:       ["pH", "pH"],
  wtO2:       ["Dissolved O₂", "溶解氧"],
  wtSilica:   ["Silica", "二氧化矽"],
  wtChlorides:["Chlorides", "氯化物"],
  wtTankLevel:["Tank level", "水箱水位"],
  wtThroughput:["Intake", "取水量"],
  wtProduct:  ["Product water", "產水量"],
  wtMakeupDraw:["Makeup draw", "補水取用"],
  wtFouling:  ["RO fouling", "RO 結垢"],
  wtResin:    ["Resin saturation", "樹脂飽和"],
  wtLowTank:  ["LOW TANK — reactor makeup at risk", "水箱水位低 — 反應堆補水受威脅"],
  wtOffSpec:  ["OFF-SPEC CHEMISTRY — corrosion risk", "水質超標 — 腐蝕風險"],
  wtInSpec:   ["Chemistry in-spec · ultrapure", "水質達標 · 超純"],
  wtMakeupDemand:["Reactor makeup demand", "反應堆補水需求"],

  // rad
  area:       ["Area monitor", "區域監測"],
  process:    ["Process monitor", "工藝監測"],
  effluent:   ["Effluent monitor", "排放監測"],
  containIso: ["Containment isolation", "安全殼隔離"],
  doseRate:   ["Dose-rate trend", "劑量率趨勢"],

  on:  ["ON", "開"],
  off: ["OFF", "關"],
  yes: ["YES", "是"],
  no:  ["NO", "否"],
};

export function setLang(zh) { zhPrimary = !!zh; }
export function isZh() { return zhPrimary; }
export function t(key) {
  const e = S[key];
  if (!e) return key;
  return zhPrimary ? e[1] : e[0];
}
// pick(en, zh) for ad-hoc strings (e.g. server-provided status)
export function pick(en, zh) { return zhPrimary ? zh : en; }

export function applyLang(root = document) {
  root.querySelectorAll("[data-i18n]").forEach(el => {
    el.textContent = t(el.getAttribute("data-i18n"));
  });
}
