using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;

/// <summary>The vault's Composure note as the game quotes it: its lead and what each of the five states is.</summary>
public class ComposureLore
{
    /// <summary>The note's first line ("The level of stress, sanity, and meter of the internal [[Coherence]]...").</summary>
    public string lead;
    /// <summary>Each state's bullet, verbatim ([[links]] kept).</summary>
    public readonly Dictionary<ComposureState, string> states = new Dictionary<ComposureState, string>();
    /// <summary>The words a state's bullet sets in bold ("This is the last point where anyone can intervene to save them.").</summary>
    public readonly Dictionary<ComposureState, string> emphasis = new Dictionary<ComposureState, string>();
    public readonly List<string> problems = new List<string>();

    public string Describe(ComposureState state) => states.TryGetValue(state, out var text) ? text : null;

    /// <summary>The first sentence of a state's bullet, for short lines.</summary>
    public string FirstSentence(ComposureState state)
    {
        string text = Describe(state);
        if (string.IsNullOrEmpty(text)) return null;
        var match = Regex.Match(text, @"^.*?[.!?](?=\s|$)");
        return match.Success ? match.Value : text;
    }

    public string Emphasis(ComposureState state) => emphasis.TryGetValue(state, out var text) ? text : null;
}

/// <summary>
/// Reads the vault's Composure note (Worldbuilding/Origin of Magic/Spellweaving/Composure.md, copied verbatim to
/// Resources/Legends): the lead line and the five bullets "- Pristine: ...", "- Clouded: ..." ... "- Surrender: ...".
/// Pure; shape breaks are reported, not repaired.
/// </summary>
public static class ComposureNote
{
    private static readonly Regex Bullet = new Regex(@"^[-*]\s+(Pristine|Clouded|Fractured|Spiraling|Surrender)\s*:\s*(.+)$", RegexOptions.IgnoreCase);
    private static readonly Regex Bold = new Regex(@"\*\*(.+?)\*\*");

    public static ComposureLore Parse(string markdown)
    {
        var lore = new ComposureLore();
        if (string.IsNullOrEmpty(markdown))
        {
            lore.problems.Add("The Composure note is empty.");
            return lore;
        }
        foreach (var raw in markdown.Replace("\r\n", "\n").Split('\n'))
        {
            string line = raw.Trim();
            if (line.Length == 0 || line.StartsWith("#", StringComparison.Ordinal)) continue;
            var bullet = Bullet.Match(line);
            if (bullet.Success)
            {
                var state = (ComposureState)Enum.Parse(typeof(ComposureState), bullet.Groups[1].Value, true);
                string text = bullet.Groups[2].Value.Trim();
                if (lore.states.ContainsKey(state)) lore.problems.Add($"The {state} state is described twice.");
                else lore.states[state] = text;
                var bold = Bold.Match(text);
                if (bold.Success) lore.emphasis[state] = bold.Groups[1].Value.Trim();
                continue;
            }
            if (lore.lead == null && !line.StartsWith("-", StringComparison.Ordinal) && !line.StartsWith("_", StringComparison.Ordinal)) lore.lead = line;
        }
        foreach (ComposureState state in Enum.GetValues(typeof(ComposureState)))
            if (!lore.states.ContainsKey(state)) lore.problems.Add($"No bullet describes the {state} state.");
        return lore;
    }
}

/// <summary>
/// The legend lore the game reads at run time, loaded once from Resources/Legends (the vault notes copied verbatim by
/// Tools &gt; Gateway to Genesis &gt; Import Lore From Vault) and the tuning in Resources/Legends/LegendSettings (code
/// defaults when the asset is absent).
/// </summary>
public static class LegendLore
{
    public const string TraitNotePath = "Legends/Legend Trait";
    public const string ComposureNotePath = "Legends/Composure";
    public const string SettingsPath = "Legends/LegendSettings";

    private static LegendTraitCatalog _traits;
    private static ComposureLore _composure;
    private static LegendSettings _settings;
    private static bool _settingsLoaded;

    // Statics survive between play sessions when domain reload is disabled; the notes may have been re-imported too.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    public static void Invalidate()
    {
        _traits = null;
        _composure = null;
        _settings = null;
        _settingsLoaded = false;
    }

    /// <summary>The Legend Traits of the vault note (an empty catalog when the note has not been imported).</summary>
    public static LegendTraitCatalog Traits
    {
        get
        {
            if (_traits == null)
            {
                string text = GameCatalog.LoadText(TraitNotePath);
                _traits = text != null ? LegendTraitNote.Parse(text) : new LegendTraitCatalog();
                if (text == null) _traits.problems.Add($"There is no Resources/{TraitNotePath}.md (run Tools > Gateway to Genesis > Import Lore From Vault).");
            }
            return _traits;
        }
    }

    public static ComposureLore Composure
    {
        get
        {
            if (_composure == null)
            {
                string text = GameCatalog.LoadText(ComposureNotePath);
                _composure = text != null ? ComposureNote.Parse(text) : new ComposureLore();
                if (text == null) _composure.problems.Add($"There is no Resources/{ComposureNotePath}.md (run Tools > Gateway to Genesis > Import Lore From Vault).");
            }
            return _composure;
        }
    }

    /// <summary>The tuning asset, or null (code defaults apply).</summary>
    public static LegendSettings Settings
    {
        get
        {
            if (!_settingsLoaded)
            {
                _settings = Resources.Load<LegendSettings>(SettingsPath);
                _settingsLoaded = true;
            }
            return _settings;
        }
    }

    private static readonly ComposureTuning DefaultComposure = new ComposureTuning();
    private static readonly SoulTuning DefaultSoul = new SoulTuning();
    private static readonly FragmentTuning DefaultFragments = new FragmentTuning();

    public static ComposureTuning ComposureTuning => Settings != null && Settings.composure != null ? Settings.composure : DefaultComposure;
    public static SoulTuning SoulTuning => Settings != null && Settings.soul != null ? Settings.soul : DefaultSoul;
    public static FragmentTuning FragmentTuning => Settings != null && Settings.fragments != null ? Settings.fragments : DefaultFragments;

    /// <summary>What ContentValidator reports: a missing note, and authored traits the note does not have.</summary>
    public static IEnumerable<string> Problems()
    {
        if (GameCatalog.LoadText(TraitNotePath) == null) yield return $"Resources/{TraitNotePath}.md is missing: legends get no traits (run Tools > Gateway to Genesis > Import Lore From Vault).";
        if (GameCatalog.LoadText(ComposureNotePath) == null) yield return $"Resources/{ComposureNotePath}.md is missing: Composure states have no description.";
        var traits = Traits;
        foreach (var legend in GameCatalog.Legends.All.Where(l => l != null))
        {
            if (!string.IsNullOrWhiteSpace(legend.soulLeitmotif) && MagicBindings.Canonical(legend.soulLeitmotif) == null)
                yield return $"Legend '{legend.legendName}': soulLeitmotif '{legend.soulLeitmotif}' is not one of the seven bindings.";
            foreach (var name in legend.originTraits ?? new List<string>())
                if (!string.IsNullOrWhiteSpace(name) && traits.Origin(name) == null) yield return $"Legend '{legend.legendName}': origin trait '{name}' is not in the vault's Legend Trait tables.";
            foreach (var name in legend.personalityTraits ?? new List<string>())
                if (!string.IsNullOrWhiteSpace(name) && traits.Personality(name) == null) yield return $"Legend '{legend.legendName}': personality trait '{name}' is not in the vault's Legend Trait tables.";
            if (legend.personalityTraits != null && legend.personalityTraits.Count(n => !string.IsNullOrWhiteSpace(n)) > LegendSoulRules.ExpressionCount)
                yield return $"Legend '{legend.legendName}': more than {LegendSoulRules.ExpressionCount} personality traits (the vault: \"A Legend has exactly three personality Legend Traits\").";
        }
    }
}
