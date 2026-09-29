# World relief and regional weather

Generation version 6 keeps the existing sequence: stencil and slot assignment, blended height/climate fields, handmade tiles, basin sculpting, topology repair, priority-flood drainage, ground selection, fertility/capital, magic and sites. The starting Q1 slot now uses Auric Grasslands. Survivor Architecture remains in the outer biome catalog.

`MacroBiomeSpec.terraces` blends broad stepped shelves and warped valleys into the height field. Auric terrain uses open treads, wooded slopes, escarpments and sanctuary ruins. Basins vary in size, orientation and shoreline; drainage determines retention and lake connectivity. Rivers cut their beds downstream-to-upstream without reversing drainage or changing handmade cells. Water depth, landform and escarpment are derived generation data. Rendering adds depth-shaded lakes, relief lighting, strata, basin haze and sparse golden grass highlights. Fog and map lenses still take precedence.

## Weather contract

`WorldWeatherFront` holds a profile, footprint, priority and lifetime. Footprints cover an axial-hex radius, a set of stencil quadrants, or the entire map. Highest priority wins; newer id breaks ties. Lifetime is measured in sevenths; zero means permanent. Expiry exposes the weather underneath. Procedural fronts use a separate deterministic seed stream and a bounded population, distributed across the whole map. The existing procedural weather profile supplies background conditions between fronts.

`CelestialWeatherSystemLogic.WeatherAt(coord)` is the query for tiles, settlements and the capital. `ActiveWeatherProfile`, the capital visuals, weather conditions and existing global effect routing reflect the capital tile. A remote front does not apply its profile's global effects to the capital. Other cities expose local weather through their map tile. `WeatherProfileSO.mapTravelMultiplier` affects travel across every covered tile; Weeping Sky increases travel fatigue by 25%. This does not add separate per-city economy modifiers: the existing effect router remains global.

Use `AddWeatherFront(profile, extent, center, radius, durationSevenths, priority, quadrants)` and retain its returned id for `RemoveWeatherFront(id)`. Invalid requests return -1. Ordinary fronts have priority 0, regional events default to 100, and the Ink world-crisis helper uses 1000. Existing hard-set story weather remains a world background event that overrides procedural fronts. Explicit event fronts can override it.

Ink stories can declare these external functions:

```ink
EXTERNAL GetTileWeather(q, r)
EXTERNAL SetRegionalWeather(profile, quadrants, duration)
EXTERNAL SetWorldWeather(profile, duration)
EXTERNAL ClearWeatherFront(id)
```

`quadrants` is a comma-separated list such as `Q1,Q2`. Duration zero is permanent. Setters return the front id. Story previews do not mutate weather. The Weather lens and known-tile details expose local conditions.

Fronts, lifetimes, next id and deterministic procedural step are saved explicitly. Travel factors and the capital weather cache are rebuilt after load. Generation version and the changed catalog prevent older worlds from silently regenerating different terrain beneath saved tile indices. Existing saves require their original generator/catalog; use a new world for this terrain update.

## Validation

WorldReliefTests exercises terraced generation across seeds, downhill drainage, capital placement, catalog changes, weather footprints and local travel. WorldWeatherTests exercises overlap, crisis expiry, permanent fronts, ties and save round-trip in the Unity Editor. Existing generation/civilization/unit tests remain relevant. Visual acceptance and ShaderLab compilation require an Editor run; a standalone C# compile cannot establish those.
