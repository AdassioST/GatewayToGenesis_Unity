using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// The aftermath and the Great Plague in play. At an Age's end the creatures come through the ended crisis
/// (<see cref="WorldAftermath"/>, AECOR E8); each Echo the Luminant Moths are traded, carried along the routes and, while
/// the plague runs, bred on the sick (<see cref="WorldGreatPlague"/>, AECOR E9). The moths' part is told once it is known
/// (midway through the crisis, or once they are understood); before that nothing about them is said.
/// </summary>
public partial class WorldSystem
{
    // The moths' part in the plague has been told (saved).
    [SaveOptionalField] private bool _plagueRevealTold;

    // ===== THE AFTERMATH (E8) =====

    // The creatures come through the ended Age's crisis (its severity from the Ages' history): the lines to tell.
    private IEnumerable<string> AftermathLines(int age)
    {
        var ages = AgeProgression.Instance;
        float severity = ages != null && ages.History.Count > 0 ? ages.History[ages.History.Count - 1].severity : 0f;
        var report = WorldAftermath.Pass(Map, Settings.generation, age, severity);
        WorldCivilization.Rebuild(Map, Settings.generation);
        var gen = Settings.generation;
        string Names(IEnumerable<string> ids)
        {
            var list = ids.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            var known = list.Where(id => Knows(id, SpeciesLevel.Identified)).Select(id => gen.Species(id)?.name ?? id).ToList();
            int unknown = list.Count - known.Count;
            if (unknown > 0) known.Add(unknown == 1 ? "a creature you have not identified" : $"{unknown} creatures you have not identified");
            return string.Join(", ", known);
        }
        var gone = report.totals.Where(t => t.Value.before > 0f && t.Value.after <= 0f).Select(t => t.Key).ToList();
        if (gone.Count > 0) yield return $"The aftermath reaches the living: the {Names(gone)} did not come through it.";
        var scarred = report.scarred.Where(id => !gone.Contains(id, StringComparer.OrdinalIgnoreCase)).ToList();
        if (scarred.Count > 0) yield return $"The {Names(scarred)} came through it scarred: they will remember more of how they were treated.";
        if (report.resettled.Count > 0) yield return $"New lineages begin to settle the emptied lands: {Names(report.resettled.Select(p => p.species))}.";
    }

    // ===== THE GREAT PLAGUE (E9) =====

    /// <summary>The Great Plague's crisis has begun and not passed.</summary>
    public bool PlagueRunning
    {
        get
        {
            var ages = AgeProgression.Instance;
            var g = Settings?.generation?.greatPlague;
            return ages != null && g != null && ages.Current != null && string.Equals(ages.Current.id, g.age, StringComparison.OrdinalIgnoreCase) && ages.StageReached >= 0;
        }
    }

    /// <summary>The moths' part in the plague is known (<see cref="WorldGreatPlague.Revealed"/>).</summary>
    public bool PlagueRevealed
    {
        get
        {
            var g = Settings?.generation?.greatPlague;
            if (Map == null || g == null) return false;
            var ages = AgeProgression.Instance;
            bool passed = ages != null && ages.History.Any(r => string.Equals(r.ageId, g.age, StringComparison.OrdinalIgnoreCase));
            var level = SpeciesKnowledge.LevelOf(Map, Settings.generation, g.vector, SpeciesLoreKeeper.View);
            return WorldGreatPlague.Revealed(g, ages?.Current?.id, ages != null ? ages.StageReached : -1, passed, level);
        }
    }

    /// <summary>The species is the plague's vector and its part is known (the cards may say it carries sickness).</summary>
    public bool KnownVector(string species) =>
        !string.IsNullOrEmpty(species) && string.Equals(species, Settings?.generation?.greatPlague?.vector, StringComparison.OrdinalIgnoreCase) && PlagueRevealed;

    // The Echo's moths (after the Enclaves'): the lines to tell (nothing before their part is known).
    private IEnumerable<string> PlagueLines()
    {
        var health = PopulationHealth.Instance;
        var inputs = new PlagueInputs { running = PlagueRunning, burden = health != null ? health.Level(HealthPressure.DiseaseBurden) : 0f };
        var echo = WorldGreatPlague.Echo(Map, Settings.generation, AgeNumber, _echoesGrown, inputs);
        var lines = new List<string>();
        string reveal = PlagueRevealLine();
        if (reveal != null) lines.Add(reveal);
        if (!PlagueRevealed) return lines;
        string moths = Settings.generation.Species(Settings.generation.greatPlague.vector)?.name ?? "moths";
        if (echo.bred > 0.05f) lines.Add($"The {moths} thicken around your settlements, fed on the sick.");
        if (echo.carried.Count > 0) lines.Add($"Trade carries the {moths} into {echo.carried.Count} new land(s).");
        return lines;
    }

    // Once: the moths' part becomes known (Great Plague.md, "The Moths Are Everywhere").
    private string PlagueRevealLine()
    {
        if (_plagueRevealTold || !PlagueRevealed) return null;
        _plagueRevealTold = true;
        string moths = Settings.generation.Species(Settings.generation.greatPlague.vector)?.name ?? "moths";
        return $"The moths are everywhere: wherever the sickness is worst, the {moths} are thickest. Your scholars are sure now that they carry it. A Militant Enclave can cull them, the Agromagical and Domestication Enclaves can sanitize, and those who farm them can be asked to stop.";
    }

    // The crisis turned a stage: tell the moths' part at once when it becomes known.
    private void OnAgesChanged()
    {
        if (Map == null || SaveSession.Restoring) return;
        string line = PlagueRevealLine();
        if (line == null) return;
        Say(line);
        Changed?.Invoke();
    }

    /// <summary>The trading Enclave's moth farms: what the card says it trades (the species' name once identified).</summary>
    public string TradedBy(Enclave e)
    {
        if (!WorldGreatPlague.Trades(Settings.generation, e)) return null;
        string id = Settings.generation.greatPlague.vector;
        return Knows(id, SpeciesLevel.Identified) ? Settings.generation.Species(id)?.name ?? id : "lantern moths";
    }

    /// <summary>What restricting the moth farms costs your stores, in words.</summary>
    public string RestrictCostText => CostText(Settings?.generation?.greatPlague?.restrictCost ?? new List<ResourceAmount>());

    public string WhyNotRestrict(Enclave e) =>
        Locked() ?? WorldGreatPlague.WhyNotRestrict(Map, Settings.generation, e, _echoesGrown, PlagueRevealed)
        ?? Unaffordable(Settings.generation.greatPlague.restrictCost ?? new List<ResourceAmount>());

    /// <summary>Ask a trading Enclave to restrict its moth farms for a few Echoes.</summary>
    public bool Restrict(Enclave e)
    {
        string why = WhyNotRestrict(e);
        if (why != null) { Say(why); return false; }
        Pay(Settings.generation.greatPlague.restrictCost);
        WorldGreatPlague.Restrict(Settings.generation, e, _echoesGrown);
        GameLog.Event($"Moth farms restricted: {e.name} until Echo {e.restrictedUntil}", Log);
        AfterCivilizationChange($"{e.name} closes its moth farms for {Settings.generation.greatPlague.restrictEchoes} Echoes{(e.suzerain ? string.Empty : $" (standing {e.influence:0}/100)")}.");
        return true;
    }
}
