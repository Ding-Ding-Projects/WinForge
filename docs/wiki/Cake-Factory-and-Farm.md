# Cake Factory & Farm · 蛋糕工廠與農場

![Cake Factory & Farm · 蛋糕工廠與農場](images/screenshot-cakefactory.png)

**EN —** The Cake Factory & Farm module is a hands-on HTML5 simulator for a reactor-powered ingredient farm and bakery line. It is not a video and it is not fully automatic: the reactor bus, supply dock, farm, audited ingredient lots, ingredient factories, plant maintenance, mixer, tunnel oven, cooling, icing, packaging, QA, signed `.cake` files and CIP cleaning all expose live controls and operator release gates.

**粵語 —** 蛋糕工廠與農場模組係一個由反應堆供電嘅 HTML5 互動模擬器，包含原料農場同烘焙生產線。佢唔係影片，亦唔係全自動：反應堆供電、農場、磨粉、攪拌、隧道焗爐、冷卻、裝飾、包裝、品檢同 CIP 清潔全部都有即時控制同操作員放行關卡。

Open in-app: `WinForge.exe --page cakefactory`

## Screenshots · 截圖

### Full WinForge module · 完整 WinForge 模組
![Full cake factory module](images/screenshot-cakefactory.png)

### Immersive HTML5 scene · 沉浸式 HTML5 場景
![Cake factory scene](images/screenshot-cakefactory-scene.png)

### HTML controls · HTML 控制台
![Cake factory controls](images/screenshot-cakefactory-controls.png)

## What It Simulates · 模擬內容

| Area · 區域 | Simulation · 模擬內容 |
|---|---|
| Reactor bus · 反應堆供電 | The factory only runs when the live reactor status bus is generating enough electrical power. A meltdown or offline reactor locks out powered actions. |
| Supply chain inputs · 供應鏈輸入 | Ingredients do not appear from nowhere: seed, irrigation water, fertilizer, animal feed, cocoa beans, brine, soda ash, phosphate, starch carrier, cartons/labels and factory utilities are finite stocks delivered through the receiving dock. |
| Ingredient farm · 原料農場 | Wheat, sugar crop, vanilla, pasture health, cow milk and eggs grow over time only when reactor power and the required farm inputs are available. Milk comes from a tracked lactating cow herd that consumes feed and water, then passes through a powered milking parlor and chilled bulk tank. |
| Lot traceability · 批號追蹤 | Receiving, harvest, dairy collection, ingredient factories and batch start all stamp audited lot or manifest IDs. Batches keep a trace manifest that lists the flour, sugar, eggs, milk, butter, leavening, salt, vanilla, cocoa and packaging lots used. |
| Ingredient conversion · 原料轉換 | Harvest, collect cow milk/eggs, then run timed powered factory jobs: mill wheat into cake flour, refine sugar crop into sugar, churn milk/cream into butter, roast/grind cocoa beans into cocoa, evaporate brine into salt and blend leavening feedstocks into baking powder. |
| Factory telemetry · 工廠遙測 | Ingredient factories consume raw inputs plus process water, culinary steam, compressed air and filter media at start, add reactor load, expose unit-operation phase, run progress and process QA, pause on low power, and release output only after completion. Each plant tracks equipment condition, calibration, bearing temperature and vibration; wear affects throughput, QA and yield until the operator services the factories. Readings include mill roll gap, flour extraction, sugar Brix, evaporator temperature, separator rpm, butterfat, cocoa roast temperature, grind size, brine salinity, crystallizer temperature and leavening blend homogeneity. |
| Bakery line · 烘焙生產線 | Operator-driven scaling, mixing, depositing, tunnel baking, spiral cooling, icing/decorating and packaging/coding. |
| Signed cake files · 已簽署蛋糕檔 | Packed cakes are minted as portable `.cake` files signed with the bakery private key. Other devices validate them with the trusted public key, forged cakes are rejected, and eating a cake deletes the file. |
| Food safety · 食物安全 | HACCP-style prompts, kill-step temperature, cooling limit, sanitation score, quality score, rejects and waste tracking. |
| CIP sanitation · CIP 清潔 | Clean-in-place locks batching while washing mixer, depositor, oven belt, icing head and packer. |

## Controls · 控制

| Control · 控制 | Use · 用途 |
|---|---|
| Station rail · 工站列 | Focus the scene on All, Farm, Prep, Bake or QA without leaving the simulator. |
| Recipe select · 配方選單 | Choose White layer cake, Butter pound cake or Chocolate layer cake. The recipe changes batch size, ingredients, bake target and target specific gravity. |
| Farm intensity · 農場強度 | Raises or lowers crop growth, pasture/livestock output and farm electrical demand. |
| Line speed · 生產線速度 | Raises or lowers bakery throughput and factory electrical demand. |
| Receive supplies · 接收補給 | Adds audited seed, water, fertilizer, feed, cocoa beans, brine, soda ash, phosphate, starch carrier, cartons/labels, process water, culinary steam, compressed air and filter media at the receiving dock. |
| Harvest · 收成 | Moves ready wheat, sugar crop and vanilla from fields into inventory and stamps harvest lot IDs. |
| Milk + eggs · 收奶蛋 | Collects cow milk from the powered milking parlor and graded eggs from the barn buffers. The milk buffer is produced by lactating cows, chilled in a bulk tank, stamped as a milk lot and checked for temperature, bacteria, somatic cell count, fat and protein. |
| Mill flour · 磨粉 | Converts harvested wheat into cake flour and bran/waste. |
| Refine sugar · 煉糖 | Converts sugar crop into usable sugar and waste. |
| Churn butter · 打牛油 | Converts milk/cream into butter while preserving cold milk inventory. |
| Roast cocoa · 烘焙可可 | Converts finite cocoa beans into usable cocoa powder. |
| Salt works · 鹽廠 | Converts finite brine into baking-grade salt using a powered evaporator and crystallizer. |
| Leavening plant · 膨鬆劑廠 | Converts finite soda ash, phosphate and starch carrier into usable baking powder. |
| Service plants · 維修廠房 | Stops ingredient-factory work, consumes utilities, lubricates bearings, replaces filters, recalibrates sensors/scales and restores plant condition. |
| Start batch · 開批 | Consumes the selected recipe's ingredients and starts the manual batch line. |
| Release step · 放行工序 | Advances only when the current stage is complete and the safety gate is satisfied. |
| CIP clean · CIP 清潔 | Starts a clean-in-place sanitation loop. Batching is locked until it completes. |
| Trust key · 信任公鑰 | Imports the embedded bakery public key from a copied `.cake` file so another device can validate that bakery. |
| Validate cake · 驗證蛋糕 | Verifies the latest `.cake` file signature and trusted public key before it can be used. |
| Eat + delete · 食用並刪除 | Consumes the latest valid cake by deleting the `.cake` file from disk. |
| Open reactor · 開反應堆 | Navigates to the Nuclear Reactor module so the reactor bus can be started/recovered. |

## Operating Procedure · 操作程序

1. Open the module with `WinForge.exe --page cakefactory`.
2. If the banner says **Waiting for nuclear reactor generation**, open the reactor module and bring the plant to generation. The cake simulator intentionally disables powered work without the reactor bus.
3. Select a recipe and set farm intensity / line speed. Higher settings make the scene more active but increase plant load.
4. Keep the supply chain stocked. Use **Receive supplies** when seed, irrigation water, fertilizer, feed, cocoa beans, brine, leavening feedstocks, cartons/labels or factory utilities are low. Each receiving action records a new supply manifest.
5. Prepare ingredients manually:
   - Harvest fields when wheat, sugar crop or vanilla are mature.
   - Keep the cow herd fed and watered, then collect milk and eggs when barn buffers are ready. Watch bulk tank temperature and milk QA before batching.
   - Mill wheat into flour and wait for the powered roller mill run to finish.
   - Refine sugar crop into sugar and wait for diffuser/evaporator completion.
   - Churn butter when milk inventory has enough reserve and wait for the separator/churn run.
   - Roast cocoa beans into cocoa when a chocolate recipe needs it, then wait for roast/grind completion.
   - Run the salt works when salt is low and wait for evaporation/crystallization.
   - Run the leavening plant when baking powder is low and wait for blend homogeneity.
   - Use **Service plants** when condition, calibration, vibration or bearing temperature starts to pull down yield or QA.
6. Press **Start batch** only after the inventory panel shows no missing ingredients and enough cartons/labels are available.
   - Starting the batch opens a batch lot manifest that records the ingredient and packaging lots consumed by that batch.
7. Watch the current stage and wait for the release gate:
   - **Weighing + scaling:** wait for scale verification.
   - **Planetary mixing:** wait for mix time and target batter specific gravity.
   - **Depositing batter:** wait for pan-weight stabilization.
   - **Tunnel oven bake:** release only after product core temperature reaches the kill step.
   - **Spiral cooling:** release only when product core temperature is safe for icing.
   - **Icing + decorating:** release when decorating is complete and sanitation remains acceptable.
   - **Packaging + coding:** release to finish the batch, log QA and count packed/rejected cakes.
8. When packaging completes, the app mints one signed `.cake` file per packed cake under the cake-factory app data folder.
9. On another device, copy the `.cake` file into the cake folder, press **Trust key** once for that bakery, then use **Validate cake**.
10. Use **Validate cake** before another module or operator uses a cake file. A forged, tampered, untrusted, replayed or expired cake is rejected.
11. Use **Eat + delete** when a workflow consumes a cake. The valid `.cake` file is deleted, so it cannot be eaten twice.
12. Run **CIP clean** when sanitation drops or after production. The simulator locks batching during CIP, then restores sanitation as the wash/rinse/drain loop progresses.

## Manual-First Behavior · 手動優先行為

The simulator deliberately avoids full automation:

- It does not start batches on its own.
- It does not harvest, mill, refine or churn ingredients automatically.
- It does not create ingredients from thin air; farm and bakery output consume finite upstream inputs.
- Milk specifically comes from lactating cows, which consume feed and water before the powered milking parlor can transfer raw milk into cold storage. Warm or high-count milk is held by the recipe gate.
- Receiving, harvest, dairy collection, factory output and batch start all carry lot IDs. A batch cannot start if required ingredient lot data is missing.
- Ingredient factories are not instant: they consume finite inputs and utilities, add reactor load, move through unit-operation phases, report process QA, pause on low power and release usable ingredients only after the process completes.
- Ingredient factories wear down. A run degrades the specific plant that ran, worn equipment slows throughput and reduces yield/QA, and maintenance consumes real utilities before restoring condition and calibration.
- It does not advance stages automatically after timers complete.
- It waits for the operator to release each HACCP gate.
- It blocks release if power, temperature, sanitation or recipe requirements are not satisfied.
- It rejects forged `.cake` files and refuses to consume them.

## Troubleshooting · 疑難排解

| Symptom · 狀況 | Cause / fix · 原因 / 修正 |
|---|---|
| Controls are disabled · 控制不能按 | Reactor is offline, not generating enough power, or in meltdown. Open the reactor and restore generation. |
| Start batch is disabled · 未能開批 | Missing recipe ingredients, active CIP cycle, no reactor bus power, or a batch already on the line. |
| Farm output stalls · 農場停產 | Seed, irrigation water, fertilizer or animal feed is low. Use Receive supplies and restore reactor power. |
| Traceability hold · 批號追蹤暫停 | A required ingredient has quantity but no lot ID. Run the relevant collection/factory step or receive audited supplies so the ledger can be restored. |
| Service plants is disabled · 無法維修廠房 | A factory run is active, reactor bus power is low, or process water/steam/compressed air/filter media is insufficient. |
| Release step is disabled · 未能放行 | Stage timer is still running, kill-step/cooling/sanitation gate is not met, or reactor power is unavailable. |
| Cake file rejected · 蛋糕檔被拒絕 | The file is forged, tampered, signed by an untrusted public key, expired or already eaten. |
| Quality drops · 品質下降 | Low power, worn or uncalibrated ingredient plants, bad oven temperature, missed specific gravity target, or low sanitation. Service plants, slow down, clean and wait for gate conditions. |
| CIP seems to stop production · CIP 停止生產 | Expected behavior: CIP locks batching until the sanitation loop finishes. |

## Verification · 驗證

The module was tested with these checks:

| Evidence · 證據 | Result · 結果 |
|---|---|
| Headless service scenarios · 無介面服務情景 | `dotnet run --project tests/ReactorSim.Tests/ReactorSim.Tests.csproj -c Debug` passed **26/26** scenarios, including cake power gating, no-auto manual mode, cow milk provenance and cold-chain QA, audited lot traceability, finite supply inputs and utilities, timed non-farm ingredient factories, unit-operation phases, process QA, factory equipment maintenance, ingredient chain, full manual batch, signed `.cake` file crypto and CIP sanitation. |
| WinForge GUI screenshot · WinForge 圖形介面截圖 | `WinForge.exe --page cakefactory` was launched from a self-contained publish and captured into `docs/screenshot-cakefactory.png`. |
| WebView asset packaging · WebView 資產封裝 | `SimAssets/cake/index.html` is included under `SimAssets/**/*.*`, copied to publish output and loaded through WebView2 virtual-host mapping. |
| Signed cake files · 已簽署蛋糕檔 | The test suite verifies private-key signing, public-key trust on another device root, forged/tampered rejection, replay rejection and eat-delete consumption. |

## Implementation Files · 實作檔案

- `Pages/CakeFactoryModule.xaml` — WinUI host for the HTML5 simulator.
- `Pages/CakeFactoryModule.xaml.cs` — WebView2 bridge, reactor-bus snapshot posting and operator action handling.
- `SimAssets/cake/index.html` — immersive HTML5 canvas, controls and JavaScript UI.
- `Services/CakeFactoryService.cs` — authoritative simulator model for recipes, inventory, stages, quality, sanitation and reactor-power dependency.
- `Services/CakeFileService.cs` — signed portable `.cake` file creation, public-key trust, validation and eat-delete consumption.
- `tests/ReactorSim.Tests/Program.cs` — headless scenario coverage for cake simulator behavior.

[← Apps, Git & Packages](Apps-Git-and-Packages.md) · [Screenshots](Screenshots.md) · [Wiki Home](Home.md)
