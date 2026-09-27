using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

/// <summary>The three kinds of origin Legend Trait (Legend Trait.md, "The Wish").</summary>
public enum OriginKind { Dissonance, Classical, Consonance }

/// <summary>An origin Legend Trait as the vault's tables write it: the wound or gift a legend is born with.</summary>
public class OriginTrait
{
    public string name;
    /// <summary>The italic line under the name, verbatim.</summary>
    public string description;
    public OriginKind kind;
    /// <summary>The Scaled Cost column (the vault: good origins "often come with a high Legend Trait cost").</summary>
    public int scaledCost;
    /// <summary>The Underdog Points column.</summary>
    public int underdogPoints;
    /// <summary>The Binding Effect cell as written ("+1 Flux, –1 Crystal").</summary>
    public string bindingEffect;
    /// <summary>What the Binding Effect adds to each binding ("to all Bindings" is spread over the seven).</summary>
    public readonly Dictionary<string, int> bindings = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
    /// <summary>The Narrative Effect cell, verbatim.</summary>
    public string narrativeEffect;
}

/// <summary>One element a personality trait can evolve along: how, and the middle trait(s) it becomes.</summary>
public class TraitPath
{
    public string element;
    /// <summary>How the trait evolves along this element, verbatim (may be empty).</summary>
    public string description;
    /// <summary>The middle trait(s) the path leads to; several when the vault offers alternatives ("A / B").</summary>
    public readonly List<string> middleTraits = new List<string>();
}

/// <summary>A starting personality Legend Trait (The Expression) as the vault's tables write it.</summary>
public class PersonalityTrait
{
    public string name;
    /// <summary>The trait's line of speech, verbatim with its quotation marks, or null when the vault has none.</summary>
    public string quote;
    /// <summary>"Emotional &amp; Relational", "Action &amp; Will" or "Cognitive &amp; Logic".</summary>
    public string group;
    /// <summary>Who the group is attuned to: Selenea, Auric Aria or Lacrimosa.</summary>
    public string attunement;
    /// <summary>The Element Evolution column: the bindings the trait may solidify into.</summary>
    public readonly List<string> elements = new List<string>();
    public readonly List<TraitPath> paths = new List<TraitPath>();

    public TraitPath PathFor(string element) => paths.FirstOrDefault(p => string.Equals(p.element, element, StringComparison.OrdinalIgnoreCase));
}

/// <summary>A middle Legend Trait: the state between a starting personality trait and its Apex Trait.</summary>
public class MiddleTrait
{
    public string name;
    public readonly List<string> elements = new List<string>();
    /// <summary>The "Development &amp; Path to Apex (Why &amp; How)" cell, verbatim without its label.</summary>
    public string whyAndHow;
}

/// <summary>The Legend Traits of the vault note, and what the note is still missing.</summary>
public class LegendTraitCatalog
{
    public readonly List<OriginTrait> origins = new List<OriginTrait>();
    public readonly List<PersonalityTrait> personalities = new List<PersonalityTrait>();
    public readonly List<MiddleTrait> middleTraits = new List<MiddleTrait>();
    /// <summary>Gaps and shape breaks in the note (fix them in the vault and re-import; nothing is repaired here).</summary>
    public readonly List<string> problems = new List<string>();

    public bool IsEmpty => origins.Count == 0 && personalities.Count == 0;

    public OriginTrait Origin(string name) => origins.FirstOrDefault(t => Same(t.name, name));
    public PersonalityTrait Personality(string name) => personalities.FirstOrDefault(t => Same(t.name, name));
    public MiddleTrait Middle(string name) => middleTraits.FirstOrDefault(t => Same(t.name, name));

    private static bool Same(string a, string b) => string.Equals(a?.Trim(), b?.Trim(), StringComparison.OrdinalIgnoreCase);
}

/// <summary>
/// Reads the vault's Legend Trait note (Worldbuilding/Society/Stellar Legacy/Legend Trait.md, copied verbatim to
/// Resources/Legends by Tools &gt; Gateway to Genesis &gt; Import Lore From Vault). What it reads:
/// <list type="bullet">
/// <item>"All Origin &amp; Physical Legend Traits": the three tables under the bold Dissonance, Classical and
/// Consonance headings (name, italic line, Scaled Cost, Underdog Points, Binding Effect, Narrative Effect). Names
/// listed without a table row carry no numbers yet and are left out.</item>
/// <item>"All Personality Legend Traits": the three group tables (trait and quote, Element Evolution, Middle Legend
/// Trait Path written as <c>[[Element]]: how -&gt; [[Middle Trait]]</c>) and the middle-trait table (Element Pairing,
/// Why &amp; How).</item>
/// </list>
/// Apex and Spellweaver traits are not read yet. Text is kept as written (the canon is copied, never paraphrased);
/// anything that breaks the shape is reported in <see cref="LegendTraitCatalog.problems"/>, not guessed. Pure, so it
/// is tested without Unity.
/// </summary>
public static class LegendTraitNote
{
    private static readonly Regex Heading = new Regex(@"^#{1,6}\s+(.+)$");
    private static readonly Regex Link = new Regex(@"\[\[([^\]|#]+)(?:#[^\]|]*)?(?:\|([^\]]+))?\]\]");
    private static readonly Regex CellSplit = new Regex(@"(?<!\\)\|");
    private static readonly Regex Breaks = new Regex(@"(?:<br\s*/?>\s*)+", RegexOptions.IgnoreCase);
    private static readonly Regex PathSegment = new Regex(@"^\[\[([^\]|#]+)\]\]\s*:\s*(.*)$");
    private static readonly Regex Quote = new Regex("_\\s*([\"“].*?[\"”])\\s*_");
    private static readonly Regex Effect = new Regex(@"([+\-–−])\s*(\d+)\s+(?:to\s+)?(all\s+Bindings|[A-Za-z]+)", RegexOptions.IgnoreCase);
    private const string Residue = "Arcanorian-Bible.pdf";

    private enum Section { None, Origins, Personalities }
    private enum Table { None, Origin, Personality, Middle }

    public static LegendTraitCatalog Parse(string markdown)
    {
        var catalog = new LegendTraitCatalog();
        if (string.IsNullOrEmpty(markdown))
        {
            catalog.problems.Add("The note is empty.");
            return catalog;
        }

        var section = Section.None;
        var table = Table.None;
        OriginKind? kind = null;
        string group = null, attunement = null;
        var residue = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        int lineNumber = 0;

        foreach (var raw in markdown.Replace("\r\n", "\n").Split('\n'))
        {
            lineNumber++;
            string line = raw.Trim();
            if (line.Length == 0) continue;

            var heading = Heading.Match(line);
            if (heading.Success)
            {
                string title = Plain(heading.Groups[1].Value);
                section = title.StartsWith("All Origin", StringComparison.OrdinalIgnoreCase) ? Section.Origins
                    : title.StartsWith("All Personality", StringComparison.OrdinalIgnoreCase) && title.IndexOf("Apex", StringComparison.OrdinalIgnoreCase) < 0 ? Section.Personalities
                    : Section.None;
                table = Table.None;
                continue;
            }
            if (section == Section.None) continue;

            if (!line.StartsWith("|", StringComparison.Ordinal))
            {
                table = Table.None;
                if (!line.StartsWith("**", StringComparison.Ordinal)) continue;
                // Bold sub-headings: the origin kinds, and the personality groups with who they are attuned to.
                string bold = Plain(line);
                if (section == Section.Origins)
                {
                    foreach (OriginKind k in Enum.GetValues(typeof(OriginKind)))
                        if (bold.StartsWith(k + " Origin", StringComparison.OrdinalIgnoreCase)) kind = k;
                }
                else
                {
                    int personality = bold.IndexOf(" Personality", StringComparison.OrdinalIgnoreCase);
                    if (personality > 0)
                    {
                        group = bold.Substring(0, personality).Trim();
                        var attuned = Regex.Match(bold, @"\(([^()]+?)\s+attuned\)", RegexOptions.IgnoreCase);
                        attunement = attuned.Success ? attuned.Groups[1].Value.Trim() : null;
                    }
                }
                continue;
            }

            var cells = Cells(line);
            if (cells.Count == 0 || cells.All(c => c.Length == 0 || Regex.IsMatch(c, @"^:?-{3,}:?$"))) continue;
            if (cells.Any(c => c.IndexOf("Scaled Cost", StringComparison.OrdinalIgnoreCase) >= 0)) { table = Table.Origin; continue; }
            if (cells.Any(c => c.IndexOf("Element Evolution", StringComparison.OrdinalIgnoreCase) >= 0)) { table = Table.Personality; continue; }
            if (cells.Any(c => c.IndexOf("Element Pairing", StringComparison.OrdinalIgnoreCase) >= 0)) { table = Table.Middle; continue; }
            if (cells.All(c => c.Length == 0)) continue;

            if (line.IndexOf(Residue, StringComparison.OrdinalIgnoreCase) >= 0) residue.Add(Plain(cells[0]));
            switch (table)
            {
                case Table.Origin:
                    ReadOrigin(cells, kind, lineNumber, catalog);
                    break;
                case Table.Personality:
                    ReadPersonality(cells, group, attunement, lineNumber, catalog);
                    break;
                case Table.Middle:
                    ReadMiddle(cells, lineNumber, catalog);
                    break;
            }
        }

        if (catalog.origins.Count == 0) catalog.problems.Add("No origin trait table was found under \"All Origin & Physical Legend Traits\".");
        if (catalog.personalities.Count == 0) catalog.problems.Add("No personality trait table was found under \"All Personality Legend Traits\".");
        if (residue.Count > 0)
            catalog.problems.Add($"Rows carrying a citation residue (\"{Residue}\", left out of the game's text): {string.Join(", ", residue.OrderBy(r => r))}.");
        ReportGaps(catalog);
        return catalog;
    }

    // ===== ROWS =====

    private static void ReadOrigin(List<string> cells, OriginKind? kind, int lineNumber, LegendTraitCatalog catalog)
    {
        if (cells.Count < 5)
        {
            catalog.problems.Add($"Line {lineNumber}: an origin trait row with {cells.Count} cells (the table has five).");
            return;
        }
        var parts = Breaks.Split(cells[0]);
        string name = Plain(parts[0]);
        if (name.Length == 0) return;
        if (kind == null)
        {
            catalog.problems.Add($"Line {lineNumber}: {name} sits under no Dissonance, Classical or Consonance heading.");
            return;
        }
        var trait = new OriginTrait
        {
            name = name,
            kind = kind.Value,
            description = parts.Length > 1 ? Clean(StripEmphasis(string.Join(" ", parts.Skip(1)))) : null,
            bindingEffect = Clean(cells[3]),
            narrativeEffect = Clean(cells[4]),
        };
        if (!TryNumber(cells[1], out trait.scaledCost)) catalog.problems.Add($"{name}: its Scaled Cost \"{cells[1]}\" is not a number.");
        if (!TryNumber(cells[2], out trait.underdogPoints)) catalog.problems.Add($"{name}: its Underdog Points \"{cells[2]}\" is not a number.");
        foreach (Match effect in Effect.Matches(Plain(cells[3])))
        {
            int amount = int.Parse(effect.Groups[2].Value) * (effect.Groups[1].Value == "+" ? 1 : -1);
            string target = effect.Groups[3].Value;
            if (target.StartsWith("all", StringComparison.OrdinalIgnoreCase))
            {
                foreach (var binding in MagicBindings.All) Add(trait.bindings, binding, amount);
                continue;
            }
            string canonical = MagicBindings.Canonical(target);
            if (canonical == null) catalog.problems.Add($"{name}: its Binding Effect names \"{target}\", which is not one of the seven bindings.");
            else Add(trait.bindings, canonical, amount);
        }
        if (catalog.Origin(name) != null) catalog.problems.Add($"{name}: listed twice among the origin traits.");
        else catalog.origins.Add(trait);
    }

    private static void ReadPersonality(List<string> cells, string group, string attunement, int lineNumber, LegendTraitCatalog catalog)
    {
        if (cells.Count < 3)
        {
            catalog.problems.Add($"Line {lineNumber}: a personality trait row with {cells.Count} cells (the table has three).");
            return;
        }
        var head = Breaks.Split(cells[0]);
        string name = Plain(head[0]);
        if (name.Length == 0) return;
        var trait = new PersonalityTrait { name = name, group = group, attunement = attunement };
        var quote = Quote.Match(cells[0]);
        if (quote.Success && Plain(quote.Groups[1].Value).Trim('"', '“', '”', '.', ' ').Length > 0) trait.quote = Clean(quote.Groups[1].Value);
        trait.elements.AddRange(Elements(cells[1]));

        bool prose = false;
        foreach (var segment in Breaks.Split(cells[2]).Select(s => s.Trim()).Where(s => s.Length > 0))
        {
            var path = PathSegment.Match(segment);
            if (path.Success)
            {
                string element = MagicBindings.Canonical(path.Groups[1].Value);
                if (element == null)
                {
                    catalog.problems.Add($"{name}: a path names \"{path.Groups[1].Value.Trim()}\", which is not one of the seven bindings.");
                    continue;
                }
                AddPath(trait, element, path.Groups[2].Value, catalog);
                continue;
            }
            // A bare middle trait with no element in front: unambiguous only when the row has a single element.
            if (trait.elements.Count == 1 && Plain(Link.Replace(segment, string.Empty)).Trim('-', '>', ' ').Length == 0)
            {
                AddPath(trait, trait.elements[0], segment, catalog);
                continue;
            }
            prose = true;
        }
        if (prose) catalog.problems.Add($"{name}: its Middle Legend Trait Path is prose, not the table's form ([[Element]]: how -> [[Middle Trait]]).");
        if (catalog.Personality(name) != null) catalog.problems.Add($"{name}: listed twice among the personality traits.");
        else catalog.personalities.Add(trait);
    }

    private static void AddPath(PersonalityTrait trait, string element, string text, LegendTraitCatalog catalog)
    {
        var path = trait.PathFor(element);
        if (path == null)
        {
            path = new TraitPath { element = element };
            trait.paths.Add(path);
        }
        string how = text, leadsTo = null;
        int arrow = text.IndexOf("->", StringComparison.Ordinal);
        if (arrow >= 0)
        {
            how = text.Substring(0, arrow);
            leadsTo = text.Substring(arrow + 2);
        }
        else if (Plain(Link.Replace(text, string.Empty)).Trim().Length == 0)
        {
            // "[[Cindergale]]: [[Fiery Passion]]": the middle trait with no description.
            how = string.Empty;
            leadsTo = text;
        }
        path.description = Clean(how);
        if (leadsTo != null)
        {
            var linked = Link.Matches(leadsTo).Cast<Match>().Select(m => m.Groups[1].Value.Trim()).Where(n => n.Length > 0).ToList();
            if (linked.Count == 0)
                linked = Regex.Split(Plain(leadsTo), @"\s*(?:/|\||,|\bor\b)\s*").Select(n => n.Trim()).Where(n => n.Length > 0).ToList();
            foreach (var middle in linked)
                if (!path.middleTraits.Contains(middle, StringComparer.OrdinalIgnoreCase)) path.middleTraits.Add(middle);
        }
        if (!trait.elements.Contains(element, StringComparer.OrdinalIgnoreCase))
            catalog.problems.Add($"{trait.name}: a path on {element}, which its Element Evolution does not list.");
    }

    private static void ReadMiddle(List<string> cells, int lineNumber, LegendTraitCatalog catalog)
    {
        if (cells.Count < 3) return;
        string name = Plain(cells[0]);
        if (name.Length == 0) return;
        var trait = new MiddleTrait { name = name, whyAndHow = Clean(Regex.Replace(cells[2], @"^\s*\*\*\s*Why\s*&\s*How\s*:?\s*\*\*\s*:?", string.Empty, RegexOptions.IgnoreCase)) };
        trait.elements.AddRange(Elements(cells[1]));
        if (catalog.Middle(name) != null) catalog.problems.Add($"Line {lineNumber}: the middle trait {name} is listed twice.");
        else catalog.middleTraits.Add(trait);
    }

    // What the tables leave open, one line each: the game needs these to evolve every trait along every element.
    private static void ReportGaps(LegendTraitCatalog catalog)
    {
        foreach (var trait in catalog.personalities)
        {
            if (trait.elements.Count != 3) catalog.problems.Add($"{trait.name}: its Element Evolution lists {trait.elements.Count} binding(s), not three.");
            if (trait.quote == null) catalog.problems.Add($"{trait.name}: no line of speech yet.");
            var open = trait.elements.Where(e => trait.PathFor(e) == null || trait.PathFor(e).middleTraits.Count == 0).ToList();
            if (open.Count > 0) catalog.problems.Add($"{trait.name}: no middle trait written for its {string.Join(", ", open)} path{(open.Count == 1 ? "" : "s")}.");
        }
        var undescribed = catalog.personalities.SelectMany(t => t.paths).SelectMany(p => p.middleTraits)
            .Where(m => catalog.Middle(m) == null).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(m => m, StringComparer.OrdinalIgnoreCase).ToList();
        if (undescribed.Count > 0)
            catalog.problems.Add($"Middle traits named on a path but missing from the middle-trait table (no Element Pairing or Why & How yet): {string.Join(", ", undescribed)}.");
    }

    // ===== TEXT =====

    private static List<string> Cells(string line)
    {
        var parts = CellSplit.Split(line).Select(c => c.Replace("\\|", "|").Trim()).ToList();
        if (parts.Count > 0 && parts[0].Length == 0) parts.RemoveAt(0);
        if (parts.Count > 0 && parts[parts.Count - 1].Length == 0) parts.RemoveAt(parts.Count - 1);
        return parts;
    }

    /// <summary>The bindings a cell names, as links or plain words ("Luminance / Void / Strand"), in order.</summary>
    private static List<string> Elements(string cell)
    {
        var found = new List<string>();
        string text = Link.Replace(cell, m => " " + m.Groups[1].Value + " ");
        foreach (var word in Regex.Split(Breaks.Replace(text, " "), @"[^A-Za-z]+"))
        {
            string binding = MagicBindings.Canonical(word);
            if (binding != null && !found.Contains(binding)) found.Add(binding);
        }
        return found;
    }

    /// <summary>Words only: links to their shown words, emphasis and breaks removed.</summary>
    private static string Plain(string text)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;
        string plain = Link.Replace(text, m => m.Groups[2].Success ? m.Groups[2].Value : m.Groups[1].Value.Trim());
        plain = Breaks.Replace(plain, " ");
        plain = StripEmphasis(plain);
        return Regex.Replace(plain, @"\s+", " ").Trim();
    }

    private static string StripEmphasis(string text) => Regex.Replace(text ?? string.Empty, @"\*\*|(?<![A-Za-z])[*_]|[*_](?![A-Za-z])", string.Empty).Trim();

    /// <summary>Verbatim text for the game: breaks become spaces and the citation residue is dropped; links stay.</summary>
    private static string Clean(string text)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;
        string clean = Breaks.Replace(text, " ").Replace("​", string.Empty);
        clean = Regex.Replace(clean, @"\s*" + Regex.Escape(Residue), string.Empty, RegexOptions.IgnoreCase);
        return Regex.Replace(clean, @"\s+", " ").Trim();
    }

    private static bool TryNumber(string cell, out int value)
    {
        string text = Plain(cell).Replace('–', '-').Replace('−', '-').Replace("+", string.Empty).Trim();
        return int.TryParse(text, System.Globalization.NumberStyles.AllowLeadingSign, System.Globalization.CultureInfo.InvariantCulture, out value);
    }

    private static void Add(Dictionary<string, int> sheet, string binding, int amount) =>
        sheet[binding] = (sheet.TryGetValue(binding, out int current) ? current : 0) + amount;
}
