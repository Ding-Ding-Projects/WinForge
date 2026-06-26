# Cake Factory & Farm Simulator Research Notes

This note records the research inputs used for `Pages/CakeFactoryModule` and `Services/CakeFactoryService`.

## Ingredient Chain

- Wheat is grown and harvested as a traceable grain lot, then cleaned, tempered, break-rolled, sifted and purified into lower-protein cake flour. The simulator models wheat moisture, foreign material, protein, temper moisture, sifter load, extraction, bran/screenings, flour moisture, flour ash/protein, QA hold and cake-flour inventory.
- Dairy forage and feed grain are not treated only as supplier stocks. The simulator models a feed-crop harvest that cuts pasture into forage, diverts mature wheat into feed grain, checks moisture and stamps lots before the TMR mixer consumes them.
- Sugar comes from sugar beets or sugarcane, not directly from storage. The simulator models a sugar crop field, crop sugar percentage, soil tare, flume washing, slicing, diffusion, lime/carbonation purification, multiple-effect evaporation, vacuum-pan crystallization, centrifuge/drying, beet pulp, molasses, sugar moisture/color/polarization, QA hold and sugar inventory.
- Eggs are produced by laying hens and are treated as a raw-food safety risk until the bake kill step is reached.
- Milk and butter come from dairy. The simulator models pasture health, raw milk collection, bulk-tank QA, HTST pasteurization/homogenization into QA-released recipe milk, cream separation, skim milk, cream pasteurization/holding/cooling, butter churning/washing/working, butter fat/moisture/salt QA, and separate raw milk, pasteurized milk and butter inventories.
- Poultry feed is not treated as an infinite finished input. The simulator models a feed mill that converts grain, starch carrier, released mineral premix and bakery byproducts into a QA-released feed lot before hens can consume it.
- Crop fertilizer is not treated as a finished supplier input. The simulator models a compost plant that converts dairy manure, poultry manure and factory organics into a QA-released fertilizer lot before fields can consume it.
- Livestock bedding is not treated as a finished supplier input. The simulator models straw from wheat harvest or receiving, then a bedding chopper/dust extractor that makes a QA-released bedding lot before cows or hens can consume it.
- Dairy mineral premix is not treated as a finished supplier input. The simulator models limestone, trace minerals, phosphate and salt as finite feedstocks, then a mineral premix micro-dosing/blending plant that makes a QA-released premix lot before the dairy ration mixer or feed mill can consume it.
- Starch carrier is not treated as a finished supplier input. The simulator models a starch wet mill that converts harvested or received feed grain into a QA-released starch lot before the feed mill or leavening plant can consume it.
- Vanilla and cocoa are flavoring inputs. Vanilla is represented as a greenhouse crop because commercial vanilla requires managed flowering, pollination/curing, and a long supply chain; cocoa is modeled as a small greenhouse/import crop that must be harvested into fermented, dried bean lots before the roast/grind factory can consume it.
- Baking powder and salt are not farm outputs, so they are represented as factory-made outputs from finite mineral/dry-store feedstocks. The salt works tracks audited brine lots, brine salinity/hardness/turbidity, clarification, vacuum evaporation, crystallization, centrifuge spin, dryer temperature, moisture, purity, screen passing, brine blowdown, QA hold and salt inventory. The baking-powder plant tracks audited baking-soda, phosphate and released starch lots, baking-soda assay, phosphate acid value, dehumidified dry blending, sifter load, dust collector pressure, blend moisture, homogeneity, leavening dust, QA hold and baking-powder inventory.
- Decorating icing is not a field crop or automatic line effect. It is represented as a prepared factory lot made from released sugar, butter, pasteurized cow milk, vanilla and cocoa when required.

## Bakery Process

- The factory flow is: receiving hoppers -> milling/refining/churning -> scaling/weighing -> mixing/aeration -> batter depositing -> tunnel oven baking -> spiral cooling -> icing/decorating -> packaging/coding.
- Cake batter quality is represented with specific gravity because aeration and mixing strongly affect volume and crumb. Each recipe has a target specific gravity and mix time.
- Baking tracks oven setpoint and product internal temperature. The line only advances at full speed when the reactor-powered oven is hot enough.
- Quality combines power stability, sanitation, oven temperature, and mix/aeration accuracy.
- Food-safety state tracks raw flour/egg controls before baking, kill-step progress during baking, protected cooling, and sanitation/CIP condition.

## Reactor Power Requirement

- The page reads `ReactorStatusApiService.I.LastSnapshot`.
- If the reactor is not generating, farm automation, milling, mixing, ovens, cooling, icing, and packaging do not produce meaningful output.
- If reactor output is below plant demand, every powered process is slowed by the available-power fraction.

## Sources

- FDA raw dough and flour/egg safety: https://www.fda.gov/food/people-risk-foodborne-illness/raw-doughs-raw-deal-and-could-make-you-sick
- FDA egg safety: https://www.fda.gov/food/buy-store-serve-safe-food/what-you-need-know-about-egg-safety
- USDA FSIS safe temperature chart: https://www.fsis.usda.gov/food-safety/safe-food-handling-and-preparation/food-safety-basics/safe-temperature-chart
- FDA Current Good Manufacturing Practice, Hazard Analysis, and Risk-Based Preventive Controls for Human Food: https://www.ecfr.gov/current/title-21/chapter-I/subchapter-B/part-117
- USDA ERS wheat sector: https://www.ers.usda.gov/topics/crops/wheat/wheat-sector-at-a-glance
- USDA ERS sugar and sweeteners: https://www.ers.usda.gov/topics/crops/sugar-sweeteners
- USDA ERS dairy: https://www.ers.usda.gov/topics/animal-products/dairy
- USDA ERS poultry and eggs: https://www.ers.usda.gov/topics/animal-products/poultry-eggs
- U.S. Wheat Associates wheat classes and cake/pastry uses: https://www.uswheat.org/working-with-buyers/wheat-classes/
- Bakerpedia cake baking and bakery process references: https://bakerpedia.com/processes/cake-baking/
- University of Hawaii CTAHR vanilla crop references: https://www.ctahr.hawaii.edu/
