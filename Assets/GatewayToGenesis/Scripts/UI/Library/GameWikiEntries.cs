using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

/// <summary>
/// The Game Wiki's entries written from the game's own data: one per resource, building or unit, technology,
/// section, pillar and aspect, council seat, legend class, legend and civic. They say what the data says (costs,
/// rates, bonuses, who can sit where), so they never drift from the game. Hand-written articles
/// (Resources/Library/GameWiki.md) with the same title win over these (<see cref="Library"/>).
/// </summary>
public static class GameWikiEntries
{
    public const string Resources = "Resources", Buildings = "Buildings and Units", Technologies = "Technologies",
        Sections = "Sections", Pillars = "Pillars", Council = "The Council", Legends = "Legends", Civics = "Civics",
        AchievementsShelf = "Achievements";

    /// <summary>The rule of a pillar challenge, shown on every pillar.</summary>
    public const string PillarChallengeRule =
        "A choice may challenge a pillar. Its chance of success is your strength in the pillar against the challenge's strength: meeting it makes success certain.";

    // Each pillar's opposite on its axis.
    private static readonly Dictionary<string, string> Opposite = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        { "aureus", "chorus" }, { "chorus", "aureus" }, { "regalia", "waltz" }, { "waltz", "regalia" },
    };

    public static List<LibraryEntry> FromGame()
    {
        var entries = new List<LibraryEntry>();
        foreach (var unit in GameCatalog.Resources.All) if (unit != null && !string.IsNullOrEmpty(unit.name)) entries.Add(Resource(unit));
        foreach (var data in GameCatalog.ProductionUnits.All) if (data != null && data.gameUnit != null) entries.Add(Building(data));
        foreach (var tech in GameCatalog.Technologies.All) if (tech != null && tech.gameUnit != null) entries.Add(Technology(tech));
        foreach (var name in GameCatalog.SectionNames.OrderBy(n => n, StringComparer.OrdinalIgnoreCase)) entries.Add(Section(name));
        foreach (var pillar in StatDefinitions.Pillars) entries.Add(Pillar(pillar));
        foreach (var substat in StatDefinitions.Substats) entries.Add(Aspect(substat));
        foreach (var seat in GameCatalog.CouncilSeats.All.OrderBy(s => s.order)) if (seat != null && !string.IsNullOrEmpty(seat.title)) entries.Add(Seat(seat));
        foreach (LegendClass legendClass in Enum.GetValues(typeof(LegendClass))) entries.Add(Class(legendClass));
        foreach (var legend in GameCatalog.Legends.All) if (legend != null && !string.IsNullOrEmpty(legend.legendName)) entries.Add(Legend(legend));
        foreach (var civic in GameCatalog.Civics.All) if (civic != null && !string.IsNullOrEmpty(civic.civicName)) entries.Add(Civic(civic));
        foreach (var achievement in Achievements.Tracker.All.OrderBy(a => a.order)) entries.Add(Achievement(achievement));
        return entries;
    }

    private static LibraryEntry Entry(string title, string shelf, string category, string summary, StringBuilder body)
    {
        return new LibraryEntry
        {
            id = LibraryArticles.GameId(title),
            title = title,
            shelf = shelf,
            category = category,
            summary = summary ?? string.Empty,
            body = body.ToString().Trim(),
            game = true,
            source = "the game's data",
        };
    }

    // ===== ACHIEVEMENTS =====

    // The vault's words, verbatim: the requirement and the flavor keep their [[links]] into the Glossary.
    private static LibraryEntry Achievement(AchievementDefinition achievement)
    {
        var body = new StringBuilder();
        Paragraph(body, "_" + achievement.flavor + "_");
        Line(body, "Earned by", achievement.requirement);
        bool unlocked = Achievements.Tracker.IsUnlocked(achievement.id);
        Line(body, "Status", unlocked ? "Unlocked" : AchievementTriggers.CanBeEarned(achievement.id) ? "Locked: it can be earned now" : "Locked: the game cannot award it yet");
        string category = AchievementNote.Plain(achievement.category);
        return Entry(AchievementNote.Plain(achievement.title), AchievementsShelf, string.IsNullOrEmpty(category) ? "Achievement" : category, AchievementNote.Plain(achievement.flavor), body);
    }

    // ===== ECONOMY =====

    private static LibraryEntry Resource(GameUnit unit)
    {
        var body = new StringBuilder();
        Paragraph(body, unit.description);
        if (!string.IsNullOrEmpty(unit.section)) Line(body, "Section", Link(unit.section));
        if (unit.role == ResourceRole.Food) Paragraph(body, "Feeds the [[Population]]: every citizen eats it, and stored Food brings newcomers.");
        if (unit.role == ResourceRole.Research) Paragraph(body, "Every citizen produces it; technologies are paid for with it.");
        var producers = GameCatalog.ProductionUnits.All.Where(p => p != null && p.gameUnit != null && Contains(p.producedResources, unit.name)).Select(p => p.gameUnit.name).ToList();
        var consumers = GameCatalog.ProductionUnits.All.Where(p => p != null && p.gameUnit != null && Contains(p.consumedResources, unit.name)).Select(p => p.gameUnit.name).ToList();
        var storers = GameCatalog.ProductionUnits.All.Where(p => p != null && p.gameUnit != null && Contains(p.storageResources, unit.name)).Select(p => p.gameUnit.name).ToList();
        List(body, "Produced by", producers.Select(Link));
        List(body, "Consumed by", consumers.Select(Link));
        List(body, "Stored by", storers.Select(Link));
        return Entry(unit.name, Resources, !string.IsNullOrEmpty(unit.type) ? unit.type : "Resource", unit.description, body);
    }

    private static LibraryEntry Building(ProductionUnitData data)
    {
        var unit = data.gameUnit;
        var body = new StringBuilder();
        Paragraph(body, unit.description);
        if (!string.IsNullOrEmpty(unit.section)) Line(body, "Section", Link(unit.section));
        if (data.isUnique) Paragraph(body, "Only one can be built.");
        List(body, "Costs", Amounts(data.buildResourceRequirements, data.buildRequirementsAmount, string.Empty));
        List(body, "Produces", Amounts(data.producedResources, data.productionRates, "/s"));
        List(body, "Consumes", Amounts(data.consumedResources, data.consumeRates, "/s"));
        List(body, "Stores", Amounts(data.storageResources, data.storageAmount, string.Empty));
        if (data.housing > 0) Line(body, "Housing", data.housing.ToString(CultureInfo.InvariantCulture));
        if (data.satisfactionPoints != 0) Line(body, "Satisfaction", Signed(data.satisfactionPoints));
        return Entry(unit.name, Buildings, !string.IsNullOrEmpty(unit.type) ? unit.type : "Building", unit.description, body);
    }

    private static LibraryEntry Technology(TechnologyData tech)
    {
        var unit = tech.gameUnit;
        var body = new StringBuilder();
        Paragraph(body, unit.description);
        if (!string.IsNullOrEmpty(unit.section)) Line(body, "Section", Link(unit.section));
        Line(body, "Tier", tech.tier.ToString(CultureInfo.InvariantCulture));
        if (tech.isEventTech) Paragraph(body, "Unlocked by an event, not by research.");
        List(body, "Requires", (tech.techRequirements ?? new List<string>()).Where(t => !string.IsNullOrEmpty(t)).Select(Link));
        List(body, "Costs", Amounts(tech.resourceRequirements, tech.resourceAmount, string.Empty));
        List(body, "Unlocks", (tech.techUnlockables ?? new List<TechUnlockable>()).Where(u => u != null).Select(u => u.name));
        if (tech.satisfactionPoints != 0) Line(body, "Satisfaction", Signed(tech.satisfactionPoints));
        string type = !string.IsNullOrEmpty(unit.type) ? unit.type + " Technology" : "Technology";
        return Entry(unit.name, Technologies, type, unit.description, body);
    }

    private static LibraryEntry Section(string name)
    {
        var data = SectionData.GetSectionData(name);
        var body = new StringBuilder();
        if (data != null)
        {
            if (!string.IsNullOrEmpty(data.title)) Paragraph(body, "_" + data.title.Trim() + "_");
            Paragraph(body, data.description);
        }
        List(body, "Resources", GameCatalog.Resources.All.Where(u => u != null && Same(u.section, name)).Select(u => Link(u.name)));
        List(body, "Buildings and units", GameCatalog.ProductionUnits.All.Where(p => p != null && p.gameUnit != null && Same(p.gameUnit.section, name)).Select(p => Link(p.gameUnit.name)));
        List(body, "Technologies", GameCatalog.Technologies.All.Where(t => t != null && t.gameUnit != null && Same(t.gameUnit.section, name)).Select(t => Link(t.gameUnit.name)));
        return Entry(name, Sections, "Section", data != null ? data.description : null, body);
    }

    // ===== PILLARS =====

    private static LibraryEntry Pillar(string pillar)
    {
        var body = new StringBuilder();
        var substats = StatDefinitions.PillarSubstats.TryGetValue(pillar, out var list) ? list : Array.Empty<string>();
        string shapes = substats.Length > 0 ? $"Shapes {string.Join(" and ", substats.Select(s => Link(Title(s))))}." : null;
        Paragraph(body, shapes);
        if (Opposite.TryGetValue(pillar, out var opposite)) Paragraph(body, $"Opposes {Link(Title(opposite))} on its axis.");
        Paragraph(body, PillarChallengeRule);
        return Entry(Title(pillar), Pillars, "Pillar of Civilization", shapes, body);
    }

    private static LibraryEntry Aspect(string substat)
    {
        string pillar = StatDefinitions.ParentPillar(substat);
        var body = new StringBuilder();
        string summary = pillar != null ? $"An aspect of {Link(Title(pillar))}." : null;
        Paragraph(body, summary);
        var derived = StatDefinitions.DerivedSource.Where(p => Same(p.Value, substat)).Select(p => CivilizationProperties.Name(p.Key)).ToList();
        if (Same(substat, "secrecy")) derived.Add(CivilizationProperties.Name(StatDefinitions.CommunionStage));
        List(body, "Drives", derived);
        return Entry(Title(substat), Pillars, pillar != null ? "Aspect of " + Title(pillar) : "Civilization Stat", summary, body);
    }

    // ===== THE COUNCIL =====

    private static LibraryEntry Seat(CouncilSeatData seat)
    {
        var body = new StringBuilder();
        Paragraph(body, seat.description);
        Paragraph(body, "A position on the council: legends of the classes below can hold it, and its bonuses apply as soon as one sits in it.");
        List(body, "Held by", (seat.allowedClasses ?? Array.Empty<LegendClass>()).Select(c => Link(LegendClasses.Title(c))));
        // Stories call on seats by area, never by title: whoever holds this seat answers for these.
        List(body, "Answers for", (seat.areas ?? Array.Empty<string>()).Select(a => CouncilAreaRules.NameOf(a, CouncilAreaCatalog.Current)));
        List(body, "Bonuses", (seat.bonuses ?? Array.Empty<DefaultSeatBonus>()).Where(b => b != null).Select(Describe));
        return Entry(seat.title, Council, "Council Seat", seat.description, body);
    }

    private static LibraryEntry Class(LegendClass legendClass)
    {
        string title = LegendClasses.Title(legendClass);
        var body = new StringBuilder();
        var seats = GameCatalog.CouncilSeats.All.Where(s => s != null && s.allowedClasses != null && s.allowedClasses.Contains(legendClass)).OrderBy(s => s.order).Select(s => Link(s.title)).ToList();
        string summary = seats.Count > 0 ? $"A {title} can hold {Join(seats)}." : $"No council seat accepts a {title} yet.";
        Paragraph(body, summary);
        List(body, "Legends of this class", GameCatalog.Legends.All.Where(l => l != null && l.legendClass == legendClass).Select(l => Link(l.legendName)));
        return Entry(title, Council, "Legend Class", summary, body);
    }

    private static LibraryEntry Legend(LegendData legend)
    {
        var body = new StringBuilder();
        if (!string.IsNullOrEmpty(legend.personalQuote)) Paragraph(body, "_" + legend.personalQuote.Trim() + "_");
        Paragraph(body, legend.originStory);
        Line(body, "Class", Link(LegendClasses.Title(legend.legendClass)));
        if (legend.canBeHeadOfState) Paragraph(body, "Can be the Head of State.");
        List(body, "Bonuses", (legend.bonuses ?? new List<LegendBonus>()).Where(b => b != null).Select(b => b.GetAutoDescription()));
        return Entry(legend.legendName, Legends, $"{legend.rarity} {LegendClasses.Title(legend.legendClass)}", legend.personalQuote, body);
    }

    private static LibraryEntry Civic(CivicData civic)
    {
        var body = new StringBuilder();
        Paragraph(body, civic.description);
        List(body, "When assigned", (civic.effects ?? new List<CivicEffect>()).Where(e => e != null).Select(e => e.GetAutoDescription()));
        List(body, "Requires", (civic.requirements ?? new List<CivicRequirement>()).Where(r => r != null).Select(r => EventText.DescribeRequirement(r.ToCondition())));
        List(body, "Requires the civics", (civic.requiredCivics ?? new List<string>()).Where(c => !string.IsNullOrEmpty(c)).Select(Link));
        List(body, "Cannot stand with", (civic.conflictingCivics ?? new List<string>()).Where(c => !string.IsNullOrEmpty(c)).Select(Link));
        if (civic.grantsCouncilPosition && civic.councilPosition != null && !string.IsNullOrEmpty(civic.councilPosition.title))
        {
            Line(body, "Grants the council seat", civic.councilPosition.title);
        }
        return Entry(civic.civicName, Civics, $"{civic.rarity} {civic.tier} Civic", civic.description, body);
    }

    // ===== WORDING =====

    private static string Describe(DefaultSeatBonus bonus) =>
        SeatBonus.Describe(bonus.bonusType, new SeatBonus { bonusType = bonus.bonusType, targetStat = bonus.targetStat, modifierValue = bonus.modifierValue, modifierType = bonus.modifierType }.ToEffect());

    private static string Title(string word) => KeywordLiveValues.Title(word);

    private static string Link(string name) => string.IsNullOrEmpty(name) ? string.Empty : "[[" + name + "]]";

    private static string Join(IList<string> items) =>
        items.Count <= 1 ? string.Concat(items) : string.Join(", ", items.Take(items.Count - 1)) + " or " + items[items.Count - 1];

    private static bool Same(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

    private static bool Contains(List<string> list, string name) => list != null && list.Any(n => Same(n, name));

    private static string Signed(float value) => (value > 0 ? "+" : string.Empty) + value.ToString("0.##", CultureInfo.InvariantCulture);

    private static IEnumerable<string> Amounts(List<string> names, List<float> amounts, string suffix)
    {
        if (names == null) yield break;
        for (int i = 0; i < names.Count; i++)
        {
            if (string.IsNullOrEmpty(names[i])) continue;
            string amount = amounts != null && i < amounts.Count ? amounts[i].ToString("0.###", CultureInfo.InvariantCulture) + suffix + " " : string.Empty;
            yield return amount + Link(names[i]);
        }
    }

    private static void Paragraph(StringBuilder body, string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        if (body.Length > 0) body.Append("\n\n");
        body.Append(text.Trim());
    }

    private static void Line(StringBuilder body, string label, string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return;
        Paragraph(body, $"**{label}:** {value}");
    }

    private static void List(StringBuilder body, string label, IEnumerable<string> items)
    {
        var list = items.Where(i => !string.IsNullOrWhiteSpace(i)).ToList();
        if (list.Count == 0) return;
        Paragraph(body, $"**{label}**");
        foreach (var item in list) body.Append("\n- ").Append(item.Trim());
    }
}
