using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// The Enclaves and the creatures in play (<see cref="WorldEnclaveEcology"/>, AECOR E7): each Echo the Domestication
/// Enclaves keep what lives around them and the Agromagical ones tend their land; what your suzerain keepers begin or
/// cease to keep is told (only species you know). At an identified den an Enclave that trusts you can be commissioned,
/// once a Phase, for stores and standing: Militant hunters cull, Domestication keepers tame, Agromagical growers restore,
/// Auric scholars study.
/// </summary>
public partial class WorldSystem
{
    /// <summary>The settings of the Enclaves' work with the creatures.</summary>
    public EnclaveEcologySettings EnclaveEcology => Settings?.generation?.enclaveEcology;

    // The Enclaves' Echo (after the creatures' own and their behavior): the lines to tell.
    private IEnumerable<string> EnclaveEcologyEcho()
    {
        var changes = WorldEnclaveEcology.Echo(Map, Settings.generation);
        foreach (var c in changes)
        {
            if (c.species == null || c.enclave == null || !c.enclave.suzerain || !Knows(c.species.id, SpeciesLevel.Identified)) continue;
            yield return c.kind == EnclaveEcologyChangeKind.Kept
                ? $"{c.enclave.name} keeps the {c.species.name} now: its keepers share all they know of them (Mastered)."
                : $"{c.enclave.name} no longer keeps the {c.species.name}.";
        }
    }

    /// <summary>The species a Domestication Enclave keeps that you know (names), and how many others it keeps.</summary>
    public (List<string> known, int unknown) KeptBy(Enclave e)
    {
        var known = new List<string>();
        int unknown = 0;
        foreach (string id in WorldEnclaveEcology.Kept(Map, Settings.generation, e))
        {
            var species = Settings.generation.Species(id);
            if (species != null && Knows(id, SpeciesLevel.Identified)) known.Add(species.name ?? id);
            else unknown++;
        }
        return (known, unknown);
    }

    // What a new suzerain Domestication Enclave shares at once (null when it keeps nothing you know).
    private string KeepersLine(Enclave e)
    {
        if (e == null || !e.suzerain || e.family != EnclaveFamily.Domestication) return null;
        var (known, _) = KeptBy(e);
        return known.Count > 0 ? $" Its keepers share all they know of the {string.Join(", ", known)} (Mastered)." : null;
    }

    // ===== COMMISSIONS =====

    /// <summary>The Enclaves that could be asked to tend the den <paramref name="site"/>, nearest first, each with why it cannot now (null: it can).</summary>
    public List<(Enclave enclave, EnclaveService service, string why)> Commissions(ResourceSite site)
    {
        var offers = new List<(Enclave, EnclaveService, string)>();
        var s = EnclaveEcology;
        if (Map == null || s == null || site == null || WorldEcology.SpeciesAt(Settings.generation, site) == null) return offers;
        var at = Map[site.center].coord;
        foreach (var e in Map.Enclaves.Where(e => e != null && WorldEnclaveEcology.ServiceOf(e.family) != EnclaveService.None)
                     .OrderBy(e => HexCoord.Distance(e.coord, at)).ThenBy(e => e.index))
        {
            // Only Enclaves you have found and that reach the den are offered; the rest would be noise on the card.
            var home = Map.Get(e.coord);
            if (home == null || !home.revealed || HexCoord.Distance(e.coord, at) > s.serviceReach) continue;
            offers.Add((e, WorldEnclaveEcology.ServiceOf(e.family), WhyNotCommission(e, site)));
        }
        return offers;
    }

    /// <summary>What a commission costs your stores, in words.</summary>
    public string CommissionCostText => CostText(EnclaveEcology?.commissionCost ?? new List<ResourceAmount>());

    public string WhyNotCommission(Enclave e, ResourceSite site) =>
        Locked() ?? WorldEnclaveEcology.WhyNotCommission(Map, Settings.generation, e, site, PhaseNow, SpeciesLoreKeeper.View)
        ?? Unaffordable(EnclaveEcology?.commissionCost ?? new List<ResourceAmount>());

    /// <summary><paramref name="e"/> tends the den <paramref name="site"/> for you (<see cref="WorldEnclaveEcology.Commission"/>).</summary>
    public bool Commission(Enclave e, ResourceSite site)
    {
        string why = WhyNotCommission(e, site);
        if (why != null) { Say(why); return false; }
        Pay(EnclaveEcology.commissionCost);
        var species = WorldEcology.SpeciesAt(Settings.generation, site);
        var before = WorldBehavior.Met(Map, species.id, WorldAuthority.Player) ? WorldBehavior.Toward(Map, Settings.generation, species, WorldAuthority.Player) : (ThreatResponse?)null;
        string line = WorldEnclaveEcology.Commission(Map, Settings.generation, e, site, PhaseNow, SpeciesLoreKeeper.Instance != null ? SpeciesLoreKeeper.Instance.State : null);
        GameLog.Event($"Commission: {e.name} ({WorldEnclaveEcology.ServiceOf(e.family)}) at {site.spec}", Log);
        // The species' ways toward you may have moved a step (a cull, a taming): told once you can read it.
        var after = WorldBehavior.Toward(Map, Settings.generation, species, WorldAuthority.Player);
        if (before.HasValue && before.Value != after && Knows(species.id, SpeciesLevel.Observed))
            line += " " + BehaviorLine(new BehaviorChange { species = species, from = before.Value, to = after, gentler = !WorldBehavior.Harsher(before.Value, after) });
        if (!e.suzerain) line += $" (standing {e.influence:0}/100)";
        // Den danger and yields follow the creatures' numbers and tempers.
        WorldCivilization.Rebuild(Map, Settings.generation);
        MarkYieldsDirty();
        AfterCivilizationChange(line);
        return true;
    }
}
