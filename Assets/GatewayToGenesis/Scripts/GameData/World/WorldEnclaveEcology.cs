using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>What an Enclave does with the creatures when you ask it (<see cref="WorldEnclaveEcology.ServiceOf"/>). Append-only.</summary>
public enum EnclaveService
{
    /// <summary>Its family does not tend creatures (yet).</summary>
    None,
    /// <summary>Militant: hunters thin a population out (less of it, and it remembers who sent them).</summary>
    Cull,
    /// <summary>Domestication: keepers teach a species to trust your people.</summary>
    Tame,
    /// <summary>Agromagical: the land a population lives on is tended until it recovers.</summary>
    Restore,
    /// <summary>Auric: scholars study a species until your people understand it.</summary>
    Study,
}

public enum EnclaveEcologyChangeKind { Kept, Released }

/// <summary>A Domestication Enclave beginning or ceasing to keep a species (<see cref="WorldEnclaveEcology.Echo"/>), for the notices.</summary>
public class EnclaveEcologyChange
{
    public EnclaveEcologyChangeKind kind;
    public Enclave enclave;
    public SpeciesSpec species;
}

/// <summary>
/// How the Enclaves act on the creatures (vault: Arcanorian Ecology.md, "Enclaves and Creatures"). Every number is a proposal (Canon Gaps.md,
/// "Enclave ecology").
/// </summary>
[Serializable]
public class EnclaveEcologySettings
{
    [UnityEngine.Header("Domestication: keeping")]
    [UnityEngine.Tooltip("Cells around a Domestication Enclave whose Macro Biomes' creatures it keeps (each species its spec's keepsPureLight allows).")]
    [UnityEngine.Min(0)] public int keepReach = 6;
    [UnityEngine.Tooltip("Temper a kept species gains toward its keepers each Echo (up to +1: gentle with its keepers).")]
    [UnityEngine.Range(0f, 1f)] public float keeperBefriend = 0.08f;
    [UnityEngine.Tooltip("Temper a kept species gains toward your people each Echo while you hold its keepers' Suzerainty (they share what they know).")]
    [UnityEngine.Range(0f, 1f)] public float sharedBefriend = 0.04f;
    [UnityEngine.Tooltip("Share of a hunt's harm a species kept by your suzerain keepers takes: a kept herd is culled with care.")]
    [UnityEngine.Range(0f, 1f)] public float keptHuntHarm = 0.25f;
    [UnityEngine.Tooltip("Production per second each species your suzerain keepers keep brings in as herds, times its abundance (up to 1).")]
    public List<ResourceAmount> herdYields = new List<ResourceAmount>();

    [UnityEngine.Header("Agromagical: tending the land")]
    [UnityEngine.Tooltip("Cells around an Agromagical Enclave whose Macro Biomes' creatures recover with its tending (every Agromagical Enclave, suzerain or not).")]
    [UnityEngine.Min(0)] public int restoreReach = 6;
    [UnityEngine.Tooltip("Share of the gap to what the land holds that a tended population recovers each Echo.")]
    [UnityEngine.Range(0f, 1f)] public float restorePerEcho = 0.15f;

    [UnityEngine.Header("Commissions (asked of an Enclave at a den)")]
    [UnityEngine.Tooltip("Standing an Enclave must hold with you before it takes a commission (a suzerain always does).")]
    [UnityEngine.Range(0f, 100f)] public float commissionStanding = 50f;
    [UnityEngine.Tooltip("Standing a commission spends (a suzerain's is not spent).")]
    [UnityEngine.Range(0f, 100f)] public float standingCost = 10f;
    [UnityEngine.Tooltip("What a commission costs your stores.")]
    public List<ResourceAmount> commissionCost = new List<ResourceAmount>();
    [UnityEngine.Tooltip("Cells from the Enclave within which it serves a den.")]
    [UnityEngine.Min(0)] public int serviceReach = 12;
    [UnityEngine.Tooltip("Militant cull: share of what the land holds the hunters take.")]
    [UnityEngine.Range(0f, 1f)] public float cullTake = 0.3f;
    [UnityEngine.Tooltip("Militant cull: the harm the species remembers, as that many hunts (toward you and toward the hunters).")]
    [UnityEngine.Min(0f)] public float cullHarm = 3f;
    [UnityEngine.Tooltip("Domestication taming: temper the species gains toward your people.")]
    [UnityEngine.Range(0f, 1f)] public float tameAmount = 0.25f;
    [UnityEngine.Tooltip("Agromagical restoring: share of the gap to what the land holds the population recovers at once.")]
    [UnityEngine.Range(0f, 1f)] public float restoreShare = 0.5f;
}

/// <summary>
/// The Enclaves and the creatures (vault: Arcanorian Ecology.md, "Enclaves and Creatures"), with no scene state (tested in <c>EnclaveEcologyTests</c>).
/// A thin layer over today's standing and Suzerainty: when the full Enclave system arrives, it changes what triggers
/// these effects, not the effects. Every Enclave is an authority to the creatures (<see cref="Enclave.AuthorityId"/>),
/// so a species can be gentle with its keepers and wary of everyone else (<see cref="WorldBehavior"/>).
///
/// - Domestication (every one, by its nature): keeps the species of the Macro Biomes around it that its spec allows
///   (<see cref="EnclaveSpec.keepsPureLight"/>: the Sprite-Light Conclave keeps Pure Light creatures). Kept species grow
///   gentle with their keepers each Echo. While you hold the keepers' Suzerainty the species also grow gentle with you, your
///   hunts of them harm them less, they bring in herds, and every one you have identified is Mastered
///   (<see cref="SpeciesLevel.Mastered"/>).
/// - Agromagical (every one): tends the land around it; the populations there recover a share each Echo.
/// - Auric: while you hold its Suzerainty your people understand every species they have observed (the keeper reads
///   <see cref="AuricUnderstanding"/>); asked, it studies one species until it is understood.
/// - Commissions: an Enclave whose standing with you reaches <see cref="EnclaveEcologySettings.commissionStanding"/> (or
///   your suzerain) serves a den within its reach once a Phase, for stores and standing: Militant hunters cull, Domestication
///   keepers tame, Agromagical growers restore, Auric scholars study.
/// Still open (later stages): Trading and Domestication Enclaves carrying species and vectors along routes (E9, the
/// Luminant Moths), Industrious Enclaves turning harvests into materials (the technology templates), Militant patrols
/// against the Atonalis (the Atonalis' own system), and the tone and binding of each Enclave. None of this concerns the
/// Atonalis.
/// </summary>
public static class WorldEnclaveEcology
{
    /// <summary>What an Enclave of <paramref name="family"/> does when asked.</summary>
    public static EnclaveService ServiceOf(EnclaveFamily family)
    {
        switch (family)
        {
            case EnclaveFamily.Militant: return EnclaveService.Cull;
            case EnclaveFamily.Domestication: return EnclaveService.Tame;
            case EnclaveFamily.Agromagical: return EnclaveService.Restore;
            case EnclaveFamily.Auric: return EnclaveService.Study;
            default: return EnclaveService.None;
        }
    }

    /// <summary>"cull", "tame", "restore", "study" for the buttons.</summary>
    public static string Verb(EnclaveService service)
    {
        switch (service)
        {
            case EnclaveService.Cull: return "cull";
            case EnclaveService.Tame: return "tame";
            case EnclaveService.Restore: return "restore";
            case EnclaveService.Study: return "study";
            default: return null;
        }
    }

    // ===== KEEPING =====

    /// <summary>The Macro Biome slots whose ranges reach within <paramref name="reach"/> cells of <paramref name="at"/>.</summary>
    public static HashSet<int> SlotsNear(WorldMap map, HexCoord at, int reach)
    {
        var slots = new HashSet<int>();
        if (map == null) return slots;
        foreach (var c in HexCoord.Spiral(at, Math.Max(0, reach)))
        {
            var t = map.Get(c);
            if (t != null && t.habitatSlot >= 0) slots.Add(t.habitatSlot);
        }
        return slots;
    }

    /// <summary>A species a Domestication Enclave of <paramref name="spec"/> would keep: its Pure Light share reaches the spec's.</summary>
    public static bool Keeps(EnclaveSpec spec, SpeciesSpec species) =>
        spec != null && species != null && 1f - Clamp01(species.structure) >= Clamp01(spec.keepsPureLight) - 0.0001f;

    /// <summary>
    /// The species a Domestication Enclave keeps now (ids, sorted): those living in the Macro Biomes within
    /// <see cref="EnclaveEcologySettings.keepReach"/> of it that its spec allows. Empty for any other family.
    /// </summary>
    public static List<string> Kept(WorldMap map, WorldGenSettings settings, Enclave e)
    {
        var kept = new List<string>();
        var s = settings?.enclaveEcology;
        if (map == null || s == null || e == null || e.family != EnclaveFamily.Domestication) return kept;
        var spec = settings.Enclave(e.spec);
        var slots = SlotsNear(map, e.coord, s.keepReach);
        foreach (var p in map.Populations)
        {
            if (p == null || p.groups <= 0f || !slots.Contains(p.slot)) continue;
            var species = settings.Species(p.species);
            if (species == null || !Keeps(spec, species) || kept.Contains(species.id, StringComparer.OrdinalIgnoreCase)) continue;
            kept.Add(species.id);
        }
        kept.Sort(StringComparer.Ordinal);
        return kept;
    }

    /// <summary>The Domestication Enclaves you hold as suzerain that keep <paramref name="species"/>.</summary>
    public static IEnumerable<Enclave> Keepers(WorldMap map, WorldGenSettings settings, string species) =>
        (map?.Enclaves ?? new List<Enclave>()).Where(e => e != null && e.suzerain && e.family == EnclaveFamily.Domestication &&
            Kept(map, settings, e).Contains(species, StringComparer.OrdinalIgnoreCase));

    /// <summary>
    /// The species is kept by keepers you hold as suzerain: what makes it Mastered once you have identified it
    /// (<see cref="SpeciesKnowledge.LevelOf"/>), and what spares it the full harm of your hunts.
    /// </summary>
    public static bool KeptForYou(WorldMap map, WorldGenSettings settings, string species) =>
        !string.IsNullOrEmpty(species) && Keepers(map, settings, species).Any();

    /// <summary>The share of a hunt's harm a species takes from <paramref name="authority"/> (<see cref="WorldBehavior.Hunted"/>).</summary>
    public static float HuntHarmScale(WorldMap map, WorldGenSettings settings, string species, string authority)
    {
        var s = settings?.enclaveEcology;
        if (s == null || !WorldAuthority.IsPlayers(authority)) return 1f;
        return KeptForYou(map, settings, species) ? Clamp01(s.keptHuntHarm) : 1f;
    }

    /// <summary>Your people understand every species they have observed: you hold an Auric Enclave's Suzerainty.</summary>
    public static bool AuricUnderstanding(WorldMap map) =>
        map?.Enclaves != null && map.Enclaves.Any(e => e != null && e.suzerain && e.family == EnclaveFamily.Auric);

    // ===== THE ECHO =====

    /// <summary>
    /// The Enclaves' Echo (after <see cref="WorldBehavior.Tick"/>): each Domestication Enclave keeps what lives around it and
    /// its kept species grow gentle with it (and with you, while you hold its Suzerainty); each Agromagical Enclave's land
    /// lets its creatures recover. Returns the species each keeper began or ceased to keep (<see cref="Enclave.kept"/> is
    /// what it kept at the last Echo).
    /// </summary>
    public static List<EnclaveEcologyChange> Echo(WorldMap map, WorldGenSettings settings)
    {
        var changes = new List<EnclaveEcologyChange>();
        var s = settings?.enclaveEcology;
        if (map == null || s == null) return changes;
        foreach (var e in map.Enclaves.Where(e => e != null).OrderBy(e => e.index))
        {
            if (e.family == EnclaveFamily.Domestication)
            {
                var kept = Kept(map, settings, e);
                foreach (string id in kept)
                {
                    WorldBehavior.Befriend(map, settings, id, e.AuthorityId, s.keeperBefriend);
                    if (e.suzerain) WorldBehavior.Befriend(map, settings, id, WorldAuthority.Player, s.sharedBefriend);
                }
                var before = e.kept ?? new List<string>();
                foreach (string id in kept.Where(id => !before.Contains(id, StringComparer.OrdinalIgnoreCase)))
                    changes.Add(new EnclaveEcologyChange { kind = EnclaveEcologyChangeKind.Kept, enclave = e, species = settings.Species(id) });
                foreach (string id in before.Where(id => !kept.Contains(id, StringComparer.OrdinalIgnoreCase)))
                    if (settings.Species(id) is SpeciesSpec gone) changes.Add(new EnclaveEcologyChange { kind = EnclaveEcologyChangeKind.Released, enclave = e, species = gone });
                e.kept = kept;
            }
            else if (e.family == EnclaveFamily.Agromagical && s.restorePerEcho > 0f)
            {
                var slots = SlotsNear(map, e.coord, s.restoreReach);
                foreach (var p in map.Populations)
                    if (p != null && p.groups > 0f && slots.Contains(p.slot)) Recover(p, s.restorePerEcho);
            }
        }
        return changes;
    }

    // A population recovers a share of the gap to what its land holds.
    private static void Recover(Population p, float share)
    {
        if (p.capacity <= p.groups) return;
        p.groups += Clamp01(share) * (p.capacity - p.groups);
    }

    // ===== HERDS =====

    /// <summary>
    /// What the herds of your suzerain keepers bring in: <see cref="EnclaveEcologySettings.herdYields"/> for each species kept,
    /// times its abundance around the keepers (up to 1). The source names the Enclave, never the species (spoilers).
    /// </summary>
    public static List<WorldYield> Yields(WorldMap map, WorldGenSettings settings)
    {
        var yields = new List<WorldYield>();
        var s = settings?.enclaveEcology;
        if (map == null || s == null || s.herdYields == null || s.herdYields.Count == 0) return yields;
        foreach (var e in map.Enclaves.Where(e => e != null && e.suzerain && e.family == EnclaveFamily.Domestication))
        {
            var slots = SlotsNear(map, e.coord, s.keepReach);
            float herds = 0f;
            foreach (string id in Kept(map, settings, e))
            {
                float groups = 0f, capacity = 0f;
                foreach (var p in map.Populations.Where(p => p != null && slots.Contains(p.slot) && string.Equals(p.species, id, StringComparison.OrdinalIgnoreCase)))
                {
                    groups += Math.Max(0f, p.groups);
                    capacity += Math.Max(0f, p.capacity);
                }
                herds += capacity > 0.0001f ? Math.Min(1f, groups / capacity) : 0f;
            }
            if (herds <= 0f) continue;
            foreach (var a in s.herdYields)
                if (a != null && !string.IsNullOrEmpty(a.resource) && a.amount != 0f)
                    yields.Add(new WorldYield { source = $"Herds: {e.name}", resource = a.resource, amount = a.amount * herds });
        }
        return yields;
    }

    // ===== COMMISSIONS =====

    /// <summary>
    /// Why <paramref name="e"/> cannot serve the den <paramref name="site"/> now, or null. <paramref name="phase"/> is the
    /// absolute Phase (<see cref="WorldMap.phaseCount"/>): an Enclave serves once a Phase. <paramref name="lore"/> is the
    /// bestiary's memory (a study is refused for a species already understood).
    /// </summary>
    public static string WhyNotCommission(WorldMap map, WorldGenSettings settings, Enclave e, ResourceSite site, int phase, LoreView lore = null)
    {
        var s = settings?.enclaveEcology;
        if (map == null || s == null) return "The creatures cannot be tended here.";
        if (e == null) return "No enclave.";
        var service = ServiceOf(e.family);
        if (service == EnclaveService.None) return $"{e.family} Enclaves do not tend creatures.";
        var home = map.Get(e.coord);
        if (home == null || !home.revealed) return "Find it first.";
        if (!e.suzerain && e.influence < s.commissionStanding) return $"{e.name} serves only those it trusts: standing {s.commissionStanding:0} needed (now {e.influence:0}).";
        if (e.commissionedPhase >= 0 && e.commissionedPhase == phase) return $"{e.name} has already served you this Phase.";
        var species = WorldEcology.SpeciesAt(settings, site);
        if (species == null) return "No creatures live here.";
        if (!WorldResources.Identified(map, site)) return "Identify the creatures here first.";
        int distance = HexCoord.Distance(e.coord, map[site.center].coord);
        if (distance > s.serviceReach) return $"Too far from {e.name} ({distance} cells; it serves within {s.serviceReach}).";
        var p = WorldEcology.PopulationAt(map, settings, site);
        switch (service)
        {
            case EnclaveService.Cull:
                if (p == null || p.groups <= 0f) return "None are left to cull.";
                break;
            case EnclaveService.Restore:
                if (p == null || p.capacity <= 0f) return "The land here holds none of them.";
                if (p.groups >= p.capacity * 0.99f) return "They are already as many as the land holds.";
                break;
            case EnclaveService.Tame:
                if (WorldBehavior.Temper(map, species.id, WorldAuthority.Player) >= 0.999f) return "It already trusts your people as far as it can.";
                break;
            case EnclaveService.Study:
                if (SpeciesKnowledge.LevelOf(map, settings, species.id, lore) >= SpeciesLevel.Understood) return "Your people already understand it.";
                break;
        }
        return null;
    }

    /// <summary>
    /// <paramref name="e"/> serves the den <paramref name="site"/> (check <see cref="WhyNotCommission"/> first): the standing
    /// is spent (not a suzerain's), and its family's work is done. A study writes into <paramref name="lore"/> (the
    /// bestiary's memory). Returns the line for the notice.
    /// </summary>
    public static string Commission(WorldMap map, WorldGenSettings settings, Enclave e, ResourceSite site, int phase, SpeciesLoreState lore = null)
    {
        var s = settings.enclaveEcology;
        var species = WorldEcology.SpeciesAt(settings, site);
        var p = WorldEcology.PopulationAt(map, settings, site);
        if (!e.suzerain) e.influence = Math.Max(0f, e.influence - Math.Max(0f, s.standingCost));
        e.commissionedPhase = phase;
        string name = species.name ?? species.id;
        switch (ServiceOf(e.family))
        {
            case EnclaveService.Cull:
                float take = p == null ? 0f : Math.Min(p.groups, Clamp01(s.cullTake) * p.capacity);
                if (p != null) p.groups -= take;
                // The species remembers who sent the hunters, and the hunters.
                WorldBehavior.Hunted(map, settings, species.id, WorldAuthority.Player, s.cullHarm);
                WorldBehavior.Hunted(map, settings, species.id, e.AuthorityId, s.cullHarm);
                return $"The hunters of {e.name} cull the {name}: {take:0.#} of their groups are gone, and they will remember who sent them.";
            case EnclaveService.Tame:
                WorldBehavior.Befriend(map, settings, species.id, WorldAuthority.Player, s.tameAmount);
                return $"The keepers of {e.name} teach the {name} to trust your people.";
            case EnclaveService.Restore:
                float had = p?.groups ?? 0f;
                if (p != null) Recover(p, s.restoreShare);
                return $"The growers of {e.name} tend the land of the {name}: {(p?.groups ?? 0f) - had:0.#} groups more can live there now.";
            case EnclaveService.Study:
                Study(lore, species.id);
                return $"The scholars of {e.name} study the {name}: your people understand it now.";
            default:
                return null;
        }
    }

    /// <summary>An Auric study: the species is remembered as identified, observed and studied (understood: <see cref="SpeciesLore.Level"/>).</summary>
    public static void Study(SpeciesLoreState lore, string species)
    {
        if (lore == null || string.IsNullOrEmpty(species)) return;
        var record = lore.Ensure(species);
        record.identified = true;
        record.observed = true;
        record.studied = true;
    }

    private static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;
}
