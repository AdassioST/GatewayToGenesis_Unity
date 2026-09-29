using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Raising an Orchestral Formation from its template (vault: Combat System.md, "Macro Level | Strategy as
/// Composition"), with no scene state. The Age decides what can be raised and played: each section's first Age, the
/// tempos Ages.md introduces, the chords each Age commands (<see cref="AgeMagic"/>). What a template asks beyond its Age
/// is cut back and reported, never silently kept.
/// </summary>
public static class OrchestralFormations
{
    /// <summary>
    /// What is wrong with <paramref name="template"/> in Age <paramref name="age"/> (empty: nothing). Technologies are
    /// checked when <paramref name="hasTechnology"/> is given.
    /// </summary>
    public static List<string> Validate(FormationTemplate template, CombatSettings settings, int age, Func<string, bool> hasTechnology = null)
    {
        var problems = new List<string>();
        if (template == null) { problems.Add("no template"); return problems; }
        settings = settings ?? new CombatSettings();
        string owner = $"Formation '{template.name ?? template.id}'";
        if (template.front.Count == 0) problems.Add($"{owner} has no front lane: nothing holds the line");
        if (!AgeMagic.TempoAvailable(template.tempo, age))
            problems.Add($"{owner} plays {template.tempo}, which Ages.md introduces in Age {AgeMagic.Numeral(AgeMagic.TempoFrom(template.tempo))}");
        Check(owner, "front", template.front, FormationRow.Front, settings, age, hasTechnology, problems);
        Check(owner, "back", template.back, FormationRow.Back, settings, age, hasTechnology, problems);
        Check(owner, "support", template.support, FormationRow.Support, settings, age, hasTechnology, problems);
        return problems;
    }

    private static void Check(string owner, string lane, List<FormationSlot> slots, FormationRow row, CombatSettings settings, int age,
        Func<string, bool> hasTechnology, List<string> problems)
    {
        foreach (var slot in slots)
        {
            var spec = settings.Section(slot?.section);
            if (spec == null) { problems.Add($"{owner}: unknown section '{slot?.section}' in the {lane} lane"); continue; }
            if (spec.row != row) problems.Add($"{owner}: {spec.name} belongs in the {spec.row.ToString().ToLowerInvariant()} lane, not the {lane}");
            if (spec.minAge > age) problems.Add($"{owner}: {spec.name} cannot be raised before Age {AgeMagic.Numeral(spec.minAge)}");
            if (!string.IsNullOrEmpty(spec.technology) && hasTechnology != null && !hasTechnology(spec.technology))
                problems.Add($"{owner}: {spec.name} needs {spec.technology}");
            if (spec.Casts)
            {
                if (slot.binding == SpellBinding.Unattuned) problems.Add($"{owner}: {spec.name} needs a binding to cast in");
                var harmony = slot.harmony ?? new List<SpellBinding>();
                if (harmony.Contains(slot.binding) || harmony.Contains(SpellBinding.Unattuned) || harmony.Distinct().Count() != harmony.Count)
                    problems.Add($"{owner}: {spec.name}'s Minor Notes must be distinct bindings other than its root");
                if (harmony.Count > 3) problems.Add($"{owner}: {spec.name} layers {harmony.Count} Minor Notes; a Tetrad (three) is the most one chord carries (Chord Layering.md)");
                var tier = (ChordTier)Math.Min(3, harmony.Count);
                if (!AgeMagic.Playable(tier, age)) problems.Add($"{owner}: {spec.name} plays a {tier} chord, beyond what Age {AgeMagic.Numeral(age)} commands");
            }
            else if (slot.binding != SpellBinding.Unattuned || (slot.harmony?.Count ?? 0) > 0)
                problems.Add($"{owner}: {spec.name} does not cast; its binding is ignored");
        }
    }

    /// <summary>
    /// The side a template raises in Age <paramref name="age"/>. Sections not yet raisable are left out; chords beyond
    /// the Age are cut to what it commands; a tempo not yet introduced falls back to Staccato. Each change is added to
    /// <paramref name="problems"/> when given.
    /// </summary>
    public static BattleSide Raise(FormationTemplate template, CombatSettings settings, int age, BattleLegend conductor = null,
        string name = null, List<string> problems = null, Func<string, bool> hasTechnology = null)
    {
        settings = settings ?? new CombatSettings();
        problems?.AddRange(Validate(template, settings, age, hasTechnology));
        var side = new BattleSide
        {
            name = name ?? template?.name ?? "Formation",
            conductor = conductor != null && conductor.State != ComposureState.Surrender ? conductor : null,
            tempo = template != null && AgeMagic.TempoAvailable(template.tempo, age) ? template.tempo : SpellTempo.Staccato,
        };
        if (template == null) return side;
        if (conductor != null && conductor.State == ComposureState.Surrender) problems?.Add($"{conductor.name} has surrendered to Dissonance and cannot command");
        foreach (var slot in template.Slots)
        {
            var spec = settings.Section(slot?.section);
            if (spec == null || spec.minAge > age) continue;
            if (!string.IsNullOrEmpty(spec.technology) && hasTechnology != null && !hasTechnology(spec.technology)) continue;
            var harmony = (slot.harmony ?? new List<SpellBinding>()).Where(h => h != SpellBinding.Unattuned && h != slot.binding).Distinct().ToList();
            while (harmony.Count > 0 && !AgeMagic.Playable((ChordTier)Math.Min(3, harmony.Count), age)) harmony.RemoveAt(harmony.Count - 1);
            side.sections.Add(CombatSection.Raise(spec, slot.binding, harmony));
        }
        return side;
    }

    /// <summary>The section kinds raisable in Age <paramref name="age"/> (and with the technologies, when given).</summary>
    public static IEnumerable<CombatSectionSpec> Raisable(CombatSettings settings, int age, Func<string, bool> hasTechnology = null) =>
        (settings ?? new CombatSettings()).Sections.Where(s => s != null && s.minAge <= age &&
            (string.IsNullOrEmpty(s.technology) || hasTechnology == null || hasTechnology(s.technology)));

    /// <summary>A short line for a card: "3 front, 2 back, 1 support · Legato · Flux, Flux+Resonance".</summary>
    public static string Summary(FormationTemplate t)
    {
        if (t == null) return string.Empty;
        var casters = t.Slots.Where(s => s != null && s.binding != SpellBinding.Unattuned)
            .Select(s => s.harmony == null || s.harmony.Count == 0 ? s.binding.ToString() : $"{s.binding}+{string.Join("+", s.harmony)}");
        string magic = string.Join(", ", casters);
        return $"{t.front.Count} front, {t.back.Count} back, {t.support.Count} support · {t.tempo}{(magic.Length > 0 ? " · " + magic : string.Empty)}";
    }
}
