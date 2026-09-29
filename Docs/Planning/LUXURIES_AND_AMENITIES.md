# Luxuries, amenities and faith resources

Implemented against the existing cultural-life system, 28 September 2026. Numerical values and the new saffron,
salt, Hearthleaf and dish names are gameplay proposals. Sky Glass's sacred geography and religious use, Candlevein
grief tea, and naturally shed Eleos foliage used in ceremonial arts come from the vault's Sky Glass and Eleos Bloom notes.

## Integration

The existing kitchen, pantry, Seventh clock, luxury demand, happiness, cultural leanings, resource storage and saves
remain the owners of these mechanics. No parallel amenity currency or separate inventory is introduced.

`CultureLifeTuning.luxuries` classifies resources into Sweets, Fine Dishes, Teas, Wines & Spirits, Spices, Fine Cloth, Ornaments and
Living Gardens. `goods` adds durable-amenity and Faith-per-unit properties. `gardens` maps living world sites to an
amenity category, capacity and Faith-per-unit. Defaults live alongside the existing cultural-life defaults; the
Culture asset inherits missing fields. Resource tooltips, map cards, the Culture panel and the in-game wiki explain use.

`CultureLifeRules.Luxuries` previews categories against available stock, chooses the best-met category, allocates
its stock, then repeats until demand is met. On equal satisfaction it favors the category spending fewer resources.
Only selected categories spend stock; alternatives remain untouched. Shared stock cannot satisfy two categories
with the same unit. This corrects the former behavior that consumed every category while scoring only the best few.

Sky Glass is a lasting amenity: the available amount limits how much can be enjoyed, but enjoying it does not consume
it or prevent its later use as a construction material. Sacred tea is consumed. Both give Faith for the amount
actually enjoyed, not for the whole stockpile. Hearthleaf Tea is an ordinary, non-sacred brew. No cultural consumption
or Faith is granted below luxury-demand population, or before the culture is founded. During food scarcity, the
runtime prevents edible luxury consumption while allowing nonfood goods and living amenities.

Living gardens require surveyed cells on player-held land. Each patch contributes once, proportionally to the
surveyed share held, multiplied by the existing bloom vigor/gift rules; withered blooms yield no amenities or Faith.
Vow Orchids remain unharvestable. Losing the land or the bloom's health removes the benefit on the next cultural Seventh.

`CultureSystem.ConsumeLuxuries` uses the shared plan, spends only its consumable draws, records their cultural use,
awards Faith and adds a small Esoteric influence. The last award is an optional saved `CultureState.culturalFaith`
field for the panel; it is not paid again on restore. Landmarks retain their existing independent Faith rates.

## Content and geography

New resources: Auric Saffron, Silver Salt, Candlevein Grief Tea, Lullroot Tea, Hearthleaf Tea, Saffron Riverfish Pilaf,
and Honeyed Peach Tart. All have resource assets, pantry classifications and cultural-family tags. Spices have zero
food value. The two dishes are cooked by existing Flavor Log recipes and support standing orders.

Candlevein and Lullroot sites now produce their respective teas; other existing Eleos Tea sources keep the original
stock. The new Hearthleaf grove produces an everyday brew. Saffron beds require high natural land desirability,
Coherence and adequate soil; salt shores require saltwater adjacency, high Coherence and silver water or leylines.
Sky Glass's old meteor-glass description is replaced with its canon Crystal sheets. Its deposits occupy coherent
high ground, and new Age-zero deposits sit on or next to sacred ground.

`ResourceSiteSpec.minimumDesirability` uses the generator's natural land score (45% land fertility, 35% Coherence,
20% magical fertility), removing direct site fertility and Coherence auras. This is the existing natural-land score,
not the settlement desirability lens, which also includes beauty, surroundings and danger. Planting checks this
minimum too. `requiresSacred` accepts sacred cells and immediate neighbours, leaving room for the rare sprite refuge.
Both fields participate in the catalog fingerprint. New placement rules apply when worlds are generated.

## Verification

Behavioral tests cover limited category spending, durable glass, proportional sacred-tea Faith, ordinary tea,
shared-stock accounting, shortage protection, surveyed/held/vigorous gardens, catalog wiring and specialty habitats.
The cultural scene test checks actual storage deductions and Faith payment alongside kitchen, holiday and save flow.
Existing pantry and world-resource tests check that food and gathering still work and that authored sites can spawn.

Final validation: Unity EditMode run `Logs/luxuries-verified.xml` passed 75/75 tests across CultureLifeTests,
CultureLifePlayTests, CultureTests, WorldResourcesTests, PantryTests and PantryPlayTests. Saffron placed 2, 1 and 1
patches on seeds 3, 1234 and 98765 respectively; habitat constraints, sacred glass and salt shores passed. The scene
flow verified actual stock consumption, preserved Sky Glass, Faith awards and saved last-award data without repayment.

## Beverages (the cellar)

Added 28 September 2026 on the owner's request: beverages are their own `FoodClass.Beverage` (appended, index 4),
distinct from Edibles and from the teas (`FoodClass.EleosTea`), and cover every ale, mead, wine and distillation that
is not made from an Eleos Bloom. The vault names no ordinary drinks, so every name and number here is a proposal.

- They are made like dishes: `RecipeSpec` gained `method` (`Cook`, `Ferment`, `Distill`). Cellar recipes live in the
  same `CultureLifeTuning.recipes` list, use the same batches, kitchen-civic bonus and standing orders, and are shown
  under "The cellar" in the Culture window's Kitchen panel. Adding a drink is adding a recipe, a resource asset and a
  Pantry kind with `cuisine: 4`.
- Defaults: Rootgrain Ale (grain; no technology), Wild Honey Mead (Rites of Harvest), Highland Rice Wine and Auric
  Peach Wine (Resource Preservation), Bitterroot Spirit (from ale) and Auric Peach Brandy (from peach wine), both on
  Chants of Ash. Ales spoil fast, wines slowly, spirits never. Peach wine and brandy carry the `peach` flag.
- They feed, but last: `CultureRules.EatingTier` puts beverages after ingredients, so a shortfall or a land claim
  opens the cellar only once edibles, teas and ingredients are gone. During food scarcity they are not drunk as luxuries.
- A drink need not feed more than its inputs (the "cooking should pay" check applies to dishes only). Drinks are a
  luxury category of their own (Wines & Spirits; the old Brews category is now Teas), grow into foodways within their
  own class, and can become the national drink. Brewing leans the culture Indulgent; each drink has its family tag.
- `ContentValidator.ValidateCultureLife` refuses a cellar recipe whose product is not a Beverage, a cooked Beverage,
  a drink made from anything a Bloom site gives or from a tea, a distillation with no fermented input, and a Beverage
  kind no cellar recipe makes. `CultureState.drinksMade` (optional) counts them; condition `culture:drinks`.
- Icons reuse the Food sprite until drink art exists.
