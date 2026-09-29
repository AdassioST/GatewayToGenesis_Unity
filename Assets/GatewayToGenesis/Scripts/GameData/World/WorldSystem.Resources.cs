using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Resource sites in play (<see cref="WorldResources"/>): expeditions identify them by surveying, harvest them as cargo
/// without claiming the ground (raising grievances with whoever holds it, if anyone does), carry the cargo home to a
/// settlement and plant seeds on fertile land you hold. Grievances fade with time; an envoy to an aggrieved enclave
/// answers them before it raises your standing.
/// </summary>
public partial class WorldSystem
{
    /// <summary>An enclave whose standing falls below this casts off your Suzerainty (a proposal).</summary>
    public const float SuzeraintyLostBelow = 75f;

    // Grievances held against you, by authority id (saved), the seeds planted on your land and the resource sites
    // standing on the map (the map's lists; saved).
    private Dictionary<string, float> _grievances = new Dictionary<string, float>();
    private List<Planting> _plantings = new List<Planting>();
    private List<ResourceSite> _resourceSites = new List<ResourceSite>();
    // The creatures of each Macro Biome (WorldEcology), bound to the map.
    private List<Population> _populations = new List<Population>();
    // What each species has learned from the authorities (WorldBehavior), bound to the map.
    private List<SpeciesBehavior> _behaviors = new List<SpeciesBehavior>();
    // Resonance plagues among the creatures (WorldPlagues), bound to the map.
    [SaveOptionalField] private List<Outbreak> _outbreaks = new List<Outbreak>();

    /// <summary>Echoes the land has lived through.</summary>
    public int EchoesGrown => _echoesGrown;

    /// <summary>
    /// Phases since the game began, from the calendar (0 without one): a den's <see cref="ResourceSite.huntedPhase"/> is
    /// compared with it, so a den can be hunted again once the Phase turns.
    /// </summary>
    public int PhaseNow
    {
        get
        {
            var time = TimeSystemLogic.Instance;
            if (time == null) return 0;
            return ((time.CurrentCycle - 1) * TimeSystemLogic.EchoesPerCycle + time.CurrentEcho - 1) * TimeSystemLogic.PhasesPerEcho + time.CurrentPhase - 1;
        }
    }

    /// <summary>Sevenths until the Phase turns (the current one counted; -1 without a calendar).</summary>
    public int SeventhsToNextPhase
    {
        get
        {
            var time = TimeSystemLogic.Instance;
            return time == null ? -1 : Math.Max(1, TimeSystemLogic.SeventhsPerPhase - time.CurrentSeventh + 1);
        }
    }

    /// <summary>A den hunted this Phase (its cooldown), by anyone.</summary>
    public bool HuntedThisPhase(ResourceSite site) => WorldResources.HuntedThisPhase(site, PhaseNow);

    /// <summary>Grievances <paramref name="authority"/> holds against you (0 when none).</summary>
    public float GrievanceOf(string authority) => authority != null && _grievances != null && _grievances.TryGetValue(authority, out float g) ? g : 0f;

    /// <summary>Every authority aggrieved with you, and how much.</summary>
    public IEnumerable<KeyValuePair<string, float>> Grievances => _grievances ?? new Dictionary<string, float>();

    // ===== CARGO =====

    /// <summary>What an expedition can carry (settlers carry none; seeds weigh nothing).</summary>
    public float CargoCapacity(WorldUnit unit) => IsExpedition(unit) ? Math.Max(0f, ExpeditionRules.cargoPerLegend) * Math.Max(1, Expeditions.PartySize(unit)) : 0f;

    /// <summary>The weight of its cargo (heavy stone fills the packs sooner than light herbs; <see cref="Expeditions.WeightOf"/>).</summary>
    public float CargoLoad(WorldUnit unit) => Expeditions.CargoWeight(unit, ExpeditionRules);

    /// <summary>Discard carried valuables permanently, never crediting the civilization's stores.</summary>
    public bool DropCargo(WorldUnit unit, string resource, float amount)
    {
        if (unit == null || !Map.Units.Contains(unit) || !IsExpedition(unit) || unit.Missing ||
            string.IsNullOrWhiteSpace(resource) || float.IsNaN(amount) || float.IsInfinity(amount) || amount <= 0f) return false;
        float left = amount, dropped = 0f;
        foreach (var item in (unit.cargo ?? new List<ResourceAmount>()).Where(a => a != null && string.Equals(a.resource, resource, StringComparison.OrdinalIgnoreCase)))
        {
            float take = Math.Min(left, Math.Max(0f, item.amount));
            item.amount -= take; left -= take; dropped += take;
        }
        if (dropped <= 0f) return false;
        unit.cargo.RemoveAll(a => a == null || a.amount <= 0f);
        _partySpecs.Remove(unit.id);
        Say($"{unit.name} discards {dropped:0.##} {resource}. These valuables are lost. Load: {Expeditions.Load(unit, ExpeditionRules):0.##}; pace x{Expeditions.LoadPace(ExpeditionRules, Expeditions.BurdenStep(unit, ExpeditionRules)):0.##}.");
        Changed?.Invoke();
        return true;
    }

    /// <summary>"12 Glimmerfern, 4 Sky Glass" for the card (null when it carries nothing).</summary>
    public static string CargoText(WorldUnit unit)
    {
        var parts = (unit?.cargo ?? new List<ResourceAmount>()).Where(a => a != null && a.amount > 0.05f).Select(a => $"{a.amount:0.#} {a.resource}").ToList();
        parts.AddRange((unit?.seeds ?? new List<string>()).GroupBy(s => s).Select(g => $"{(g.Count() > 1 ? $"{g.Count()} x " : string.Empty)}seeds of {g.Key}"));
        return parts.Count > 0 ? string.Join(", ", parts) : null;
    }

    // In one of your settlements the cargo is unloaded into your stores.
    private void DeliverCargo(WorldUnit unit, List<string> notice)
    {
        if (unit?.cargo == null || unit.cargo.Count == 0) return;
        var here = Map.Get(unit.coord);
        if (here == null || here.settlement < 0 || here.settlement >= Map.Settlements.Count) return;
        var units = GameUnitsLogic.Instance;
        var delivered = unit.cargo.Where(a => a != null && a.amount > 0f).ToList();
        foreach (var a in delivered) units?.ChangeResourceFromName(a.resource, a.amount, false);
        unit.cargo.Clear();
        if (delivered.Count > 0) notice.Add($"{UnitLabel(unit)} unloads its cargo at {Map.Settlements[here.settlement].name}: {string.Join(", ", delivered.Select(a => $"+{a.amount:0.#} {a.resource}"))}.");
    }

    // ===== HARVEST =====

    // A party's reward multiplier, less at a den whose creatures have learned to flee or hide from you (WorldBehavior).
    private float HarvestMultiplier(WorldUnit unit, UnitSpec spec)
    {
        float multiplier = spec?.rewardMultiplier ?? 1f;
        var site = unit != null ? WorldResources.SiteAt(Map, Map.Get(unit.coord)) : null;
        return multiplier * WorldBehavior.HuntShare(Map, Settings.generation, site, WorldAuthority.Player);
    }

    /// <summary>What a harvest where the unit stands would bring back (its party's reward multiplier included).</summary>
    public List<ResourceAmount> HarvestPreview(WorldUnit unit) =>
        unit == null ? new List<ResourceAmount>() : WorldResources.HarvestAt(Map, Settings.generation, Map.Get(unit.coord), HarvestMultiplier(unit, SpecOf(unit)));

    /// <summary>The grievances a harvest where the unit stands would raise, and with whom (null: none).</summary>
    public (string owner, float grievance)? HarvestGrievance(WorldUnit unit)
    {
        var tile = unit != null ? Map.Get(unit.coord) : null;
        string owner = WorldResources.Owner(tile);
        var spec = WorldResources.SpecAt(Map, Settings.generation, tile);
        if (owner == null || spec == null || spec.grievance <= 0f) return null;
        return (WorldResources.OwnerName(Map, Settings.generation, owner), spec.grievance);
    }

    private string WhyNotHarvest(WorldUnit unit, UnitSpec spec)
    {
        if (!WorldUnits.Can(spec, UnitAbility.Harvest)) return "It cannot harvest.";
        if (unit.Moving) return "Stop it first.";
        var tile = Map.Get(unit.coord);
        string why = WorldResources.WhyNotHarvest(Map, Settings.generation, tile, AgeNumber, PhaseNow, SeventhsToNextPhase);
        if (why != null) return why;
        if (CargoLoad(unit) >= CargoCapacity(unit) - 0.5f) return "Its packs are full: bring the cargo home to a settlement first.";
        return null;
    }

    // The harvest goes into the packs (as much as they hold); seeds too. On ground another authority holds, it is taken
    // from them: grievances rise, and an enclave thinks less of you.
    private void FinishHarvest(WorldUnit unit, UnitSpec spec, WorldTile tile, List<string> notice)
    {
        if (WorldResources.WhyNotHarvest(Map, Settings.generation, tile, AgeNumber, PhaseNow, SeventhsToNextPhase) != null) return;
        var site = WorldResources.SiteAt(Map, tile);
        var siteSpec = Settings.generation.ResourceSite(site.spec);
        var gathered = WorldResources.HarvestAt(Map, Settings.generation, tile, HarvestMultiplier(unit, spec));
        float room = Math.Max(0f, CargoCapacity(unit) - CargoLoad(unit)), total = gathered.Sum(a => a.amount * Expeditions.WeightOf(ExpeditionRules, a.resource));
        float share = total > room && total > 0f ? room / total : 1f;
        unit.cargo = unit.cargo ?? new List<ResourceAmount>();
        foreach (var a in gathered)
        {
            float amount = a.amount * share;
            if (amount <= 0f) continue;
            var had = unit.cargo.FirstOrDefault(c => c != null && string.Equals(c.resource, a.resource, StringComparison.OrdinalIgnoreCase));
            if (had != null) had.amount += amount;
            else unit.cargo.Add(new ResourceAmount { resource = a.resource, amount = amount });
        }
        // A den is hunted (its Macro Biome's creatures fall; once a Phase); anything else is harvested once an Age.
        // The species remembers (WorldBehavior): a step toward fear or anger is told at once, with the danger it brings.
        string learned = null;
        var prey = WorldEcology.SpeciesAt(Settings.generation, site);
        if (prey != null)
        {
            var before = WorldBehavior.Toward(Map, Settings.generation, prey, WorldAuthority.Player);
            WorldEcology.Hunt(Map, Settings.generation, site, WorldAuthority.Player, HarvestMultiplier(unit, spec));
            // A hunt leaves suffering where the creatures fell.
            AddSuffering(tile.index, Feeling.Pain, WorldSuffering.HuntPain);
            AddSuffering(tile.index, Feeling.Vitality, WorldSuffering.HuntVitality);
            site.huntedPhase = PhaseNow;
            var after = WorldBehavior.Toward(Map, Settings.generation, prey, WorldAuthority.Player);
            if (after != before)
            {
                // Told once you have watched it long enough to read it (SpeciesLevel.Observed).
                if (Knows(prey.id, SpeciesLevel.Observed)) learned = BehaviorLine(new BehaviorChange { species = prey, from = before, to = after, gentler = false });
                WorldCivilization.Rebuild(Map, Settings.generation);
            }
            MarkYieldsDirty();
        }
        else foreach (int cell in site.cells) Map[cell].harvestedAge = AgeNumber + 1;
        bool seeds = siteSpec.seeds && !site.planted;
        if (seeds) (unit.seeds = unit.seeds ?? new List<string>()).Add(siteSpec.id);
        string what = string.Join(", ", gathered.Select(a => $"{a.amount * share:0.#} {a.resource}"));
        notice.Add($"{UnitLabel(unit)} {(prey != null ? "hunts" : "harvests")} {site.name} at {Place(tile)}: {what}{(seeds ? $", and seeds of {siteSpec.name}" : string.Empty)}{(share < 0.999f ? " (the packs are full)" : string.Empty)}.");
        if (learned != null) notice.Add(learned);
        string owner = WorldResources.Owner(tile);
        if (owner != null && siteSpec.grievance > 0f)
        {
            notice.Add(RaiseGrievance(owner, siteSpec.grievance, $"{unit.name} took {siteSpec.name} from their ground"));
            // Taking what is another's leaves shame on the ground it was taken from.
            AddSuffering(tile.index, Feeling.Shame, WorldSuffering.GrievanceShame * siteSpec.grievance / 10f);
        }
        Changed?.Invoke();
    }

    // Grievances with an authority: its ledger rises, and an enclave's standing with you falls (past a point it casts
    // off your Suzerainty). Returns the notice line.
    private string RaiseGrievance(string owner, float amount, string why)
    {
        _grievances = _grievances ?? new Dictionary<string, float>();
        WorldResources.Raise(_grievances, owner, amount);
        string name = WorldResources.OwnerName(Map, Settings.generation, owner);
        GameLog.Event($"Grievance +{amount:0.#} with {name}: {why}", Log);
        var enclave = WorldResources.EnclaveOf(Map, owner);
        if (enclave == null) return $"{name} holds this ground: its grievances against you rise to {GrievanceOf(owner):0}.";
        enclave.influence = Math.Max(0f, enclave.influence - amount * Math.Max(0f, ExpeditionRules.grievanceStanding));
        if (enclave.suzerain && enclave.influence < SuzeraintyLostBelow)
        {
            enclave.suzerain = false;
            AfterCivilizationChange(null);
            return $"{name} casts off your Suzerainty: its grievances against you rise to {GrievanceOf(owner):0}.";
        }
        return $"{name} holds this ground: its grievances against you rise to {GrievanceOf(owner):0} (standing {enclave.influence:0}/100).";
    }

    // Once a Seventh grievances fade a little.
    private void GrievanceTick() => WorldResources.Decay(_grievances, ExpeditionRules.grievanceDecayPerSeventh);

    // An envoy to an aggrieved enclave answers its grievances first; what is left raises your standing.
    private float AnswerGrievances(Enclave e, float envoy)
    {
        string key = e.AuthorityId;
        float held = GrievanceOf(key);
        if (held <= 0f) return envoy;
        float answered = Math.Min(held, envoy);
        _grievances[key] = held - answered;
        if (_grievances[key] <= 0.01f) _grievances.Remove(key);
        return envoy - answered;
    }

    // ===== PLANTING =====

    private string WhyNotPlantHere(WorldUnit unit, UnitSpec spec)
    {
        if (!WorldUnits.Can(spec, UnitAbility.Plant)) return "It cannot plant.";
        if (unit.seeds == null || unit.seeds.Count == 0) return "It carries no seeds: harvest a crop that gives them first.";
        if (unit.Moving) return "Stop it first.";
        return WorldResources.WhyNotPlant(Map, Settings.generation, Map.Get(unit.coord), unit.seeds[0]);
    }

    /// <summary>The seeds the unit would plant next (a resource site's name), or null.</summary>
    public string NextSeeds(WorldUnit unit) => unit?.seeds != null && unit.seeds.Count > 0 ? Settings.generation.ResourceSite(unit.seeds[0])?.name ?? unit.seeds[0] : null;

    private void FinishPlant(WorldUnit unit, WorldTile tile, List<string> notice)
    {
        if (unit.seeds == null || unit.seeds.Count == 0 || WorldResources.WhyNotPlant(Map, Settings.generation, tile, unit.seeds[0]) != null) return;
        var spec = Settings.generation.ResourceSite(unit.seeds[0]);
        WorldResources.Plant(Map, tile, spec.id, AgeNumber);
        unit.seeds.RemoveAt(0);
        WorldCivilization.Rebuild(Map, Settings.generation);
        string soil = spec.plantedFertility > 0.001f ? $" It enriches the soil (+{spec.plantedFertility:P0} fertility)." : spec.plantedFertility < -0.001f ? $" It drains the soil ({spec.plantedFertility:P0} fertility)." : string.Empty;
        notice.Add($"{UnitLabel(unit)} plants {spec.name} at {Place(tile)}.{soil}");
        var legends = LegendProgress.Instance;
        foreach (var member in Expeditions.Members(unit).ToList()) legends?.Award(member, LegendLore.FragmentTuning.improvement, $"Planted {spec.name}");
        MarkYieldsDirty();
        Changed?.Invoke();
    }

    // ===== THE LAND MOVING, ECHO BY ECHO =====

    // Once an Echo of the game's clock the sterile blooms move: Fated Flowers decay where sorrow gathers (fast where
    // Dissonance is high) and Forsaken ones heal where Coherence is high, Glimmerfern fades where its silver river or its
    // Forsaken field is gone, and new patches sprout (WorldResources.GrowEcho). Blooms that move creep a cell, and the
    // settlements draw blooms up around them by their mood and districts. Only what the map can see is announced.
    // Then the creatures (WorldEcology): populations grow, are preyed on and spread, dens thin out, recover or appear.
    // The Echo just lived shapes the creatures (WorldRhythm): its births, decay, spreading and predation, and how much
    // their memories weighed. Then the new Echo is announced by what the creatures do in it (once you know any).
    private void OnEcho(int _)
    {
        if (Map == null || SaveSession.Restoring) return;
        SyncCalendar();
        int season = WorldRhythm.Previous(Map.echo);
        var changes = WorldResources.GrowEcho(Map, Settings.generation, ++_echoesGrown, AgeNumber);
        // A generation passes for the Eleos Blooms that eat cocktails: they specialize, generalize or become varieties.
        var evolved = EvolveBlooms();
        int sitesBefore = Map.ResourceSites.Count;
        var living = WorldEcology.Tick(Map, Settings.generation, AgeNumber, _echoesGrown, season);
        var sickness = WorldPlagues.Tick(Map, Settings.generation, AgeNumber, _echoesGrown, season);
        var learned = WorldBehavior.Tick(Map, Settings.generation, AgeNumber, season);
        // Then the Enclaves: keepers keep and befriend, growers tend their land (WorldEnclaveEcology).
        var enclaves = EnclaveEcologyEcho().ToList();
        // Then the Great Plague's moths: traded, carried along the routes, bred on the sick (WorldGreatPlague).
        var moths = PlagueLines().ToList();
        // Dens grown hostile or calm again, and the season's hunger, change the danger around them (WorldResources.Refresh).
        WorldCivilization.Rebuild(Map, Settings.generation);
        var lines = changes.Select(GrowthLine).Concat(living.Select(EcologyLine)).Concat(evolved).Concat(sickness.Select(PlagueLine)).Concat(learned.Where(c => Knows(c.species.id, SpeciesLevel.Observed)).Select(BehaviorLine)).Concat(enclaves).Concat(moths).Where(l => l != null).ToList();
        string begins = SeasonLine();
        if (begins != null) lines.Insert(0, begins);
        if (lines.Count > 0) Say(string.Join(" ", lines));
        MarkYieldsDirty();
        Changed?.Invoke();
    }

    /// <summary>What the civilization knows of a species reaches <paramref name="level"/> (<see cref="SpeciesKnowledge"/>, with the saved lore).</summary>
    public bool Knows(string species, SpeciesLevel level) =>
        SpeciesKnowledge.LevelOf(Map, Settings.generation, species, SpeciesLoreKeeper.View) >= level;

    /// <summary>Mirror the time system's calendar onto the map, where the world's rules read it (<see cref="WorldRhythm"/>).</summary>
    private void SyncCalendar()
    {
        var time = TimeSystemLogic.Instance;
        if (Map == null) return;
        Map.echo = time != null ? time.CurrentEcho : 0;
        Map.echoPhase = time != null ? time.CurrentPhase : 0;
        Map.phaseCount = PhaseNow;
        Map.ritualSeventh = time != null && time.IsRitualSeventh;
    }

    // A new Phase: its Echo's effects open, peak or wane (den yields, hungry predators' reach), and every den may be hunted again.
    private void OnPhase(int _)
    {
        if (Map == null || SaveSession.Restoring) return;
        SyncCalendar();
        WorldCivilization.Rebuild(Map, Settings.generation);
        MarkYieldsDirty();
        Changed?.Invoke();
    }

    // The Ritual Seventh: the world attunes (WorldRhythm.Attune). Pure Light creatures surge or overload, and every species
    // calms toward whoever let it be this Phase. Only what you know of is told.
    private void OnRitualSeventh(int _)
    {
        if (Map == null || SaveSession.Restoring) return;
        SyncCalendar();
        var attuned = WorldRhythm.Attune(Map, Settings.generation, AgeNumber);
        var gen = Settings.generation;
        var told = attuned.Where(a => Knows(a.population.species, SpeciesLevel.Identified))
            .GroupBy(a => a.surged)
            .Select(g => $"{string.Join(", ", g.Select(a => $"the {gen.Species(a.population.species)?.name} in the {WorldEcology.MacroBiomeOf(Map, a.population.slot)?.name ?? "wilds"}").Distinct())} {(g.Key ? "surge with it" : "are overloaded where Coherence is too thin")}")
            .ToList();
        if (told.Count > 0) Say($"The Ritual Seventh attunes the world: {string.Join("; ", told)}.");
        MarkYieldsDirty();
        Changed?.Invoke();
    }

    // "The Echo of Silence begins: ..." once you know any creature (you have identified a species).
    private string SeasonLine()
    {
        var rhythm = WorldRhythm.Of(Settings.generation, Map.echo);
        if (rhythm == null || string.IsNullOrEmpty(rhythm.summary)) return null;
        bool known = Settings.generation.species.Any(s => s != null && Knows(s.id, SpeciesLevel.Identified));
        return known ? $"The {rhythm.name} begins: {rhythm.summary}." : null;
    }

    // A species' behavior toward you moving a step (you have met it: you know what it is).
    private static string BehaviorLine(BehaviorChange change)
    {
        if (change?.species == null) return null;
        string mood = change.gentler ? (change.to == ThreatResponse.Tolerates ? "it has grown used to you" : "it is calmer with you")
            : change.to == ThreatResponse.FleesOnSight || change.to == ThreatResponse.FleesWhenThreatened ? "it has learned to fear you" : "it has turned against you";
        string now = change.to == ThreatResponse.Tolerates ? "lets you come near now" : $"{WorldBehavior.Words(change.to)} now when you come near";
        return $"The {change.species.name} {now}: {mood}.";
    }

    // A resonance plague among creatures you have identified (E10): by its folklore until you understand one of its hosts.
    private string PlagueLine(PlagueChange change)
    {
        var gen = Settings.generation;
        var known = change?.hosts.Where(p => Knows(p.species, SpeciesLevel.Identified)).Select(p => gen.Species(p.species)?.name).Where(n => n != null).Distinct().ToList();
        if (known == null || known.Count == 0) return null;
        bool understood = change.plague.hosts.Any(h => Knows(h, SpeciesLevel.Understood));
        string name = WorldPlagues.Name(change.plague, understood), who = "the " + string.Join(" and the ", known);
        string where = WorldEcology.MacroBiomeOf(Map, change.outbreak.slot)?.name ?? "the wilds";
        switch (change.kind)
        {
            case PlagueChangeKind.BrokeOut:
                return understood ? $"{name} breaks out among {who} of the {where}: {change.plague.description}" : $"A sickness runs through {who} of the {where}; people call it {name}.";
            case PlagueChangeKind.Spread: return $"{UpperFirst(name)} has reached {who} of the {where}.";
            default: return $"{UpperFirst(name)} has passed from the {where}; the survivors carry its mark.";
        }
    }

    private static string UpperFirst(string text) => string.IsNullOrEmpty(text) ? text : char.ToUpperInvariant(text[0]) + text.Substring(1);

    // Only dens on known ground are announced, and only by what is known of them (an unidentified den by its kind).
    private string EcologyLine(EcologyChange change)
    {
        var site = change.site;
        if (site == null || site.center < 0 || site.center >= Map.Count || !Map[site.center].known) return null;
        var tile = Map[site.center];
        var gen = Settings.generation;
        string what = WorldResources.Identified(Map, site) ? site.name : WorldResources.Label(Map, gen, tile);
        switch (change.kind)
        {
            case EcologyChangeKind.Arrived: return $"Creatures have spread to {Place(tile)}: {what}.";
            case EcologyChangeKind.Depleted: return $"{what} at {Place(tile)}: too few are left to hunt.";
            case EcologyChangeKind.Recovered: return $"{what} at {Place(tile)}: their numbers have recovered.";
            default: return $"{what} at {Place(tile)}: none are left.";
        }
    }

    private string GrowthLine(GrowthChange change)
    {
        var site = change.site;
        if (site == null || site.center < 0 || site.center >= Map.Count) return null;
        var tile = Map[site.center];
        var gen = Settings.generation;
        switch (change.kind)
        {
            case GrowthKind.Turned:
                if (!tile.known) return null;
                bool decayed = (gen.ResourceSite(change.from)?.turnHurt ?? 0f) > 0f;
                return $"The {change.fromName} at {Place(tile)} {(decayed ? "decay" : "heal")} into {site.name}.";
            case GrowthKind.Faded:
                return tile.known ? $"The {change.fromName} at {Place(tile)} fades away." : null;
            case GrowthKind.Volunteered:
            {
                // What a settlement's life drew up around it (WorldResources.Volunteer): known ground is always told.
                if (!tile.known || change.settlement == null) return null;
                string why = change.reason == "grief" ? "fed by its people's grief"
                    : change.reason == "ease" ? "fed by its people's ease"
                    : $"drawn by its {Rules.tributaries.District(change.reason)?.name ?? change.reason}";
                return $"{site.name} take root beside {change.settlement.name}, {why}.";
            }
            default:
                return WorldResources.Sighted(tile, site, gen.ResourceSite(site.spec)) ? $"Something new grows at {Place(tile)}: {WorldResources.Label(Map, gen, tile)}." : null;
        }
    }

    // ===== IDENTIFYING =====

    // A survey that explores a site's first cell decodes it: its name, what it gives and what it does to the land
    // around. An expedition is offered the next knot of the site's event line and its legends are remembered for the find.
    private void IdentifySites(IEnumerable<WorldTile> explored, WorldUnit by, UnitSpec spec, List<string> notice)
    {
        var fresh = explored.Where(t => t.resourceSite >= 0).ToList();
        foreach (var group in fresh.GroupBy(t => t.resourceSite))
        {
            var site = WorldResources.SiteAt(Map, group.First());
            // Identified before this survey: one of its other cells was explored already.
            if (site == null || site.planted || site.cells.Count(c => Map[c].explored) > group.Count()) continue;
            Identify(site, by, spec, notice);
        }
    }

    private void Identify(ResourceSite site, WorldUnit by, UnitSpec spec, List<string> notice)
    {
        var siteSpec = Settings.generation.ResourceSite(site.spec);
        if (siteSpec == null) return;
        var effects = new List<string>();
        if (Math.Abs(siteSpec.landValue) > 0.01f) effects.Add($"land value {siteSpec.landValue:+0.#;-0.#}");
        if (siteSpec.yields.Any(y => y != null && y.amount > 0f)) effects.Add("yields once held");
        if (siteSpec.harvest.Any(h => h != null && h.amount > 0f)) effects.Add(siteSpec.seeds ? "can be harvested, with seeds" : "can be harvested");
        if (siteSpec.kind == ResourceKind.Bloom)
        {
            effects.Insert(0, $"an Eleos Bloom, {WorldResources.NicheWord(siteSpec.niche)}");
            if (siteSpec.dissonanceAura < -0.001f) effects.Add("drinks Dissonance");
            if (siteSpec.soothe > 0f) effects.Add("a sanctuary for expeditions");
            if (siteSpec.niche == BloomNiche.Predator) effects.Add("lures travellers");
            if (WorldResources.Withering(site, Settings.generation)) effects.Add("withering for want of feeling");
        }
        notice.Add($"Identified: {siteSpec.name}{(effects.Count > 0 ? $" ({string.Join(", ", effects)})" : string.Empty)}.");
        if (by == null) return;
        var legends = LegendProgress.Instance;
        int fragments = (int)Math.Round(Math.Max(0f, siteSpec.landValue) / 2f);
        if (legends != null && fragments > 0)
            foreach (var member in Expeditions.Members(by).ToList()) legends.Award(member, LegendLore.FragmentTuning.discovery, fragments, $"Identified {siteSpec.name}");
        if (spec == null || spec.role != UnitRole.Expedition || siteSpec.expeditionStories == null) return;
        string key = "site:" + siteSpec.id;
        _expeditionLines.TryGetValue(key, out int told);
        if (told >= siteSpec.expeditionStories.Count || string.IsNullOrEmpty(siteSpec.expeditionStories[told])) return;
        _expeditionLines[key] = told + 1;
        Tell(siteSpec.expeditionStories[told], by, $"Resource site '{siteSpec.id}'");
    }

    // The party that found the story plays it: its Director the protagonist, its companions co-protagonists.
    private void Tell(string story, WorldUnit by, string source)
    {
        string leader = by?.leader;
        if (leader != null) LastLeader = leader;
        if (leader != null && EventSystemLogic.Instance != null)
            EventSystemLogic.Instance.Summon(story, leader, Expeditions.Members(by).Where(m => !string.Equals(m, leader, StringComparison.OrdinalIgnoreCase)));
        var volumes = EventVolumeManager.Instance;
        if (volumes != null && volumes.UnlockStory(story))
            EventSystemLogic.Instance?.TriggerEventCheck();
        else GameLog.Warning($"{source}: story '{story}' was not found in Resources/Events.", Log);
    }
}
