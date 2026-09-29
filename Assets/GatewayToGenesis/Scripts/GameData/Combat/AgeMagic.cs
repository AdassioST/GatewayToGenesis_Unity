using System;
using System.Collections.Generic;

/// <summary>
/// A spell's rhythm (vault: The Principles of Magic.md, "Rhythm"). A formation plays its spells at one tempo
/// (Amadea.md: "in one key, at one tempo"). Saved by index: append only.
/// </summary>
public enum SpellTempo
{
    /// <summary>Sharp, punctured bursts: instant effects, rapid fire. Hits hardest in the opening measures.</summary>
    Staccato,
    /// <summary>Smooth, continuous flow: sustained, ongoing barriers. Steady, and the formation is warded.</summary>
    Legato,
    /// <summary>Gradual acceleration for surging power: spell barrages that grow each measure.</summary>
    Accelerando,
    /// <summary>Gentle slowdown for controlled release: strong early, fading, and less prone to Discordant Interference.</summary>
    Ritardando,
    /// <summary>Interleaved bursts and lulls: chaotic spells, wildly uneven, that shake the enemy's Composure.</summary>
    Polyrhythm,
}

/// <summary>A chord's size (vault: The Principles of Magic.md): the root alone, then one to three Minor Notes.</summary>
public enum ChordTier { Unison, Dyad, Triad, Tetrad }

/// <summary>
/// What each Age lets a civilization's Spellweavers play in battle, from Ages.md's line for each Age (canon):
/// Age 0 "Flickering Staccato Rhythm. Unreliable Unison and Dyad Spells. Mostly Minor Note magic."; Age I "Introducing
/// Legato. First Stable Unison. Introducing Major Notes, and sparse Dyad Chords"; Age II "Introducing Ritardando. Stable
/// Dyad Chords. Unison Primacy. Rare Triad Chords"; Age III "Introducing Accelerando. First Reliable Triad Chords. Dyad
/// Chord Primacy. Unison Mastery"; Age IV "Introducing Polyrhythms. Reliable Basic Triad Chords"; Age V "Advanced Triad
/// Chords. Dyad Chord Mastery. Unreliable Tetrad Chords"; Age VI "All rhythm types available. First Semi Stable Tetrad
/// Chords. Triad Chord Primacy". The words are canon; the chances they turn into are proposals (<see cref="CombatTuning"/>).
/// Creatures cast by instinct through Coherence-Binding Tissue and are not bound by this table.
/// </summary>
public static class AgeMagic
{
    /// <summary>How well an Age plays a chord tier, in the vault's words.</summary>
    public enum Command { Locked, Unreliable, Sparse, SemiStable, Stable, Reliable, Advanced, Primacy, Mastery }

    // [age][tier] for Ages 0-VI; later Ages keep Age VI's row.
    private static readonly Command[][] Rows =
    {
        new[] { Command.Unreliable, Command.Unreliable, Command.Locked, Command.Locked },     // 0
        new[] { Command.Stable, Command.Sparse, Command.Locked, Command.Locked },             // I
        new[] { Command.Primacy, Command.Stable, Command.Sparse, Command.Locked },            // II
        new[] { Command.Mastery, Command.Primacy, Command.Reliable, Command.Locked },         // III
        new[] { Command.Mastery, Command.Primacy, Command.Reliable, Command.Locked },         // IV
        new[] { Command.Mastery, Command.Mastery, Command.Advanced, Command.Unreliable },     // V
        new[] { Command.Mastery, Command.Mastery, Command.Primacy, Command.SemiStable },      // VI
    };

    /// <summary>The Age each tempo is introduced in (Staccato from Age 0).</summary>
    public static int TempoFrom(SpellTempo tempo)
    {
        switch (tempo)
        {
            case SpellTempo.Legato: return 1;
            case SpellTempo.Ritardando: return 2;
            case SpellTempo.Accelerando: return 3;
            case SpellTempo.Polyrhythm: return 4;
            default: return 0;
        }
    }

    public static bool TempoAvailable(SpellTempo tempo, int age) => age >= TempoFrom(tempo);

    public static Command CommandOf(ChordTier tier, int age)
    {
        var row = Rows[Math.Max(0, Math.Min(age, Rows.Length - 1))];
        return row[(int)tier];
    }

    public static bool Playable(ChordTier tier, int age) => CommandOf(tier, age) != Command.Locked;

    /// <summary>The largest chord an Age can play.</summary>
    public static ChordTier MaxTier(int age)
    {
        var tier = ChordTier.Unison;
        foreach (ChordTier t in Enum.GetValues(typeof(ChordTier))) if (Playable(t, age)) tier = t;
        return tier;
    }

    /// <summary>Major Notes arrive in Age I: before it, magic is "mostly Minor Note", and spells land weaker.</summary>
    public static bool MajorNotes(int age) => age >= 1;

    /// <summary>The chance a chord of <paramref name="tier"/> collapses into Discordant Interference in <paramref name="age"/>.</summary>
    public static float Interference(ChordTier tier, int age, CombatTuning tuning)
    {
        tuning = tuning ?? CombatTuning.Default;
        switch (CommandOf(tier, age))
        {
            case Command.Unreliable: return tuning.unreliable;
            case Command.Sparse: return tuning.sparse;
            case Command.SemiStable: return tuning.semiStable;
            case Command.Stable: return tuning.stable;
            case Command.Reliable: return tuning.reliable;
            case Command.Advanced: return tuning.advanced;
            case Command.Primacy: return tuning.primacy;
            case Command.Mastery: return tuning.mastery;
            default: return 1f;
        }
    }

    /// <summary>The tempos an Age has introduced.</summary>
    public static IEnumerable<SpellTempo> Tempos(int age)
    {
        foreach (SpellTempo t in Enum.GetValues(typeof(SpellTempo))) if (TempoAvailable(t, age)) yield return t;
    }

    /// <summary>The Roman numeral used by the vault ("0", "I" ... "XIV").</summary>
    public static string Numeral(int age)
    {
        string[] n = { "0", "I", "II", "III", "IV", "V", "VI", "VII", "VIII", "IX", "X", "XI", "XII", "XIII", "XIV" };
        return age >= 0 && age < n.Length ? n[age] : age.ToString();
    }
}
