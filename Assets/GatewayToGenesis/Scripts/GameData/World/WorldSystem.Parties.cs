using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// The moves only small parties have (<see cref="PartyShapes"/>): a Solo or Duo retreats from danger to the nearest
/// safe ground at its retreat pace, and a legend alone can go to ground (missing in action) once its Composure is deep
/// enough, or instead of breaking when worn to nothing, and turns up Sevenths later at the nearest ground of your
/// territory. While missing it stays on the map's list (it still holds its expedition slot and is not free for the
/// council) but is not drawn and nothing befalls it.
/// </summary>
public partial class WorldSystem
{
    /// <summary>The shape of an expedition's party (Solo, Duo, Trio, Company).</summary>
    public PartyShape PartyShapeOf(WorldUnit unit) => PartyShapes.Of(ExpeditionRules, unit);

    // Held ground as the units see it (inside your authority or a settlement).
    private static bool HeldForUnits(WorldTile t) => t != null && (WorldAuthority.IsPlayers(t.authorityId) || t.settlement >= 0);

    // ===== RETREAT =====

    /// <summary>Why the expedition cannot retreat now, or null (with the way to the nearest safe ground).</summary>
    public string WhyNotRetreat(WorldUnit unit, out List<int> path)
    {
        path = null;
        if (!IsExpedition(unit)) return "Only an expedition retreats.";
        string why = PartyShapes.WhyNotRetreat(unit, UnitSurroundings.Of(Map, unit), ExpeditionRules);
        if (why != null) return why;
        var x = ExpeditionRules;
        var here = Map.Get(unit.coord);
        bool found = MicroNavigation.FindNearest(Map, Settings.generation, WorldUnits.MicroPosition(unit), id =>
        {
            var t = Map[id / MicroNavigation.PerCell];
            return t != here && PartyShapes.Safe(HeldForUnits(t), t.danger, t.dissonance, x);
        }, out path, out _);
        return found && path.Count > 0 ? null : "No safe ground can be reached from here.";
    }

    public string WhyNotRetreat(WorldUnit unit) => WhyNotRetreat(unit, out _);

    /// <summary>Slip away to the nearest safe ground at the retreat pace, dropping any work; it makes camp there.</summary>
    public bool Retreat(WorldUnit unit)
    {
        string why = WhyNotRetreat(unit, out var path);
        if (why != null)
        {
            Say(why);
            return false;
        }
        WorldUnits.Order(Map, unit, path);
        unit.task = UnitTask.None;
        unit.workLeft = 0f;
        unit.autoExplore = false;
        unit.returning = false;
        unit.onArrival = UnitTask.None;
        unit.retreating = true;
        var end = Map.Get(HexHierarchy.Parent(unit.path[unit.path.Count - 1]));
        UnitSays(unit, $"{unit.name} retreats", $"{UnitLabel(unit)} slips away from {Place(Map.Get(unit.coord))} toward {Place(end)}.");
        Changed?.Invoke();
        return true;
    }

    // ===== MISSING IN ACTION =====

    /// <summary>Why the expedition's legend cannot go to ground now, or null.</summary>
    public string WhyNotGoToGround(WorldUnit unit)
    {
        if (!IsExpedition(unit)) return "Only a legend on an expedition goes to ground.";
        var state = LegendProgress.Instance != null && unit.leader != null ? LegendProgress.Instance.Composure(unit.leader) : ComposureState.Clouded;
        return PartyShapes.WhyNotVanish(unit, UnitSurroundings.Of(Map, unit), state, ExpeditionRules);
    }

    /// <summary>The lone legend drops out of sight, to turn up later at the nearest ground of your territory.</summary>
    public bool GoToGround(WorldUnit unit)
    {
        string why = WhyNotGoToGround(unit);
        if (why != null)
        {
            Say(why);
            return false;
        }
        GoMissing(unit, forced: false);
        return true;
    }

    // Where and when a legend gone to ground turns up: the nearest held micro hex (where it stands, if held; else the
    // Capital), after the base Sevenths plus the walk there made slowly and unseen.
    private void GoMissing(WorldUnit unit, bool forced)
    {
        var x = ExpeditionRules;
        var spec = SpecOf(unit);
        var from = WorldUnits.MicroPosition(unit);
        string place = Place(Map.Get(unit.coord));
        string legend = unit.leader ?? unit.name;
        HexCoord to;
        float walk;
        if (HeldForUnits(Map.Get(unit.coord)))
        {
            to = from;
            walk = 0f;
        }
        else if (MicroNavigation.FindNearest(Map, Settings.generation, from, id => HeldForUnits(Map[id / MicroNavigation.PerCell]), out var path, out float fatigue) && path.Count > 0)
        {
            to = MicroNavigation.Coord(Map, path[path.Count - 1]);
            walk = WorldUnits.SeventhsFor(unit, spec, fatigue);
        }
        else
        {
            // Nothing held can be reached on foot: it finds its way to the Capital however it can.
            to = MicroNavigation.Center(Map.Capital);
            float stamina = spec != null ? Math.Max(0.1f, spec.stamina) : 1f;
            walk = HexCoord.Distance(from, to) * 2.5f / stamina;
        }
        // Settlers scattered in the wild are lost (they were counted out of the city when they left).
        int settlers = unit.settlers;
        PartyShapes.Vanish(unit, to, PartyShapes.MissingSevenths(x, walk));
        _partySpecs.Remove(unit.id);
        string lost = settlers > 0 ? $" The {settlers} settlers in its care scatter." : string.Empty;
        if (forced)
        {
            UnitSays(unit, $"{legend} is missing", $"{unit.name} is worn to nothing at {place}. Rather than break, {legend} goes to ground; no word will come until they turn up.{lost}");
            // Alone, the road is let go of more lightly than a party breaking together.
            LegendProgress.Instance?.Strain(legend, x.breakStrain * 0.5f, $"Went to ground from {unit.name}");
        }
        else UnitSays(unit, $"{legend} goes to ground", $"{legend} slips away from everything at {place}; no word will come until they turn up.");
        GameLog.Event($"{legend} went missing at {unit.coord} ({(forced ? "worn out" : "chose to")}), turns up at {to} in {unit.missingSevenths:0.#} Sevenths", Log);
        Changed?.Invoke();
    }

    // Once a Seventh: the missing come closer to turning up.
    private void TickMissing()
    {
        foreach (var unit in Map.Units.Where(u => u.Missing).ToList())
        {
            if (!PartyShapes.Tick(unit)) continue;
            PartyShapes.Reappear(unit, ExpeditionRules);
            _partySpecs.Remove(unit.id);
            var spec = SpecOf(unit);
            var notice = new List<string>();
            if (spec != null) Look(unit, spec, notice);
            string legend = unit.leader ?? unit.name;
            UnitSays(unit, $"{legend} turns up", $"{legend} walks out of the wild at {Place(Map.Get(unit.coord))}, worn but whole, and makes camp. {string.Join(" ", notice)}".TrimEnd());
            // Finding the way back is its own growth (Fragments of Acceptance, as for a broken road).
            if (unit.leader != null) LegendProgress.Instance?.Award(unit.leader, LegendLore.FragmentTuning.brokenRoad, $"Found the way back to {Place(Map.Get(unit.coord))}");
            Changed?.Invoke();
        }
    }
}
