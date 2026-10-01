# Arcanoria — seamless world generation design

Design proposal, 2026-09-26. This extends S07/S09 in the established ROADMAP and takes priority as the user's next workstream. It is a generation specification and reviewable schematic, not an implemented Unity generator. The earlier Age-first delivery order is superseded for the immediate queue only; world/Age interfaces remain dependencies for later integration.

## 1. Confirmed composition: Quadrants, Macro Biome slots and intersections

The user's reference is a regional composition stencil, not the final tile grid. **Q1–Q7 are quadrants containing groups of Macro Biomes; W is water; I is the procedural intersections between Macro Biomes.** Preserve the green Q1 start and the connected Q2–Q1–Q2 core of mainland Arcanoria. The exact catalogs of all seven quadrants remain to be authored; do not infer biome identities from colors alone.

The hierarchy uses AECOR's scale names (owner decision, Sept 28, 2026; ARCHITECTURE.md "World scales"): **Overall Map → Quadrant → Macro Biome → Inter-Biome Definitions → Sector**, with meso cells and micro hexes beneath as the playable detail. Macro rendering aggregates those same cells. A Quadrant ID such as Q2 denotes a Quadrant definition; repeated Q2 blocks also need distinct instance IDs. A slot is a placement opportunity for one Macro Biome within its Quadrant, not a square border visible in the finished landscape. Inter-Biome Definitions are what lies inside a Macro Biome and may cross its Sectors: covers, landmarks (features), grandfields and resource sites. Sectors are the nine compass parts of each Macro Biome (Central, North, North-East and so on round the compass), assigned after generation. Intersections (stencil I) are the procedural ground between Macro Biomes.

The user supplied these working catalogs:

- **Q2:** two slots containing Violet Grove and Great Expanse. A world seed can assign Grove left/Expanse right or the reverse; each biome also selects an allowed orientation.
- **Q5:** four slots containing Taiga, Magical Rift, Wind Plains and Auric Grasslands. Their placement and allowed rotations vary while the quadrant retains its mountainous regional identity.
- **Q1:** starting quadrant; green central reference. Its detailed biome catalog is still open.
- **Q3/Q4/Q6/Q7:** preserve their reference positions and quadrant identity; detailed catalogs and directional climate constraints remain open. Northeastern tundra was an example of a quadrant theme, not a finalized assignment to a specific S number.

**The intersections (I) are generated connective space, not an additional Macro Biome in the shuffle bag.** They synthesize mountain continuations, foothills, forest ecotones, plains, coast, straits and river passages from its neighbors. They may become land or water where the world topology permits. Their geometry should hide the slot stencil without erasing a named biome's identity.

### Ocean and continent topology

Use an **outer ocean belt around the assembled continental silhouette**, not a compulsory circular moat immediately around the three starting blocks. This lets mountain systems and procedural land seams connect peripheral quadrants where the composition calls for a contiguous continent. The three central blocks are always connected mainland. Other quadrant instances carry explicit topology roles: attached land, offshore island, archipelago or remote continent; the detailed assignment remains a content decision.

Reserve a closed navigable water route around the complete primary mainland component and an outer ocean apron at the map boundary. Bays, shelves, straits and islands can make this ring irregular. Proposed initial minimum navigable width: three meso cells along the reserved ocean circuit, subject to ship/pathfinding tuning. The circuit must enclose the mainland, not merely be any cycle inside a bay. Ports and cross-ocean routes must connect to it. No procedural seam may accidentally dam the reserved circuit.

The Capital begins inside Q1 on ordinary freshwater and usable soil with stable moderate Coherence. It is not guaranteed a Sacred Site, nexus or grandfield. Guarantee a reachable low-risk discovery and two early expansion directions. Green habitat does not bypass Age 0's famine or restricted magic.

## 2. One connected world, with constrained variation

The geographical identity is fixed; detailed composition varies by world seed. Treat the inspiration as a design goal: strategy-game adjacency/readability plus shuffled authored regional content. Do not assume either reference game's exact implementation.

**Fixed composition contracts:** central connected mainland, quadrant theme constraints, outer ocean belt, global bounds, guaranteed initial reachability and named unique locations' eligibility rules.

**Generated each new world:** coast details, islands, mountain/rain-shadow structure, ordinary rivers and lakes, soil patches, resource grandfields, local habitat, seed placements, and the assignment/rotation of eligible authored landmarks within region slots. Story-critical location identities persist; their positions can vary within approved slots.

**Changes by Age:** Grand Thread Ring state, leyline curves and active junctions, magical-water reaches, magical fertility, some hazards, trade desirability and enclave development. Built roads, settlement coordinates, ordinary river catchments and discovered geography remain stable unless a specific event changes them. A reset's geographic regeneration policy is a separate unresolved decision; do not infer it from an Age change.

**Changes through play:** roads/bridges, extraction, depletion or regeneration where enabled, irrigation, pollution, ownership, Atonalis effects, local dissonance sources, and anchored leyline deflections.

### Seam rules

Generate a global coarse height/climate field first, then refine it within regions. Region generation is never an isolated rectangle with independent boundary noise. Shared edges have one canonical owner and shared samples. Authored chunks declare edge ports (river elevation/flow, coast, pass, path, leyline compatibility) and a buffer zone. Stitch compatible ports through a blending strip; reject or reposition impossible chunks.

Use world-coordinate seeded noise and stable feature IDs. Use a halo around each streamed chunk for interpolation/path queries. Its size is the largest finite local kernel; long-range hydrology and magic are computed in their global coarse graphs before chunk refinement. Halo data is read-only in the neighbor's ownership region.

Hard geographic constraints are solved before cosmetic detail. Constrain a connected mainland mask and protect the enclosing outer ocean loop, then add noisy coastlines within those masks. Validate connectivity again after erosion, island stamping and feature placement. On failure, repair locally, then use bounded deterministic retries and a known-valid fallback; never an unbounded reroll.

## 3. Three zoom levels: the same place, three readings

Recommended prototype ratios:

- **Micro:** local terrain patches, riverbanks, buildings/improvements, vegetation, extraction footprints and visible activity.
- **Meso / civilization:** one playable strategy hex owns **7 micro hexes**. Settlement territory, army/expedition movement, yields, borders and diplomacy operate here.
- **Macro / atlas:** one aggregate owns **49 meso hexes = 343 micro hexes** in the interior. It shows regional biome mixtures, major watersheds, ocean passages, discovered settlements and major magic routes.

A colored square in the user's reference is a **macro biome region containing multiple macro cells**, not necessarily one macro cell. This allows a recognizable region to hold a large civilization map rather than only a handful of hexes.

### Exact ownership without a false geometric promise

Use a hierarchical index-7 axial lattice. For child coordinates `(q,r)`, parent center coordinates in the child lattice are `F(Q,R)=(2Q-R,Q+3R)`. Each parent owns offsets `(0,0),(1,0),(0,1),(-1,1),(-1,0),(0,-1),(1,-1)`. The transform determinant is 7, and these offsets cover its seven residue classes exactly once. Find the unique offset for which `F^-1(child-offset)` has integer components; this is the parent ID. Negative coordinates use integer divisibility, not truncating division.

One application groups micro into meso. Two additional applications group 49 meso into macro. The intermediate index exists in data but does not need a fourth UI zoom level. This is a proposed implementation with directly testable ownership, not an imported library requirement.

Regular hexes do not nest into larger regular hexes with perfectly straight borders. The real parent outline is the union of its child polygons. Draw this outline at detailed zoom; at atlas zoom a simplified center glyph is allowed, but it must not change ownership, selection, adjacency or river locations. Parent lattice orientation rotates; do not force child and parent art to share flat edges. If regular hexes at every scale are non-negotiable, use center-based approximate hierarchical cells and clipped boundary children instead, losing constant child counts at those edges.

World-edge parents store only valid children and their actual area/count. Never pad yields with imaginary ocean or land. Prefer a world domain composed of whole macro parents plus an ocean apron when exact ratios matter everywhere.

### Zoom invariants

A click at a world position selects the same underlying place across all views. The camera preserves its world-space focal point. Parent highlights resolve to descendants, and micro selections resolve to ancestors. Coasts, rivers, roads and leylines remain the same polylines with scale-dependent simplification; they are never regenerated on zoom. Maintain connectivity and shared endpoints during simplification.

Sum extensive quantities such as population and available stock. Use area-weighted means/distributions for soil and Coherence, plus min/max and hazard extent where needed. Macro biomes show mixture proportions with ecotone shading rather than a majority label that erases coastlines. Meso adjacency comes from actual child-boundary contact, not assumed art neighbors. Micro simulations do not add a second copy of meso production.

Start performance trials around **30,720 meso cells (192 × 160 candidate axial domain), 215,040 micro cells**, before padding/clipping. This is a sizing candidate, not a benchmark claim. Exact whole-macro worlds should round to complete parent groups. Keep compact data arrays and mesh/Tilemap batches; do not instantiate a GameObject or ticking component per tile. Stream detailed rendering near the camera; aggregate simulation can remain active elsewhere. Choose final scale only after profiling.

## 4. Slot assignment, rotations and procedural stitching

A biome is a **constrained recipe**, optionally with authored landmark interiors, not a finished independent terrain square. It supplies climate/soil ranges, elevation envelopes, vegetation/resource rules, mandatory motifs, permitted features, and boundary ports. A quadrant supplies the larger mountain backbone, prevailing moisture/wind and coastline/topology obligations. The recipe deforms within those obligations.

### Assignment algorithm

1. Build a quadrant-instance adjacency graph from the reference stencil. Reserve mainland connectivity, the ocean circuit, passes and required freshwater opportunities before allocating slots.
2. Create each quadrant's slot graph with eligibility tags: coast/interior, highland/lowland, wet/dry tendency, required river/pass contacts and approximate area.
3. Create the biome multiset for that quadrant. Assign exactly one biome to each required slot, without replacement unless the catalog explicitly permits repeats. Q2 has 2 permutations and Q5 has 24 permutations before eligibility/rotation constraints; these are theoretical maxima, not guaranteed valid outputs.
4. Enumerate allowed 60-degree rotations for hex-native templates. Use each template's allowed subset; do not rotate north-facing climate requirements blindly. Reflections are a separate opt-in and disabled for named sites by default. Non-grid source artwork can be resampled; logical ports still need validation.
5. Use a seeded constraint solver: pick the most constrained slot, shuffle eligible biome/orientation pairs, place, propagate neighbor constraints and backtrack. A biome's exact inner landmarks can remain fixed relative to one another while its exterior blends.
6. Score valid layouts for useful variety: distinct routes, separated scarce resources, readable transitions and starting fairness. Use bounded deterministic retries; report why unsatisfiable catalogs fail instead of silently deleting a biome or cutting the ocean.
7. Synthesize the intersections from all adjacent ports/fields together. It is a positive-width transition area with its own tile ownership. Preserve each biome's protected interior; move/blend only declared buffer bands. Resolve four-way seams jointly, not four independent edge strips.
8. Validate the final terrain and feature graphs again. Record seed, generator version, catalog hash, assignments, orientations, port connections and any repairs.

### What must match at a seam

Elevation and slope continuity; climate/moisture continuity unless an authored magical boundary allows a sharp change; river elevation, discharge and direction; road/pass reachability; coastline and water level; mandatory biome borders; and required exclusion space for unique features. River ports cannot be joined uphill. Build a global drainage solution after terrain constraints are stitched, then refine rivers into micro geometry. Lake outlets, closed basins and waterfalls require explicit treatment rather than broken slopes.

A mountainous Q5 is generated as one regional range system. Taiga occupies a compatible cold/wet part, Wind Plains a plateau/pass or rain shadow, Auric Grasslands a suitable valley/foothill, and Magical Rift a compatible fracture corridor. These subroles are proposals; variants can change elevations/climate envelopes. Do not force all 24 arrangements if some cannot satisfy the quadrant's geography. Conversely, do not reduce every seed to the same arrangement by overconstraining slots: authored variants and a deformable mountain backbone should allow meaningful alternatives.

Random streams are split by stable purpose/ID: topology, slot allocation, terrain, hydrology, resources, features, and Age magic. Adding a landmark must not consume the random numbers that determine the continent or move the Capital in an existing version.

## 5. Physical water, magical pathways and two fertility layers

### Physical terrain and water

Generate elevation → drainage basins → flow accumulation → rivers/lakes/wetlands → soils and vegetation. Rivers follow shared hydrological edges with a global downstream direction. Drain outlets reach the ocean or a deliberately closed basin. Preserve tributary connectivity at every zoom.

**Land fertility** is a field based on soil depth/minerals, freshwater availability, climate, drainage, erosion and existing damage. Floodplains can be rich but hazardous; a wet bog need not be fertile. Vegetation, irrigation and depletion modify this field through play. Land fertility does not automatically become high just because magic is high.

### Coherence, dissonance and magical fertility

Adopt the user's Coherence Seeds and Dissonance Seeds as generation sources. A source has a stable ID, position, strength, influence radius, falloff and affinity. Dissonance Seeds represent regional generation influences; an Atonalis Dissonance Core is a distinct living/entity feature that can add a moving or local negative source. They must not share a class merely because both dampen Coherence.

Compute a proposed bounded Coherence field from a quadrant baseline + distance-decaying positive seed influences + leyline support + Sacred Site support + constructed Anchors − distance-decaying dissonance/core influences. Use terrain-aware/geodesic attenuation where barriers matter. Overlapping sources mean Coherence need not rise monotonically along every path away from one core; isolated-source tests must show the expected falloff. Track negative influence separately for attribution and hazards rather than calling a clipped value a negative Coherence score.

**Sacred Sites are fixed local maxima and protected refuges.** Their protected interiors retain the highest harmonic integrity under ordinary Age drift and common dissonance effects. The vault says immune to most effects, not unconditionally invincible; exceptional story effects need explicit overrides. Atonalis avoidance is a movement rule as well as a map color.

**Magical fertility** is a separate field combining Coherence stability, available magical flow, affinity compatibility and Age-specific access. A high-Coherence but low-flow site can be stable without being highly productive. Sacred Sites provide exceptional potential, while Age 0 limits usable magic. Both fertility overlays must remain inspectable independently: fertile mundane farmland, magically rich barren rock, dual-fertile silver floodplain, and poor/dissonant wasteland are all valid outcomes. Numerical field ranges and coefficients remain tunable proposals.

### Leylines from seeds and Grand Thread Rings

A leyline is a persistent lineage plus an Age-specific embedded path, sourced from a Coherence Seed. Grand Thread Ring phase/orientation supplies the large-scale direction/potential field. Route curves through favorable Coherence, often alongside rivers, while allowing overland and undersea routes. Solve the network globally/coarsely first, then refine it continuously across the intersections and chunk boundaries. Give line families stable IDs, capacities/flux and affinities; segment count does not determine identity.

Use physical path intersections after refinement to create junctions:

- **Leyline Convergence:** exactly two distinct leyline families meet in a connected junction footprint.
- **Leyline Basin:** three or more distinct families meet within a bounded connected hotspot. For more than three, retain the Basin category and record multiplicity. The treatment of four-plus is a proposed extension of the user's three-line rule.
- A bend, a fork of one lineage, duplicate segment, or two lines crossing a third at widely separated places is not a three-line Basin. Count participating line families at the shared local junction, not graph degree; a two-line crossing can have four outgoing edges.

A Basin is magical geography, not automatically a freshwater lake. It can coincide with one if hydrology permits. A Sacred Site may coincide with a Basin but is independently defined and remains fixed if the lines move.

### Silver rivers of Lunehymn

At a physical river/leyline intersection, create an infusion source. Overlapping stretches infuse along their overlap rather than emitting one source per sampled segment. Transport a proposed Lunehymn concentration downstream with flow, dilution at tributaries, loss over distance/time and possible local storage in lakes. Mark reaches silver only above a visible threshold. This gives a meaningful silver reach/floodplain rather than turning an entire river silver because one upstream pixel intersects a line.

Compute crossings from the full physical geometry, never from simplified atlas art. Silver water can increase magical fertility and feed extraction or habitats; land fertility changes only through explicitly defined ecological effects. When the leyline shifts away, the ordinary river stays in place; infusion stops and residual concentration fades under a tunable retention policy. Strong persistent infusion can create a named silver wetland or lake without conflating the two water graphs.

## 6. Feature catalog and placement rules

Represent terrain, fields, paths and entities separately. A tile can simultaneously be a forested floodplain, silver-water reach, grandfield edge and enclave territory. A single giant tile-type enum cannot represent this world.

### A. Terrain and hydrology — the physical substrate

Elevation, slope, mountain chains, passes, plateaus, valleys, soil, vegetation, wetlands, lakes, river reaches, deltas, estuaries, coasts, shelves, reefs and open sea. Climate exposure and rain shadows belong here. Add seasonal flood, drought, snow/ice and coastal-storm masks later; distinguish a temporary hazard from permanent terrain. Natural harbors and navigable crossings are important geographic opportunities.

### B. Magical environment — overlapping fields and networks

Coherence/Dissonance Seeds, active Dissonance Cores, leylines, two-line Convergences, three-plus-line Basins, fixed Sacred Sites, silver Lunehymn rivers/lakes, Resonance Anchor influence and dangerous rifts. Store Coherence, magical fertility and affinity separately. A magical rift need not be a traversable physical canyon; its physical and magical tags are independent.

### C. Resources and grandfields — extraction geography

Ordinary scattered deposits support local play. A Resource Grandfield is a sparse multi-cell footprint with one dominant resource, graded density toward its edge, extraction access and a required dedicated Outpost. Keep Building Material and Vital Resource categories, exact resource ID and yield source separate. Do not stack the same grandfield bonus once per occupied cell. The vault's tenfold production language is a balance target to validate, not a reason to multiply every affected modifier together without a cap policy.

Grandfields follow substrate requirements: minerals/crystals with compatible geology, vegetation resources with suitable habitat, magical resources with flow/affinity. Correlation is useful, but do not put every useful resource on every nexus/Sacred Site. Define finite stock versus renewable flow per resource rather than globally. Reserve viable access and competing opportunities instead of placing every valuable field beside the starting Capital.

### D. Settlements, enclaves and inhabited places

Capital, towns, Major Settlements, Outposts, ruins and enclave footprints are entities over terrain. Enclave placement considers its function, binding, founding wound, supply access and Age. Use the vault's ten functional families: Agromagical, Militant, Auric, Weaver, Domestication, Trading, Industrious, Regal, Indulgent and Esoteric. Examples: a trading enclave at a pass/estuary; agromagical enclave near dual-fertile land; industrious enclave near extraction and logistics; esoteric enclave near a controlled magical anomaly.

Do not fully populate every category in Age 0. The vault describes few enclaves initially, with tribes and early survival-oriented Militant/Trading enclaves; famine survivors can establish Agromagical enclaves in Renewal. Generate candidate sites/latent histories early, then let Age and events establish institutions. Stable geography can therefore feel alive without teleporting cities every Age.

### E. Trade, transport and strategic geography

A **Trade Nexus** is a rare geography-backed hub candidate: a sheltered estuary/harbor, traversable mountain pass, major crossing or meeting of viable routes. It is not every intersection and not automatically every magical Basin. Distinguish potential nexus terrain from an operating hub with infrastructure. Trade Nodes are controllable/fortifiable logistics points; Trade Routes are edges between nodes, hubs and settlements.

Route cost considers terrain, physical crossings, safety, infrastructure and Coherence bonuses. The vault specifically says trade-route disruption occurs through blockade or pillage at a Trade Node. Therefore an Age's leyline shift changes efficiency/preferred routes, not automatic destruction of trade connectivity. Any new direct route-cutting mechanics need an explicit design change. Record alternate paths and chokepoints; do not make all quadrants depend on one unavoidable bridge.

### F. Landmarks, discovery and story sites

Separate **landmark** (recognizable place) from **discovery site** (interaction/content). A landmark may host several discoveries across Ages. Categories include Memory Fields, Old World Remnants, ruins, observatories, caves, buried structures, named natural wonders, ritual sites, Crisis Wonders and later megastructure ruins. Store unseen → spotted → surveyed → explored → resolved/depleted states separately from truth knowledge and story prerequisites.

A visible ruin should not reveal its late-game truth or full name prematurely. A cleared location can host a later event without regenerating its terrain. Use unique IDs and protected placement rules for canon locations; generic variants may repeat.

### G. Threats and living ecology

Atonalis nests, migratory creatures, hunting ranges, spawning habitats, corruption scars, plague vectors and contested frontiers. Separate spawn potential from actual occupants. Migration follows the same traversable geography and Sacred Site avoidance rules. Seeded variation should create interpretable habitats and travel risks, not random encounters unrelated to place.

### H. Information and history

Fog of war, survey accuracy, last observed Age, old leyline routes, former silver reaches, abandoned roads, battle sites and prior ownership. Map knowledge becomes stale when the magical network moves. Preserve an optional previous-Age overlay so the player can explain why an old trading city declined.

### Placement priority and conflicts

World topology → protected canonical anchors/Sacred Sites → global terrain/drainage → Macro Biome interiors and intersection reconciliation → seeds/magic → resource distributions → habitable sites and nexus opportunities → discoveries/hazards → initial inhabitants. Some stages iterate: reserved sites constrain terrain before their final footprints are placed.

Features use explicit compatibility/exclusion rules and budgets per quadrant/area. A Sacred Site excludes ordinary active Atonalis occupation; a grandfield can overlap a river only for compatible extraction types; settlements avoid inaccessible cliffs/flood channels unless authored for them. Keep scarcity, minimum separation and reachability diagnostics visible to content authors. No blanket guarantee that every quadrant contains every feature.

## 7. Age transition without regenerating the continent

1. Snapshot old magic network and inhabited/anchored sites. Freeze gameplay mutation at a controlled transition boundary.
2. Advance Grand Thread Ring parameters deterministically from world seed, Age, reset-stage context and authored events; do not reseed all geography.
3. Re-route seed-sourced leyline families subject to Sacred Site protection and explicit Anchor constraints; preserve line identities even when segments change.
4. Recompute intersections, Coherence and magical fertility. Stop old river infusions, introduce new ones and apply the chosen Lunehymn retention model.
5. Update path bonuses, extraction potentials, trade efficiency, habitats and eligible enclave events. Keep existing ownership, buildings, roads and ordinary river polylines.
6. Validate affected regions and commit a versioned change set atomically. A repeated callback must not shift lines or award consequences twice.
7. Present a forecast/transition overlay: old/new line paths, newly silver/dimmed rivers, endangered routes and changed magical fertility. Keep physical fertility unchanged unless an explicit ecological effect applies.

Prototype progression can call this through a debug Age selector before S02 is implemented. Production integration requires the real transition event and the state contracts; save/load remains held. Compute previews without spending resources or mutating the current world.

## 8. Generation pipeline and data boundaries

Proposed pure generation stages: WorldRecipe → QuadrantGraph → SlotAssignment → RegionalHeightClimate → SharedSeamConstraints → DrainageGraph → BiomeRefinement → SeedFields → AgeMagicGraph → Features → InitialSimulationState → ValidationReport. Authored constraints can require bounded feedback between adjacent stages; log why a repair occurred.

Proposed content assets: WorldRecipe, QuadrantDefinition, BiomeRecipe, BiomeVariant, EdgePortSet, LandmarkTemplate, ResourceGrandfieldDefinition and AgeMagicProfile. Runtime plain data: WorldTopology, QuadrantInstance, BiomePlacement, HierarchicalCellId, RiverGraph, LeylineGraph, MagicSource, ScalarField, FeatureInstance and WorldChangeSet. Names are proposals, not existing classes.

Keep render LOD separate from simulation. Mesos own normal economy/travel aggregation; micro cells own physical detail and local edits feeding those aggregates. A single quantity has one authoritative owner. Use chunk meshes/Tilemap batches, pooled feature markers and dirty-region updates; do not tick every visible plant or every tile independently. Cache fields and aggregate summaries with generation/version dependencies. Store generator version, catalog hash, original assignments and player deltas for future persistence; a seed alone is insufficient after generator changes.

## 9. Acceptance tests and remaining choices

Required tests: same seed/version/catalog gives identical output; every required biome appears exactly once; rotations use allowed sets; the intersections have no gaps/overlaps; shared heights and river endpoints agree; mainland and enclosing ocean circuit remain connected with minimum width; no river climbs uphill except an explicit magical override; every child has exactly one parent, including negative coordinates and map edges; aggregate yields equal authoritative children; zoom never changes paths/ownership; two lineages give Convergence and three-plus colocated lineages give Basin; repeated segments never overcount; ordinary water does not move on Age change; silver concentration changes only along reachable downstream water; Sacred Sites persist; Age change commits once; protected sites and generated features satisfy exclusion and reachability rules.

Performance acceptance must be measured on the target device: generation time, memory, worst-case chunk build, panning/zoom frame time, Age recomputation and largest-feature overlays. No timing targets are certified by this document.

Outstanding content decisions: complete Q1–Q7 biome catalogs and per-instance slot counts; attached/offshore role of outer quadrant instances; permitted orientations and reflection rules; detailed world dimensions; macro grouping ratio after prototype; seed influence/flow coefficients; silver-river retention; grandfield depletion; exceptional Sacred Site overrides; whether resets reshuffle geography. These do not block a deterministic Q2/Q5 seam prototype.

## Sources and authority

The user's supplied image and clarifications establish Q1–Q7 quadrants, I intersections, W water, Q2/Q5 example catalogs, rotation/shuffling, the three zoom levels, seed fields, Convergence/Basin counts and silver-river interaction. Those instructions take precedence over earlier assumptions in this draft.

Vault source: [Arcanoria — Map Features](<C:/Arcanoria Master/Arcanoria/Worldbuilding/Rise & Fall, Crisis/Arcanoria.md>) specifies moving leylines, fixed Sacred Sites, settlement/authority rules, grandfields, trade geography and enclave development. [Leylines](<C:/Arcanoria Master/Arcanoria/Worldbuilding/Origin of Magic/Conduits of Magic/Leylines.md>), [Grand Thread Rings](<C:/Arcanoria Master/Arcanoria/Worldbuilding/Origin of Magic/Conduits of Magic/Grand Thread Rings.md>) and [Sacred Site](<C:/Arcanoria Master/Arcanoria/Worldbuilding/World Environment/Landmarks/Sacred Site.md>) are short supporting notes; the detailed operational algorithms above are design proposals, not claims that those notes already specify them.

## Validation performed for this design delivery

The hierarchy proposal was checked over 25,921 parent positions spanning positive and negative coordinates, with 181,447 child-to-parent round trips; a complete three-level group contains 343 distinct micro cells. A sign error in the draft forward transform was corrected before delivery. The final mapping in section 3 matches the validated inverse.

The illustrative browser study was checked for seed-driven Q5 catalog permutation, displayed orientations, meso/micro zoom and Age/field controls. Its 360-pixel layout had no horizontal overflow. This study demonstrates composition, exact cell grouping and example fields; it does not implement the production seam solver, drainage simulation, content-placement rules or Unity integration. The acceptance tests in section 9 and WG01–WG13 remain work to implement. No Unity runtime or EditMode validation is claimed for this documentation change.

## Implementation, September 27, 2026

The generator and the three-scale viewer are built in Unity (uncommitted; roadmap 3.1 lists each WG task's state). How
the design became code, and where it was simplified:

- **Slots and catalogs.** Each same-quadrant block of the stencil is one slot; blocks of a quadrant that only an intersection separates
  form one quadrant instance ("Q5 South-East"). A quadrant's catalog is shared by all its slots. Q2 fills its two slots with
  Violet Grove and Great Expanse in either order; **Q5 has three slots in the stencil, so each world draws three of its
  four biomes** and places and turns them. Q1, Q3, Q4, Q6 and Q7 use placeholder catalogs until their biomes are named.
- **Handmade tiles (the hybrid).** Biomes own hand-drawn tiles (`Resources/World/Tiles/*.txt`, a honeycomb drawing with
  a legend, optional heights and allowed rotations). The generator stamps them at seeded places and rotations inside
  each slot's protected interior (three or more cells from its edge), never touching one another; the rest of the slot
  is procedural, and the intersections plus the slots' buffer bands blend the neighbours.
- **Scale.** Eight meso cells across a stencil cell and an ocean apron of one and a half stencil cells: about 34,000
  meso cells, 237,000 micro hexes and 700 macro aggregates. Generation takes about half a second on the dev PC.
- **Seams.** Instead of explicit edge ports, every slot within reach of a cell's nearest slot lends its recipe, weighted
  by how much nearer it is, so height, relief and moisture are continuous everywhere, including where the second-nearest
  slot changes. The solver keeps facing seam heights within 0.3 (a simplified port check); the intersections carry any remaining
  slope. A mountainous quadrant raises a range along the intersections between its own slots. Ground is chosen by coherent noise,
  so it forms patches and seams interlock.
- **Water.** A gentle continental dome, then priority-flood drainage (ties broken by a seeded hash so water fans across
  flats), lakes of 4 to 80 cells (a larger hollow stays land and drains across; a lake that would cut a slot off the
  mainland stays dry), flow accumulation and rivers. The capital stands in Q1 on the mainland within three cells of
  freshwater (a pond is placed and reported otherwise).
- **Magic.** One leyline family per Coherence Seed, integrated as a curve through the Grand Thread Rings' field (each
  ring turns one way about a centre that orbits a little each Age); its footprint is the cells it crosses. Junctions
  count families over one connected footprint. Silver Lunehymn is carried downstream with dilution and a residue after
  an Age. Sacred Sites are fixed Coherence maxima with a protected radius of two cells.
- **Viewer.** One quad and a shader that finds each pixel's micro hex, meso cell and macro aggregate with the lattice
  arithmetic; borders are true unions of children. Readings blend by zoom: micro, meso, macro (the macro reading
  shows each aggregate's leading biome shaded by each cell's own). Scrolling out of the capital opens the world;
  scrolling in at the closest zoom over the capital returns.

Still open: target-device profiling, the WG12 debug Age selector and transition overlay, grandfields, enclave
candidates and trade nexus sites (WG10), the quadrant catalogs, island roles for outer quadrants (the quadrant `island` flag
turns its intersections to sea, but no quadrant is marked one yet), reflections, and edge ports for rivers and passes as authored data.

### World review follow-up � 2026-09-26

Implemented after review of the current generator:
- Every meso tile has explicit authority, administrative reach and an impassable flag. The capital and independent enclave sites project authority across passable land; water and barriers stop it. Unclaimed land stays wilderness. Discovery does not annex land. Micro children inherit their meso tile's state; this is initial territorial geography, not the future diplomacy/annexation simulation. Existing exploration production rules remain unchanged pending that system.
- Leylines now start in elevated seed sites and take strictly descending adjacent steps to the first lake or ocean. Falling base coherence, ordinary rivers and the moving Grand Thread Rings guide the choice between downhill branches. Elevation is a hard constraint; coherence is a preference, allowing routes through local coherence rises instead of stranding a line. The legacy leylineLength field is retained for serialized compatibility but no longer truncates paths.
- Leyline drift spreads with squared distance falloff over five meso cells, crossing slot seams. It raises coherence and magical fertility and is recomputed each Age. Land fertility remains separate. Tile tooltips show drift and a provisional desirability score (45% land fertility, 35% coherence, 20% magical fertility; zero for water/impassable tiles).
- Seeded wet depressions supplement the priority-flood drainage and lake rules. Authored patches and reserved connections are protected. Moon Hollow, Open Steppe and Golden Meadow now include multi-cell water bodies and banks.
- Added Reedwater Crossing, Sheltered Harbor, Elderwood Grandfield, Duskstone Exposure, Harmonic Observatory, Freshwater Refuge, Independent Watch Community and Traders Rest Enclave. Placement can require freshwater, coast or minimum coherence. Grandfields supply concentrated resource yields; trade sites and enclaves are groundwork for later interactions.
- Added the Authority overlay and ownership/access information to known-tile tooltips. Generator version is now 3; handmade cell content and feature placement rules contribute to the catalog fingerprint.

Validation: standalone C# generation tests and authored tile parsing pass; full game C# compilation passes against Unity assemblies. Unity batch mode reports no valid Editor license, so scene rendering, interaction and editor content validation remain unverified. Do not mark WG10 or diplomacy complete: trade operations, enclave interactions, territory transfer, site narratives and resource balance still need implementation/playtesting.

## Civilization layer and lenses, September 27, 2026

Built on the generator (generator version 4; older saves are refused by the existing version check). Source for every
rule: Arcanoria.md, Map Features, unless marked as a proposal. All numbers live in `World.asset`
(`generation.grandfields/enclaves/threats/nexus` and `settlements`) and are proposals.

| # | Feature (canon) | Built | Code |
|---|---|---|---|
| 1 | Developing Towns: inside Administrative Authority, distance from other settlements | Found on explored ground inside your authority, `spacing` cells from any settlement; extends authority by `townAuthorityRadius`; explores what it walks to | `WorldCivilization.Found/WhyNotFound` |
| 2 | Major Settlements: from towns at enough City Development, within Government Capacity, attuned to a binding, anchors of authority, towns in range answer to them | Promotion at `majorThreshold`, capacity = base + a Capital building (unnamed: capacity stays 1); binding suggested by the ground and cyclable; `majorAuthorityRadius`; `governedBy` | `WorldCivilization.Promote`, `CityDevelopment.SuggestBinding` |
| 3 | Outposts: detached, for extraction/trade/Resonance Anchors, upgraded when incorporated | Hold only their own cell (authority `outpost`); incorporate into a town once your authority touches them | `WorldAuthority`, `WorldCivilization.Incorporate` |
| 4 | City Development: Coherence eases it and is needed to fully develop; roads and a Trade Nexus raise it; geology sets it | Ten named terms over the worked radius (land fertility, freshwater, Coherence, magical fertility, leylines/junctions, trade, grandfields, Sacred ground, minus danger and dissonance) under a Coherence ceiling; settlements grow toward it each seventh and yield per 10 points | `CityDevelopment`, `WorldCivilization.Tick/Yields` |
| 5 | Resource Grandfields: sparse, multi-cell, one resource, need a dedicated Outpost, tenfold as a target | Seeded footprints on compatible ground, densest at the heart; an Outpost on one extracts it once per field (flat yield plus a percent on every producer, capped per resource at `grandfieldCapPercent`, the tenfold target) | `WorldSites.PlaceGrandfields`, `WorldCivilization.Extractions` |
| 6 | Trade Nexus needs gifted geography; Trade Nodes; routes prefer high Coherence along leylines; roads raise adjacent City Development; only blockade/pillage at a node disrupts | Nexus sites (harbor, estuary, pass, confluence; kinds take turns so each world has a mix); roads planned over land preferring Coherence and reusing road; Trade Nodes every `nodeSpacing` cells and at gifted cells; efficiency follows Coherence and leylines and is refreshed each Age, never deleted | `WorldSites.FindNexusSites`, `WorldCivilization.PlanRoad/BuildRoad` |
| 7 | Resonance Anchors raise Coherence and redirect leylines | Built at any non-Capital settlement: Coherence support over `anchorRadius`, and a pull on the downhill leyline walk within `anchorPull`; the current Age is re-committed at once | `WorldMagic.SetAnchors/Reapply` |
| 8 | Units faster on leylines; seers reveal farther; low Coherence is an attrition zone | Expeditions walk the cheapest path from the nearest settlement (leylines x0.6, roads x0.5, dissonance and danger slower); a target on a leyline reveals `leylineRevealBonus` more | `WorldPaths`, `WorldSystem.Journey` |
| 9 | Threats: spawn potential, Atonalis avoid Sacred Sites | Vibrational Fallout Scars at the Dissonance Seeds (Age 0), Atonalis Nests from Age II; danger field with quadratic falloff, zero within two cells of a Sacred Site and halved to five | `WorldSites.PlaceThreats/RecomputeDanger` |
| 10 | Enclaves: ten families, binding, origin wound, Wound Resonance, few at the start (Militant and Trading), Agromagical after the famine, Suzerainty | Age-gated specs placed by function (freshwater, nexus, silver water...), own authority, binding, Wound Resonance and its three states; envoys raise standing to Suzerainty (yields while suzerain) | `WorldSites.PlaceEnclaves`, `WorldCivilization.SendEnvoy` |

Also: Religious Havens (at a Sacred Site or high Vibrational Density; Faith scaled by Coherence), the previous Age's
leylines as a layer, and settlement/road/enclave/threat/nexus markers.

**Lenses** replace the overlays: Normal, Settle, Fertility, Coherence (leylines on), Magic, Authority, Trade,
Grandfields, Danger, Height, Biomes, Composition. Each is tied to City Development: Settle paints the potential a town
would reach on every cell (bright where one may be founded now); the others paint one of its terms, and the hover line
says what the cell adds ("Coherence 64%: +12.8 City Development, ceiling 77"). `WorldLenses` is pure; the renderer
colours it.

**Saved**: settlements, roads and enclaves (`WorldSystem._settlements/_routes/_enclaves`); threats and later-Age
grandfields are re-placed on restore, anchors re-synced.

**Tests**: `WorldCivilizationTests` (12, pure) plus the existing 35; the Unity batch run passes everything except
`GenesisLoopPlayTests.TheAgeOfDesolationPassesIntoTheAgeOfRenewal`, which fails in the Age-story completion flow
(`EventSystemLogic`/`EventVolumeManager`/`AgeProgression` were being edited by another session at the same time).

**Next hooks for other mechanics**: Composure/attrition for armies can read `danger` and `dissonance`; blockade and
pillage act on `TradeRoute.nodes`; diplomacy (S25) extends `Enclave.influence/suzerain`; Ornaments read
`Settlement.agesPresent` and `binding`; the Capital improvement that raises Government Capacity plugs into
`SettlementRules.capacityBuilding`; Religion/Prophets read Sacred Sites and Havens.

## Units, technology-driven Ages and Era Score, September 27, 2026 (later)

- **Units** (`WorldUnits`, `WorldSettings.units`): Wasteland Scout (stamina 6, sight 2, Pathfinder Training, one plus one per
  Hollow Watchpost; the first is given at the Capital when Pathfinder Training is researched), Settler (stamina 3, takes 5 citizens, Pathfinder Training; founds a
  Developing Town, an Outpost or a Religious Haven where it stands and is consumed), Builder (stamina 4, Woodcraft
  Mastery, 3 improvements of 2 Sevenths each). Trained at a settlement for their cost. Expeditions are gone.
- **Travel fatigue**: every cell costs `WorldPaths.StepCost` (terrain `moveCost`, x0.5 on roads, x0.6 on leylines,
  more with dissonance and danger). A unit spends its stamina per Seventh continuously (Cycle.md: a Seventh is four
  Days), stops while time is paused, a story is open or the save menu is up, and keeps the Seventh's pace before
  Horology. Scouts explore the ground around each cell they enter; a leyline widens sight.
- **Hotspots**: known land that yields (its ground, its feature or a grandfield). Only held ground yields its terrain
  now (inside your authority), features yield once found; each improvement level adds half the hotspot's yield
  (`SettlementRules.improvementBonus`, max 3), improved grandfield cells add a share of the field's base yield.
- **The technology tree drives the Age** (`AgeRules.Advance`): Act and crisis beats may be gated by a technology; the
  Age waits at a closed gate and opens at once when it is researched early. Age of Desolation: Act I runs to *Echoes
  of Hunger*, which opens Act II and the famine's first stage (A Brown Auric Peach?) and grants 3 Crisis Advantages;
  Acts II and III keep their sevenths until their technologies exist. The Act I tree (20 technologies) follows the
  owner's sheet; effects without a system yet are Special placeholders.
- **Era Score** (Ages.md): kept per Act by `AgeProgression`; Event Technologies award 2; towns 2, havens 2, outposts
  1, a settled Trade Nexus 3, a Major Settlement 3, a suzerainty 2, landmarks 1 (unique 3), Sacred Sites 2, an
  enclave met 1, a masterwork improvement 1 (proposals, `SettlementRules.era*`). Shown on the lower HUD's sun
  (`EraScoreHud`) and the world view's top bar; the surplus above 12 eases the crisis (`CrisisTuning.eraScore*`).
- **World view**: most of the screen is the map; a top bar (Age, Act, Era Score, the gate technology), a lower-left
  dock (Lens, Layers, Units, scale, Capital), a hover card with everything about a cell (including travel fatigue and
  where the selected unit could go), a selection card with actions, toasts. Left click selects, right click sends.
- **Look**: a dark-fantasy palette in `World.asset` and a grading pass (`WorldRenderer.GradeGround`: contrast,
  saturation, north-west hill-shading, cold peaks, depth-graded water).
- Generator version 5 (older saves are refused). Tests: `AgeGateTests` (3), `WorldUnitTests` (4).

## Map access, authority and claims, September 27, 2026 (later still)

- **The map opens with a technology** (`WorldSettings.mapTechnology`, Pathfinder Training). Before it only the Capital
  is known: M, the banner's button and scrolling out do nothing but raise a notice. Researching it gives the first
  Wasteland Scout at the Capital (`UnitSpec.grantOnUnlock`, once per world, saved in `WorldSystem._granted`).
- **One continuous zoom**: the capital camera (`CameraMovement`) owns the wheel until its widest view; a notch out past
  it opens the world (`WorldView.ZoomOutOfCapital`; over the HUD, where that camera ignores the wheel, the world view
  takes the notch itself). Leaving the world hands the capital back at its widest view. While the world is open the
  capital camera ignores the wheel (the old bug: zooming the map also zoomed the capital).
- **Authority begins at the Capital's own cell** (`capitalAuthorityRadius` 0); the fog begins beside it
  (`startExploreRadius` 0, `startRevealRadius` 1). Explored wilderness bordering your authority is claimed cell by
  cell (`WorldSystem.Claim`, `WorldAuthority.WhyNotClaim`): 15 Food and 10 Elderwood, +10% per cell already claimed
  (`SettlementRules.claimCost`, `claimCostGrowth`; proposals). Claims are saved (`WorldMap.Claims`) and re-applied
  right after the Capital when authority is rebuilt. Towns still extend authority as before.
- **Selection**: clicking a cell walks through its settlement (where units are trained) and then its units; bare
  land opens a card with Claim.
- **Notices** (`NotificationFeed`, look in `Resources/UI/NotificationTheme`): rhombuses from `Prefabs/UI/Notification`
  pop into the capital HUD's `NotificationGrid` (its placeholders hidden) and stack from the bottom. Issues (purple,
  at the bottom, stay until solved): units need orders (count), no research, research stalled for a resource, empty
  council seats while legends wait (count, the legend's portrait), the Age waiting for its technology, land to claim.
  News (green for the world, teal otherwise) waits above until dismissed with a right click (a left click goes to it
  and dismisses it). In the world view a plate says how many notices wait in the capital; clicking it returns.
  The world view's own top bar and toasts are gone; the Age banner shows over both.
- **The Age banner measures the Age by the tree** (`AgeProgression.TreeProgress`, `AgeRules.ActPath`): each Act is an
  equal share; an Act ended by a technology fills with the technologies researched on the path to it (itself and the
  prerequisites no earlier Act covers), an Act with no technology yet with its sevenths.
- Fixed on the way: a blank line in `World.asset` had stopped Unity reading everything after it (the unit list, so
  nothing could be trained); `ContentValidator` now flags an empty unit list or claim cost.

## Micro travel, provisions and unit abilities, September 27, 2026 (latest)

The owner asked for units to travel hex by hex on the micro hexes (the seven inside every meso cell), with the meso
scale kept for reading the world at large; for fatigue, supplies and attrition with camps; and for the groundwork of
abilities that act on a whole meso hex (the first: surveying one). Codex began it (`MicroNavigation` ids, masks on
`WorldTile`, the first `UnitSpec` endurance fields); this finishes and wires it through. All numbers are proposals in
`World.asset` (`units`, `provisions`).

- **The travel grid** (`MicroNavigation`, `MicroGrid`). A micro hex is `cell index x 7 + child` (its place in
  `HexHierarchy.ChildOffsets`); its neighbours follow one fixed pattern (a parent's centre always has residue 0, so the
  lattice is the same around every cell), so the world's graph is never stored twice: about 34,000 cells give 237,000
  hexes. Orders at the **micro** reading go to the hex clicked; at the **meso** (and macro) reading to the cell's
  centre hex, where its sites and settlements stand. When that hex cannot be entered (a lake, a crag), the unit goes to
  the nearest one it can reach, ring by ring up to six hexes away, the cheapest of the nearest ring.
- **What a step costs**: the ground of the hex entered (its cell's terrain `moveCost`; on a seam between two walkable
  lands a rim hex may carry the neighbour's ground, exactly as the renderer paints it, so what a hex looks like is what
  walking it costs; water and impassable ground never spill over); roughness off-road on steep cells (up to +60% from
  `escarpment`); **crags** (generator stage 11: steep escarpments break into hexes no one climbs, never a cell's
  centre, every pair of walkable neighbouring cells keeps a crossing, about 4,000 per world); **fords** where the drawn
  river line passes (x1.5, a great river x2.2; a river line is a wall no step slips past); **roads** hex by hex along
  the drawn road line (x0.5; a road bridges a ford and cuts through crags); then the cell's leylines (x0.6),
  dissonance, danger and weather (the same `CellFactor` as `WorldPaths.StepCost`); and the **slope** between cells:
  +6 per unit of height climbed, +2 descending. `WorldGeometry` holds the river meander and road lines that both the
  renderer and the grid read.
- **Search**: an exact A* (every step costs at least `MicroGrid.MinStep`) with pooled buffers and a stamp instead of
  clearing, and ground components so an unreachable target is refused at once. Real scale, dev PC: grid build 44 ms on
  first use, a 225-hex cross-continent way 20 ms (Dijkstra 51 ms), a 77-hex way 2 ms. Hover previews are cached per
  target and bounded (60,000 hexes; beyond that the card says the way is planned when ordered).
- **Pace**: stamina is spent per micro hex: a Wasteland Scout walks about six hexes of open plain per Seventh, some two
  cells (the world walks about 2.6 times larger than when units stepped cell to cell). A journey of 24 hexes (about
  nine cells) counts as completed (`WorldSystem.JourneySteps`, the Rekindling's "Complete an Expedition").
- **Knowledge per hex**: `WorldTile.microKnownMask` (walked on or beside: every unit knows the hex it walks, scouts the
  hexes within `knowRadius` 1) and `microSurveyMask` (surveyed), both saved. Any hex known makes its cell known (claims
  still need a known cell); a cell whose every enterable hex is surveyed is explored and its sites investigated. The
  micro reading draws this fog per hex: surveyed in full colour, walked muted, glimpsed (unwalked in a known cell) darker.
- **Sight** is in micro hexes: scout 5, settler and builder 3; a leyline adds `leylineRevealBonus` (now 4), a ridge or
  an escarpment's lip 2 (a vantage point); weariness and hunger take one or two away.
- **Provisions** (`WorldUnits.Needs`, `ProvisionRules`, each unit's `UnitSpec` rates):
  - *Rations*: one a Seventh on the march or at work (x0.7 in camp, more in harsh weather). Inside your authority the
    stores refill them (12 a Seventh in a settlement, half elsewhere), paid in food value from the stored food (the most
    perishable first), then from Food. In camp a unit gathers `forageRationsPerSeventh` by the land's fertility (less in
    danger and harsh weather). Scout 12 rations, settler 16 (eats 1.5), builder 10.
  - *Fatigue* 0-100: gained by the travel fatigue walked (x1.2 for scouts and builders, x1.4 for settlers) and by work
    (4-5 a Seventh, x1.5 for a meso-hex survey or building), shed in camp (25 a Seventh, x1.5 in a settlement), about a
    third of that standing idle. Past 40 it slows the unit (half pace at 100). At 100 a walking unit makes camp by
    itself, keeps its road and walks on once rested (30).
  - *Attrition* 0-100: hunger (8 a Seventh, settlers 10), marching or working at 90 fatigue or more (6), harsh weather
    (12 per point above the ordinary, halved in camp), danger (10 at full) and dissonance (5 at full); none of the
    land's in a settlement. Rest while fed heals it (3 a Seventh in camp, 9 in a settlement). It slows the unit (half
    pace at 100); at 100 the unit is lost (`lostAtFullAttrition`) with any citizens it carried; a legend leading it
    returns alone.
  - *Pace* (`WorldUnits.Efficiency`) = weariness x wear x hunger (x0.6), never below 0.15: it scales walking and work.
  - Warnings reach the notices once each until the danger passes (`WorldSystem.UnitNotice`): rations low, starving,
    worn down, failing, exhausted, lost. Starving units are also a standing issue.
- **Camps and self-reliance**: *Make camp* by hand (it stops); *Return for rations* walks to the nearest settlement and
  camps there until rested and full. A unit exploring by itself ranges out to the nearest cell no unit has passed
  over, turns back while its rations still carry it home, rests when weary, and stops for orders when neither the
  stores nor any settlement can feed it.
- **Abilities (groundwork)** (`UnitAbilities`, `AbilityInfo`): each ability names its task, its reach (`Hex`, `Around`
  within a radius, or `Meso`: all seven hexes of the meso hex the unit stands in), its duration and its toll (fatigue
  while working); `WorldSystem.WhyNotWork/Work` start it and `FinishWork` applies its effect. Built on it: **Survey**
  (the hexes within `surveyRadius` 1: at a cell's heart, the whole cell, 1 Seventh), **Survey meso hex**
  (`UnitAbility.SurveyMeso`: the whole meso hex from anywhere in it, 2.5 Sevenths scaled by what is left, x1.5 fatigue;
  Shift + right click at the meso reading sends a scout to sweep a cell on arrival), Forage and Improve (meso reach).
  Adding one: a flag, a task, an entry in `UnitAbilities.All`, its spec numbers and its effect.
- **The view**: the hovered hex glows at the micro reading and the card tells its own ground, crags, fords, road,
  whether a unit walked or surveyed it and what entering it costs; the route line gives hexes, fatigue, Sevenths and
  the rations eaten beyond your authority (the path turns amber when they will not last) and warns when the unit will
  have to rest. The destination hex pulses. Clicking picks the unit drawn under the cursor. The unit card shows
  rations, fatigue, attrition and pace, where it can eat, and its actions (abilities, explore, make or break camp,
  return, halt). A camp shows a tent, a starving or failing unit a red ring; crags are hatched rock.
- **Content fix**: `World.asset` units had no `abilities` at all (settlers could not found towns, builders could not
  improve, scouts could not survey); they now have them, and `ContentValidator` flags a unit whose role lacks its
  ability, or that eats rations but carries none.
- **Saves**: generator version 8 (crags are generated; older saves are refused), tile fields `microKnownMask` and
  `microSurveyMask`, `WorldSystem._unitTimeRemainder`; units carry their hex, rations, fatigue, attrition and camp
  state.
- **Tests**: `WorldUnitTests` (15, pure): the neighbour pattern, micro and meso orders with the nearest-ground
  fallback, crags, exact search, ground, leylines, roads, fords and slopes, rations, attrition and rest, pace, surveys
  by hex and by meso hex, exploring outward, sight, builders, and a generated world whose neighbours all keep a
  crossing.
- **Open (proposals to confirm)**: every number above; a unit lost at 100 attrition; resupply drawing from the stores
  anywhere inside your authority; ways planned over the true ground even through the fog; no line of sight yet; crags
  only on escarpments.

## Expeditions of legends, September 27, 2026 (evening)

The owner replaced the Wasteland Scout and the Settler with **expeditions of legends**; this supersedes what the
sections above say of those two units (the Builder stays a trained worker). The vault has no Expedition note: every
rule and number below is game design and a proposal in `World.asset` (`expeditions`, and the `expedition` unit whose
numbers are per legend).

- **The party** (`Expeditions`, `WorldUnit.leader` = the Director, `companions`, `settlers`): up to four legends. The
  unit it walks as is built from the per-legend unit for its party (`Expeditions.Effective`, cached in
  `WorldSystem.SpecOf`): every legend carries, eats and forages its own rations, settlers eat a share, halve the pace
  and give the Settle ability, each companion adds a tenth to rewards, and the Director's Soul Leitmotif adds one
  strength (Luminance +2 sight, Cindergale x1.15 pace, Crystal x0.8 wear via `UnitSpec.wearMultiplier`, Void x0.8
  rations, Strand x1.25 rewards, Flux x1.3 camp recovery, Resonance x0.75 hardship).
- **Slots** (`WorldSystem.ExpeditionSlots`): one per legend in the field; 1 + 2 per point of Government Capacity + 1
  per Hollow Watchpost (the building that used to allow one more scout).
- **Forming** at a settlement that is not an Outpost (the settlement card: a Director chooser and Form). Companions
  join, leave and take the lead, settlers join (5 citizens, 15 Elderwood; outfitting is free), and the party disbands,
  only in your settlements. A seated legend leaves its seat to set out (not while the seat is on cooldown);
  `LegendLeaderLogic` offers no legend on the road for the council. The first expedition is formed free at Pathfinder
  Training under a legend no seat holds (`_granted` "first-expedition"); the notices ask for one when none is out.
- **Hardship** (`Expeditions.Hardship`, read each Seventh by `LegendProgress` through `WorldSystem.HardshipOf`): wear
  past 20 (3 strain at 100), hunger 2, marching exhausted 1, and outside settlements Dissonance (2 at full) and harsh
  weather (1 per point); the Director carries 1.25x. On the road a legend recovers at 0.5 a Seventh, at home rate
  while the party is camped in one of your settlements.
- **Mishaps** (`Expeditions.MishapRisk`, `RollMishap`; rolled in `WorldSystem.OnSeventh`, drawn from
  `WorldNoise.Hash01` over the world seed, the unit and its `mishapRolls`, so a save replays the same fortune): the
  chance grows with wear from 25, hunger, exhaustion, danger, Dissonance, weather and each Fractured or Spiraling
  member, capped at 60%. Injury, fever, spoiled rations, lost bearings, quarrel, whispers of Dissonance, ambush, and a
  Spiraling companion's desertion; each has a condition and a target (the most strained likeliest, the Director for
  the party's own), strains legends through `LegendProgress.Strain` and wears, tires or unprovisions the party.
- **Losses**: attrition 100 breaks the party (every member +20 strain, settlers lost); a legend lost to Dissonance
  leaves the party and its companions take +8 (`WorldSystem.OnLegendLost`); a party left without a legend is lost.
- **Tests**: `ExpeditionTests` (pure), `ExpeditionPlayTests` (the scene), `WorldViewPlayTests` (the first expedition).

## Territorial pull, Administrative Capacity and beauty, September 27, 2026 (night)

Asked for: Administrative Authority that grows by itself through the political pull of whatever you have built, most
from the Capital, then trade, then towns, Outposts and grandfields, each holding at most so many cells; easy ground
adopted before mountains, fertile and well-made land first; more land harder to manage, so tall play pays as much as
wide play, with the point where one overtakes the other visible; a Beauty lens. Built as one scalable system
(`WorldTerritory`, `WorldBeauty`) that politics and rival civilizations can read and extend. Every number is a proposal
in `World.asset` (`settlements.territory`, terrain `governance`/`beauty`, feature `beauty`).

- **Seats** (`SeatKind`, `TerritoryRules.seats`, strongest first): Capital (pull 1, reach 9, holds 30 cells, adopts
  1/Seventh), Major Settlement (0.8, 8, 36, 0.6), a settled Trade Nexus (0.6, halved while cut off from the Capital's
  roads), Developing Town (0.55, 5, 16, 0.35), grandfield extraction (0.45, prefers its own field), Religious Haven,
  Trade Nodes of connected roads (0.4, 3, 4) and Outposts (0.4, 3, 4, a detached pocket). A Resonance Anchor adds 25%
  pull and 1 reach. Enclaves pull too (a rival pull your society will not adopt against; a Suzerainty stops it).
- **Pull** fades linearly with **governance travel**: each cell's governance difficulty (`TerrainSpec.governance`: plains
  1, woodland 1.3, foothills 1.5, marsh 1.8, highlands 2.2, rift scar 2; peaks and water never), plus cliffs
  (escarpment), dissonance and danger; roads halve it and river valleys ease it (x0.85). A cell's pull is the
  strongest seat's plus 25% of the others'. Pull crosses only wilderness and your own land.
- **Passive adoption** (`WorldTerritory.Tick`, each Seventh once the map is open): every seat with room brings in its
  own best candidate at its own pace. A candidate is wilderness bordering the seat's land, known (or beside a
  settlement), pulled at 0.2 or more and not pulled harder by a rival. Society picks by pull x **priority**
  (`WorldTerritory.Priority`): fertility, freshwater, Coherence, beauty, grandfields (double for the extracting seat),
  sites and surveyed ground first; danger, dissonance and hard ground last. Land held by an Outpost's pull stays
  detached (`WorldAuthority.Outpost`). A town answers to the Major Settlement whose pull reaches it strongest.
  Settlements now found with a small core (`townAuthorityRadius` 1, `majorAuthorityRadius` 2); the pull does the rest.
- **Administrative Capacity vs load** (`WorldTerritory.Realm` → `RealmReport`): capacity = 12 (the Capital) + 3 per
  Government Capacity + each seat's capacity (towns 4 and Majors 10, scaled x0.5 at City Development 0 to x1.5 at
  100) + 1 per settlement on the road network + 10 x (held land's Coherence - 0.4) + technologies/buildings listed in
  `TerritoryRules` (none yet) + the legend answering for the council's "governance" area (4; half through a related
  area). Each held cell's load = difficulty x (1 + 0.08 x governance travel to the nearest seat), eased by Coherence and
  beauty; a settlement's own cell 0.25. **Strain** = load / capacity.
- **Wide vs tall**: efficiency is 100% up to strain 0.8 (comfort), then falls 0.9 per unit (floor 40%) and scales all
  the map's flat yields and City Development growth. `comfortCells` (horizontal favoured below), `breakEvenCells` (the
  strain, 0.956, where N x efficiency peaks: more land lowers total output past it; vertical favoured) and `stopCells`
  (1.1: society stops adopting) are reported with the vertical lever (capacity per +10 City Development everywhere).
  At real scale the Capital alone is comfortable to ~15 cells, breaks even ~18 and stops ~21; towns, development,
  roads, Coherence and Government Capacity move all three. Past 1.4 (collapse) the weakest-held adopted land drifts
  back to the wilderness at 0.5 cells a Seventh.
- **Border policy** (saved; the world view's Realm panel): Expand (adopt to over strain), Measured (default: stop at
  the break-even), Hold (adopt nothing; claims still work). Claims remain and count against capacity.
- **Beauty** (`WorldBeauty`, -1 hideous to +1 sublime, `TerrainSpec.beauty` and `FeatureSpec.beauty` plus water in
  view, Sacred ground, silver water, leyline lights, vistas, Coherence, minus dissonance and danger): adoption priority
  (0.8 per unit), a City Development term (`beautyWeight` 6), held land yields +/-20% (`beautyWork`), load eased 15%.
- **UI**: lenses Beauty and Territorial pull (next adoptions bright, rival pull red); hover rows Beauty and Pull; the
  Realm dock button and panel (ledger, capacity sources, seats held/max, next adoptions, policy buttons); settlement
  and wilderness cards show pull; an "Administration overextended" issue in the notices.
- **For other systems**: `GameValues` domains `territory` (all/adopted/claimed), `admin_capacity`, `admin_strain`,
  `admin_efficiency`, `expansion` (horizontal/balanced/vertical/overextended: 1 while favoured);
  `WorldSystem.Realm`, `NextAdoptions`, `AdministrationContext` (add capacity sources there). Rival civilizations plug in
  as seats with their own authority id.
- Save schema: `WorldSystem._adopted _adoptionProgress _driftProgress _borderPolicy` (saves from before do not load,
  like earlier schema changes). Tests: `WorldTerritoryTests` (11). Rebuild at 34k cells ~5 ms; one adoption ~6 ms.

## Resource sites, harvests, seeds and borders, September 27, 2026 (late night)

Asked for: resources that make tiles worth having (the smaller cousins of Resource Grandfields), identified only by a
survey, that raise the land's value and make their neighbours fairer or uglier and more or less coherent (an
Emberwhisper spire is beautiful but unsettles Coherence); a proposal of 20 to populate the map; seeds (highland rice)
carried to fertile land you hold; expeditions harvesting them as cargo and valuables without claiming the tile
(grievances when the ground belongs to someone); and a default-view outline of each civilization's borders in its
own colour (yours a faint gold). Built as `WorldResources` (pure, `WorldResourcesTests`) and
`WorldSystem.Resources.cs`; every number, name and line of text is a proposal in `World.asset`
(`generation.resourceSites`).

- **Placement** (`WorldResources.PlaceAge`, with the grandfields each Age): each kind arrives in its Age, `count`
  patches of up to `size` cells, `spacing` apart, `minDistance` steps from the Capital, on its terrains and biomes,
  never on water, peaks, features, grandfields, settlements or enclaves. Rules can ask for nearby terrain (highland
  rice within 2 cells of peaks), moisture, fertility, a Coherence window, dissonance, freshwater, silver water or a
  leyline. Placed sites are saved (`WorldSystem._resourceSites`), so a site never moves after a load.
- **Knowledge**: a site shows only its kind once its cell is known ("Unidentified flora", with a hint such as "a glow
  among the leaves"); rays of Aetherlight and Emberwhisper spires show from afar. A survey that explores one of its
  cells identifies it (a notice lists its land value, yields and harvest), awards discovery fragments, and the first
  expedition to identify certain kinds is offered an event line.
- **Land value** (identified only): City Development term *Resources* = 1.5 x the land value of each site a settlement
  works (each site once, capped at +/-18); adoption priority +0.12 per point, so society reaches for valuable land
  first. Blights (negative land value) push both down. Identified sites yield while their cell is yours or your
  Outpost's (their yields shared among a patch's cells, scaled by improvement, beauty and administration).
- **Neighbours** (`WorldResources.Refresh`, before beauty and authority in every rebuild): beauty of its own cell plus
  an aura that fades over `auraRadius`; a Coherence aura handed to the magic as a per-cell shift (`WorldMagic.SetShifts`,
  recomputed without re-routing leylines); a fertility aura added to land fertility (taken back before it is lent
  again). The Resources lens colours identified sites, sighted ones grey, and the land around green where sites help
  and red where they spoil it.
- **Harvest** (expedition ability, 1 Seventh): once an Age per site, an identified site's harvest goes into the packs
  (40 cargo per legend; seeds weigh nothing; the party's reward multiplier applies). The tile is not claimed. In the
  wilderness or on your own land nothing else happens; on ground an enclave or independent claim holds, that holder's
  grievances rise (5-15 per harvest), an enclave's standing with you falls by as much (below 75 it casts off your
  Suzerainty), grievances fade 0.5 a Seventh, and an envoy answers them before it raises your standing. Cargo is
  unloaded into your stores in any of your settlements.
- **Seeds and planting** (expedition ability, 1.5 Sevenths): crops and silverreed give seeds with their
  harvest (glimmerfern did until it became a sterile bloom). Planted on surveyed, fertile ground you hold (each kind's fertility and Coherence minimum), they become a
  planted patch worth its `plantedShare` of a wild one (yields x the cell's fertility / 0.5); some change the soil
  (earth-beans +8% fertility, feral peaches -6%). Plantings are saved.
- **Borders**: every held cell is outlined where it meets another holder or the wilderness, in the holder's colour
  (`settlements.borderColor`, a faint gold for you; each enclave its own; independent claims amber), at every lens and
  zoom (`WorldRenderer.RefreshOwners`, `_OwnerTex` in `WorldTerrain.shader`).
- **New usable resources**: Highland Rice (Stored Food, food value 1.2, spoils 0.4% a Seventh), and a new *Valuables*
  section: Sky Glass, Silverreed, Lumenwool (placeholder icons borrowed from existing resources).

### The 20 resource sites (proposal)

| Site | Kind | Age | Where | Land value | Neighbours | Harvest (per Age) |
|---|---|---|---|---|---|---|
| Highland Rice Terraces | crop | 0 | highlands, foothills, mesas within 2 of peaks, moist | +3 | fairer | 20 Highland Rice + seeds |
| Wild Earth-Bean Tangle | crop | 0 | plains and steppes, fertile | +2 | +fertility | 24 Earth-Beans + seeds (enrich soil) |
| Feral Auric Peach Trees | crop | 0 | auric meadow/copse, orchards | +3 | fairer, -fertility | 18 Dried Auric Peaches + seeds (drain soil) |
| Bitter Root Hollow | crop | 0 | marsh, bog, woodland, taiga | +1.5 | - | 22 Bitter Roots + seeds |
| Deep-Rooted Grain Stand | crop | I | plains and steppes by fresh water | +3 | - | 16 Deep-Rooted Grain + seeds |
| Lesser Glimmerfern Grove | bloom (was flora; sterile since the Eleos Blooms section) | 0 | beside Forsaken Flowers or on hurtful ground | +4 | much fairer, +Coherence | 12 Glimmerfern |
| Elderwood Sentinel | flora | 0 | forests | +2 | fairer, +Coherence | 25 Elderwood |
| Silverreed Beds | flora | I | wet ground by fresh water and silver or a leyline | +3 | fairer, +Coherence | 14 Silverreed + seeds |
| Hollow Thornbriar | blight | I | rift scar, ruins, orchards, ash; wounded ground | -3 | uglier, -Coherence, -fertility | nothing |
| Lanternback Grazers | Pure Light fauna | 0 | plains, steppes, meadows; Coherence 0.3+ | +3 | fairer | 10 Lumenwool (yields Aetherlight too) |
| Ember Salamander Den | Pure Light fauna | I | highlands, mesas, rift scar, ash | +2 | -Coherence | 6 Emberwhisper |
| Moonveil Moths | Pure Light fauna | II | moonlit groves, violet glades, marsh | +3 | fairer, +Coherence | 6 Lunehymn |
| Choir Cicada Swarm | Pure Light blight | I | orchards, meadows, plains | -2 | -fertility (2 cells), -Coherence | nothing |
| Rays of Aetherlight | light | 0 | anywhere, Coherence 0.45+; seen from afar | +5 | fairer, +Coherence, +fertility (2 cells) | 8 Aetherlight |
| Lunehymn Well | water | 0 | groves, woods, marsh, taiga; Coherence 0.3+ | +5 | fairer, +Coherence (2 cells) | 10 Lunehymn |
| Emberwhisper Spire | mineral | I | highlands, foothills, mesas, rift scar; seen from afar | +4 | much fairer, **-Coherence (2 cells)** | 10 Emberwhisper |
| Duskstone Outcrop | mineral | 0 | highlands, foothills, ruins, scrub | +2 | - | 25 Duskstone |
| Skyglass Shards | mineral | I | ash, rift scar, ruins, scrub | +3 | - | 8 Sky Glass |
| Sour Tar Seep | blight | 0 | ash, scar, ruins, marsh, bog; out of tune | -4 | much uglier, -Coherence, -fertility (2 cells) | nothing |
| Resonant Hot Spring | water | 0 | highlands, foothills, taiga, woods | +4 | fairer, +Coherence, +fertility | nothing (yields Faith) |

Event lines (`Events/Expeditions.ink`, placeholder prose): *The Terraces No One Planted* (highland rice), *The Moonlit
Vigil* (glimmerfern; the vault's courtship rite), *Lanterns in the Grass* (Lanternback Grazers), *The Whispering
Spire* (Emberwhisper spire).

- Save schema: `WorldSystem._plantings _grievances _resourceSites`, `WorldUnit.cargo/seeds` (optional fields), tile
  `harvestedAge`. Saves from before do not load, like earlier schema changes. `ContentValidator` checks every site's
  resources, terrains, event lines and that a site with no harvest says why.
- Tests: `WorldResourcesTests` (placement by Age and determinism, knowledge, land value, auras and idempotent refresh,
  harvest, grievances, planting, save round trip, and the real catalog placed on three real worlds).

## Settling hex by hex, and surveys, September 27, 2026 (latest)

Owner direction: land is acquired one micro hex at a time and takes longer; society's own adoption depends on the
Capital's population (nothing below 25 citizens, likelier the more there are); an expedition explores a cell once it
has seen all seven hexes, or, sent to survey a cell, walks each of the seven, and a survey is likelier to trigger events
and find spare resources than passing by; the survey should be intuitive. Every number below is a proposal (World.asset
defaults, `TerritoryRules` and `ExpeditionSettings`).

- **Settling** (`WorldTerritory`): `WorldTile.microHeldMask` (saved, optional) holds the settled hexes of a wilderness
  cell. `NextHex` picks the unsettled hex touching the most held ground (neighbour cells of the authority and the cell's
  settled hexes), so land fills in from the border; crags come with the rest (`FullySettled`). A cell fully settled
  joins `Adopted` (or `Claims`). `Candidates` puts begun cells first (most settled), so a seat finishes one cell before
  starting another. Drift clears a lost cell's mask.
- **Pace**: `SeatSpec.adoptPerSeventh` is now micro hexes per Seventh at full population (the Capital 1: a cell in 7
  Sevenths instead of 1). `PopulationShare`: 0 below `adoptionMinPopulation` (25), `adoptionLeastShare` (0.2) at it,
  rising to 1 at `adoptionFullPopulation` (100); `RealmContext.population` comes from `PopGrowthLogic.population` (-1:
  not counted). With a roll (WorldSystem: `WorldNoise` stream "territory-adoption", saved counter `_adoptionRolls`) the
  pace is a chance rolled once per seat per whole Seventh; without one (tests) the expected hexes accumulate.
- **Claims**: paying puts the cell in `WorldMap.Claiming` (saved `WorldSystem._claiming`, `_claimProgress`);
  `AdvanceClaims` settles one hex of each every `claimSeventhsPerHex` (0.6) and moves it to `Claims` when whole. Pending
  claims count toward the claim cost growth; society does not adopt a cell being claimed.
- **Passing exploration** (`WorldSystem.Look`): a cell whose every open hex has been seen up close
  (`WorldMap.FullySeen`: known or surveyed, crags aside) is explored at once, its sites investigated, unless a party
  was sent to survey it. `MicroNavigation.Surveyed` now means the hex's survey bit only (explored in passing is not
  surveyed), so a survey can still follow.
- **Surveys** (`WorldSystem.SurveyCell`, `WorldUnit.surveying/surveyCell`, optional save fields): the party walks to
  the nearest hex still to survey (`UnitAbilities.CellTargets`), works `HexSevenths` there (`mesoSurveySevenths` / 7),
  and goes on; when none is left the cell is explored (if it was not) and the survey rolls its finds. Any other order
  (go, halt, camp by hand, return, retreat, auto-explore, going missing) ends it. The old "survey around" task is kept
  only for saves made mid-task.
- **Finds** (`Expeditions.RollSurvey`, `Cache`): deterministic per cell (streams "passing-finds" and "survey-finds").
  Events 6% passing / 30% surveyed; spare resources 8% / 40% (the cell's forage x2.5, or its yields for 90 s, times the
  party's reward multiplier, straight into the stores). Default events (`DefaultSurveyFinds`, no vault source): old
  waymarks (reveal 3), a hidden spring (rations refilled, -25 fatigue), traces of those before (2 Lyrical Fragments of
  discovery each); a `Story` find plays an ink knot with the party cast.
- **UI**: unit card *Survey this meso hex* / *Survey another meso hex* (then click; Esc cancels) / *Stop surveying*,
  Shift + right click surveys the cell, a selected cell's card offers *Survey it with* the nearest expedition; hover
  shows hexes seen or surveyed and settling progress; settled hexes are dots in the border colour (gold ring and dots
  for a claim); the Realm panel says when adoption waits for citizens.
- Saves: new fields are `[SaveOptionalField]`, and `SaveStateCodec.Restore` now lets a system's or tile's optional
  field be missing, so this change by itself does not break older saves. Tests: `WorldTerritoryTests` (15),
  `ExpeditionTests` (survey rolls, caches), `WorldUnitTests` (cell survey targets), `SurveyPlayTests`,
  `TerritoryPlayTests` (population gate, a cell settled hex by hex).

## Cover and ordinary animals, September 27, 2026 (latest)

Asked for: regular animals beside the Pure Light ones, and features like deep forests that hold intriguing things of
their own, block the view and make exploring harder while being useful for production, several of that kind. Built as
`WorldCover` (pure, `WorldCoverTests`); every number, name and line is a proposal in `World.asset`
(`generation.covers`, `generation.resourceSites`).

- **Cover** (`CoverSpec`, `WorldCover.Place`, once with the world at Age 0; the same seed and catalog give the same
  patches, so nothing is saved): blob-shaped patches of `minSize`-`maxSize` cells on their terrains, `spacing` apart,
  never within 3 cells of the capital, on settlements or enclaves. Each patch copies its effects onto its cells
  (`WorldTile.cover/concealed/coverSight/coverTravel/coverSurvey/coverFinds/coverHardship`).
- **Exploring it**: concealing cover blocks line of sight (`WorldCover.Visible`: a party sees a forest's edge, not what
  lies beyond, unless it stands on high ground); from inside, sight is capped (1-3 micro hexes); travel is multiplied
  (in `MicroGrid.CellFactor`, so micro routes and meso paths agree); surveys take longer (`UnitAbilities.Duration`); some
  cover wears parties down (attrition per Seventh, `UnitSurroundings.hardship`); survey events and spare finds are
  likelier inside (`Expeditions.RollSurvey` odds, capped at 90%). Features and resource sites inside concealing cover
  stay hidden (no marker, no name, "Something unseen" in the beauty breakdown) until the cell is explored.
- **Holding it**: held cells yield (`WorldCover.YieldsOf`, in land yields and hotspot improvement), cover adds to the
  ground's forage, adds beauty, and multiplies governance difficulty (dense ground is harder to administer).
- **UI**: cover tints the ground, mottled hex by hex; the hover card has a Cover row (effects, yields, "what stands
  inside is hidden"), travel fatigue names it, and an unwalked forest reads "Deep Forest" instead of "Unknown wilderness".

| Cover | Ground | Sight | Travel | Surveys | Wear | Finds | Held, per cell | Forage |
|---|---|---|---|---|---|---|---|---|
| Deep Forest | woodland, taiga, violet wood, auric copse | blocked, 1 inside | x1.6 | x1.5 | - | x1.6 | Elderwood, Game Meat | Game Meat 3, Bitter Roots 1 |
| Mistfen | marsh, bog | blocked, 1 inside | x1.8 | x1.6 | 3 | x1.4 | Peat | River Fish 2 |
| Reed Sea | plains, steppes | blocked, 2 inside | x1.3 | x1.3 | - | x1.3 | Game Meat, Hides | Game Meat 2, Earth-Beans 2 |
| Canyon Maze | highlands, foothills, mesas, rift scar | blocked, 2 inside | x2 | x1.4 | 1 | x1.6 | Duskstone | - |
| Bramble Thicket | scrub, ruins, orchards | open, 3 inside | x1.8 | x1.2 | 1.5 | x1.2 | Bitter Roots | Bitter Roots 3, Wild Honey 1 |
| Ashen Haze | golden ash, rift scar | blocked, 1 inside | x1.3 | x1.5 | 4 | x2 | Sky Glass | - |

- **Ordinary animals** (resource sites, harvested like any other): Steppe Aurochs Herd (+3, Game Meat and Hides, manure
  +fertility), Highland Ibex (+2, near peaks), Taiga Elk (+2.5), River Trout Run (+3, by fresh water), Wild Bee Hollow
  (+2.5, Wild Honey, pollination +fertility over 2 cells), Bog Eel Pools (+1.5), Wild Boar Sounder (+1.5, roots up
  fields: -fertility), Grey Wolf Pack (-1.5 from Age I: takes calves; hunted for Hides).
- **Sites found only inside cover** (`ResourceSiteSpec.covers`): Heartwood Hollow (Deep Forest; +4, +Coherence,
  Elderwood and Glimmerfern), Drowned Cache (Mistfen; Duskstone and Sky Glass), Echo Vault (Canyon Maze; +4, yields
  Research, cannot be carried off), Ash-Buried Relics (Ashen Haze; Sky Glass and Duskstone).
- **New resources**: Game Meat (food value 2, spoils 6%), River Fish (1.2, 7%), Wild Honey (1.5, never spoils) in
  Stored Food; Hides and Peat in Valuables (borrowed icons).
- Measured on three real worlds: 12-24 patches of each cover (Deep Forest ~10 cells a patch; Mistfen patches stay small,
  ~2 cells, because marsh ground is scattered); every one of the 32 resource sites places its full count.
- The catalog fingerprint includes cover, so a save from before it names a different world. Tests: `WorldCoverTests`
  (placement and determinism, line of sight, sight from inside, travel, hardship, find odds, hidden sites, yields,
  forage and governance, and the real catalog).

## Desirability, outskirt tributaries and districts, September 27, 2026 (latest)

Owner direction: housing develops better where people want to live (beauty, high Coherence, magical and land
fertility, leylines, the surrounding hexes); two ways to settle, minor and major civilization hubs: outskirt
tributaries serve an existing settlement (the Capital's outskirt towns), lead back to it, help make trade routes, stand
as nodes, build roads and raise administrative authority nearby without being full settlements, and are built only
inside controlled territory, while settlers found independent Outposts and towns, beyond it too. Tributaries unlock at
The Rekindling, begin as generalists and can be upgraded to one specialised district (defence, barracks, science...);
districts gain from adjacency to one another and to what lies near them, never stand right beside one another, but
their radii connect so one meso hex stands for the whole outskirt district. Every number below is a proposal
(`SettlementRules.desirability`, `SettlementRules.tributaries`; World.asset takes the code defaults until saved).

- **Desirability** (`WorldDesirability`, `DesirabilityRules`): a cell's own appeal is the weighted mean of beauty
  (rescaled 0-1), Coherence, magical fertility, land fertility and leylines (a Basin 1, a Convergence 0.85, a line 0.7,
  else 0.6 x leyline influence; weights 0.2/0.25/0.15/0.25/0.15), blended 40% with the mean appeal of the six land hexes
  around it, less 0.4 x danger and 0.5 x dissonance, clamped 0-1 (0 on water and impassable ground). Not
  `WorldTile.Desirability`, the generator's raw reading used for enclave placement. `GrowthFactor`: every settlement's
  City Development grows (and falls) x0.5 on the least desirable ground to x1.6 on the most. New Desirability lens
  (shared 0-100% scale; cells where a tributary could be raised now are lifted toward white); the land card shows it.
- **Tributaries** (`SettlementKind.Tributary`, `WorldTributaries`; `Settlement.parent` = its hub, `Settlement.district`,
  both `[SaveOptionalField]`): raised by `WorldSystem.RaiseTributary(tile, hub)` from a held cell's card, one action per
  hub in reach. Rules: The Rekindling researched; known, dry, passable land inside your Administrative Authority,
  nothing on it, no settlement or enclave within 1 cell (`spacing` 2); a hub (Capital, Major Settlement, Developing Town
  or Religious Haven, not detached) within `hubReach` 3 with a free place: Capital and Major 3, Town and Haven 1, +1 per
  25 City Development. Cost 40 Food + 20 Elderwood, +15% per tributary standing. Founded with a road to its hub
  (`WorldCivilization.BuildRoad`, so it joins the network through its hub and other roads may end on it), its own cell a
  Trade Node, radius 1 explored, named "<hub> Outskirts I, II...". Independent settlements keep only the tributary
  spacing from a tributary. It answers to its hub, forms no expeditions (unless its district outfits them), holds no
  Anchor, is never promoted, is not counted as a joined settlement for network capacity (its seat counts instead).
- **Seat of pull**: `SeatKind.Tributary` (strength 0.45, reach 3.5, 5 cells, 0.2 hexes/Seventh, capacity 1.5 scaled by
  development), times its district's pull; reach +1 at pull x1.5 or more.
- **Growth**: toward 100 x its desirability, never more than `aboveHub` 15 above its hub's City Development, at 0.2 a
  Seventh x the desirability pace (ticked after the hubs). It lends its hub City Development (`DevelopmentTerm.Outskirts`:
  each district's `hubDevelopment` x adjacency, full from 40 development; at most 15 per hub).
- **Districts** (`DistrictSpec`, `TributaryRules.districts`): the generalist Outskirt Hamlet (housing 2 per 10
  development for the Capital's people through `PopGrowthLogic.SetBaseHousing("Outskirt tributaries")`, a little Food,
  +2 hub development) and seven upgrades: Bastion (defence: wards off 45% of the danger within 2 cells, that land never
  drifts away, pull +35%), Barracks (an expedition slot, two at adjacency +100%; outfits expeditions like a town),
  Lyceum (Research 0.03/s per 10 development), Market Quarter (+6 hub development, capacity 1.5, some Food and Research),
  Granary Fields (Food 0.08), Sanctum (Faith 0.04, +2 hub development), Artisan Quarter (Elderwood 0.04, Duskstone 0.02).
  Each has a minimum development (5-15) and a cost; the first upgrade pays the price, a later change x1.5 and keeps 50%
  of the development, a return to the hamlet is free (and keeps 50%).
- **Adjacency** (`AdjacencyRule`, `WorldTributaries.Adjacency`): per district, shares of its effect per unit of a
  source read within `adjacencyRadius` 1: linked districts (tributaries within `linkDistance` 2, whose radii touch; a
  named district or any), a hub within 2, freshwater, coast, a leyline, a junction, mean Coherence / magical / land
  fertility / beauty / dissonance, high ground, cover, a grandfield, identified resource sites, another road than its
  own, a Trade Nexus, Sacred ground, danger. Effects are x (1 + adjacency), adjacency within -50%..+150%. Clashes: a
  Barracks lowers a Lyceum (-25%) and a Lyceum a Barracks (-20%), an Artisan Quarter the hamlets (-10%), danger the
  Granary (-40%). A Bastion reads the raw threat, a Granary the danger left after the watch, so a Bastion beside the
  fields shields them. The tributary card lists every district with its adjacency on that ground, best first.
- **Map**: a soft glow in the district's colour over its radius (linked districts run together into one quarter) and a
  dot (generalist) or diamond (district) at its heart. Hub cards show "Tributaries n of m"; the expedition section shows
  only where parties can form.
- Content check: the technology, costs, yields, the generalist and district names in adjacency rules
  (`ContentValidator`). Tests: `WorldTributaryTests` (6: desirability, placement, growth and hub development, district
  upgrades, adjacency and links, the Bastion's watch); `WorldCivilizationTests` growth now expects the desirability pace.
- Open: a tributary cut off when its hub is lost keeps standing (no hub: no cap, no hub development); districts on
  enclave borders do not raise grievances; no events are cast on districts yet; the numbers need a balance pass against
  the Capital's own housing and Research.

### Follow-up: districts by Enclave category, and orphaned tributaries (same day)

Owner direction: one district for each Enclave category (Enclave.md, "The Horizontal Worldbuilding of Enclaves": the ten
categories and their activities), "fully Arcanoria"; tributaries whose hub is lost rejoin the nearest hub.

- The seven districts above are folded into the categories (Bastion + Barracks -> Militant, Lyceum + Sanctum -> Auric,
  Market Quarter -> Trading, Granary Fields -> Agromagical, Artisan Quarter -> Industrious) and five are new (Weaver,
  Domestication, Regal, Indulgent, Esoteric); the Outskirt Hamlet stays the generalist. `DistrictSpec.enclave` names the
  category, `role` repeats its activities. Ids are the category in lower case; an unknown saved id reads as the hamlet.
- `DistrictSpec.stats` (`DistrictStat`: whole points per 50 development x (1 + adjacency), rounded down), applied by
  `WorldSystem.RecomputeYields` through `EffectRouter` (`WorldTributaries.StatEffects`, one source per tributary): the
  four categories that name a pillar raise it (Auric Aureus, Weaver Waltz, Regal Regalia, Esoteric Chorus), Militant
  Ambition, Indulgent morale (`MoraleModifier`). A Capital council seat gives +1 to a pillar, the scale used here.
- Yields per 10 development: Agromagical Food 0.07 + Glimmerfern 0.004 (housing 1.5); Militant Game Meat 0.02 + Hides 0.01
  (ward, holds land, pull, expedition slot, outfits; min development 15); Auric Research 0.03 + Faith 0.02; Weaver
  Silverreed 0.004 (+3 hub development); Domestication Game Meat 0.03 + Hides 0.01 + Lumenwool 0.005; Trading Food 0.02
  (+6 hub development, capacity 1.5); Industrious Elderwood 0.04 + Duskstone 0.02; Regal none (capacity 2, pull, min
  development 20); Indulgent Wild Honey 0.01 (housing 1); Esoteric Research 0.01 + Sky Glass 0.004.
- New adjacency source `Enclave`: each enclave of the district's category within `enclaveReach` (5) cells, twice when
  suzerain (+0.3 each for most districts); a Regal District's envoys count every category ("any", +0.15). Pairings
  follow Enclave.md's interdependencies where it gives them (Agromagical with Weaver and Indulgent, Auric with
  Domestication); the clashes (Militant vs Auric, Industrious vs hamlet and Domestication, Militant vs Indulgent, Regal
  vs Regal, Esoteric away from hubs and thriving on dissonance) are game proposals, not vault canon.
- Orphans (`WorldTributaries.Rehome`, run at the start of `WorldCivilization.Rebuild`): a tributary whose hub is gone,
  detached or no longer a hub rejoins the nearest hub within reach with a free place, else the nearest within reach,
  else the nearest anywhere (the Capital first on a tie), keeps its district and development, and gets a free road to
  its new hub when none joins them (`BuildRoad(..., rebuild: false)`). Moves wait in `WorldMap.rehomed` (never saved)
  for the next civilization notice. No code removes or detaches a hub yet (roadmap D07), so this only guards the future.
- Tests: `WorldTributaryTests` now 9 (ten categories and pillar stats, kindred enclaves and Suzerainty, orphans
  rejoining). `ContentValidator` checks every category has a district, each category name and each stat.

## Loss, damage and ruins, September 27, 2026 (latest)

Owner direction: settlements can be lost, pillaged or damaged; orphaned tributaries with no connecting route wither; anything
but the Capital can be lost, and the Capital can be profoundly damaged; the fallen become ruins that stay on the map and can
be investigated to compost the failure into a variety of bonuses (Enlightenment, sometimes civics, Research, resources), so
loss is transformation without being fully punishing; adopting a civic from the ruins earns Digestive Rebirth. Every number
is a proposal (`SettlementRules.loss`, `LossRules`).

- **Damage** (`Settlement.damage` 0-100, `harmedBy`, `warned`; optional save fields; `WorldRuins.Tick` each Seventh once the
  map is open, never while a save restores): danger at the cell above 0.15 (after a Militant ward) deals up to 5 a Seventh
  at danger 1; a tributary no road joins to a hub (orphaned with no hub, or its road cut) withers at 4 a Seventh; stories
  pillage through the new `settlement:<capital|exposed|Name> -N` consequence (`+N` repairs; `WorldSystem.Pillage`). Each point
  of damage costs 0.25 City Development, and a damaged settlement grows slower (x (1 - damage/100)). Safe settlements heal
  2 a Seventh (x1.5 on the Capital's roads); Repair pays 8 Elderwood + 6 Food per 10 points at once. A notice at 50.
- **The Capital** is capped at 90 damage and never falls; while damaged the realm's land and settlement output is multiplied
  by 1 - 0.5 x damage/100 (`WorldRuins.CapitalOutput`, in the yields, the ledger and the hover card). Threats sit at least 12
  cells from it (radius 6-7), so only stories harm it at the start.
- **Falling** (`WorldRuins.Fall`): at 100 a settlement leaves `WorldMap.Settlements` for `WorldMap.Ruins` (saved
  `WorldSystem._ruins`); its roads stay, its tributaries rejoin the nearest hub (`WorldTributaries.Rehome`), Anchors re-sync,
  danger is recomputed. A `Ruin` keeps the former kind, district, binding, founding and fallen Age, full Ages, development,
  whether it stood beyond your authority, and the cause. 404 is reported for one lost beyond your authority.
- **Ruins** (`UnitTask.Investigate`, `UnitAbilities.Investigate`, riding on the SurveyMeso flag so no unit asset changes;
  `investigateSevenths` 3): an expedition standing on a ruin (or sent there from the ruin's card) reads it once
  (`WorldRuins.Findings`, deterministic per ruin): base salvage 10 Elderwood + 4 Duskstone, 300 s of its former production
  (its district's or kind's yields at the development it had), Research 8 + 0.4 per development; Enlightenment of a
  researchable technology at 30% + 0.4% per development; a civic its people lived by at 20% (+20% for a town, Major
  Settlement or haven, +10% for a Weaver, Regal or Auric district). Each legend of the party earns a discovery fragment;
  Era Score 1. A civic left behind is adopted from the ruin's card with its requirements waived (slots and conflicts still
  hold: `CivicManager.UnlockCivic(..., inherited: true)`), which reports Digestive Rebirth.
- **UI**: settlement cards show damage, what harms it now or how fast it heals, and Repair; ruins show on bare land and under
  settlements (what it was, when and why it fell, investigate or send the nearest expedition, adopt the civic); the unit card
  offers "Investigate the ruins" on unread ruins; the map draws ruins as grey diamonds (a gold ring while a civic waits) and a
  red ring under damaged settlements.
- Tests: `WorldRuinTests` (5: harm and healing, withering and pillage; the fallen as ruins and the Capital never lost;
  a hub's fall rehoming its tributary; findings and their odds; the story consequence), `AchievementTests.Rules_LossAndRuins`.
- Open: reclaiming a ruin (Welcome Back, Traitors), random raids from threats (damage is continuous for now), grievances
  or events when an enclave's neighbour falls, and the Digestive Rebirth "reform a culture" path.

### Follow-up: settlement Composure, the Old World, reclaiming and the Chronicle (same night)

Owner direction: rename damage/health to Composure with the exact five levels of legends, unified (withering lowers it);
several broken roads and a few ruins at the start so the world looks like it ended once already; repairing or using those
roads beats not using them but lacks the benefits of rebuilt roads; reclaiming lost land of your own gives +3 Era Score; a
section showing the timeline of Era Score, dated by the Cycle/Echo/Phase calendar; the culture half of Digestive Rebirth is
left to the agent building the culture system.

- **Composure** replaces damage: `Settlement.strain` (optional save field) read through `ComposureState` and
  `ComposureRules.StateOf` with the legends' `ComposureTuning` (`LossRules.composure`: Clouded 10, Fractured 40, Spiraling
  70, Surrender 100, baseline 20, restRecovery 2). One shared step, `ComposureRules.Ease` (legends' `Next` now calls it):
  the Seventh's strain is added (danger 6 at danger 1 above 0.15, withering 5), then it eases toward the baseline (x1.5
  on the Capital's roads). Output while Fractured/Spiraling uses the legends' council factors (0.9/0.7) on settlement
  yields, growth and district effects (`WorldRuins.Output`, in `WorldTributaries.Scale`); the Capital stops just short of
  Surrender and its Spiraling costs the realm 30% output. Pillage and the `settlement:` consequence add strain; Mend pays
  8 Elderwood + 6 Food per 10 strain above the baseline. Notices when a settlement deepens to Fractured or Spiraling.
- **The Old World** (`WorldRuins.PlaceOldWorld`, `OldWorldRules`, stream "old-world", called when WorldSystem generates
  the map): 9 ancient ruins (`Ruin.ancient`, cause "cataclysm", fallen before the Ages, one city, towns and outposts,
  development 20-60) spaced 7 apart and at least 4 cells from the Capital, investigable like any ruin (Enlightenment
  +15%), and broken roads joining them and the Capital's ground as a spanning tree (`WorldMap.OldRoads`,
  `WorldTile.oldRoad`; laid again from the seed on every load, not saved). Walking one costs x0.75
  (`WorldPaths.OldRoadFactor`, meso steps and the micro grid's `Mark.OldRoad`; a road is x0.5). Road planning prefers
  them; a road built over one restores those cells at 35% of the cost (`TradeRoute.restored`, `WorldTile.restoredRoad`):
  no Trade Node there, half worth to City Development and route efficiency, governance halfway, until "Rebuild" pays
  the other 65%. Drawn faded and gapped; restored stretches paler.
- **Reclaiming** (+3 Era Score, `LossRules.eraReclaim`): a settlement or tributary founded on the ruins of one of yours
  (`WorldRuins.Reclaimable`, `Ruin.reclaimed`) earns it once and reports Welcome Back, Traitors (`SettlementReclaimed`);
  cells of yours that slip away are remembered (`WorldSystem._lostLand`, `WorldRuins.TrackLand`) and bringing any back
  earns +3 once per Seventh. The Old World's ruins are no one's to reclaim.
- **The Chronicle** (`EraTimeline`, `EraAward`, `AgeProgression._eraTimeline`, optional save field; `EraTimelineWindow`):
  every Era Score award since the world began, dated to its Age, Act, Cycle (and its name), Echo, Phase and Seventh,
  shown oldest first by Age, then Cycle and Echo. Opened by clicking the Era Score sun or the world map's Chronicle
  button; Esc closes it. Awards before this change are not in it (the old log kept only 12 lines per Age).
- Save fix: `_ruins` was missing from `GameSnapshot`'s WorldSystem schema (ruins would not have been saved); it is now
  listed with `_lostLand`, and AgeProgression lists `_eraTimeline`.
- Tests: `WorldRuinTests` (Composure unified, Surrender and the Capital, Old World placement and travel, restoring and
  rebuilding roads, lost land, the Chronicle's order), `AchievementTests.Rules_LossAndRuins` (Welcome Back, Traitors).
- Open: the "reform a culture" half of Digestive Rebirth (another agent's culture system); random raids; drift-and-reclaim
  cycles could farm the land award (once a Seventh); a settlement founded on an Old World ruin gets nothing special yet.

## Eleos Blooms, September 27, 2026 (latest)

Asked for: Eleos Blooms as resources on the map, fitted into the resource-site niche (food is being relabelled
Edible / Ingredient / Spice by the culture session at the same time). Built on the resource sites as a new kind,
`ResourceKind.Bloom`, from Eleos Bloom.md; every number and every placement rule is a proposal (Canon Gaps.md).

- **Emotional Residue** (`WorldResources.Residue`, `WorldTile.residue`, `EleosSettings` in `generation.eleos`): the
  feeling a place holds, 0-1, derived at every civilization rebuild and never saved. The wild's own is 0.1. A
  settlement adds 0.5 (Capital), 0.4 (Major) or 0.25 (others), plus up to 0.3 more as its people's Composure strains,
  fading over 3 cells. A ruin adds 0.45 over 2 cells (the grief of the fallen; the Old World ruins count too).
  Dissonance adds 0.6 per point, counted before the blooms drink any (feeling left unmetabolized in the soil).
- **Vigor** (`ResourceSite.vigor`, `[SaveOptionalField]`): the mean residue over a bloom's cells divided by its
  `residueNeed`, capped at 1. Everything a bloom does scales with it: land value, yields, auras, danger, soothe
  (`WorldResources.Gift`), and its harvest (`HarvestShare`). Below `witherBelow` (0.2) it withers: it gives nothing and
  threatens nothing, a harvest brings back only its fallen leaves (25%), and its marker fades to grey. Blooms thrive
  once your people live near them.
- **Niches** (`BloomNiche`):
  - *Listeners* (Tier 1) drink Dissonance around them (`dissonanceAura`, handed to the magic through
    `WorldMagic.SetDissonanceShifts`; `BaseDissonance` keeps placement and residue reading the ground before the
    blooms touch it). Their shed leaves make Eleos Tea.
  - *Healers* (Bioluminescence-Dominant) ease an expedition's Composure strain each Seventh on and beside them
    (`soothe` into `WorldTile.sanctuary` and `UnitSurroundings.sanctuary`, taken off `Expeditions.Hardship`). A party
    camped in their light rests as in a settlement (`WorldSystem.HardshipOf`).
  - *Predators* (Movement-Dominant) cast danger (`dangerAura` into `WorldTile.siteDanger`, the base of
    `WorldSites.RecomputeDanger`, so travel, ambushes, City Development and desirability all feel it). They also lure
    parties in: `WorldTile.lure` raises mishap risk by `lureRisk` (0.25 at full lure, not eased by a small party) and
    allows the new `MishapKind.Lure` ("Lured by a bloom": strain 10, party strain 2, attrition 10, fatigue 15), which a
    companion can ease by pulling the victim free.
- **Lumen Seeds**: listeners and healers that give seeds plant like crops, but only where there is feeling to imprint
  them. `WhyNotPlant` asks for residue of at least half the bloom's `residueNeed` ("plant near your people").
- **UI**: an unidentified bloom reads "Unidentified bloom (petals that brighten as you draw near)". Identified, the
  hover card adds an Eleos Bloom row (niche, vigor or withering, the residue it finds and needs), and the effects
  around it (drinks Dissonance, eases strain, danger and lure). "From sites nearby" lists Dissonance drunk, a
  sanctuary and a lure. The identify notice names the niche and flags a withering bloom.
- **Validation** (`ContentValidator`): only blooms may drink Dissonance, heal or prey; a predator needs danger, a
  healer needs soothe; Emotional Residue sources cannot be negative.

| Bloom | Niche | Ground | Need | Land value | Around it | Harvest |
|---|---|---|---|---|---|---|
| Shame Moss | listener | marsh, bog, ruins, woodland (Dissonance 0.03+) | 0.25 | +1.5 | -6% Dissonance | Eleos Tea 6, seeds |
| Sorrowbells | listener | ruins, ash, orchards, woodland, foothills (Dissonance 0.03+) | 0.3 | +2 | -10% Dissonance over 2 | Eleos Tea 8, seeds |
| Memory Marigolds | listener | plains, steppes, auric meadow | 0.25 | +2 | -4% Dissonance, fairer | Eleos Tea 6, seeds |
| Vow Orchids | listener | moonlit grove, violet glade/wood (Coherence 0.35+) | 0.35 | +3 | -5% Dissonance, +Coherence | witnessed, never picked |
| Lullroots | listener | woodland, taiga, violet wood (moist) | 0.25 | +2 | -4% Dissonance, eases 1.5 strain | Eleos Tea 6, seeds |
| Candlevein Bloom | healer | ruins, ash, orchards, plains, woodland | 0.4 | +3 | eases 3 strain | Eleos Tea 8, seeds |
| Xochi-Singers | healer | auric meadow/copse, violet glade, woodland, plains | 0.35 | +3 | eases 2.5 strain, fairer | Eleos Tea 4, seeds |
| Skyroot Matriarch | healer | taiga, violet wood, woodland (Coherence 0.4+, seen from afar) | 0.2 | +5 | eases 2, +10% Coherence, -8% Dissonance over 2 | untouchable |
| Threshold Cushion | predator | scrub, mesas, ruins, ash | 0.15 | -1.5 | danger 35%, lure | untouchable |
| Glottis-Mouth Trap | predator | ruins, leyline ravines, rift scar, taiga | 0.15 | -2 | danger 45%, lure | untouchable |
| Hearth-Eater | predator | woodland, taiga, ash, ruins, foothills | 0.2 | -2.5 | danger 50%, lure | untouchable |

- **Eleos Tea** (`GameResources/Eleos Tea.asset`, section Valuables, borrowed Glimmerfern icon): yielded while held and
  harvested as cargo. The culture session gave it its own kitchen category, `FoodClass.EleosTea` (drunk like food, food value 0.5,
  a "national tea"; more teas to come), and tagged it Agromagical in `CultureTuning.resources`.
- Left out on purpose: Soul-Stitcher, the Tier 3 Nymphic blooms (Bride-of-the-Bell, Velvet Widows: Lazarus's garden,
  Age II) and the Tier 4 dryads (Age IV).
- **Sterile blooms that grow by themselves, Echo by Echo** (owner direction, same night; Eleos Bloom.md, Glimmerfern.md,
  The Auric Aria's Suicide): Fated Flowers, Forsaken Flowers and Glimmerfern give no seeds and cannot be planted
  (`WhyNotPlant`: "sterile"; stale seeds of them are dropped from packs on load). They are never placed with an Age:
  the landscape moves with the game's clock. `WorldResources.GrowEcho` runs once at the world's start (after the Old
  World's ruins; up to each count at once) and at every Echo (`TimeSystemLogic.OnEchoChange`, 63 Sevenths; never on
  a load). `WorldSystem._echoesGrown` is saved and seeds each Echo's sprouting, so a world grows the same way once.
  Changes the map can see are announced ("Something new grows at...", "The Fated Flowers at ... decay into
  Forsaken Flowers.", "... heal into ...", "... fades away.").
  - **History and hurt** (`WorldTile.history/hurt`, derived with the residue; `EleosSettings`): history is the strongest
    past at a cell: a ruin 0.8 over 3 cells, an Atonalis Nest 0.7 over 2 (where Atonalis live and fall, and their Rose
    Seeds with them), a landmark 0.6 over 1, a Sacred Site 0.5 over 2, an Old World road 0.3, ruin-like ground (ruin
    field, skeletal orchard, golden ash, rift scar) 0.4, and a settlement 0.1 per Age it has stood (at most 0.5). Hurt
    is the sorrowful part of the residue: a ruin's grief, the settlements' strain, and Dissonance.
  - **Turning** (`turnsInto`; `ResourceSite.turning`, saved): each Echo a patch's turn runs `turnPerEcho` further
    while it is due, plus `turnPerDissonance` per point of its Dissonance; it recedes as fast when not due. At 1 the
    patch turns, unless the other site's own turn would be due there at once (no flickering).
    - **Fated Flowers** (gold, +3, fairer, +Coherence, drink Dissonance): sprout on ground with history 0.3+ and hurt
      at most 0.25, 1 an Echo up to 5. They decay into Forsaken Flowers while the field's hurt is 0.3+ (unless its
      Coherence holds at 0.7+): about three Echoes at 0.35 a step, plus 2.5 per point of Dissonance, so high
      Dissonance withers them within one Echo.
    - **Forsaken Flowers** (crimson, +2, drink more Dissonance): also sprout where history and hurt (0.3+) meet, 1 an
      Echo up to 3. They heal back into Fated Flowers while their Coherence is 0.7+, about three Echoes, slowed 1.5 per
      point of Dissonance.
  - **Fading** (`fadePerEcho`; `ResourceSite.fading`, saved): a sprouted Glimmerfern patch whose centre is no longer
    rooted fades, and is gone at 1 (the sites are numbered again). This happens when its silver river lost the
    leylines, or its Forsaken field healed and no sorrow remains.
  - **Ways to grow** (`WorldResources.Rooted`; any one will do): hurt (`minimumHurt`), a site nearby (`nearSites`), a
    silver river (`silverRiverReach`), or a lake a silver river runs into (`silverLakeReach`). Silver rivers are river
    cells where a leyline's Lunehymn runs silver. A lake counts whole once a silver river flows into it or touches it
    (`WorldResources.SilverShores`, `WorldTile.silverRiverSteps/silverLakeSteps`).
    - **Lesser Glimmerfern Grove** (a healer: eases 1.5 strain; harvest Glimmerfern 12): within 2 cells of Forsaken
      Flowers or on hurt 0.3+, 1 an Echo up to 4, fading over 3 Echoes.
    - **Moonlit Glimmerfern** (Glimmerfern.md: "thrives in moonlit groves"): on the banks of a silver river (1 cell),
      patches of 2, 1 an Echo up to 6, fading over 3 Echoes once the silver leaves.
    - **Glimmerfern Lakeshore**: where a silver river runs into a lake, a grove of up to 30 cells around its shores
      for 3 hexes, seen from afar (+5, eases 2 strain, harvest Glimmerfern 20), up to 3, fading over 4 Echoes.
  - The hover card shows "Sterile" with the cell's history and sorrow, how far a Fated or Forsaken field's turn has
    run and what drives it, "Moonlit" for the silver groves, and a fading warning. The validator rejects a sprouting
    spec that gives seeds, an unknown `nearSites` or `turnsInto`, a turn with no trigger or no pace, and negative
    reaches.
- Tests: `EleosBloomTests` (residue sources, vigor and withering, non-blooms untouched, listeners drink Dissonance and
  a second refresh changes nothing, predators' danger and lure mishap, withered predators harmless, healers ease
  hardship, Lumen Seeds need residue, history and hurt, sprouting Echo by Echo, Fated decay hastened by Dissonance,
  Forsaken healing on Coherence, Glimmerfern fading, silver rivers and silver-fed lakes). The real catalog is checked by
  `WorldResourcesTests.Content_EveryResourceSiteFindsGroundOnRealWorlds` in the Test Runner.
- Open: blooms that stay withered never die (no removal yet); no expedition event lines for blooms; predators cannot be
  burned out or tamed; residue does not yet count legend deaths, battles or festivals; there is no Emotional Residue lens.

## AECOR scale names, Sectors and the bestiary, September 28, 2026

The owner adopted the scale names of their earlier design, AECOR: **Overall Map → Quadrant → Macro Biome →
Inter-Biome Definitions → Sector** (section 1). The code, `World.asset`, the stencil (Q1-Q7, I, W), the handmade
tiles (`macrobiome:`), tests and docs were renamed with no compatibility shims; scale ownership is in ARCHITECTURE.md,
"World scales". The renames:

| Before | Now |
|---|---|
| `SectorSpec`, `sectors`, stencil tokens S1–S7 | `QuadrantSpec`, `quadrants`, Q1–Q7 (`WorldTile.quadrant`) |
| `BiomeSpec`, `biomes`, `WorldTile.biome`, tile header `biome:` | `MacroBiomeSpec`, `macroBiomes`, `WorldTile.macroBiome`, `macrobiome:` |
| stencil P, `WorldRegion.Connective`, `connective` | I (intersections), `WorldComposition.Intersection`, `intersectionTerrains` |
| `WorldRegion.Slot`, `WorldTile.region` | `WorldComposition.MacroBiome`, `WorldTile.composition` |
| `WorldTile.quadrant` (quarters around the capital), `HexCoord.Quadrant()` | `WorldTile.quarter`, `HexCoord.Quarter()` |
| `WeatherExtent.Sectors`, `WorldWeatherFront.sectors` | `WeatherExtent.Quadrants`, `.quadrants` |
| the Biomes lens | the Macro Biomes lens |

- **Sectors:** `WorldSectors.Assign` gives every Macro Biome cell one of nine `CompassSector`s, measured from the
  centre of its slot's cells (Central about a ninth, then 45-degree wedges; north up). Intersections and the ocean have
  none. Generator version 9. The hover card names the place "Violet Grove, North-East Sector".
- **Bestiary:** `CreatureTaxonomy` holds AECOR's 21 diet-subgroup pairs; `generation.species` holds 15 species; every
  Fauna site names its species, and the three predatory blooms name theirs (Trapper Apex Predators). None of it applies
  to the Atonalis. The identified site's hover card shows the creature. Tests: `BestiaryTests`,
  `WorldGenerationTests.Sectors_*`.
- **Ecology (E2, the same day):** each Macro Biome's range (its cells plus the nearest intersection ground,
  `WorldTile.habitatSlot`) holds populations of the bestiary's species, ticked each Echo by `WorldEcology`: capacity
  from habitat, people's pressure and Pure Light fragility, prey, growth, migration into neighbouring Macro Biomes and
  new dens. Dens are hunted once an Echo and can be depleted. Numbers in `generation.ecology`; details in
  vault: Arcanorian Ecology.md, "Where Creatures Live".

## Blooms among the living, September 28, 2026

Asked for: blooms that grow near settlements according to what the settlement is living through (dark blooms where it
suffers, gentle ones where it is content), only once seeds have been carried there or the land is highly coherent;
Fated Flowers that move like Glimmerfern; districts that draw their own blooms (the Indulgent District draws Lust
Berries); and creatures drawn to blooms as part of the food web. Canon anchors: Eleos Bloom.md (a bloom "near a
hospice... develops differently from one growing near a battlefield"; a Lumen Seed needs physical inheritance plus an
emotional imprint; sprite, animal and ritual pollinators; no residue means no movement) and the Canon Ledger note
Nostalgia_Perfumery_And_Lust_Berries.md (Lust Berries cannot grow in natural earth). Every number is a proposal.

- **Volunteer blooms** (`WorldResources.Volunteer`, run at the end of `GrowEcho` every Echo but never at the world's
  start). For each settlement:
  - **Lumen Seeds must reach the ground first** (`SeedSource`). Any of these counts: seeds planted within 3 cells
    (`seedReach`; any planting, rice included, since lumen grains ride in the soil), mean Coherence 0.65+ within 2 cells
    (`seedCoherence`; sprite pollinators), or a creature drawn to blooms living in its Macro Biome at 30% abundance or
    more (`pollinatorAbundance`).
  - **Mood** (`MoodOf`, from the settlement's Composure strain): Content at strain 10 or less, Suffering at 40 or more
    (the legends' Fractured state). In between, no mood draws blooms, but districts still do.
  - **What it draws** (`Draws`, `DrawReason`): `ResourceSiteSpec.drawnBy` (Content, Suffering, Either) and
    `ResourceSiteSpec.districts`. A tributary's district draws its blooms whatever the mood. Sterile blooms, unmerged
    refuges and den-blooms (those naming a species) never volunteer.
  - Each Echo there is a 50% chance (`volunteerChance`) of one new patch, picked by `volunteerWeight`, within 2 cells
    (never on the settlement's own cell), of up to 2 cells. It grows on "tended ground" (`FitsTended`): any open dry
    land within the bloom's Coherence bounds, with residue of at least half its need (the imprint). No terrain or
    climate rule applies. A settlement keeps at most 2 volunteers, plus 1 with a district.
  - **They follow the settlement's life**: `ResourceSite.tendedBy` (`[SaveOptionalField]`, -1 wild) names the
    settlement. When it no longer draws the bloom (the mood turned, the district changed, the settlement fell), the
    patch fades by 0.34 an Echo (`volunteerFadePerEcho`), so it is gone in 3 Echoes. A settlement in grief loses its
    marigolds and grows Sorrowbells, and the Sorrowbells fade again once it recovers.
  - Notice: "Sorrowbells take root beside Hearth, fed by its people's grief." ("... ease", "drawn by its Indulgent
    District"). Card: a "Drawn up" row names the settlement and why, or warns that it no longer draws the bloom.
- **Drift** (`WorldResources.Drift`, `ResourceSiteSpec.driftPerEcho`, a chance each Echo). A patch lets go of its
  poorest cell and takes the best fitting cell beside it (`DriftGround`: residue up to 1.5x its need; history and
  sorrow if it needs them; away from sorrow for blooms that turn under it; its ways to grow). On even ground it still
  wanders (slack 0.03 plus a little jitter). A withering bloom cannot move, volunteers stay home, a den-bloom keeps to
  its Macro Biome, and a multi-cell patch stays in one piece. Fated and Forsaken Flowers also fade now (0.25 an Echo)
  when their ground no longer holds them, and sprout again elsewhere, like Glimmerfern. Card: "Wandering".
- **Food web** (`SpeciesSpec.blooms`: bloom ids, a niche word, or "bloom").
  - `WorldResources.ThrivingBlooms`/`BloomGround` gather the cells within 2 of every non-withering bloom (vigor x
    falloff).
  - In `WorldEcology.Tick` every capacity goes through `Holds`: what the land holds, times
    `BloomGain` = 1 + 0.5 (`ecology.bloomDraw`) x the bloom ground in the range over 10 cells (`bloomCellsFull`),
    cached per species and Macro Biome each Echo. Predators feel it through their prey (the grey wolf hunts the
    Lanternback Grazer that grazes Glimmerfern).
  - `PlaceDen` puts a drawn species' new den beside its blooms when any stand in the Macro Biome.
  - A thriving pollinator population seeds the settlements in its Macro Biome (above). Card: "Draws" lists the
    identified species a bloom draws.
- **Validation**: `driftPerEcho` is a chance. Only non-sterile, non-den Eleos Blooms may volunteer, and they need a
  weight. Districts must exist. `SpeciesSpec.blooms` must name a bloom or a niche. `contentStrain` must be below
  `sufferingStrain`.

| Bloom | Drift | Drawn by | Districts | Weight |
|---|---|---|---|---|
| Fated / Forsaken Flowers | 0.5 (+ fade 0.25) | none | none | n/a |
| Lesser / Moonlit / Lakeshore Glimmerfern | 0.5 | none | none | n/a |
| Shame Moss | 0.15 | Suffering | none | 1 |
| Sorrowbells | 0.15 | Suffering | Militant | 1 |
| Memory Marigolds | 0.15 | Content | Trading | 1 |
| Vow Orchids | 0.1 | Content | Regal | 0.6 |
| Lullroots | 0.1 | Content | Hamlet, Agromagical | 1 |
| Candlevein Bloom | 0.05 | Suffering | Agromagical | 0.8 |
| Xochi-Singers | 0.05 | Content | Weaver | 0.8 |
| Skyroot Matriarch | 0 (cannot leave its grove) | none | none | n/a |
| Threshold Cushion / Glottis-Mouth Trap / Hearth-Eater | 0.35 / 0.35 / 0.3 | none (dens) | none | n/a |
| **Lust Berries** (new, predator, count 0) | 0 | none | Indulgent | 1 |

  Lust Berries: needs residue 0.35, adds 4% Dissonance, danger 10% with a lure, beauty 0.2, -2% Coherence. It is never
  placed in the wild and cannot be gathered or planted. The card text stays clear of the ledger's explicit content.

| Species | Drawn to |
|---|---|
| Wild Bee, Luminant Moth, Forest Sprite, Grassland Sprite | listeners, healers |
| Moonveil Moth | the three Glimmerferns, Vow Orchids |
| Choir Cicada | Xochi-Singers, Sorrowbells |
| Ashfall Beetle | Forsaken Flowers, Sorrowbells, Shame Moss, Candlevein Bloom |
| Lanternback Grazer | the three Glimmerferns |
| Meadow Hare | Memory Marigolds, Lullroots |

- Tests: `BloomLifeTests` (9): seeds first, coherence and pollinators as seed sources, grief replacing ease and fading
  back, districts, determinism, sterile and den blooms never volunteer, Fated drift over remembering ground, withering
  and volunteers stay put, food-web gain. Offline 785 pass; the batch EditMode run passed everything except
  `EcologyTests.Content_TheRealBestiaryLivesOnARealWorld`'s 100 ms Echo budget. An A/B run showed that test failing just
  as badly with the bloom step switched off (105-123 ms vs 104-107 ms), so the overrun predates this change.
- Open: Lust Berries have no harvest resource yet (Canon: wine, distillate); predator blooms do not yet prey on the
  creatures they lure; drift is not announced; festivals, battles and legend deaths do not yet feed the moods; no
  Emotional Residue lens.

## Surveys that keep their progress, surveying by itself, and routing, September 29, 2026

Owner's asks: a survey should not be cancelled the moment the party moves or does something else (count it as
progress), a clear sign over the unit that it is surveying and how far along, a survey that goes on by itself across
the whole meso hex, pathfinding of production quality, and the "Land can be claimed" notice only when a claim can
really be made.

- **Progress kept** (`WorldUnit.surveyPaused/surveyWorkHex/surveyWork`, optional save fields). A move whose way ends
  inside the survey cell keeps `surveying` on. Rest (exhausted, or weary between hexes), a retreat from a mishap and a
  walk back for rations only interrupt it: the party takes the survey up again by itself once idle
  (`WorldSystem.TickSurvey` in the idle branch of `StepUnits`). Player orders elsewhere (Go, Halt, Make camp, Explore
  by itself, Return for rations, a chase, a retreat or going to ground by hand, other work) call `PauseSurvey`: the
  work already done on the hex under way is kept, the card offers *Resume survey* / *Forget the survey*, and the survey
  resumes by itself when the party's journey ends in that cell or it breaks a hand-made camp there. *Stop surveying*
  (`StopSurvey`) ends it; hexes already surveyed always stay surveyed.
- **Work on a hex** now scales with the cell's cover (`coverSurvey`, as `UnitAbilities.Duration` already did), and a
  surveyor worn out at its work makes camp instead of working on at a crawl.
- **Progress shown**: `WorldSystem.SurveyProgress` (hexes surveyed of those that can be walked, plus the share of the
  hex under way) and `WorldUnits.WorkProgress` (`WorldUnit.workTotal`). WorldView draws a tag over each of your parties
  at work, pooled in the HUD canvas under the other panels: "Surveying 43%", "Resting · survey 43%", "Resupplying ·
  survey 43%", "Surveying 43% · waiting", "Survey paused 43%", "Foraging 60%", with a brass progress bar; hidden on the
  atlas reading. A surveying party no longer counts as waiting for orders.
- **Surveying by itself** (`WorldUnit.autoSurvey`, `SetAutoSurvey`): it finishes (or takes up) its survey, then picks
  the nearest walkable cell out of the fog with hexes left, outside settlements and not surveyed by another party
  (within about ten Sevenths' walk, `NextToSurvey`), resting and resupplying through the same `LookAfterItself` that
  exploring by itself now shares; stranded without rations, or with nothing left in reach, it stops and says so.
- **Routing**. `MicroGrid.Search` is one A* core (`Run`) with cached hex coordinates, an `avoid` set, and ties on the
  estimate broken toward the goal (fewer hexes expanded on open ground, still deterministic). `CostsTo` prices many
  targets in one search; `MicroNavigation.NextOnTour` orders a cell's hexes exactly (Held-Karp over at most seven,
  weighed by the true walking costs between them) and gives the way to the first; the surveyor re-plans at each hex.
  Others standing on hexes are walked around: a surveyor skips the hexes they hold and waits a quarter Seventh when
  they bar the rest; any of your parties halted by others or finding its way closed re-routes to where it was going
  (`WorldSystem.Reroute`; around others only for a fair detour, at most about twice the way plus six cheapest steps,
  and never when they stand on the very hex it was going to). Chases keep their own planner (`WorldPursuit.Route`).
- **Claim notice**: `ClaimableCount` now counts land only when a claim would go through: the map open, and the stores
  and costs payable (`CanPayForClaim`). Before, it counted every known bordering cell while the stores were empty,
  so the notice and the Claim hint showed for claims that could not be paid.
- Tests: `WorldUnitTests.ASurveyTourWalksTheCellsHexesInTheOrderThatWalksLeast` (checked against every order),
  `WaysGoAroundHexesOthersHoldAndOneSearchPricesManyTargets`; `SurveyPlayTests` now checks moves within the cell,
  halting sets aside with progress kept, resume, stop.
- Proposals (numbers): waiting 0.25 Sevenths, detour cap 2x + 6 steps, surveyor rests at fatigue 80 between hexes.

## Survey plans on the map, faster surveys, claims at once and the settling forecast, September 29, 2026 (later)

The owner asked that survey plans be shown on the map the way the research plan is shown in the technology tree, that
surveying a hex take half as long, that a claim put its land in your sphere at once, and that the map show which hexes
society will add by itself and how long that takes.

- **Survey plans** (`MicroNavigation.NextOnTour` now fills an optional `order` with the whole tour;
  `WorldSystem.SurveyPlan(unit)` / `SurveyPlan(unit, cell)`, planned from where the party stands, or from the cell's
  hex nearest it when it is still on its way). WorldView draws them under the units (`WorldRenderer.PlanMark`,
  `PlanLine`, in `SetUnits`) at the local and region readings: a ring on every hex still to survey, the tour's way
  from the party (active surveys and the selected party), and, once hexes are about 34 px apart on screen, a numbered
  rhombus badge per hex like the research plan's (`WorldRenderer.PlanColor` violet = the tree's planned rim,
  `PlanActiveColor` gold on the hex under way). A set-aside survey shows too, dimmer. While picking a cell to survey,
  the hovered cell shows the tour the party would walk. Tours are cached per party (moved, the cell's survey mask,
  the land or knowledge changed).
- **Survey time halved**: `UnitSpec.mesoSurveySevenths` 2.5 -> 1.25 (World.asset and the code default), so each hex
  takes about 0.18 Sevenths before cover and party size.
- **Claims at once** (superseded the same day: claims now take one micro hex, see the next section): `WorldTerritory.ClaimNow` settles every open hex of the cell, adds it to `Claims` and rebuilds;
  `WorldSystem.Claim` calls it. `TerritoryRules.claimSeventhsPerHex` and `WorldSystem.ClaimSevenths` are gone;
  `AdvanceClaims(map, settings)` only finishes claims an older save left half settled. `_claimProgress` stays in the
  save schema, unused (the codec looks fields up by name).
- **Settling forecast** (`WorldTerritory.Forecast` -> `AdoptionPlan`; `WorldSystem.AdoptionForecast`, refreshed on
  land changes and at most once a second): for each seat with room, the cell `Tick` settles next and its hexes in
  `NextHex` order, each timed at the seat's average pace (adoptPerSeventh x Pace x PopulationShare; seats on one cell
  add up), counting the time already waited toward the next roll and the share of the current Seventh gone by
  (`TimeSystemLogic.GetSeventhProgress`). Nothing on the map changes (the mask is put back). The map glows those hexes
  in the border colour (the next one pulsing) with "~N" Sevenths plates up close (only the next hex's between 34 and
  52 px), one "Settling · all ~N Sevenths" plate per cell farther out; the land card and hover say when the next hex
  and the whole cell join; the Realm panel's *Next* row lists them with times; the key explains both plans.
- Tests: `WorldTerritoryTests.Claims_TakeTheirWholeCellAtOnce`, `Claims_LeftHalfSettledByAnOlderSaveFinishAtOnce`,
  `Forecast_NamesTheHexesSocietySettlesNextInOrderAndWhen`; `WorldUnitTests.ASurveyTourWalksTheCellsHexesInTheOrderThatWalksLeast`
  checks the whole order. Pure tests green (WorldTerritory 17, WorldUnit 20, WorldCivilization 15, Expedition 49).
- Proposals: the badge spacing thresholds (34 / 52 px), the forecast being an average (each Seventh is a roll).

## Land held micro hex by micro hex: claims and adoption, September 29, 2026 (latest)

The owner asked that claiming land give you the micro hex, not the whole meso cell, claiming small hex by small hex,
and that the land society adopts by itself through administrative pull go the same way.

- **A hex is yours at once.** `WorldTile.microHeldMask` (saved, optional) now means hexes of a wilderness cell you
  hold, adopted or claimed; the new `microClaimMask` (saved, optional; `GameSnapshot.TileFields`) marks the claimed
  ones. `WorldTerritory.HexHeld(map, id, authority)` (any hex of a cell you or an Outpost hold, or a held hex of a
  wilderness cell), `Touching`, `HeldShare(t)` (1 inside your authority, held/open hexes of a wilderness cell) and
  `ClaimedHexes(map)` (an older save's whole-cell claims count every open hex). The cell's `authorityId` still flips
  only when every open hex is held (`WorldTerritory.Complete`: into `Claims` if any hex of it was claimed, else
  `Adopted`, then a rebuild), so settlements, roads, improvements and enclaves keep reading whole cells.
- **What a held hex gives**: `WorldUnits.LandYields` and `WorldSystem.CellYields` pay a wilderness cell's ground,
  resource site and cover yields times `HeldShare`; `WorldTerritory.Realm` weighs its load, Coherence and beauty by
  the share (`RealmReport.hexes`, `land` = cells' worth; averageLoad and the wide/tall comparisons use `land`);
  `Compute` counts the share toward the pulling seat's `held` (now a float), and `Tick`/`ClaimHex` add 1/open hexes
  as they go; a party standing on a held hex draws rations as inside your authority (`UnitSurroundings.held`).
- **Borders follow the hexes**: `WorldRenderer` builds the owner texture at micro resolution (held hexes of wilderness
  cells coded as yours) and `WorldTerrain.shader` samples `_OwnerTex` by micro hex (`MicroAt`, `_MicroInfo`), so the
  outline wraps each held hex. The old settled-hex dots are gone (the border shows them); an older save's pending
  whole-cell claim keeps its gold ring.
- **Claims, one hex at a time**: `WorldAuthority.WhyNotClaimHex(map, id)` (open, not a crag, not yet yours, touching a
  hex you hold, its cell claimable); `WhyNotClaim(map, t)` now asks for a hex of the cell touching held land ("No hex
  of it borders land you hold."); `WorldTerritory.ClaimHex(map, settings, id, out whole)`. `WorldSystem.Claim(tile,
  hex?)` takes the clicked hex or the one touching your land most (`ClaimTarget` = `NextHex`); a hex costs a seventh of
  `claimFoodValue`/`claimCost` (they still price a whole cell's worth), raised by `claimCostGrowth` per 7 hexes
  claimed (`WorldAuthority.ClaimScale` takes cells' worth as a float). `ClaimNow` remains for older saves' `Claiming`.
- **Adoption, one hex at a time**: `NextHex` returns only hexes touching held ground (-1 otherwise) and reads other
  cells' held hexes; `WhyNotAdopt` uses it instead of cell adjacency, so society grows from a claimed or settled hex
  too. Each settled hex is yours immediately (the Realm is recomputed after each, so strain stops adoption at the right
  hex). Past collapse, `Weakest` also considers wilderness cells held in part: their adopted hexes slip away, claimed
  hexes never do.
- **UI**: clicking at the micro reading also picks the hex (`WorldView._selectedHex`); the land card offers *Claim this
  hex* and *Claim the next hex / a bordering hex*, shows "You, N/7 hexes (k claimed)", and the hover at the micro
  reading says whether the hex under the cursor is yours or can be claimed. Realm panel: whole cells plus hexes held in
  part. Notice, Claim tutorial (done once any hex is claimed) and Realm tutorial updated. GameWiki World Map and
  Territory entries rewritten.
- Tests: `WorldTerritoryTests.Claims_TakeOneHexAtATimeBorderingLandYouHold` (new), the adoption test checks a single
  hex is yours with its share of land and load, the drift test covers a partly held cell and a claimed hex that stays;
  `WorldCivilizationTests` claim reason updated. Pure run 1101/1101; batchmode (Unity closed) TerritoryPlayTests,
  WorldViewPlayTests, GenesisLoopPlayTests and WorldTerritoryTests 31/31, shader compiled clean.
- Proposals: the per-hex price (a seventh of a cell), growth per 7 hexes claimed, and a partly held cell yielding and
  weighing by its share. Not seen by eye in the Editor yet (the border outline at the region and atlas readings may
  look busier with micro-hex edges).

## De facto and core land, disputes and occupied cores, September 29, 2026 (latest)

The owner changed the rule that a cell joins your authority only once all its hexes are held: with 4 of 7 hexes a
cell becomes de facto yours, with all 7 it is core territory, so a cell shared 4/3 is a territorial dispute (a
grievance for the minority, a casus belli for the ruler to integrate it as core); left as an API for occupied
territory later, above all for recovering lost cores.

- **`WorldHoldings`** (new, pure; `WorldHoldingsTests`): who holds each micro hex (`HexHolder`: yours in
  `WorldTile.microHeldMask`, other holders' in the new saved `WorldMap.HexHoldings` list of `HexHolding {cell, holder,
  mask, core}` (WorldSystem `_hexHoldings`, optional), and a cell given whole (`WorldTile.whole`, derived: Capital,
  settlement, enclave and feature reach, Outposts) holds every hex no one else does). `MaskOf/Hexes/Shares/FreeMask`,
  `Status` -> `HoldStatus` None/Partial/DeFacto/Core, `Ruler` (at least `TerritoryRules.deFactoHexes` = 4 hexes and
  strictly the most; a tie rules no one), `Fillable` (wilderness, or a cell the holder rules de facto: its free hexes
  can still be settled or claimed), `CoresOf` (you: `Claims`/`Adopted`; others: `HexHolding.core`, kept with no hexes
  left; a cell given whole: its authority), `MarkCore`, `TakeHex(map, id, holder)` (moves one hex, returns the
  previous holder; rebuild after), `Disputes/Dispute` -> `TerritoryDispute {cell, ruler, shares, cores, open,
  Grievance(holder)}`, `CasusBelliOf` -> `CasusBelli` None/Integrate/Recover, `Grievances`, `CasusBelliFor`, `Occupied`.
- **Establish**: hex-held land is ruled first (before any reach, so no settlement overrides it): the ruler becomes the
  cell's authority; older saves' whole-cell Claims/Adopted with no mask get every hex. `Project` marks cells whole.
  `WorldTile.hold` (derived) is the authority's status. Compute: an Outpost's pull turns any non-whole, unclaimed
  Player cell into its pocket (was: only Adopted).
- **Territory**: `WorldTerritory.AfterHex` (the 4th hex rebuilds: de facto; the last calls `Complete`: core into
  Claims/Adopted), `ClaimHex(..., out HoldStatus)`, `TickResult.deFacto`; `NextHex`/`WhyNotAdopt`/`Candidates`/
  claims work in `Fillable` cells; `HeldShare(map, t)` is 1 for core (or hand-set cells with no hold worked out),
  held/open otherwise, so a de facto cell yields and weighs by its hexes; `RealmReport.cells` counts core cells,
  `deFacto` the cells ruled de facto; drift can take the adopted hexes of de facto cells. Units draw rations on any
  hex they hold. Claims refuse hexes another holds ("only taken by force").
- **UI**: borders drawn per hex holder (a shared cell shows each side's hexes); land card and hover show a *Hold* row
  (core / de facto N/7 / others' shares / dispute: recover, integrate or grievance) and the hovered hex's holder; the
  Realm panel counts core cells, de facto cells and disputes. GameWiki World Map + Territory (Disputed land) updated.
- Tests: `WorldHoldingsTests` (7: thresholds, 4/3 both ways, tie, occupied core recovered, another's core claim
  outliving its hexes, a hex taken from a whole cell), WorldTerritory adoption/claim/drift updated, TerritoryPlayTests
  waits for core. Pure 1108/1108; batchmode 46/46 (play tests included).
- Proposals / open: 4 of 7 (`deFactoHexes`), de facto yielding by hexes rather than whole, holders other than you only
  arise through the API today (no rival civilization yet; an enclave appearing over your partial hexes is the one
  live case), no war or diplomacy consumes the casus belli yet, and taking a whole cell by force does not mark it core
  for you (only settling/claiming its last hex does).
