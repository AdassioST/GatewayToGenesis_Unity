# Arcanoria: a culture that remembers how it became itself

Design audit and ten-task implementation plan · 28 September 2026

## Recommendation

Keep the existing culture system and develop its connective tissue. Its strongest possible identity is **a civilization whose recipes, performances, laws, landscapes, and inherited wounds remember one another**. The player should be able to follow an ordinary action into a local custom, a public institution, and eventually an inheritance of the next Age.

The current foundation is substantial. Replacing it with another technology tree, culture currency, or national-trait picker would discard its best work. The ten additions below make the existing mechanics produce more consequential and personal stories.

This is a plan, not an implementation or a claim that tests passed. It examines the working tree, including uncommitted additions, while another agent is completing custom recipes. Recipe interfaces and some surrounding files are changing; rebaseline them at dispatch. No gameplay files were changed for this audit.

Three agents across three waves provide nine parallel slots. This plan has **ten tasks**: in Wave 3, Agent C completes T09 and then T10 within its slot. Waves need not have equal durations. The task scopes deliberately stop short of implementing all later-Age religion, diplomacy, spellcraft, and Constellation systems.

## What exists, and how it fits together

The audit covered the culture model, pure rules, orchestration, tuning, naming, UI, Ink stories, world-party adapter, and four culture test files; it also traced relevant pantry, save, achievement, population, government, Legend, ruin, ecology, and Age interfaces. Sources are listed below. This is source-level analysis, not a playtest of the current Unity build.

### Identity and emergence

`CultureSystem` creates one national culture. Horology unlocks a founding story; three answers choose Song, Hearth, or Ruins, followed by nation, demonym, and cultural-adjective naming. Ten leanings drift each Seventh from myth, pillars, active civics, resource use, terrain, and tributary districts. The character label can name two close families, but `ApplyCharacter` applies the leading family's effect.

**Strength:** identity emerges from how the player lives. The rules are understandable, tunable, and separated from scene state.

**Gap:** one national vector flattens local difference. The leading-family reward makes a diverse profile mechanically less expressive than its description. Myth influence is permanent; changes in practice lack a persistent explanation beyond the current inputs. Terrain classification uses first matching words, making descriptions act as simulation data.

### Foodways and material culture

Familiarity tracks food classes separately, using both holdings and recorded use. A sustained leading food can become national; national foods receive output bonuses. Peach adoption also changes the monocrop event score. Kitchens and cellars have recipes, batch production, standing orders, and separate tea/beverage rules. The live working tree contains `InventedRecipe` and `CultureInvention`: custom names, input shares, template similarity, inherited food/spoilage/luxury properties, and stable recipe IDs are being developed.

**Strength:** cuisine is already connected to the economy, identity, crises, and player authorship. This should be a flagship feature.

**Gap:** stockpiles are a proxy for lived experience; food made, held, eaten, taught, and ritually served are not the same thing. Culture does not yet preserve a recipe's community of origin, hosts, occasions, or transmission history. A national production bonus does not itself explain why people care about that dish.

**Boundary:** custom recipe composition, derivation, validation, resource registration, and its editor remain owned by the active recipe agent. None of these ten tasks rebuilds them.

### Social life and wellbeing

Unity comes from song, civics, districts, and landmarks, modified by happiness. Rites spend resources for Unity, joy, morale, and lived leanings. Cultural parties travel to settlements to hold festivals, award Meaning fragments, relieve settlement strain, and strengthen nearby presence. Holidays remember an occasion, recur every Echo, spend food, and give benefits; their number also raises maximum morale. Landmarks have progression lines, costs, generated names, and benefits.

Luxuries allocate shared stock without counting the same unit twice. Amenities include living gardens and retained cultural goods. The survival/living happiness split is explicit; the current tuning gives living no weight below a population threshold, and increases its weight as population grows.

The world already supplies an additional connection: `WorldResources.MoodOf`, `DrawReason`, and `SeedSource` connect settlement strain, district identity, and physical seed/pollinator availability to bloom establishment. This is existing behavior, not a missing feature. Memorials should contribute meaningful practice/provenance to it without replacing its ecological rules.

**Strength:** this is considerably richer than a passive culture meter. Nonconsumptive amenities and scarcity protection are especially worth preserving.

**Gap:** global happiness and luxury access hide who can participate. Small settlements still have meaningful social lives even if they cannot afford expensive luxuries. Most activities resolve through similar reward bundles; venues, audience, repertoire, shared history, and calendar conditions do little to distinguish them. Festival participants are counted, but the culture adapter does not use their relationship histories. Calendar traditions cannot yet express annual, seasonal, lunar, or recovery observances independently of the common Echo recurrence.

### Territory, ruins, and continuity

Presence grows on held land, faster near settlements, and fades after land is lost. Cohesion is mean presence over held cells. Ruins can supply civics through the world system; culture reforms can digest an investigated ruin or lose presence when no ruin is available. Naming and moments preserve part of the civilization's history.

**Strength:** culture is spatial, and failure leaves reusable history. Ruins already retain district, binding, civic, cause, and Age information.

**Gap:** presence measures territorial rootedness, not interpersonal trust or cultural agreement. People and roads do not yet carry a repertoire of practices between communities. In `Reform`, an eligible ruin can support a shift toward any requested family; the ruin's actual civic or history does not constrain that choice. This makes heritage too interchangeable. Existing ruin-civic adoption must be extended, not duplicated.

### Narrative, UI, persistence, and engineering

Culture has tokens, conditions, consequences, notifications, achievements, a HUD, a detailed window, and a map lens. Moments record founding, naming, foods, reforms, festivals, and landmarks. The save schema owns the whole culture state, with optional fields for later additions. Effects use replaceable source ledgers. Pure rule tests cover drift, naming, foodways, spread, consumption, calendar behavior, and luxury allocation; coroutine integration tests cover founding and social-life flows.

**Strength:** the project already has the infrastructure needed for additions with observable causes and reversible effects.

**Gap:** a moment is mainly text plus an Age and culture-relative Seventh, without a stable causal ID, linked participants, evidence, or distinct public interpretation. The UI mainly describes current totals and actions. It does not let the player trace a tradition from recipe to community to festival to reform. Tests inspected establish intended behavior, not proof that this evolving working tree compiles or passes today.

### Principal design risks to resolve

- **Reward feedback:** happiness boosts Unity; Unity buys holidays; holidays add joy and maximum morale. Existing caps and cooldowns help, but long-run balance must measure this loop rather than add another uncapped multiplier.
- **Uniformity bias:** one dominant-family effect and territorial cohesion can imply that sameness is the route to success. Preserve local diversity as a source of useful alternatives, without giving diversity an automatic universal bonus.
- **Duplicate accounting:** recipe ingredients, completed dishes, ordinary meals, luxuries, and feast servings can otherwise count the same material repeatedly as cultural participation.
- **History without causes:** generic bonuses lose the emotional specificity already present in the vault.
- **Scope inflation:** S16–S19 already plan civic evolution, public memory, religion, and ceremonial magic. These additions should deliver narrow working slices and compatible records, not claim those entire roadmap systems complete.

## Canon boundaries

Use three labels in content records and review notes: **explicit canon**, **canon-supported adaptation**, and **new game rule**. Research informs the latter two; it never establishes Arcanorian lore or numerical tuning.

- **Explicit canon:** Civic defines ten families; communal Flavor Logs; redistribution and legitimacy through Feast of Abundance; preservation of Legends through plays; Moonlit Vigil; Harmonic Quorum; Sky Glass Burials; and later Polyphonic Choral Singers. Respect each practice's Age range.
- **Explicit canon:** Enclaves have an origin wound, identity, specialization, autonomy, and relationships. The Hunger produces mourning bakeries and recipes dedicated to those who starved. Waltz argues for shared magical responsibility. Eleos Blooms respond to emotional residue but still need their actual biological conditions.
- **Explicit canon:** Echoing Bonds connects memory and loyalty. Legend Relationship describes meaningful relationship progression. Stellar Legacy Score describes cultural inheritance and next-Age World Truths.
- **Already marked as proposals:** the three founding myths, Unity as a resource, national-food mechanics, most activity/holiday numbers, landmark chains, naming vocabulary, happiness weighting, and cellar beverages. `Canon Gaps.md` explicitly records these distinctions.
- **Do not misread:** Memory Markets are later Dissonant markets associated with Velvet Nectar, not a harmless name for a cultural archive. Do not turn all musical traditions into a national Soul Leitmotif; individual souls and collective performance are different things. Do not turn cultural contact into biological inheritance, or allow ordinary celebrations to skip Legend relationship tests.

## Research translated into design

These are bounded design inferences, not claims that a game simulation reproduces human societies. Sources were consulted on 28 September 2026. UNESCO supplies a heritage framework; the other sources include empirical studies and models with different evidential strength.

- **R1 — Living heritage:** UNESCO describes practices maintained and recreated by communities, including transmission through education. Design inference: a tradition needs practitioners and renewal, while an archived record can survive inactivity. Applies to T01, T06, T10. [UNESCO: living heritage and education](https://ich.unesco.org/en/about-01159).
- **R2 — Network structure:** Derex and Boyd (2016) experimentally found that partially connected groups could accumulate more complex cultural solutions than fully connected groups in their task. Design inference: local variants plus selective exchange can be interesting; universal instant sharing should not be the only optimum. This does not establish a universal optimal network. Applies to T02, T06, T07. [Original study abstract](https://pubmed.ncbi.nlm.nih.gov/26929364/).
- **R3 — Collective memory:** Coman and colleagues (2016) experimentally connected conversation networks and individual memory updating to convergence of collective memories. Design inference: public retelling and underlying events deserve separate records. The study does not validate a particular censorship or political-unrest formula. Applies to T03, T09, T10. [Original study](https://pmc.ncbi.nlm.nih.gov/articles/PMC4961177/).
- **R4 — Ritual participation:** Konvalinka and colleagues (2011) measured shared arousal dynamics in a fire-walking ritual, differentiated by social connection. Design inference: who participates and their ties can matter alongside expenditure. This was a small, specific field setting, not proof of magical effects or automatic trust. Applies to T04, T08. [Original study](https://pmc.ncbi.nlm.nih.gov/articles/PMC3100954/).
- **R5 — Eating together:** Dunbar (2017) found associations between social eating, connectedness, and wellbeing in a UK survey. Design inference: shared meals deserve a social mechanism separate from nutritional output. Its observational evidence does not justify guaranteeing friendship from a meal. Applies to T05. [Original study](https://pmc.ncbi.nlm.nih.gov/articles/PMC6979515/).
- **R6 — Local institutions:** Ostrom's work emphasizes variation in successful local arrangements rather than one universally correct rule. Design inference: communities should negotiate access and stewardship in ways fitted to their circumstances. Applies to T05, T09. [Ostrom's explanation of her findings](https://www.nobelprize.org/prizes/economic-sciences/2009/ostrom/164465-ostrom-williamson-interview-transcript/).
- **R7 — Migration and learning:** Mesoudi (2018, corrected 2019) models how migration and acculturation interact with between-group variation. Design inference: arrival, contact, adoption, and retention are separate processes. This is a model, not an empirical assimilation timetable. Applies to T02, T07. [Original paper and correction notice](https://journals.plos.org/plosone/article?id=10.1371/journal.pone.0205573).
- **R8 — Teaching:** Caldwell and colleagues' knot-tying experiment found teaching benefits depended on complexity; complex tasks benefited more than simpler ones. Design inference: invest in teaching where a practice is hard to preserve, rather than require a guild for every custom. Applies to T06. [Human Teaching and Cumulative Cultural Evolution](https://pmc.ncbi.nlm.nih.gov/articles/PMC6290649/).

## The ten additions

### T01 — Living traditions with visible causes

**Player promise:** “We do this because of what happened here.” Repeated participation can establish a named local practice with an origin, bearers, and a visible route to recognition.

**Canon/research:** Civic's everyday practices and Enclave identity; R1. The lifecycle, thresholds, and interfaces are new game rules.

**Build:** introduce stable tradition definitions and instances. Separate a recorded tradition from its current practice: emerging, practiced, dormant, revived. Keep a bounded participation history and explanation of contributing actions. National food adoption, rites, and landmarks reference this record rather than becoming competing tradition registries. Give the player recognition, local preservation, or defer choices; never silently spend resources because a threshold was crossed. Start with a simple non-food gathering and adapters for existing actions.

**How:** pure `TraditionRules`, serializable `TraditionState`, typed `CulturalOccurrence`, and one lifecycle orchestrator. T01 owns the shared extension envelope, compatibility migration, deterministic tick phases, effect-source policy, and read-only query contracts used by every task. Retain the ten-family vector as an aggregate description; allow practice-specific benefits without multiplying all family bonuses.

**Acceptance:** the same practice history produces the same result after reload; three independent instances retain different causes; dormancy removes active effects without deleting history; revival does not duplicate achievements; old saves load without inventing historical participants. Existing food/rite flows remain functional.

**Owner/dependencies:** Agent A, Wave 1. Own new `Culture/Traditions/*`, shared `Culture/Integration/*`, and integration-only patches in core culture/save files after the recipe handoff. Size: L.

### T02 — Local cultures and routes of exchange

**Player promise:** “Our river town celebrates differently from our orchard town, and travelers carry both traditions.”

**Canon/research:** autonomous specialized Enclaves, their interdependencies, Moonlit Vigil's refugees; R2/R7. Settlement adoption and contact formulas are adaptations.

**Build:** settlement practice profiles and contact edges from actual roads, cultural-party visits, and explicitly attributed arrivals. Local adoption needs exposure and participation. A road enables contact; it does not automatically transfer ownership or loyalty. Cultural parties carry a selected repertoire, and visible provenance identifies who introduced it. Preserve variants when national recognition occurs.

**How:** pure settlement profiles and contact snapshots in `Culture/Local/*`; read `WorldPaths`, tributaries, authority, and completed party travel. Add a minimal arrival provenance payload at the actual admission boundary: source when known, otherwise unknown. Do not assign an invented foreign culture to anonymous migrants or infer local population counts that the simulation does not have. Use participation coverage, not fabricated demographics. Presence remains the existing map measure; label it rootedness and stop presenting it as agreement.

**Acceptance:** disconnected communities do not exchange spontaneously; a completed visit transfers an attributed exposure once; a road interruption blocks future contact but does not erase learned customs; cultural adoption does not change territory; unknown migrant origins remain unknown. A two-settlement fixture develops different practices and can later share one.

**Owner/dependencies:** Agent B, Wave 1. Own `Culture/Local/*`, `WorldSystem.CulturalContact.cs`, and local-profile tests. Uses frozen contract below, no same-wave dependency on T01 implementation. Size: L.

### T03 — Shared wounds, memorial places, and inheritance

**Player promise:** “The bakery remembers the famine, and the garden remembers the people who never came home.”

**Canon/research:** mourning bakeries/Ash-Loaves after the Hunger, Enclave origin wounds, Eleos emotional ecology, and Stellar Legacy continuity; R3. The playable records and recovery rules are adaptations.

**Build:** capture a limited set of meaningful historical causes: resolved crisis, settlement fall, recovered ruin, and lost Legend. The player may dedicate an existing dish, landmark, or observance to one. Dedication creates a persistent link, not a new recipe. Memorial practice can support a capped recovery contribution; the loss itself grants no farmable currency. Memorial gardens interact with actual bloom sites through their existing species constraints, rather than conjuring plants from grief.

**How:** `Culture/Memory/*` keeps immutable evidence references and separate dedication records. Subscribe to actual Age/Legend events; add an owner adapter where settlement-loss events are absent. Snapshot traditions and dedications at Age passage. Preserve next-Age recognition and dormant records now; expose a future World Truth link without building Constellations or awarding canonization automatically.

Keep remembrance distinct from ongoing suffering. If a memorial contributes emotional residue, use a bounded, expiring practice source accepted by the existing ecology owner; do not increase settlement strain to keep flowers alive. The first release may display the dedication and current ecological suitability without a new numerical bloom bonus. Grief-linked species retain their established needs; a comforting memorial does not universally feed every species.

**Acceptance:** loss/reload/recovery does not duplicate memorials; dedications survive renamed recipes through IDs; repeated losses cannot stack recovery indefinitely; ordinary mourning requires no feast or high morale; a new Age retains lineage; a memorial without a suitable existing bloom gives no ecological bonus.

**Owner/dependencies:** Agent C, Wave 1. Own `Culture/Memory/*`, memory adapters, and tests. Uses frozen event contract; integrates T01 at the barrier. Size: L.

### T04 — A meaningful calendar and ritual repertoire

**Player promise:** “Choosing when and how to celebrate matters as much as paying for it.”

**Canon/research:** first golden-fruit rites, Moonlit Vigil, Sky Glass Burials, canon calendar; R4 and UNESCO's ritual framework. Do not import later Carnival/choral institutions into Age 0.

**Build:** extend holidays with selectable cause, venue, repertoire, food/tea reference, and recurrence: existing every-Echo anniversaries, once-per-Cycle harvest observance, and a Ritual-Seventh gathering. Distinguish remembrance, hospitality, and performance objectives. A low-cost quiet observance must remain valid during hardship; resource-rich feasts can be postponed or explicitly scaled down. Explain costs and conditions before commitment.

**How:** `Culture/Observance/*` provides a versioned schedule and prepare/commit/cancel lifecycle. Use the game's Cycle/Echo/Phase/Seventh clock and world calendar flags; never wall-clock timers. Reserve or atomically revalidate resources, return reservations on cancellation, and mark a specific occurrence completed only once. Reuse existing holiday records and migrate them to their original recurrence. New food choices are served through the recipe adapter, not cooked by this feature.

**Acceptance:** Echo/Cycle rollover, save during preparation, cancellations, insufficient food, and time jumps neither double-charge nor double-reward. A vigil requires its actual calendar/location gates. A quiet memorial does not inherit the current high-morale gate for creating a celebratory holiday. Existing holidays keep their dates.

**Owner/dependencies:** Agent A, Wave 2; T01/T03 and recipe read contract. Own `Culture/Observance/*` and new observance panel. Size: L.

### T05 — Hospitality, redistribution, and access to culture

**Player promise:** “A feast changes who belongs at the table.” This is the main addition around the custom-recipe system.

**Canon/research:** Feast of Abundance explicitly ties distribution to legitimacy; the All-Welcome Cozy Inn and communal Flavor Logs; R5/R6. Participation coverage and rewards are new rules.

**Build:** a communal-table action with explicit serving policy: public welcome, recovery support, or patron-hosted gathering. Choose existing authored or custom foods. Show planned portions, people/groups represented where known, and what the stores will lose. Successful distribution creates hospitality history and opportunities for contact; luxury monopolization becomes a visible access problem rather than an invisible national average. Poor settlements can participate through simple gatherings without pretending all luxuries are equally available.

**How:** `Culture/Hospitality/*` consumes finished stock through one atomic pantry adapter, records serving, and reads recipe identity without editing ingredients or derived outputs. Track attributed participant groups only where supported; otherwise use settlement-level coverage. Food scarcity reserves survival needs first. Reuse luxury allocation and wellbeing; add a bounded participation contribution separate from expensive luxury demand. Keep the existing best-met category allocator intact.

**Acceptance:** serving cannot count the same batch as feast, luxury draw, and ordinary consumption; a public meal has distinct consequences from hoarding the same dish; insufficient stock fails without partial rewards; national-food familiarity records actual use once; recipe rename/load retains references; raw custom inputs never bypass recipe validation.

**Owner/dependencies:** Agent B, Wave 2; T01/T02, recipe agent handoff. Own `Culture/Hospitality/*`, its panel, and serving-adapter tests. No T04 dependency: this action works independently of scheduled holidays. Size: L.

### T06 — Apprenticeships and cultural institutions

**Player promise:** “Our traditions survive because someone learned to carry them.”

**Canon/research:** communal Flavor Logs, Culinary Alchemists evolving into Cooking Guilds, Ballad & Fantasy Plays, and shared responsibility in Waltz; R1/R2/R8. Age-gate named institutions; early transmission can be informal.

**Build:** use existing suitable landmarks as places for teaching. Select a tradition and an eligible bearer or community facilitator; choose preserve or adapt. Teaching takes time/capacity that could be used for other cultural work. Completed teaching creates a new bearer or a documented local variant. A written record helps recovery but is distinct from active practitioners. Begin with one culinary practice and one song/rite, without locking ordinary use behind schooling.

**How:** `Culture/Transmission/*` owns teaching orders and institution capacity; reuse Legend availability and landmark identity. Store the institution and source practice by stable ID. Do not require a Legend for every village custom. Losing one teacher pauses affected work, rather than deleting a culture. Complex practices need greater continuity, but no universal “better culture” rating. Preserve recipe permission/unlock semantics: teaching metadata cannot grant unavailable ingredients or technologies.

**Acceptance:** a completed order survives reload without granting twice; teaching competes for a real capacity slot; an unavailable teacher pauses predictably; records and living practice are distinguishable; a variant retains its parent; later-Age guilds cannot appear early; saved custom-recipe references resolve after runtime resource registration.

**Owner/dependencies:** Agent C, Wave 2; T01/T02/T03, recipe reference contract. Own `Culture/Transmission/*` and teaching panel/tests. Size: L.

### T07 — Earned syncretism and specific ruin inheritance

**Player promise:** “We choose what to inherit from this lost people, and what to change.”

**Canon/research:** ruin-civic inheritance already implemented; Enclave histories; Digestive Rebirth in Achievement; R2/R7. Specific combination recipes are new rules, not permission to invent ancient lore as fact.

**Build:** replace interchangeable ruin support with explicit inheritance choices derived from the ruin's recorded civic, district, binding, and known history. Let two practiced traditions produce an authored hybrid after actual contact and sustained use. Offer preserve side by side, adapt, or replace, with clear costs and locally different responses. An unknown ruin offers an honestly uncertain, limited option. The player should see what was retained and what was lost.

**How:** `Culture/Syncretism/*` uses deterministic authored compatibility rules, capped variant depth, two parent IDs, and evidence IDs. Extend the existing reform API and ruin adoption path; do not create a second civic-adoption ledger. Preserve existing one-time ruin IDs and achievement semantics. Modernization checks actual completed Age history. Ship three tested combinations, including one culinary memorial tradition and one communal performance tradition; do not generate arbitrary combinations of every resource and civic.

**Acceptance:** a ruin's unrelated history cannot justify any chosen family; prior consumption still blocks repeat digestion; two traditions without contact cannot hybridize; side-by-side preservation remains viable; replacing a civic removes its old ledger effects; unknown or destroyed source data does not crash inspection; inherited traditions survive the Age snapshot.

**Owner/dependencies:** Agent A, Wave 3; T01–T06 as applicable. Own `Culture/Syncretism/*`, reform UI adapter, and final shared-system integration. Size: XL, including integration responsibility.

### T08 — Ensembles whose relationships affect their performance

**Player promise:** “The people performing together matter, and their history can be heard in the result.”

**Canon/research:** Waltz's collective performance, Echoing Bonds, the existing Legend Relationship model, and age-gated Polyphonic Choral Singers; R4. Performance quality and local effects are adaptations.

**Build:** cultural parties choose an eligible repertoire and intent such as remembrance or reassurance. Read participants' readiness, availability, relevant ties, and local conditions to preview outcome bands. Completion creates an attributed shared experience and a bounded, expiring settlement benefit. Advanced regional Coherence effects require their actual later-Age civic/technology prerequisites; early songs do not become an unrestricted map-healing spell.

**How:** `Culture/Performance/*` adds deterministic evaluation, named effect sources, and a deduplicated completion payload. Route Legend experiences through existing relationship APIs. Ordinary coattendance can add a memory or modest affection, but cannot manufacture the shared wound needed to establish a significant tie, bypass capacity seven, or immediately advance relationship stages. World effects must use an owned overlay consumed by the world calculation, not repeatedly modify base terrain.

**Acceptance:** two casts can produce different explained outcomes; replaying completion cannot farm Meaning or ties; failed/cancelled performances clean up reservations; preview never changes state; removing a source removes its effect; accessible text feedback conveys everything without requiring rhythm input or audio. Advanced magic is inaccessible in the early-Age fixture.

**Owner/dependencies:** Agent B, Wave 3; T01/T02/T04/T06. Own `Culture/Performance/*`, festival integration adapter, and performance panel/tests. T08 consumes T04's completed Wave-2 contract, not T09/T10. Size: XL.

### T09 — Public memory and civic legitimacy

**Player promise:** “What we commemorate can support our laws—or expose the gap between our ideals and our actions.”

**Canon/research:** public redistribution and legitimacy, plays preserving Legends, Harmonic Quorum, and roadmap S17's separation of events from accounts; R3/R6. The response formulas are proposals.

**Build:** a focused council decision links one practiced tradition to a current civic promise. Compare a bounded set of recorded actions with that promise: hospitality with admission/distribution, stewardship with preservation, remembrance with a dedication. Offer acknowledge, revise the promise, or sponsor a different account. Show differing local support without treating cultural difference itself as unrest. Ship two short authored disputes using real recorded causes.

**How:** `Culture/PublicMemory/*` stores versioned accounts separately from T03 evidence and uses the existing government accord/effect system. Do not alter historical actions, death counters, or evidence when an account changes. This is a culture-specific S17 slice, not a complete propaganda, atrocity, prison, revolt, or rival-diplomacy system. Account disagreement can affect a capped, explained cultural contribution to accord; it is not automatic crime detection.

**Acceptance:** an official account cannot erase underlying evidence; policy reversal removes its current effect but preserves history; reactions depend on affected practices and actions, not species or family stereotypes; Ink preview is read-only; disputed facts do not become omniscient truth. Both authored disputes have costs, alternatives, and an inspectable outcome.

**Owner/dependencies:** Agent C, Wave 3 first; T01/T02/T03/T05. Own `Culture/PublicMemory/*`, new dedicated Ink volume, and council panel adapter/tests. Size: M.

### T10 — A cultural atlas that explains and compares choices

**Player promise:** “I can see why this is our culture, where it lives, and what my next decision would change.” This is a playable planning tool, not just a statistics page.

**Canon/research:** the existing White-Haven Library, Flavor Logs, named places, and inherited history; R1/R3. The atlas itself is a UI invention, not a newly asserted canonical institution.

**Build:** a focused culture browser with a selected tradition's origin, parent practices, bearer/venue locations, historical cause, current participation, and linked dishes/Legends. Add a local comparison and a read-only preview of preserve/adapt/replace where a command supports it. Distinguish known evidence, public accounts, and future possibilities. Cross-link existing journals and Library articles rather than duplicate their prose. Surface one contextual opportunity in the HUD; avoid a new notification per tick.

**How:** new `UI/Culture/Atlas/*` view modules and `CultureAtlasReadModel`; retain the existing window as entry point and the recipe editor as its own module. Every feature ships basic UI in its task, so the atlas only composes stable contracts. Integrate T09 immediately and read T07/T08 through the frozen query contract; no reverse dependency. Finish with the three cross-system scenarios below.

**Acceptance:** from a named memorial food, reach its history, venue, and living practice within three intentional selections; local differences are visible; unknown history stays unknown; previews leave all state unchanged; keyboard navigation works; narrow layouts and long player names remain usable. Saving/loading while a panel is open does not retain stale entity references.

**Owner/dependencies:** Agent C, Wave 3 after T09. Read T01–T09; no writes to their domain implementations. Own atlas/UI composition and integrated scenario tests. Size: S–M, deliberately limited to composition rather than a replacement UI framework.

## Three-wave execution plan

Agents A, B, and C are **planned Opus assignments**. This audit did not start implementation agents. The current tool roster does not expose an Opus model, so dispatch must happen in an environment that offers it; do not silently substitute another model.

### Pre-dispatch handoff (coordination, not an eleventh feature)

The coordinator takes a dated baseline of the active working tree after the recipe agent reaches a stable handoff. Include untracked culture files: these are substantial existing work, not disposable leftovers. Save/review or commit that baseline before creating isolated implementation branches; a fresh worktree from the current committed branch alone may omit much of the system audited here.

Read any applicable repository instructions at dispatch. No AGENTS.md was found in the repository file inventory during this audit. Avoid parallel Unity imports into the same `Library` directory. Each branch gets its own build/import state; serialized content and save changes are integrated by one owner.

Freeze this minimum contract before workers begin:

- `CultureEntityRef`: stable kind and ID, optional display label. Recipe IDs come from the recipe owner; names are not keys.
- `CulturalOccurrence`: stable dedupe key, action kind, absolute game-calendar stamp plus Age ID, source/settlement/actor/recipe references, quantity with explicit units, and optional evidence reference. Preserve unknown values rather than fabricate them.
- `ICultureQuery`: traditions, local profiles, origins, dedications, accounts, pending actions, and read-only command previews. Returned snapshots cannot mutate live lists.
- `CultureCommandResult`: succeeded, human-readable reason, actual paid resources, and emitted occurrence IDs. Observations and state mutations remain distinct.
- `IRecipeCultureRead`: recipe ID lookup, resource identity, authored/custom status, method, tags, unlock/availability, and immutable display data. No derivation or mutation API.
- A serving boundary owned by the pantry/recipe integration owner performs atomic finished-stock spending and emits one consumption occurrence. Ingredients used to produce a dish are production inputs, not additional feast portions.
- `CultureExtensionState`: versioned optional envelope with per-feature records and defaults. A owns the envelope and registration; feature owners own their nested types. No enum reordering, no global schema bump without compatibility review.

These are proposed interfaces, not existing APIs. Wave-1 workers compile against the same agreed signatures and fixtures. They must not independently invent three event buses. Recipe changes after handoff go through the adapter; core composition stays with the recipe owner.

### Wave 1 — Origins, places, and persistence

- **Agent A: T01** — common tradition lifecycle, contracts, tick phases, save integration.
- **Agent B: T02** — settlement identity/contact modules against the frozen contracts.
- **Agent C: T03** — history, memorials, and Age inheritance records against the same contracts.

Parallelism is real: B/C use agreed DTOs and test fixtures, not unfinished T01 methods. A integrates the three modules at the barrier. **Exit gate:** load a pre-addition save; create two locally different practices and one memorial; save/reload; verify all IDs, sources, and derived effects. No duplicate events or phantom population origins.

### Wave 2 — Practice, sharing, and teaching

- **Agent A: T04** — observances/calendar and common integration maintenance.
- **Agent B: T05** — communal table/access and recipe-serving integration.
- **Agent C: T06** — teaching, institutions, and preservation.

Each depends on Wave 1, not on another Wave-2 implementation. T04 can consume the already-frozen serving contract while T05 adds its independent hospitality policy. **Exit gate:** an invented recipe can be served, commemorated, and taught without changing its recipe-owned properties, duplicate inventory spending, or relationship shortcuts. Old holiday dates and existing kitchen orders survive migration.

### Wave 3 — Inheritance, performance, and political meaning

- **Agent A: T07** — specific ruin inheritance/syncretism plus final integration ownership.
- **Agent B: T08** — ensembles/relationships and bounded regional outcomes.
- **Agent C: T09 → T10** — focused public-memory decisions, then atlas composition and cross-system scenarios.

T10 builds from stable query contracts while A/B finish; its final validation occurs after their modules integrate. No A/B task waits on T10. **Exit gate:** all ten additions are reachable in the scoped content; the integrated build passes the release checks below. A owns the final build/merge; each worker fixes defects in its own files. Barrier repair stays inside the wave, not an unacknowledged fourth implementation wave.

### Ownership and collision rules

- **A alone integrates:** `CultureModel.cs`, `CultureSystem.cs`, `CultureSystem.Life.cs`, `CultureSettings.cs`, shared `Culture.asset`, save/bootstrap/condition/validator registries, and effect-source registration. Coordinate any overlaps with the recipe owner first.
- **B owns world-contact/performance adapters**, requesting minimal published events from world owners instead of rewriting `WorldSystem.cs`, `WorldMap.cs`, or settlement population logic.
- **C owns memory/public-account modules and atlas composition.** C is the sole final editor of `CultureWindow.cs` and `CultureHud.cs` after the recipe UI handoff. Other tasks contribute separate panel classes and documented registration entries.
- Every task owns its tests and content in separate files; use separate Ink volumes and generated JSON outputs. A compiles/imports and registers shared content at barriers. Do not hand-edit compiled Ink JSON or `Library.json`.
- Reuse `EffectRouter`, `GameValues`, `ContentValidator`, `NotificationFeed`, and existing story scheduling. Avoid new parallel ledgers for inventory, civic adoption, Legend ties, or Age score.
- All callbacks enqueue observations; process deterministic phases once per Seventh: committed source actions → occurrence dedupe → local participation/contact → tradition lifecycle → social effects → UI notifications. World and population work retain their authority; culture must not assume MonoBehaviour subscription order makes inventory safe.
- Retain immutable key events; bound routine observations and contact history with summaries. Do not scan every world cell for every tradition each frame. Update settlement adjacency on topology changes and use indexed local snapshots.

### Effort and completion claims

Sizes are relative engineering estimates, not agent-time guarantees: S is a contained adapter/view, M is a focused feature, L spans state/rules/UI/content, XL includes substantial integration. Wave 3 is likely the longest. T09 is intentionally a two-dispute slice and T10 a composer of already-shipped panels so their combined slot is credible.

Three waves can finish this **bounded redesign** if the handoff contracts hold and integration gates pass. It cannot credibly promise an entire grand-strategy culture/religion/diplomacy simulation, final audio production, or complete content for every Age in the same allocation. Later-Age gates and records are delivered; unimplemented later content stays visibly unavailable.

## Release proof, balance, and differentiators

Every task must deliver a player command or visible passive behavior, authored initial content, a reason/preview, migration handling, source-labelled effects, and meaningful tests. A state class with no reachable gameplay is not complete.

Run the existing culture, pantry, save, event, Legend-relationship, Age, and relevant world/edict suites on the integrated build. New tests should cover failure paths and system boundaries, not simply restate arithmetic. The audit itself did not run those suites.

Use three deterministic playable scenarios:

1. **The bread that remembers:** resolve a Hunger fixture → dedicate an existing/custom non-peach dish to the loss → share it locally → establish a quiet observance → teach it → carry its provenance into the next Age. Verify recipe properties never change and each serving spends once.
2. **The road between two songs:** two settlements retain different practices → a cultural party actually visits → one practice is learned → an institution preserves the variant → an eligible hybrid is offered. Break the road; learned culture remains, future exchange stops. No land changes owner.
3. **The promise and the feast:** adopt a hospitality promise → restrict or fulfill distribution → a grounded account dispute appears → resolve through the council → perform a named observance with real participants → inspect the separate event and account in the atlas. Reload before resolution; rewards and testimony remain consistent.

Balance fixtures should compare concentrated and plural local practices at the same resource budget; verify neither automatically dominates. Simulate repeated holidays, visits, memorials, teaching, and recipe serving across multiple Cycles. Record Unity sources/sinks, food spent, morale contributions, tradition counts, pending prompts, runtime cost, and save growth. Require capped social effects, bounded repetitive history, no escalating free-reward cycle, and no prompt flood. Set numeric performance budgets from a measured baseline on the actual project machine rather than inventing a millisecond promise here.

**Defensible selling points to validate in playtesting:**

- **Player-made food gains a social biography:** not only a name and stats, but an origin, teachers, occasions, and descendants.
- **Failure leaves playable inheritance:** ruined settlements and crises change later customs instead of becoming forgotten penalties.
- **Magic has social authors:** the history between performers affects a ritual without bypassing the setting's relationship rules.
- **A nation contains places with their own voices:** exchange can create new possibilities without erasing local identity.
- **The player can inspect causality:** the atlas explains how choices became culture, and lets them preview the next change.

These are proposed product strengths, not a researched claim that no other game has similar mechanics. The most valuable demonstration is the first scenario: a loaf invented by the player becomes the food of a memorial, travels with its teachers, and remains recognizable after an Age passes.

## Implementation handoff prompt

The [dispatch index](<C:/Arcanoria Master/GatewayToGenesis_Unity/Docs/Planning/CultureRedesign/README.md>) provides ten individual briefs and the exact wave assignments. The [integration notes](<C:/Arcanoria Master/GatewayToGenesis_Unity/Docs/Planning/CultureRedesign/INTEGRATION.md>) distinguish verified current entry points from interfaces that must be added.

Give each Opus agent this common instruction, followed by its wave assignments above:

> Implement only your assigned task scope in CULTURE_REDESIGN.md against the agreed baseline and frozen culture/recipe contracts. Preserve all existing working-tree features. Read applicable repository guidance and the task's canon sources. Distinguish lore from new game rules. Respect file ownership; propose shared-file patches to Agent A, and UI registration patches to Agent C. Deliver working rules, saved state, reachable UI, minimal authored content, meaningful boundary tests, and a concise report of modified files and tests actually run. Do not edit recipe derivation/editor internals, hand-edit generated lore/Ink JSON, bypass Legend progression, or broaden into deferred systems. At the barrier, fix integration failures in your owned files and provide a playable acceptance example.

## Local evidence index

The following links are source locations, not claims of runtime verification. Method names above remain more robust than line numbers while the recipe agent edits files.

- [Culture model](<C:/Arcanoria Master/GatewayToGenesis_Unity/Assets/GatewayToGenesis/Scripts/GameData/Culture/CultureModel.cs>), [culture rules](<C:/Arcanoria Master/GatewayToGenesis_Unity/Assets/GatewayToGenesis/Scripts/GameData/Culture/CultureRules.cs>), [orchestrator](<C:/Arcanoria Master/GatewayToGenesis_Unity/Assets/GatewayToGenesis/Scripts/GameData/Culture/CultureSystem.cs>).
- [Social life rules](<C:/Arcanoria Master/GatewayToGenesis_Unity/Assets/GatewayToGenesis/Scripts/GameData/Culture/CultureLifeRules.cs>), [life tuning/content](<C:/Arcanoria Master/GatewayToGenesis_Unity/Assets/GatewayToGenesis/Scripts/GameData/Culture/CultureLifeTuning.cs>), [life integration](<C:/Arcanoria Master/GatewayToGenesis_Unity/Assets/GatewayToGenesis/Scripts/GameData/Culture/CultureSystem.Life.cs>), [recipe work in progress](<C:/Arcanoria Master/GatewayToGenesis_Unity/Assets/GatewayToGenesis/Scripts/GameData/Culture/CultureInvention.cs>).
- [World culture adapter](<C:/Arcanoria Master/GatewayToGenesis_Unity/Assets/GatewayToGenesis/Scripts/GameData/World/WorldSystem.Culture.cs>), [ruin model and rules](<C:/Arcanoria Master/GatewayToGenesis_Unity/Assets/GatewayToGenesis/Scripts/GameData/World/WorldRuins.cs>), [world calendar/ecology](<C:/Arcanoria Master/GatewayToGenesis_Unity/Assets/GatewayToGenesis/Scripts/GameData/World/WorldRhythm.cs>), [population/admission](<C:/Arcanoria Master/GatewayToGenesis_Unity/Assets/GatewayToGenesis/Scripts/GameData/PopGrowthLogic.cs>).
- [Relationship rules](<C:/Arcanoria Master/GatewayToGenesis_Unity/Assets/GatewayToGenesis/Scripts/GameData/Legends/LegendRelationships.cs>), [Ballad journal](<C:/Arcanoria Master/GatewayToGenesis_Unity/Assets/GatewayToGenesis/Scripts/GameData/Events/BalladJournal.cs>), [Age progression](<C:/Arcanoria Master/GatewayToGenesis_Unity/Assets/GatewayToGenesis/Scripts/GameData/Ages/AgeProgression.cs>), [save schema](<C:/Arcanoria Master/GatewayToGenesis_Unity/Assets/GatewayToGenesis/Scripts/GameData/Saves/GameSnapshot.cs>).
- [Culture window](<C:/Arcanoria Master/GatewayToGenesis_Unity/Assets/GatewayToGenesis/Scripts/UI/Culture/CultureWindow.cs>), [culture stories](<C:/Arcanoria Master/GatewayToGenesis_Unity/Assets/Resources/Events/Culture.ink>), [architecture](<C:/Arcanoria Master/GatewayToGenesis_Unity/Assets/GatewayToGenesis/Scripts/ARCHITECTURE.md>), [roadmap S16–S19](<C:/Arcanoria Master/GatewayToGenesis_Unity/Assets/GatewayToGenesis/Scripts/ROADMAP.md>), [luxury design/previous validation report](<C:/Arcanoria Master/GatewayToGenesis_Unity/Docs/Planning/LUXURIES_AND_AMENITIES.md>).
- [Culture rule tests](<C:/Arcanoria Master/GatewayToGenesis_Unity/Assets/GatewayToGenesis/Tests/Editor/CultureTests.cs>), [life tests](<C:/Arcanoria Master/GatewayToGenesis_Unity/Assets/GatewayToGenesis/Tests/Editor/CultureLifeTests.cs>), [founding integration test](<C:/Arcanoria Master/GatewayToGenesis_Unity/Assets/GatewayToGenesis/Tests/Editor/CulturePlayTests.cs>), [life integration test](<C:/Arcanoria Master/GatewayToGenesis_Unity/Assets/GatewayToGenesis/Tests/Editor/CultureLifePlayTests.cs>).
- [Canon gaps](<C:/Arcanoria Master/GatewayToGenesis_Unity/Assets/Resources/Library/Vault~/Canon Gaps.md>) and [Library import policy](<C:/Arcanoria Master/GatewayToGenesis_Unity/Assets/Resources/Library/README.md>).
- [Canon: Civic](<C:/Arcanoria Master/Arcanoria/Worldbuilding/Society/Societal Resources/Foundation/Civic.md>), [Waltz Pillar](<C:/Arcanoria Master/Arcanoria/Worldbuilding/Society/Societal Resources/Foundation/Waltz Pillar.md>), [Enclave](<C:/Arcanoria Master/Arcanoria/Worldbuilding/World Environment/Enclaves/Enclave.md>), [The Inescapable Hunger](<C:/Arcanoria Master/Arcanoria/Worldbuilding/Rise & Fall, Crisis/Crisis/The Inescapable Hunger.md>).
- [Canon: Eleos Bloom](<C:/Arcanoria Master/Arcanoria/Worldbuilding/World Environment/Bestiary/Eleos Bloom.md>), [Echoing Bonds](<C:/Arcanoria Master/Arcanoria/Worldbuilding/Origin of Magic/Bindings & Elements/Echoing Bonds.md>), [Stellar Legacy Score](<C:/Arcanoria Master/Arcanoria/Worldbuilding/Society/Stellar Legacy/Stellar Legacy Score.md>), [The Registers of Magic](<C:/Arcanoria Master/Arcanoria/Worldbuilding/Origin of Magic/Spellweaving/The Registers of Magic.md>).
