# Cake Factory & Farm Simulator Research Notes

This note records the research inputs used for `Pages/CakeFactoryModule` and `Services/CakeFactoryService`.

## Ingredient Chain

- Wheat is grown, harvested, milled and sifted into lower-protein cake flour. The simulator models wheat growth, wheat inventory, milling extraction, bran/waste, and cake-flour inventory.
- Sugar comes from sugar beets or sugarcane. The simulator models a sugar crop field, refining yield, sugar inventory, and crop pulp/waste.
- Eggs are produced by laying hens and are treated as a raw-food safety risk until the bake kill step is reached.
- Milk and butter come from dairy. The simulator models pasture health, milk collection, butter churning from milk, and separate milk/butter inventories.
- Poultry feed is not treated as an infinite finished input. The simulator models a feed mill that converts grain, starch carrier, mineral premix and bakery byproducts into a QA-released feed lot before hens can consume it.
- Crop fertilizer is not treated as a finished supplier input. The simulator models a compost plant that converts dairy manure, poultry manure and factory organics into a QA-released fertilizer lot before fields can consume it.
- Livestock bedding is not treated as a finished supplier input. The simulator models straw from wheat harvest or receiving, then a bedding chopper/dust extractor that makes a QA-released bedding lot before cows or hens can consume it.
- Vanilla and cocoa are flavoring inputs. Vanilla is represented as a greenhouse crop because commercial vanilla requires managed flowering, pollination/curing, and a long supply chain; cocoa is modeled as a small greenhouse/import crop inventory.
- Baking powder and salt are not farm outputs, so they are represented as factory-made outputs from mineral/dry-store feedstocks.
- Decorating icing is not a field crop or automatic line effect. It is represented as a prepared factory lot made from released sugar, butter, cow milk, vanilla and cocoa when required.

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
