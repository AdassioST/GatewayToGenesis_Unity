using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>Climax compositions in order of the percentage that permits them (vault: "Resolution Climax — 60% to 90%"). Append only.</summary>
public enum BattleClimaxTier { None, Unison, Dyad, Ensemble, Linked, FusedFinale, LinkedFinales, GrandResolution }

/// <summary>
/// Tempo Fever (vault: Combat System.md, "Tempo Fever — Crescendo and Resolution"). A Measure holds up to two
/// performance sequences: Execution (every Note of every performed Spotlit phrase) and Abjuration (only when the window
/// had a playable lane). Before ignition the Fever counts four consecutive Perfect sequences (the pips); once lit it is
/// judged by the Measure: flawless raises it 5 points, the first imperfect Measure Falters (holds), a second in a row
/// breaks it. Silence is never flawless. A Sympathetic Collapse shatters it and a declared Resolution ends it.
/// </summary>
[Serializable]
public sealed class BattleTempoFever
{
    public const int Pips = 4, Ignition = 25, Step = 5, Cap = 90, ClimaxAt = 60;
    public int percentage;
    /// <summary>Consecutive Perfect sequences toward ignition (0-3; the fourth ignites at Assessment).</summary>
    public int streak;
    /// <summary>The last Measure was not flawless: the percentage held, and one more imperfect Measure breaks the Fever.</summary>
    public bool faltering;
    /// <summary>Set at Commitment when a Climax allowance was spent: the Fever ends at this Measure's Assessment.</summary>
    public bool resolutionDeclared;
    /// <summary>A Sympathetic Collapse this Measure: the Fever ends at Assessment without a Falter.</summary>
    public bool shattered;
    /// <summary>How the last Assessment changed the Fever (for the meter and the music).</summary>
    public string lastChange = "";

    public bool Active => percentage > 0;
    public bool Climax => percentage >= ClimaxAt;
    public string Movement => percentage >= ClimaxAt ? "Resolution Climax" : percentage > 0 ? "Tempo Crescendo" : "Ordinary Measure";
    public BattleClimaxTier Tier => TierAt(percentage);
    public BattleTempoFever Clone() => (BattleTempoFever)MemberwiseClone();

    public static BattleClimaxTier TierAt(int percentage) =>
        percentage >= 90 ? BattleClimaxTier.GrandResolution : percentage >= 85 ? BattleClimaxTier.LinkedFinales : percentage >= 80 ? BattleClimaxTier.FusedFinale :
        percentage >= 75 ? BattleClimaxTier.Linked : percentage >= 70 ? BattleClimaxTier.Ensemble : percentage >= 65 ? BattleClimaxTier.Dyad :
        percentage >= ClimaxAt ? BattleClimaxTier.Unison : BattleClimaxTier.None;

    /// <summary>Called once at the Measure's Assessment with every recorded phrase of the battle.</summary>
    public void Assess(IEnumerable<BattleRhythmRecord> records, bool attacker, int measure)
    {
        var actual = records.Where(r => r.attacker == attacker && r.measure == measure).ToList();
        var execution = actual.Where(r => !r.abjuration).SelectMany(r => r.results).Where(r => r.playable).ToList();
        var defense = actual.Where(r => r.abjuration).SelectMany(r => r.results).Where(r => r.playable).ToList();
        bool performed = execution.Count > 0, defended = defense.Count > 0;
        bool executionPerfect = performed && execution.All(Perfect), defensePerfect = defense.All(Perfect);
        if (!Active)
        {
            // Before ignition there is no grace. Silence resets the pips; its Abjuration cannot sustain a flow that was not played.
            if (!performed) streak = 0;
            else
            {
                streak = executionPerfect ? streak + 1 : 0;
                if (defended) streak = defensePerfect ? streak + 1 : 0;
            }
            if (streak >= Pips) { percentage = Ignition; streak = 0; faltering = false; lastChange = "Ignited"; }
            else lastChange = streak > 0 ? "Building" : "";
        }
        else if (resolutionDeclared) End("Resolved");
        else if (shattered) End("Shattered");
        else if (performed && executionPerfect && (!defended || defensePerfect))
        {
            lastChange = faltering ? "Steadied" : "Rising";
            faltering = false; percentage = Math.Min(Cap, percentage + Step);
        }
        else if (faltering) End("Broken");
        else { faltering = true; lastChange = "Faltered"; }
        resolutionDeclared = false; shattered = false;
    }

    private void End(string why) { percentage = 0; streak = 0; faltering = false; lastChange = why; }
    private static bool Perfect(BattleRhythmResult r) => !r.assisted && r.grade == BattleExecution.Perfect;

    /// <summary>Each Interference on a friendly working weighs 1 + the Fever; the Hold falls by the total, halves rounding up.</summary>
    public static int InterferenceWeight(int count, int percentage) => (int)Math.Floor(Math.Max(0, count) * (1d + Math.Max(0, Math.Min(Cap, percentage)) / 100d) + .5d);

    /// <summary>The score follows the Fever (vault: "The Battle Takes Musical Form"). Cues for an adaptive soundtrack.</summary>
    public static IReadOnlyList<string> MusicLayers(int percentage)
    {
        var layers = new List<string>();
        if (percentage >= 25) layers.Add("Percussion");
        if (percentage >= 40) layers.Add("Countermelody");
        if (percentage >= 55) layers.Add("Tightened mix");
        if (percentage >= 60) layers.Add("Climax motif");
        if (percentage >= 75) layers.Add("Additional voices");
        if (percentage >= 85) layers.Add("Dissonant harmonics");
        if (percentage >= 90) layers.Add("Edge of resolution");
        return layers;
    }
}

/// <summary>A Resolution as the battle remembers it: raw material for the Stellar Legacy Score and the performers' biographies.</summary>
[Serializable]
public sealed class BattleResolutionRecord
{
    public long action;
    public int measure, percentage;
    public bool attacker;
    public BattleClimaxTier tier;
    public string core, outcome = "Declared";
    public List<string> contributors = new List<string>(), notes = new List<string>(), bindings = new List<string>(), inDeathKnell = new List<string>();
    public float integrity, composure;
    public string Words => $"{tier} at {percentage}%: {core} with {string.Join(", ", contributors)} ({outcome})";
}
