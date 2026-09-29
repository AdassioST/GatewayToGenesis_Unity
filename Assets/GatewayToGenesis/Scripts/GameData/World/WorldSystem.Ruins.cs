using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Settlement Composure, loss, ruins and the Old World in play (<see cref="WorldRuins"/>): each Seventh settlements are
/// strained by danger and withering and ease when safe; the Surrendered become ruins that stay on the map. An expedition
/// investigates a ruin once for what the failure left behind (salvage, Research, perhaps an Enlightenment, perhaps a civic
/// adopted from the ruins: Digestive Rebirth). Reclaiming what was yours (a settlement founded on your ruins, land that
/// slipped away brought back) earns Era Score; a settlement lost beyond your authority earns 404, one reclaimed Welcome
/// Back, Traitors. Roads built over the Old World's broken roads restore them cheaply, as lesser roads until rebuilt.
/// </summary>
public partial class WorldSystem
{
    // The ruins of fallen settlements and of the Old World (the map's list; saved) and the cells of yours that slipped
    // away (saved), with what was held at the last look (derived: rebuilt after a load).
    [SaveOptionalField] private List<Ruin> _ruins = new List<Ruin>();
    [SaveOptionalField] private List<int> _lostLand = new List<int>();
    private HashSet<int> _heldLast;

    /// <summary>A settlement Surrendered and fell into ruins (raised once per fall, after it is announced).</summary>
    public event Action<Ruin> SettlementFell;
    /// <summary>A settlement of yours was founded on the ruins of one of yours: the ruin is reclaimed.</summary>
    public event Action<Ruin> RuinReclaimed;

    public LossRules LossRules => Rules != null ? Rules.loss : null;

    /// <summary>Share of the realm's output left while the Capital's Composure is shaken (1 when Clouded or calmer).</summary>
    public float CapitalOutput => Map == null || LossRules == null ? 1f : WorldRuins.CapitalOutput(Map, LossRules);

    /// <summary>A settlement's Composure state (a legend's five states).</summary>
    public ComposureState ComposureOf(Settlement s) => LossRules == null ? ComposureState.Clouded : WorldRuins.StateOf(LossRules, s);

    // A Seventh of strain and easing; deepening Composure and the fallen are announced (a settlement lost beyond your
    // authority earns 404); land of yours lost or reclaimed is tracked.
    private void LossTick(int sevenths)
    {
        if (Map == null || !MapUnlocked || LossRules == null) return;
        bool anchored = Map.Settlements.Any(s => s.anchor);
        var result = WorldRuins.Tick(Map, Settings.generation, LossRules, sevenths, AgeNumber);
        foreach (var (s, state) in result.deepened)
            Say(s.kind == SettlementKind.Capital
                ? $"The Capital's Composure is {state}: the whole realm yields {1f - CapitalOutput:P0} less until it eases or is mended."
                : state == ComposureState.Spiraling
                    ? $"{s.name} is Spiraling ({WorldRuins.CauseWords(s.harmedBy)}): mend it now, or it Surrenders and falls into ruins."
                    : $"{s.name}'s Composure is {state} ({WorldRuins.CauseWords(s.harmedBy)}): it yields less.");
        foreach (var ruin in result.fallen.Where(r => r != null))
            AfterFall(ruin);
        if (result.fallen.Count > 0 && anchored) WorldCivilization.SyncAnchors(Map, Rules, true);
        if (result.fallen.Count > 0) AfterCivilizationChange(null);
        else if (result.changed) { RecomputeYields(); Changed?.Invoke(); }
        TrackLand();
    }

    // Announce a fall and record it (404 for a settlement lost beyond your authority).
    private void AfterFall(Ruin ruin)
    {
        // A people's way of life ends here: grief and estrangement sink into the ground it stood on.
        if (Map.Get(ruin.coord) is WorldTile fell)
        {
            AddSuffering(fell.index, Feeling.Tumult, WorldSuffering.RuinGrief);
            AddSuffering(fell.index, Feeling.Estrangement, WorldSuffering.RuinGrief);
        }
        GameLog.Event($"{ruin.name} fell ({ruin.cause}) at {ruin.coord}", Log);
        Say($"{ruin.name} Surrenders and falls, {WorldRuins.CauseWords(ruin.cause)}. Its ruins remain on the map: an expedition can investigate them for what its failure left behind, and a settlement founded there reclaims it.");
        Achievements.Report(AchievementEvent.Of(AchievementSignal.SettlementLost, flag: ruin.detached).From($"settlement-lost:{ruin.settlement}", $"settlement:{ruin.settlement}"));
        SettlementFell?.Invoke(ruin);
    }

    /// <summary>
    /// Pillage <paramref name="s"/> (stories, raids): <paramref name="amount"/> strain at once, wearing its City Development
    /// down; anything but the Capital falls into ruins at Surrender. True when it fell.
    /// </summary>
    public bool Pillage(Settlement s, float amount, string reason = null)
    {
        if (Map == null || s == null || LossRules == null || !Map.Settlements.Contains(s)) return false;
        bool surrendered = WorldRuins.Strain(LossRules, s, amount, WorldRuins.Pillage);
        if (!surrendered || s.kind == SettlementKind.Capital)
        {
            if (!string.IsNullOrEmpty(reason)) Say($"{s.name} is pillaged, {reason}: its Composure is {ComposureOf(s)} (strain {s.strain:0}).");
            AfterCivilizationChange(null);
            return false;
        }
        bool anchored = s.anchor;
        var ruin = WorldRuins.Fall(Map, Settings.generation, s, WorldRuins.Pillage, AgeNumber);
        if (ruin == null) return false;
        if (anchored) WorldCivilization.SyncAnchors(Map, Rules, true);
        AfterFall(ruin);
        AfterCivilizationChange(null);
        return true;
    }

    /// <summary>
    /// A story's <c>settlement:</c> consequence: <paramref name="target"/> is "capital", "exposed" (the settlement in the
    /// most danger, the Capital last) or a settlement's name; a negative <paramref name="value"/> pillages it by that much
    /// strain, a positive one eases its strain. False when no such settlement stands.
    /// </summary>
    public bool ApplySettlementDamage(string target, int value, string story)
    {
        var s = SettlementNamed(target);
        if (s == null) return false;
        if (value < 0) Pillage(s, -value, story != null ? $"as told in {story}" : null);
        else if (value > 0 && s.strain > 0f)
        {
            s.strain = Math.Max(0f, s.strain - value);
            AfterCivilizationChange(null);
        }
        return true;
    }

    /// <summary>The settlement a story names: "capital", "exposed" (most endangered, the Capital last) or its name.</summary>
    public Settlement SettlementNamed(string target)
    {
        if (Map == null || string.IsNullOrEmpty(target)) return null;
        if (string.Equals(target, "capital", StringComparison.OrdinalIgnoreCase)) return WorldCivilization.Capital(Map);
        if (string.Equals(target, "exposed", StringComparison.OrdinalIgnoreCase))
            return Map.Settlements.OrderBy(s => s.kind == SettlementKind.Capital ? 1 : 0).ThenByDescending(s => Map.Get(s.coord)?.danger ?? 0f)
                .ThenByDescending(s => HexCoord.Distance(s.coord, Map.Capital)).ThenBy(s => s.id).FirstOrDefault();
        return Map.Settlements.FirstOrDefault(s => string.Equals(s.name, target, StringComparison.OrdinalIgnoreCase));
    }

    public string MendCostText(Settlement s) => CostText(LossRules?.mendCostPer10, WorldRuins.MendScale(LossRules, s));

    /// <summary>Why <paramref name="s"/>'s Composure cannot be mended now, or null.</summary>
    public string WhyNotMend(Settlement s)
    {
        if (Map == null || s == null || LossRules == null) return "Nothing to mend.";
        if (WorldRuins.MendScale(LossRules, s) <= 0f) return "Its Composure is at rest.";
        return Unaffordable(LossRules.mendCostPer10, WorldRuins.MendScale(LossRules, s));
    }

    /// <summary>Mend a settlement's Composure at once: its strain falls to the Clouded baseline (City Development lost to it grows back as it would).</summary>
    public bool Mend(Settlement s)
    {
        string why = WhyNotMend(s);
        if (why != null) { Say(why); return false; }
        Pay(LossRules.mendCostPer10, WorldRuins.MendScale(LossRules, s));
        s.strain = Math.Min(s.strain, LossRules.composure.baseline);
        AfterCivilizationChange($"{s.name}'s Composure is mended: {ComposureOf(s)} again.");
        return true;
    }

    // ===== RECLAIMING =====

    // A settlement founded on the ruins of one of yours reclaims it: Era Score and Welcome Back, Traitors. Returns the
    // words for the founding notice (null when nothing was reclaimed).
    private string Reclaim(Settlement s)
    {
        var ruin = WorldRuins.Reclaimable(Map, s.coord);
        if (ruin == null || LossRules == null) return null;
        ruin.reclaimed = true;
        ruin.reclaimedBy = s.name;
        EraOnce("reclaim-ruin:" + ruin.id, LossRules.eraReclaim, $"Reclaimed the ruins of {ruin.name}");
        Achievements.Report(AchievementEvent.Of(AchievementSignal.SettlementReclaimed).From($"settlement-reclaimed:{ruin.id}", $"settlement:{ruin.settlement}"));
        RuinReclaimed?.Invoke(ruin);
        return $"It rises on the ruins of {ruin.name}: what was lost is reclaimed.";
    }

    // Land of yours that slipped away is remembered; bringing any of it back earns Era Score (once a Seventh).
    private void TrackLand()
    {
        if (Map == null || LossRules == null) return;
        var held = new HashSet<int>(Map.Tiles.Where(t => WorldAuthority.IsPlayers(t.authorityId)).Select(t => t.index));
        if (_heldLast == null) { _heldLast = held; return; }
        _lostLand = _lostLand ?? new List<int>();
        int back = WorldRuins.TrackLand(_heldLast, held, _lostLand);
        _heldLast = held;
        if (back <= 0) return;
        AgeProgression.Award(LossRules.eraReclaim, back == 1 ? "Reclaimed a cell of lost land" : $"Reclaimed {back} cells of lost land");
        Say(back == 1 ? "A cell of land your people lost is yours again." : $"{back} cells of land your people lost are yours again.");
    }

    /// <summary>Cells of yours that slipped away and have not been reclaimed yet.</summary>
    public IReadOnlyList<int> LostLand => _lostLand ?? new List<int>();

    // ===== OLD ROADS =====

    /// <summary>What building <paramref name="s"/>'s road would restore of the Old World's (cells) and build anew.</summary>
    public (int restored, int fresh) RoadPlan(Settlement s)
    {
        if (Map == null || s == null || !WorldCivilization.PlanRoad(Map, Settings.generation, s, out var path, out _)) return (0, 0);
        return WorldRuins.RoadCells(Map, path);
    }

    /// <summary>The routes from <paramref name="s"/> with restored Old World stretches still to rebuild.</summary>
    public IEnumerable<TradeRoute> RestoredRoutes(Settlement s) =>
        Map == null || s == null ? Enumerable.Empty<TradeRoute>() : Map.Routes.Where(r => (r.from == s.id || r.to == s.id) && WorldRuins.RestoredCount(r) > 0);

    public string RebuildCostText(TradeRoute route) => CostText(Rules.roadCostPerCell, WorldRuins.RebuildScale(LossRules?.oldWorld, route));

    public string WhyNotRebuild(TradeRoute route)
    {
        string locked = Locked();
        if (locked != null) return locked;
        if (route == null || WorldRuins.RestoredCount(route) == 0) return "Nothing of it is only restored.";
        return Unaffordable(Rules.roadCostPerCell, WorldRuins.RebuildScale(LossRules?.oldWorld, route));
    }

    /// <summary>Rebuild a route's restored Old World stretches into full road: its efficiency, City Development and governance in full.</summary>
    public bool RebuildRoad(TradeRoute route)
    {
        string why = WhyNotRebuild(route);
        if (why != null) { Say(why); return false; }
        Pay(Rules.roadCostPerCell, WorldRuins.RebuildScale(LossRules?.oldWorld, route));
        int cells = WorldRuins.RestoredCount(route);
        WorldCivilization.RebuildRestored(Map, Settings.generation, route);
        AfterCivilizationChange($"{cells} cells of restored Old World road are rebuilt (efficiency {route.efficiency:P0}).");
        return true;
    }

    // ===== RUINS =====

    private string WhyNotInvestigate(WorldUnit unit)
    {
        if (!IsExpedition(unit)) return "Only an expedition of legends can read what the ruins left.";
        return WorldRuins.WhyNotInvestigate(WorldRuins.At(Map, unit.coord));
    }

    /// <summary>The expedition free to investigate <paramref name="ruin"/> nearest it (standing there first), or null.</summary>
    public WorldUnit InvestigatorFor(Ruin ruin)
    {
        if (Map == null || ruin == null || ruin.investigated) return null;
        return Map.Units.Where(u => !u.Missing && IsExpedition(u) && WorldUnits.Can(SpecOf(u), UnitAbilities.Investigate.ability))
            .OrderBy(u => u.coord == ruin.coord ? 0 : 1).ThenBy(u => u.Moving || u.Working ? 1 : 0).ThenBy(u => HexCoord.Distance(u.coord, ruin.coord)).ThenBy(u => u.id).FirstOrDefault();
    }

    /// <summary>Send <paramref name="unit"/> to investigate <paramref name="ruin"/> (it starts at once when already there).</summary>
    public bool InvestigateRuin(WorldUnit unit, Ruin ruin)
    {
        if (unit == null || ruin == null) return false;
        if (unit.coord == ruin.coord) return Work(unit, UnitAbilities.Investigate);
        return Go(unit, ruin.coord, UnitTask.Investigate);
    }

    // The party read the ruins: salvage and Research into the stores, perhaps an Enlightenment, perhaps a civic left
    // for adoption; each legend is remembered for it.
    private void FinishInvestigate(WorldUnit unit, List<string> notice)
    {
        var ruin = WorldRuins.At(Map, unit.coord);
        if (WorldRuins.WhyNotInvestigate(ruin) != null) return;
        ruin.investigated = true;
        var findings = WorldRuins.Findings(Map, Rules, ruin);
        float multiplier = Math.Max(0f, SpecOf(unit)?.rewardMultiplier ?? 1f);
        var units = GameUnitsLogic.Instance;
        var found = new List<string>();
        foreach (var a in findings.resources)
        {
            float amount = a.amount * multiplier;
            if (units != null && GameCatalog.IsResource(a.resource)) units.ChangeResourceFromName(a.resource, amount, false);
            found.Add($"{amount:0.#} {a.resource}");
        }
        notice.Add($"{unit.name} investigates the ruins of {ruin.name}: {string.Join(", ", found)}.");
        if (findings.enlighten && units != null)
        {
            var candidates = units.ResearchableTechnologies().Where(t => !t.enlightenedCompleted && t.gameUnit != null)
                .Select(t => t.gameUnit.name).OrderBy(n => n, StringComparer.Ordinal).ToList();
            string technology = WorldRuins.Pick(candidates, findings.pick);
            if (technology != null && units.EnlightenTechnology(technology, $"The records in the ruins of {ruin.name}"))
                notice.Add($"Its records enlighten {technology}.");
        }
        if (findings.civic)
        {
            var civics = CivicManager.Instance;
            var candidates = GameCatalog.Civics.All.Where(c => c != null && (civics == null || !civics.IsCivicActive(c.civicName)))
                .Select(c => c.civicName).OrderBy(n => n, StringComparer.Ordinal).ToList();
            ruin.civic = WorldRuins.Pick(candidates, findings.pick);
            if (ruin.civic != null) notice.Add($"Its people's ways survive in what they left: {ruin.civic} can be adopted from the ruins.");
        }
        var legends = LegendProgress.Instance;
        if (legends != null && LossRules.fragments > 0)
            foreach (var member in Expeditions.Members(unit).ToList())
                legends.Award(member, LegendLore.FragmentTuning.discovery, LossRules.fragments, $"Read the ruins of {ruin.name}");
        EraOnce("ruin:" + ruin.id, LossRules.eraRuin, $"Investigated the ruins of {ruin.name}");
        GameLog.Event($"{unit.name} investigated the ruins of {ruin.name} (enlighten {findings.enlighten}, civic {ruin.civic ?? "none"})", Log);
    }

    /// <summary>
    /// Why the civic left in <paramref name="ruin"/> cannot be adopted now, or null. <paramref name="replacing"/>: one of
    /// your civics it would take the place of (T07: its slot and conflicts no longer count).
    /// </summary>
    public string WhyNotAdoptRuinCivic(Ruin ruin, string replacing = null)
    {
        if (ruin == null || string.IsNullOrEmpty(ruin.civic)) return "The ruins left no civic.";
        if (ruin.civicAdopted) return $"{ruin.civic} was adopted from these ruins already.";
        var civics = CivicManager.Instance;
        if (civics == null) return "No government to adopt it.";
        if (!string.IsNullOrEmpty(replacing))
        {
            if (!civics.IsCivicActive(replacing)) return $"{replacing} is not one of your civics.";
            if (string.Equals(replacing, ruin.civic, StringComparison.OrdinalIgnoreCase)) return $"{ruin.civic} is yours already.";
        }
        var (can, reasons) = civics.CanUnlockCivic(ruin.civic, inherited: true, replacing: replacing);
        return can ? null : string.Join("; ", reasons);
    }

    /// <summary>
    /// Adopt the civic <paramref name="ruin"/> left behind: its people already lived by it, so its requirements are waived
    /// (slots and conflicts still hold). Earns Digestive Rebirth (its "reform a culture" half belongs to the culture system).
    /// <paramref name="replacing"/> (T07): one of your civics is removed first, with all its effects (and its removal
    /// penalties), and the ruin's civic takes its place; if the ruin's civic then cannot be adopted, yours is restored.
    /// The ruin's one-time adoption (<see cref="Ruin.civicAdopted"/>) is the same ledger either way.
    /// </summary>
    public bool AdoptRuinCivic(Ruin ruin, string replacing = null)
    {
        string why = WhyNotAdoptRuinCivic(ruin, replacing);
        if (why != null) { Say(why); return false; }
        if (!string.IsNullOrEmpty(replacing))
        {
            if (!CivicManager.Instance.RemoveCivic(replacing, $"Replaced by {ruin.civic}, from the ruins of {ruin.name}")) return false;
            if (!CivicManager.Instance.UnlockCivic(ruin.civic, $"Ruins of {ruin.name}", inherited: true))
            {
                CivicManager.Instance.UnlockCivic(replacing, "Restored: the replacement failed", inherited: true);
                return false;
            }
        }
        else if (!CivicManager.Instance.UnlockCivic(ruin.civic, $"Ruins of {ruin.name}", inherited: true)) return false;
        ruin.civicAdopted = true;
        Achievements.Report(AchievementEvent.Of(AchievementSignal.RuinCivicAdopted).From($"ruin-civic:{ruin.id}:{ruin.civic}", ruin.civic));
        AfterCivilizationChange($"{ruin.civic} is adopted from the ruins of {ruin.name}: the ruins of the past fuel the roots of the present.");
        return true;
    }
}
