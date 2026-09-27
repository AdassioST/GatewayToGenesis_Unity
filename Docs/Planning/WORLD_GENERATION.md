# Arcanoria — seamless world generation design

Design proposal, 2026-09-26. This extends S07/S09 in the established ROADMAP and takes priority as the user's next workstream. It is a generation specification and reviewable schematic, not an implemented Unity generator. The earlier Age-first delivery order is superseded for the immediate queue only; world/Age interfaces remain dependencies for later integration.

## 1. Confirmed composition: sectors, biome slots and P

The user's reference is a regional composition stencil, not the final tile grid. **S1–S7 are sectors containing groups of macrobiomes; W is water; P is procedural connective terrain.** Preserve the green S1 start and the connected S2–S1–S2 core of mainland Arcanoria. The exact catalogs of all seven sectors remain to be authored; do not infer biome identities from colors alone.

The hierarchy is **world → quadrant family → sector instance → biome slot → meso cells → micro cells**. Macro rendering aggregates those same cells. A sector ID such as S2 denotes a sector definition; repeated S2 blocks also need distinct instance IDs. A quadrant is a broad directional grouping and can contain more than one sector. A biome slot is a placement opportunity within a sector, not a square border visible in the finished landscape.

The user supplied these working catalogs:

- **S2:** two slots containing Violet Grove and Great Expanse. A world seed can assign Grove left/Expanse right or the reverse; each biome also selects an allowed orientation.
- **S5:** four slots containing Taiga, Magical Rift, Wind Plains and Auric Grasslands. Their placement and allowed rotations vary while the sector retains its mountainous regional identity.
- **S1:** starting sector; green central reference. Its detailed biome catalog is still open.
- **S3/S4/S6/S7:** preserve their reference positions and sector identity; detailed catalogs and directional climate constraints remain open. Northeastern tundra was an example of a sector theme, not a finalized assignment to a specific S number.

**P is generated connective space, not an additional biome in the shuffle bag.** It synthesizes mountain continuations, foothills, forest ecotones, plains, coast, straits and river passages from its neighbors. P may become land or water where the world topology permits. Its geometry should hide the slot stencil without erasing a named biome's identity.

### Ocean and continent topology

Use an **outer ocean belt around the assembled continental silhouette**, not a compulsory circular moat immediately around the three starting blocks. This lets mountain systems and procedural land seams connect peripheral sectors where the composition calls for a contiguous continent. The three central blocks are always connected mainland. Other sector instances carry explicit topology roles: attached land, offshore island, archipelago or remote continent; the detailed assignment remains a content decision.

Reserve a closed navigable water route around the complete primary mainland component and an outer ocean apron at the map boundary. Bays, shelves, straits and islands can make this ring irregular. Proposed initial minimum navigable width: three meso cells along the reserved ocean circuit, subject to ship/pathfinding tuning. The circuit must enclose the mainland, not merely be any cycle inside a bay. Ports and cross-ocean routes must connect to it. No procedural seam may accidentally dam the reserved circuit.

The Capital begins inside S1 on ordinary freshwater and usable soil with stable moderate Coherence. It is not guaranteed a Sacred Site, nexus or grandfield. Guarantee a reachable low-risk discovery and two early expansion directions. Green habitat does not bypass Age 0's famine or restricted magic.

## 2. One connected world, with constrained variation

The geographical identity is fixed; detailed composition varies by world seed. Treat the inspiration as a design goal: strategy-game adjacency/readability plus shuffled authored regional content. Do not assume either reference game's exact implementation.

**Fixed composition contracts:** central connected mainland, sector theme constraints, outer ocean belt, global bounds, guaranteed initial reachability and named unique locations' eligibility rules.

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

A biome is a **constrained recipe**, optionally with authored landmark interiors, not a finished independent terrain square. It supplies climate/soil ranges, elevation envelopes, vegetation/resource rules, mandatory motifs, permitted features, and boundary ports. A sector supplies the larger mountain backbone, prevailing moisture/wind and coastline/topology obligations. The recipe deforms within those obligations.

### Assignment algorithm

1. Build a sector-instance adjacency graph from the reference stencil. Reserve mainland connectivity, the ocean circuit, passes and required freshwater opportunities before allocating slots.
2. Create each sector's slot graph with eligibility tags: coast/interior, highland/lowland, wet/dry tendency, required river/pass contacts and approximate area.
3. Create the biome multiset for that sector. Assign exactly one biome to each required slot, without replacement unless the catalog explicitly permits repeats. S2 has 2 permutations and S5 has 24 permutations before eligibility/rotation constraints; these are theoretical maxima, not guaranteed valid outputs.
4. Enumerate allowed 60-degree rotations for hex-native templates. Use each template's allowed subset; do not rotate north-facing climate requirements blindly. Reflections are a separate opt-in and disabled for named sites by default. Non-grid source artwork can be resampled; logical ports still need validation.
5. Use a seeded constraint solver: pick the most constrained slot, shuffle eligible biome/orientation pairs, place, propagate neighbor constraints and backtrack. A biome's exact inner landmarks can remain fixed relative to one another while its exterior blends.
6. Score valid layouts for useful variety: distinct routes, separated scarce resources, readable transitions and starting fairness. Use bounded deterministic retries; report why unsatisfiable catalogs fail instead of silently deleting a biome or cutting the ocean.
7. Synthesize P from all adjacent ports/fields together. It is a positive-width transition area with its own tile ownership. Preserve each biome's protected interior; move/blend only declared buffer bands. Resolve four-way seams jointly, not four independent edge strips.
8. Validate the final terrain and feature graphs again. Record seed, generator version, catalog hash, assignments, orientations, port connections and any repairs.

### What must match at a seam

Elevation and slope continuity; climate/moisture continuity unless an authored magical boundary allows a sharp change; river elevation, discharge and direction; road/pass reachability; coastline and water level; mandatory biome borders; and required exclusion space for unique features. River ports cannot be joined uphill. Build a global drainage solution after terrain constraints are stitched, then refine rivers into micro geometry. Lake outlets, closed basins and waterfalls require explicit treatment rather than broken slopes.

A mountainous S5 is generated as one regional range system. Taiga occupies a compatible cold/wet part, Wind Plains a plateau/pass or rain shadow, Auric Grasslands a suitable valley/foothill, and Magical Rift a compatible fracture corridor. These subroles are proposals; variants can change elevations/climate envelopes. Do not force all 24 arrangements if some cannot satisfy the sector's geography. Conversely, do not reduce every seed to the same arrangement by overconstraining slots: authored variants and a deformable mountain backbone should allow meaningful alternatives.

Random streams are split by stable purpose/ID: topology, slot allocation, terrain, hydrology, resources, features, and Age magic. Adding a landmark must not consume the random numbers that determine the continent or move the Capital in an existing version.

## 5. Physical water, magical pathways and two fertility layers

### Physical terrain and water

Generate elevation → drainage basins → flow accumulation → rivers/lakes/wetlands → soils and vegetation. Rivers follow shared hydrological edges with a global downstream direction. Drain outlets reach the ocean or a deliberately closed basin. Preserve tributary connectivity at every zoom.

**Land fertility** is a field based on soil depth/minerals, freshwater availability, climate, drainage, erosion and existing damage. Floodplains can be rich but hazardous; a wet bog need not be fertile. Vegetation, irrigation and depletion modify this field through play. Land fertility does not automatically become high just because magic is high.

### Coherence, dissonance and magical fertility

Adopt the user's Coherence Seeds and Dissonance Seeds as generation sources. A source has a stable ID, position, strength, influence radius, falloff and affinity. Dissonance Seeds represent regional generation influences; an Atonalis Dissonance Core is a distinct living/entity feature that can add a moving or local negative source. They must not share a class merely because both dampen Coherence.

Compute a proposed bounded Coherence field from a sector baseline + distance-decaying positive seed influences + leyline support + Sacred Site support + constructed Anchors − distance-decaying dissonance/core influences. Use terrain-aware/geodesic attenuation where barriers matter. Overlapping sources mean Coherence need not rise monotonically along every path away from one core; isolated-source tests must show the expected falloff. Track negative influence separately for attribution and hazards rather than calling a clipped value a negative Coherence score.

**Sacred Sites are fixed local maxima and protected refuges.** Their protected interiors retain the highest harmonic integrity under ordinary Age drift and common dissonance effects. The vault says immune to most effects, not unconditionally invincible; exceptional story effects need explicit overrides. Atonalis avoidance is a movement rule as well as a map color.

**Magical fertility** is a separate field combining Coherence stability, available magical flow, affinity compatibility and Age-specific access. A high-Coherence but low-flow site can be stable without being highly productive. Sacred Sites provide exceptional potential, while Age 0 limits usable magic. Both fertility overlays must remain inspectable independently: fertile mundane farmland, magically rich barren rock, dual-fertile silver floodplain, and poor/dissonant wasteland are all valid outcomes. Numerical field ranges and coefficients remain tunable proposals.

### Leylines from seeds and Grand Thread Rings

A leyline is a persistent lineage plus an Age-specific embedded path, sourced from a Coherence Seed. Grand Thread Ring phase/orientation supplies the large-scale direction/potential field. Route curves through favorable Coherence, often alongside rivers, while allowing overland and undersea routes. Solve the network globally/coarsely first, then refine it continuously across P and chunk boundaries. Give line families stable IDs, capacities/flux and affinities; segment count does not determine identity.

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

Route cost considers terrain, physical crossings, safety, infrastructure and Coherence bonuses. The vault specifically says trade-route disruption occurs through blockade or pillage at a Trade Node. Therefore an Age's leyline shift changes efficiency/preferred routes, not automatic destruction of trade connectivity. Any new direct route-cutting mechanics need an explicit design change. Record alternate paths and chokepoints; do not make all sectors depend on one unavoidable bridge.

### F. Landmarks, discovery and story sites

Separate **landmark** (recognizable place) from **discovery site** (interaction/content). A landmark may host several discoveries across Ages. Categories include Memory Fields, Old World Remnants, ruins, observatories, caves, buried structures, named natural wonders, ritual sites, Crisis Wonders and later megastructure ruins. Store unseen → spotted → surveyed → explored → resolved/depleted states separately from truth knowledge and story prerequisites.

A visible ruin should not reveal its late-game truth or full name prematurely. A cleared location can host a later event without regenerating its terrain. Use unique IDs and protected placement rules for canon locations; generic variants may repeat.

### G. Threats and living ecology

Atonalis nests, migratory creatures, hunting ranges, spawning habitats, corruption scars, plague vectors and contested frontiers. Separate spawn potential from actual occupants. Migration follows the same traversable geography and Sacred Site avoidance rules. Seeded variation should create interpretable habitats and travel risks, not random encounters unrelated to place.

### H. Information and history

Fog of war, survey accuracy, last observed Age, old leyline routes, former silver reaches, abandoned roads, battle sites and prior ownership. Map knowledge becomes stale when the magical network moves. Preserve an optional previous-Age overlay so the player can explain why an old trading city declined.

### Placement priority and conflicts

World topology → protected canonical anchors/Sacred Sites → global terrain/drainage → biome interiors and P reconciliation → seeds/magic → resource distributions → habitable sites and nexus opportunities → discoveries/hazards → initial inhabitants. Some stages iterate: reserved sites constrain terrain before their final footprints are placed.

Features use explicit compatibility/exclusion rules and budgets per sector/area. A Sacred Site excludes ordinary active Atonalis occupation; a grandfield can overlap a river only for compatible extraction types; settlements avoid inaccessible cliffs/flood channels unless authored for them. Keep scarcity, minimum separation and reachability diagnostics visible to content authors. No blanket guarantee that every sector contains every feature.

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

Proposed pure generation stages: WorldRecipe → SectorGraph → SlotAssignment → RegionalHeightClimate → SharedSeamConstraints → DrainageGraph → BiomeRefinement → SeedFields → AgeMagicGraph → Features → InitialSimulationState → ValidationReport. Authored constraints can require bounded feedback between adjacent stages; log why a repair occurred.

Proposed content assets: WorldRecipe, SectorDefinition, BiomeRecipe, BiomeVariant, EdgePortSet, LandmarkTemplate, ResourceGrandfieldDefinition and AgeMagicProfile. Runtime plain data: WorldTopology, SectorInstance, BiomePlacement, HierarchicalCellId, RiverGraph, LeylineGraph, MagicSource, ScalarField, FeatureInstance and WorldChangeSet. Names are proposals, not existing classes.

Keep render LOD separate from simulation. Mesos own normal economy/travel aggregation; micro cells own physical detail and local edits feeding those aggregates. A single quantity has one authoritative owner. Use chunk meshes/Tilemap batches, pooled feature markers and dirty-region updates; do not tick every visible plant or every tile independently. Cache fields and aggregate summaries with generation/version dependencies. Store generator version, catalog hash, original assignments and player deltas for future persistence; a seed alone is insufficient after generator changes.

## 9. Acceptance tests and remaining choices

Required tests: same seed/version/catalog gives identical output; every required biome appears exactly once; rotations use allowed sets; P has no gaps/overlaps; shared heights and river endpoints agree; mainland and enclosing ocean circuit remain connected with minimum width; no river climbs uphill except an explicit magical override; every child has exactly one parent, including negative coordinates and map edges; aggregate yields equal authoritative children; zoom never changes paths/ownership; two lineages give Convergence and three-plus colocated lineages give Basin; repeated segments never overcount; ordinary water does not move on Age change; silver concentration changes only along reachable downstream water; Sacred Sites persist; Age change commits once; protected sites and generated features satisfy exclusion and reachability rules.

Performance acceptance must be measured on the target device: generation time, memory, worst-case chunk build, panning/zoom frame time, Age recomputation and largest-feature overlays. No timing targets are certified by this document.

Outstanding content decisions: complete S1–S7 biome catalogs and per-instance slot counts; attached/offshore role of outer sector instances; permitted orientations and reflection rules; detailed world dimensions; macro grouping ratio after prototype; seed influence/flow coefficients; silver-river retention; grandfield depletion; exceptional Sacred Site overrides; whether resets reshuffle geography. These do not block a deterministic S2/S5 seam prototype.

## Sources and authority

The user's supplied image and clarifications establish S1–S7 sectors, P seams, W water, S2/S5 example catalogs, rotation/shuffling, the three zoom levels, seed fields, Convergence/Basin counts and silver-river interaction. Those instructions take precedence over earlier assumptions in this draft.

Vault source: [Arcanoria — Map Features](<C:/Arcanoria Master/Arcanoria/Worldbuilding/Rise & Fall, Crisis/Arcanoria.md>) specifies moving leylines, fixed Sacred Sites, settlement/authority rules, grandfields, trade geography and enclave development. [Leylines](<C:/Arcanoria Master/Arcanoria/Worldbuilding/Origin of Magic/Conduits of Magic/Leylines.md>), [Grand Thread Rings](<C:/Arcanoria Master/Arcanoria/Worldbuilding/Origin of Magic/Conduits of Magic/Grand Thread Rings.md>) and [Sacred Site](<C:/Arcanoria Master/Arcanoria/Worldbuilding/World Environment/Landmarks/Sacred Site.md>) are short supporting notes; the detailed operational algorithms above are design proposals, not claims that those notes already specify them.

## Validation performed for this design delivery

The hierarchy proposal was checked over 25,921 parent positions spanning positive and negative coordinates, with 181,447 child-to-parent round trips; a complete three-level group contains 343 distinct micro cells. A sign error in the draft forward transform was corrected before delivery. The final mapping in section 3 matches the validated inverse.

The illustrative browser study was checked for seed-driven S5 catalog permutation, displayed orientations, meso/micro zoom and Age/field controls. Its 360-pixel layout had no horizontal overflow. This study demonstrates composition, exact cell grouping and example fields; it does not implement the production seam solver, drainage simulation, content-placement rules or Unity integration. The acceptance tests in section 9 and WG01–WG13 remain work to implement. No Unity runtime or EditMode validation is claimed for this documentation change.

## Implementation, September 27, 2026

The generator and the three-scale viewer are built in Unity (uncommitted; roadmap 3.1 lists each WG task's state). How
the design became code, and where it was simplified:

- **Slots and catalogs.** Each same-sector block of the stencil is one slot; blocks of a sector that only P separates
  form one sector instance ("S5 South-East"). A sector's catalog is shared by all its slots. S2 fills its two slots with
  Violet Grove and Great Expanse in either order; **S5 has three slots in the stencil, so each world draws three of its
  four biomes** and places and turns them. S1, S3, S4, S6 and S7 use placeholder catalogs until their biomes are named.
- **Handmade tiles (the hybrid).** Biomes own hand-drawn tiles (`Resources/World/Tiles/*.txt`, a honeycomb drawing with
  a legend, optional heights and allowed rotations). The generator stamps them at seeded places and rotations inside
  each slot's protected interior (three or more cells from its edge), never touching one another; the rest of the slot
  is procedural, and P plus the slots' buffer bands blend the neighbours.
- **Scale.** Eight meso cells across a stencil cell and an ocean apron of one and a half stencil cells: about 34,000
  meso cells, 237,000 micro hexes and 700 macro aggregates. Generation takes about half a second on the dev PC.
- **Seams.** Instead of explicit edge ports, every slot within reach of a cell's nearest slot lends its recipe, weighted
  by how much nearer it is, so height, relief and moisture are continuous everywhere, including where the second-nearest
  slot changes. The solver keeps facing seam heights within 0.3 (a simplified port check); P carries any remaining
  slope. A mountainous sector raises a range along the P between its own slots. Ground is chosen by coherent noise,
  so it forms patches and seams interlock.
- **Water.** A gentle continental dome, then priority-flood drainage (ties broken by a seeded hash so water fans across
  flats), lakes of 4 to 80 cells (a larger hollow stays land and drains across; a lake that would cut a slot off the
  mainland stays dry), flow accumulation and rivers. The capital stands in S1 on the mainland within three cells of
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
candidates and trade nexus sites (WG10), the sector catalogs, island roles for outer sectors (the sector `island` flag
turns its P to sea, but no sector is marked one yet), reflections, and edge ports for rivers and passes as authored data.

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
