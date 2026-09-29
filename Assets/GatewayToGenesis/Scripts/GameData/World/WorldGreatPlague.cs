using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// The Great Plague's vector in the living world (vault: Arcanorian Ecology.md, "The Great Plague's Moths"; vault: Great Plague.md). Every number is a
/// proposal (Canon Gaps.md, "The Great Plague's moths").
/// </summary>
[Serializable]
public class GreatPlagueSettings
{
    [UnityEngine.Tooltip("The vector species (its SpeciesSpec.vector feeds Disease Burden through WorldEcology.VectorPressure).")]
    public string vector = "luminant-moth";
    [UnityEngine.Tooltip("The Age whose crisis is the Great Plague (its asset id).")]
    public string age = "age-of-renewal";
    [UnityEngine.Tooltip("The crisis stage (0-based) from which the moths are known to carry it: Great Plague.md, \"The Moths Are Everywhere\", midway.")]
    [UnityEngine.Min(0)] public int revealStage = 1;

    [UnityEngine.Header("Trade")]
    [UnityEngine.Tooltip("Share of the moths in one Macro Biome a trade route carries each Echo into each other Macro Biome it passes (caravans, lanterns, moth gardens).")]
    [UnityEngine.Range(0f, 1f)] public float routeCarry = 0.1f;
    [UnityEngine.Tooltip("Groups an Enclave that trades the moths (EnclaveSpec.trades) brings each Echo into each Macro Biome of your settlements within tradeReach.")]
    [UnityEngine.Min(0f)] public float tradeGroups = 0.5f;
    [UnityEngine.Min(0)] public int tradeReach = 16;

    [UnityEngine.Header("The plague-era lifecycle")]
    [UnityEngine.Tooltip("While the plague runs, moths near your settlements grow by this share of themselves each Echo at Disease Burden 1: the sick's blood feeds them (they may outgrow their land and spread).")]
    [UnityEngine.Min(0f)] public float plagueBreeding = 0.5f;

    [UnityEngine.Header("Restricting the moth farms")]
    [UnityEngine.Tooltip("Echoes a trading Enclave stops its moth trade when asked (after the moths are known to carry the plague).")]
    [UnityEngine.Min(1)] public int restrictEchoes = 4;
    [UnityEngine.Tooltip("Standing it needs to agree (a suzerain always does), and spends (a suzerain's is not spent).")]
    [UnityEngine.Range(0f, 100f)] public float restrictStanding = 40f;
    [UnityEngine.Range(0f, 100f)] public float restrictStandingCost = 15f;
    public List<ResourceAmount> restrictCost = new List<ResourceAmount>();
}

/// <summary>Where the plague stands for the moths: from the Ages and the people (<see cref="WorldGreatPlague.Echo"/>).</summary>
public struct PlagueInputs
{
    /// <summary>The Great Plague's crisis has begun and not passed.</summary>
    public bool running;
    /// <summary>Disease Burden now (0-1).</summary>
    public float burden;
}

/// <summary>What the moths did at an Echo, for the notices.</summary>
public class PlagueEcho
{
    /// <summary>Macro Biome slots the routes carried them into, where none lived before.</summary>
    public List<int> carried = new List<int>();
    /// <summary>Groups the trading Enclaves brought.</summary>
    public float traded;
    /// <summary>Groups bred on the sick near your settlements.</summary>
    public float bred;
}

/// <summary>
/// The Great Plague's moths (vault: Arcanorian Ecology.md, "The Great Plague's Moths"), with no scene state (tested in <c>GreatPlagueTests</c>). The
/// Luminant Moths are beloved lantern-light; a Domestication Enclave farms and trades them (<see cref="EnclaveSpec.trades"/>).
/// Once an Echo (after the Enclaves' own):
///
/// - Trade routes carry them between the Macro Biomes they pass; the trading Enclaves bring them to your settlements.
/// - While the plague runs, moths near your settlements breed on the sick (Great Plague.md's plague-era lifecycle).
/// - They carry the Luminant Decay to people as a disease vector (<see cref="SpeciesSpec.vector"/>, fed to Disease Burden
///   by <see cref="WorldEcology.VectorPressure"/>).
/// - Their part is known midway (<see cref="Revealed"/>): from the crisis stage <see cref="GreatPlagueSettings.revealStage"/>,
///   once the plague's Age has passed, or once the moths are Understood (an Auric Enclave's study).
/// - What works: culling through a Militant Enclave (E7; it costs the moths' behavior toward you, E3), sanitizing through
///   the Agromagical and Domestication Enclaves (E6's Enclave treatments), and asking the trading Enclave to restrict its
///   farms (<see cref="Restrict"/>). None of it touches the crisis' severity: the moths are the mechanism, not the root
///   (the Dual Confluence Stream's saturation, still to come with the Resonance economy).
/// </summary>
public static class WorldGreatPlague
{
    /// <summary>The Enclave trades the plague's vector.</summary>
    public static bool Trades(WorldGenSettings settings, Enclave e) =>
        e != null && settings?.greatPlague != null && string.Equals(settings.Enclave(e.spec)?.trades, settings.greatPlague.vector, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// The moths' part in the plague is known: its Age has passed (<paramref name="agePassed"/>), its crisis reached
    /// <see cref="GreatPlagueSettings.revealStage"/> (<paramref name="currentAge"/>, <paramref name="stageReached"/>, 0-based;
    /// -1 before the first), or the moths are understood (<paramref name="level"/>).
    /// </summary>
    public static bool Revealed(GreatPlagueSettings g, string currentAge, int stageReached, bool agePassed, SpeciesLevel level)
    {
        if (g == null) return false;
        if (agePassed || level >= SpeciesLevel.Understood) return true;
        return string.Equals(currentAge, g.age, StringComparison.OrdinalIgnoreCase) && stageReached >= g.revealStage;
    }

    /// <summary>The Echo's moths: routes carry them, trading Enclaves bring them, and while the plague runs they breed on the sick.</summary>
    public static PlagueEcho Echo(WorldMap map, WorldGenSettings settings, int age, int echo, PlagueInputs inputs)
    {
        var report = new PlagueEcho();
        var g = settings?.greatPlague;
        var e = settings?.ecology;
        var species = settings?.Species(g?.vector);
        if (map == null || g == null || e == null || species == null) return report;
        var habitat = WorldEcology.Habitat(map, settings, species.id, age);
        float[] pressure = null;
        Population Ensure(int slot)
        {
            var p = WorldEcology.Find(map, slot, species.id);
            if (p != null) return p;
            if (!habitat.TryGetValue(slot, out var cells) || cells.Count == 0) return null;
            pressure = pressure ?? WorldEcology.Pressure(map, e);
            map.Populations.Add(p = new Population { slot = slot, species = species.id, capacity = WorldEcology.Capacity(map, e, species, cells, pressure) });
            return p;
        }

        // Trade routes carry them between the Macro Biomes they pass (from what lived there at the Echo's start).
        var before = map.Populations.Where(p => p.groups > 0f && string.Equals(p.species, species.id, StringComparison.OrdinalIgnoreCase)).ToDictionary(p => p.slot, p => p.groups);
        foreach (var route in map.Routes ?? new List<TradeRoute>())
        {
            var slots = route.cells.Select(c => map.Get(c)).Where(t => t != null && t.habitatSlot >= 0).Select(t => t.habitatSlot).Distinct().OrderBy(s => s).ToList();
            foreach (int from in slots)
            {
                if (!before.TryGetValue(from, out float groups) || groups <= 0f) continue;
                foreach (int to in slots.Where(s => s != from))
                {
                    var there = Ensure(to);
                    if (there == null) continue;
                    if (there.groups <= 0f && !report.carried.Contains(to)) report.carried.Add(to);
                    there.groups += Clamp01(g.routeCarry) * groups;
                }
            }
        }

        // The trading Enclaves bring them to your settlements (unless asked to restrict their farms).
        foreach (var enclave in map.Enclaves.Where(x => x != null && Trades(settings, x) && x.restrictedUntil <= echo))
        {
            foreach (int slot in map.Settlements.Where(s => HexCoord.Distance(s.coord, enclave.coord) <= g.tradeReach)
                         .Select(s => map.Get(s.coord)?.habitatSlot ?? -1).Where(s => s >= 0).Distinct().OrderBy(s => s))
            {
                var there = Ensure(slot);
                if (there == null) continue;
                there.groups += g.tradeGroups;
                report.traded += g.tradeGroups;
            }
        }

        // The plague-era lifecycle: the sick's blood feeds the moths near your settlements.
        if (inputs.running && inputs.burden > 0f && g.plagueBreeding > 0f)
        {
            var near = SlotsNearSettlements(map, e.vectorReach);
            foreach (var p in map.Populations.Where(p => p.groups > 0f && near.Contains(p.slot) && string.Equals(p.species, species.id, StringComparison.OrdinalIgnoreCase)))
            {
                float bred = p.groups * g.plagueBreeding * Clamp01(inputs.burden);
                p.groups += bred;
                report.bred += bred;
            }
        }
        return report;
    }

    /// <summary>The Macro Biome slots within <paramref name="reach"/> of any of your settlements.</summary>
    public static HashSet<int> SlotsNearSettlements(WorldMap map, int reach)
    {
        var slots = new HashSet<int>();
        foreach (var s in map.Settlements)
            foreach (var c in HexCoord.Spiral(s.coord, Math.Max(0, reach)))
                if (map.Get(c) is WorldTile t && t.habitatSlot >= 0) slots.Add(t.habitatSlot);
        return slots;
    }

    // ===== RESTRICTING THE FARMS =====

    /// <summary>Why the Enclave cannot be asked to restrict its moth farms now, or null. <paramref name="revealed"/>: the moths' part is known.</summary>
    public static string WhyNotRestrict(WorldMap map, WorldGenSettings settings, Enclave e, int echo, bool revealed)
    {
        var g = settings?.greatPlague;
        if (map == null || g == null || e == null) return "No enclave.";
        if (!Trades(settings, e)) return "It trades no moths.";
        var home = map.Get(e.coord);
        if (home == null || !home.revealed) return "Find it first.";
        if (!revealed) return "No one yet knows the moths have anything to do with the sickness.";
        if (e.restrictedUntil > echo) return $"Its farms are already restricted ({e.restrictedUntil - echo} Echo(es) left).";
        if (!e.suzerain && e.influence < g.restrictStanding) return $"{e.name} will not give up its trade for a stranger: standing {g.restrictStanding:0} needed (now {e.influence:0}).";
        return null;
    }

    /// <summary>The Enclave stops its moth trade for <see cref="GreatPlagueSettings.restrictEchoes"/> Echoes (check <see cref="WhyNotRestrict"/> first).</summary>
    public static void Restrict(WorldGenSettings settings, Enclave e, int echo)
    {
        var g = settings.greatPlague;
        e.restrictedUntil = echo + Math.Max(1, g.restrictEchoes);
        if (!e.suzerain) e.influence = Math.Max(0f, e.influence - Math.Max(0f, g.restrictStandingCost));
    }

    private static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;
}
