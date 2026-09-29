using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

/// <summary>What the culture is living right now, gathered by <see cref="CultureSystem"/> each Seventh.</summary>
public class CultureInputs
{
    public FoundingMyth myth;
    /// <summary>Pillar values by key (aureus, regalia, waltz, chorus).</summary>
    public IDictionary<string, float> pillars = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
    /// <summary>The family of each civic in force (civics with no family are left out).</summary>
    public List<EnclaveFamily> civics = new List<EnclaveFamily>();
    /// <summary>What the people eat and work, by family (any unit: shares are what count).</summary>
    public IDictionary<EnclaveFamily, float> used = new Dictionary<EnclaveFamily, float>();
    /// <summary>The ground the culture lives on, by family, weighted by presence.</summary>
    public IDictionary<EnclaveFamily, float> land = new Dictionary<EnclaveFamily, float>();
    /// <summary>The family of each tributary district.</summary>
    public List<EnclaveFamily> districts = new List<EnclaveFamily>();
}

/// <summary>One food in this Seventh's diet: its class and how much of it was held and drawn.</summary>
public struct DietEntry
{
    public string resource;
    public FoodClass cuisine;
    /// <summary>Food value held in the cellars (or amount, for ingredients and spices).</summary>
    public float held;
    /// <summary>Amount eaten or used this Seventh.</summary>
    public float used;
    /// <summary>A dish or drink the people invented themselves.</summary>
    public bool invented;

    public DietEntry(string resource, FoodClass cuisine, float held, float used, bool invented = false)
    {
        this.resource = resource; this.cuisine = cuisine; this.held = held; this.used = used; this.invented = invented;
    }
}

/// <summary>
/// The culture's arithmetic, with no scene state (tested in <c>CultureTests</c>):
/// - Leanings: the ten families (the vault's civic families) the culture leans toward. Each Seventh it drifts a little
///   toward what it is living (<see cref="Target"/>): its founding myth, pillars, civics, what it eats and works, its land
///   and districts. A reform moves it at once (<see cref="Reform"/>).
/// - Foodways: familiarity with each food follows its share of the diet of its class; the first of a class, familiar
///   enough and for long enough, becomes national (<see cref="NextNational"/>).
/// - Spread: held cells take on the culture, faster near settlements; cells no longer held let it fade.
/// - Names: suggested demonyms and adjectives (Iridia: Iridian), validation, and the story tokens ({nation}...).
/// </summary>
public static class CultureRules
{
    public static readonly EnclaveFamily[] Families = (EnclaveFamily[])Enum.GetValues(typeof(EnclaveFamily));

    /// <summary>The family each pillar leans toward (Civic.md: Auric is the Aureus Pillar's, Weaver the Waltz's, Regal the Regalia's, Esoteric the Chorus's).</summary>
    public static EnclaveFamily FamilyOfPillar(string pillar)
    {
        switch ((pillar ?? string.Empty).Trim().ToLowerInvariant())
        {
            case "aureus": return EnclaveFamily.Auric;
            case "regalia": return EnclaveFamily.Regal;
            case "waltz": return EnclaveFamily.Weaver;
            default: return EnclaveFamily.Esoteric;
        }
    }

    // ===== LEANINGS =====

    /// <summary>Leanings as an array by family index (missing families 0).</summary>
    public static float[] ToArray(IEnumerable<CultureLeaning> leanings)
    {
        var a = new float[Families.Length];
        if (leanings != null) foreach (var l in leanings) if (l != null) a[(int)l.family] += Math.Max(0f, l.share);
        return a;
    }

    public static List<CultureLeaning> ToList(float[] shares) =>
        Families.Select(f => new CultureLeaning { family = f, share = shares != null && (int)f < shares.Length ? shares[(int)f] : 0f }).ToList();

    /// <summary>Scaled to sum 1 (an empty or all-zero vector stays zero).</summary>
    public static float[] Normalize(float[] raw)
    {
        var a = new float[Families.Length];
        if (raw == null) return a;
        float sum = 0f;
        for (int i = 0; i < a.Length && i < raw.Length; i++) sum += Math.Max(0f, raw[i]);
        if (sum <= 0f) return a;
        for (int i = 0; i < a.Length && i < raw.Length; i++) a[i] = Math.Max(0f, raw[i]) / sum;
        return a;
    }

    /// <summary>The myth's baseline as leanings (what a new culture is).</summary>
    public static float[] Baseline(FoundingMyth myth)
    {
        var a = new float[Families.Length];
        if (myth != null) foreach (var pair in myth.baseline) a[(int)pair.Key] += pair.Value;
        return Normalize(a);
    }

    /// <summary>
    /// What the culture is living now, as leanings: each input shared by its own weights, then weighted
    /// (<see cref="CultureTuning.mythWeight"/>, pillars, each civic, what is used, land, each district) and normalized.
    /// Inputs that are empty add nothing, so a culture of myth alone is its myth.
    /// </summary>
    public static float[] Target(CultureInputs inputs, CultureTuning tuning)
    {
        tuning = tuning ?? new CultureTuning();
        var raw = new float[Families.Length];
        if (inputs == null) return raw;
        Add(raw, Baseline(inputs.myth), tuning.mythWeight);
        if (inputs.pillars != null)
        {
            var pillars = new float[Families.Length];
            foreach (var p in inputs.pillars) pillars[(int)FamilyOfPillar(p.Key)] += Math.Max(0f, p.Value);
            Add(raw, Normalize(pillars), tuning.pillarWeight);
        }
        if (inputs.civics != null) foreach (var f in inputs.civics) raw[(int)f] += tuning.civicWeight;
        Add(raw, Normalize(FromMap(inputs.used)), tuning.usedWeight);
        Add(raw, Normalize(FromMap(inputs.land)), tuning.landWeight);
        if (inputs.districts != null) foreach (var f in inputs.districts) raw[(int)f] += tuning.districtWeight;
        return Normalize(raw);
    }

    /// <summary>Move <paramref name="current"/> a share <paramref name="rate"/> of the way to <paramref name="target"/> (both normalized; the result too).</summary>
    public static float[] Drift(float[] current, float[] target, float rate)
    {
        var from = Normalize(current);
        var to = Normalize(target);
        if (from.Sum() <= 0f) return to;
        if (to.Sum() <= 0f) return from;
        float k = Clamp01(rate);
        var result = new float[Families.Length];
        for (int i = 0; i < result.Length; i++) result[i] = from[i] + (to[i] - from[i]) * k;
        return Normalize(result);
    }

    /// <summary>A reform: a share <paramref name="shift"/> of the whole culture taken from the others (in proportion) and given to <paramref name="toward"/>.</summary>
    public static float[] Reform(float[] current, EnclaveFamily toward, float shift)
    {
        var from = Normalize(current);
        float k = Clamp01(shift);
        var result = new float[Families.Length];
        for (int i = 0; i < result.Length; i++) result[i] = from[i] * (1f - k);
        result[(int)toward] += k;
        return Normalize(result);
    }

    /// <summary>Families from the strongest leaning down.</summary>
    public static List<EnclaveFamily> Ranked(float[] shares) =>
        Families.OrderByDescending(f => shares != null && (int)f < shares.Length ? shares[(int)f] : 0f).ThenBy(f => (int)f).ToList();

    /// <summary>The culture's character: its leading family, and the second when it runs close (within <paramref name="closeness"/> of the first).</summary>
    public static string Character(float[] shares, float closeness = 0.8f)
    {
        var s = Normalize(shares);
        if (s.Sum() <= 0f) return "Unformed";
        var ranked = Ranked(s);
        var first = ranked[0];
        var second = ranked[1];
        return s[(int)second] >= s[(int)first] * closeness && s[(int)second] > 0f ? $"{first} and {second}" : first.ToString();
    }

    // ===== FOODWAYS =====

    /// <summary>
    /// A Seventh at the table: each food's share of its class (what the cellars hold, weighted <see cref="CultureTuning.heldWeight"/>,
    /// and what was drawn or used, the rest), familiarity moving toward it, and who is first of each class. Adds foods not
    /// known yet. A declined food can be offered again once its familiarity falls below <see cref="CultureTuning.declineReset"/>.
    /// </summary>
    public static void Taste(List<Foodway> foodways, IEnumerable<DietEntry> diet, CultureTuning tuning)
    {
        if (foodways == null) return;
        tuning = tuning ?? new CultureTuning();
        var entries = (diet ?? Enumerable.Empty<DietEntry>()).Where(d => !string.IsNullOrEmpty(d.resource)).ToList();
        foreach (var d in entries)
        {
            var way = foodways.FirstOrDefault(f => string.Equals(f.resource, d.resource, StringComparison.OrdinalIgnoreCase));
            if (way == null)
            {
                if (d.held <= 0f && d.used <= 0f) continue;
                foodways.Add(way = new Foodway { resource = d.resource, cuisine = d.cuisine });
            }
            way.cuisine = d.cuisine;
            way.invented |= d.invented;
            way.eaten += Math.Max(0f, d.used);
        }
        foreach (FoodClass cuisine in Enum.GetValues(typeof(FoodClass)))
        {
            var ofClass = entries.Where(d => d.cuisine == cuisine).ToList();
            float held = ofClass.Sum(d => Math.Max(0f, d.held)), used = ofClass.Sum(d => Math.Max(0f, d.used));
            float heldWeight = held > 0f ? (used > 0f ? Clamp01(tuning.heldWeight) : 1f) : 0f;
            float usedWeight = used > 0f ? 1f - heldWeight : 0f;
            var shares = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
            foreach (var d in ofClass)
                shares[d.resource] = (held > 0f ? heldWeight * Math.Max(0f, d.held) / held : 0f) + (used > 0f ? usedWeight * Math.Max(0f, d.used) / used : 0f);
            // The people favour what they invented themselves when the table decides what comes first.
            bool Own(string resource) => foodways.Any(f => f.invented && string.Equals(f.resource, resource, StringComparison.OrdinalIgnoreCase));
            float Standing(KeyValuePair<string, float> p) => p.Value * (Own(p.Key) ? Math.Max(1f, tuning.inventedPreference) : 1f);
            string first = shares.Count == 0 ? null : shares.OrderByDescending(Standing).ThenBy(p => p.Key, StringComparer.Ordinal).First().Key;
            if (first != null && shares[first] <= 0f) first = null;
            foreach (var way in foodways.Where(f => f.cuisine == cuisine))
            {
                shares.TryGetValue(way.resource, out float share);
                float rate = Clamp01(tuning.familiarityRate * (way.invented ? Math.Max(1f, tuning.inventedFamiliarityRate) : 1f));
                way.familiarity = Clamp01(way.familiarity + (share - way.familiarity) * rate);
                way.firstSevenths = string.Equals(way.resource, first, StringComparison.OrdinalIgnoreCase) ? way.firstSevenths + 1 : 0;
                if (way.declined && way.familiarity < tuning.declineReset) way.declined = false;
            }
        }
    }

    /// <summary>
    /// The food the people have grown accustomed to enough to call it theirs: not national yet, not declined, familiar
    /// (<see cref="CultureTuning.nationalFamiliarity"/>), first of its class for <see cref="CultureTuning.nationalSevenths"/>
    /// in a row, and room left in its class. The people's own inventions are the most eligible: they need only
    /// <see cref="CultureTuning.inventedNationalEase"/> of both and are offered before anything else. Then edibles, teas,
    /// drinks, ingredients and spices; null when none.
    /// </summary>
    public static Foodway NextNational(IEnumerable<Foodway> foodways, CultureTuning tuning)
    {
        tuning = tuning ?? new CultureTuning();
        var list = foodways?.Where(f => f != null).ToList() ?? new List<Foodway>();
        float ease = Math.Max(0f, Math.Min(1f, tuning.inventedNationalEase));
        bool Ready(Foodway f) => f.invented
            ? f.familiarity >= tuning.nationalFamiliarity * ease - 1e-5f && f.firstSevenths >= (int)Math.Ceiling(tuning.nationalSevenths * ease)
            : f.familiarity >= tuning.nationalFamiliarity && f.firstSevenths >= tuning.nationalSevenths;
        return list.Where(f => !f.national && !f.declined && Ready(f)
                               && list.Count(o => o.national && o.cuisine == f.cuisine) < Math.Max(1, tuning.maxNationalPerClass))
            .OrderByDescending(f => f.invented).ThenBy(f => TableOrder(f.cuisine)).ThenByDescending(f => f.familiarity).ThenBy(f => f.resource, StringComparer.Ordinal).FirstOrDefault();
    }

    /// <summary>The kitchen's categories in the order the table shows them: Edibles, Teas, Beverages, Ingredients, Spices.</summary>
    public static readonly FoodClass[] TableClasses = { FoodClass.Edible, FoodClass.EleosTea, FoodClass.Beverage, FoodClass.Ingredient, FoodClass.Spice };

    public static int TableOrder(FoodClass cuisine) => Math.Max(0, Array.IndexOf(TableClasses, cuisine));

    /// <summary>Whether the category fills a belly: edibles and teas (drunk like food), ingredients eaten raw and, last of all, the cellar's drinks; never spices.</summary>
    public static bool Feeds(FoodClass cuisine) => cuisine != FoodClass.Spice;

    /// <summary>
    /// When the stores draw on it for hunger: edibles and teas together first (0), ingredients only once they run out
    /// (1), and the cellar's beverages last of all (2: a people does not drink its way through a famine while there is
    /// bread). Spices are never drawn on (<see cref="Feeds"/>).
    /// </summary>
    public static int EatingTier(FoodClass cuisine) => cuisine == FoodClass.Beverage ? 2 : cuisine == FoodClass.Ingredient ? 1 : 0;

    /// <summary>"national food", "national tea", "national drink", "national ingredient", "national spice".</summary>
    public static string NationalLabel(FoodClass cuisine) =>
        cuisine == FoodClass.Edible ? "national food" : cuisine == FoodClass.EleosTea ? "national tea" : cuisine == FoodClass.Beverage ? "national drink"
        : cuisine == FoodClass.Ingredient ? "national ingredient" : "national spice";

    public static string ClassName(FoodClass cuisine, bool plural = false) =>
        cuisine == FoodClass.Edible ? (plural ? "Edibles" : "Edible") : cuisine == FoodClass.EleosTea ? (plural ? "Teas" : "Tea")
        : cuisine == FoodClass.Beverage ? (plural ? "Beverages" : "Beverage")
        : cuisine == FoodClass.Ingredient ? (plural ? "Ingredients" : "Ingredient") : (plural ? "Spices" : "Spice");

    // ===== SPREAD =====

    /// <summary>How near a settlement a cell is (1 at the settlement, 0 at <paramref name="reach"/> cells or beyond).</summary>
    public static float Nearness(int distance, int reach) => reach <= 0 ? (distance <= 0 ? 1f : 0f) : Clamp01(1f - distance / (float)reach);

    /// <summary>
    /// A cell's presence after a Seventh: a held cell grows toward 1 (<see cref="CultureTuning.spreadPerSeventh"/> plus
    /// <see cref="CultureTuning.seatSpreadBonus"/> by nearness, slower as it fills); one no longer held fades.
    /// </summary>
    public static float Spread(float presence, bool held, float nearness, CultureTuning tuning)
    {
        tuning = tuning ?? new CultureTuning();
        presence = Clamp01(presence);
        if (!held) return presence * (1f - Clamp01(tuning.fadePerSeventh));
        float growth = Math.Max(0f, tuning.spreadPerSeventh) + Math.Max(0f, tuning.seatSpreadBonus) * Clamp01(nearness);
        return Clamp01(presence + growth * (1f - presence));
    }

    /// <summary>Mean presence over the held cells (how much of the land the culture has become): 0-1.</summary>
    public static float Cohesion(IEnumerable<float> heldPresence)
    {
        var list = heldPresence?.ToList() ?? new List<float>();
        return list.Count == 0 ? 0f : Clamp01(list.Average());
    }

    // ===== NAMES =====

    public const int MaxNameLength = 24;

    /// <summary>Trimmed, inner spaces collapsed, each word capitalized as typed words usually are ("iridia" to "Iridia").</summary>
    public static string CleanName(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return string.Empty;
        var words = name.Trim().Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
        return string.Join(" ", words.Select(w => w.Length > 0 && char.IsLower(w[0]) ? char.ToUpperInvariant(w[0]) + w.Substring(1) : w));
    }

    /// <summary>Why a name cannot be used, or null: 2-24 letters, spaces, apostrophes and hyphens.</summary>
    public static string WhyNotName(string name, string what = "The name")
    {
        string n = CleanName(name);
        if (n.Length < 2) return $"{what} needs at least two letters.";
        if (n.Length > MaxNameLength) return $"{what} can have at most {MaxNameLength} characters.";
        foreach (char c in n)
            if (!char.IsLetter(c) && c != ' ' && c != '\'' && c != '-' && c != '’') return $"{what} can only hold letters, spaces, apostrophes and hyphens.";
        if (!n.Any(char.IsLetter)) return $"{what} needs letters.";
        return null;
    }

    /// <summary>
    /// What a nation's people are called, from its name, as most languages of the game's world would say it: Iridia to
    /// Iridian, Vaelora to Vaeloran, Aster to Asterian, Kethe to Kethean, Xian-K'in to Xian-K'inian. The player can change it.
    /// </summary>
    public static string SuggestDemonym(string name)
    {
        string n = CleanName(name);
        if (n.Length == 0) return string.Empty;
        string lower = n.ToLowerInvariant();
        if (lower.EndsWith("ia") || lower.EndsWith("a") || lower.EndsWith("o")) return n + "n";
        if (lower.EndsWith("e")) return n + "an";
        if (lower.EndsWith("y")) return n.Substring(0, n.Length - 1) + "ian";
        if (lower.EndsWith("i") || lower.EndsWith("u")) return n + "an";
        return n + "ian";
    }

    /// <summary>What its ways are called: the demonym (Iridian), as the culture is named after its people.</summary>
    public static string SuggestAdjective(string name) => SuggestDemonym(name);

    /// <summary>The people in the plural ("Iridians"); a demonym already plural or ending in s is kept.</summary>
    public static string Plural(string demonym)
    {
        if (string.IsNullOrEmpty(demonym)) return demonym;
        string lower = demonym.ToLowerInvariant();
        return lower.EndsWith("s") || lower.EndsWith("i") ? demonym : demonym + "s";
    }

    /// <summary>
    /// The culture's words in story text: {nation}, {citizens} (the demonym), {people} (its plural), {culture} (the
    /// adjective), {myth} (the founding myth's title), {national_food} (a food waiting to be declared national) and
    /// {national_label} ("national food"). Unnamed, the words fall back to "your people" and the like.
    /// </summary>
    public static string Expand(string text, CultureIdentity identity, string mythTitle = null, string pendingFood = null, FoodClass pendingClass = FoodClass.Edible)
    {
        if (string.IsNullOrEmpty(text) || text.IndexOf('{') < 0) return text;
        bool named = identity != null && identity.IsNamed;
        string demonym = named && !string.IsNullOrWhiteSpace(identity.demonym) ? identity.demonym : named ? SuggestDemonym(identity.name) : null;
        string adjective = named && !string.IsNullOrWhiteSpace(identity.adjective) ? identity.adjective : demonym;
        var sb = new StringBuilder(text);
        sb.Replace("{nation}", named ? identity.name : "your nation");
        sb.Replace("{citizens}", demonym ?? "your people");
        sb.Replace("{people}", demonym != null ? Plural(demonym) : "your people");
        sb.Replace("{culture}", adjective ?? "your people's");
        sb.Replace("{myth}", string.IsNullOrEmpty(mythTitle) ? "the founding myth" : mythTitle);
        sb.Replace("{national_food}", string.IsNullOrEmpty(pendingFood) ? "this food" : pendingFood);
        sb.Replace("{national_label}", NationalLabel(pendingClass));
        return sb.ToString();
    }

    // ===== STORY CONSEQUENCES =====

    /// <summary>
    /// The target of a "culture:" consequence: "myth song" (found the culture with a myth), "embrace" / "decline" (the
    /// waiting national food), "leaning Weaver" (+N shifts N% toward a family), "presence" (+N% presence everywhere held), "unity" (+N Unity), "joy" (+N% joy).
    /// </summary>
    public static bool ParseConsequence(string target, out string verb, out string argument)
    {
        verb = argument = null;
        if (string.IsNullOrWhiteSpace(target)) return false;
        var parts = target.Trim().Split(new[] { ' ' }, 2, StringSplitOptions.RemoveEmptyEntries);
        verb = parts[0].ToLowerInvariant();
        argument = parts.Length > 1 ? parts[1].Trim() : null;
        switch (verb)
        {
            case "myth": return FoundingMyths.Find(argument) != null;
            case "leaning": return TryFamily(argument, out _);
            case "embrace":
            case "decline":
            case "presence":
            case "unity":
            case "joy":
                return true;
            // Public memory (T09): "account <dispute spec> <acknowledge|revise|sponsor>".
            case "account": return AccountAnswer(argument, out _, out _);
            default: return false;
        }
    }

    /// <summary>A "culture:account" argument: the dispute's kind and the answer (T09).</summary>
    public static bool AccountAnswer(string argument, out string spec, out string answer)
    {
        spec = answer = null;
        var parts = (argument ?? string.Empty).Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2) return false;
        spec = parts[0];
        answer = parts[1].ToLowerInvariant();
        return answer == "acknowledge" || answer == "revise" || answer == "sponsor";
    }

    public static bool TryFamily(string name, out EnclaveFamily family)
    {
        family = default;
        return !string.IsNullOrWhiteSpace(name) && Enum.TryParse(name.Trim(), true, out family) && Enum.IsDefined(typeof(EnclaveFamily), family);
    }

    /// <summary>Player wording of a "culture:" consequence (the outro lists it).</summary>
    public static string Describe(string target, int value)
    {
        if (!ParseConsequence(target, out string verb, out string argument)) return $"Culture: {target}";
        switch (verb)
        {
            case "myth":
                var myth = FoundingMyths.Find(argument);
                return $"Your People Are Founded: {myth.title}";
            case "embrace": return "Your People Embrace a National Food";
            case "decline": return "Your People Keep Their Table Varied";
            case "presence": return $"Your Culture Takes Root: {value:+#;-#;0}% Presence On Your Land";
            case "unity": return $"Your People Draw Together: {value:+#;-#;0} Unity";
            case "joy": return $"Your People Rejoice: {value:+#;-#;0}% Joy";
            case "account":
                AccountAnswer(argument, out _, out string answer);
                return answer == "acknowledge" ? "The Council Acknowledges the Gap" : answer == "revise" ? "The Council Revises Its Promise" : "A Legend Sponsors Another Account";
            default:
                TryFamily(argument, out var family);
                return $"Your Culture Leans {(value >= 0 ? "Toward" : "Away From")} the {family} Ways ({Math.Abs(value)}%)";
        }
    }

    private static void Add(float[] into, float[] shares, float weight)
    {
        if (weight <= 0f || shares == null) return;
        for (int i = 0; i < into.Length && i < shares.Length; i++) into[i] += shares[i] * weight;
    }

    private static float[] FromMap(IDictionary<EnclaveFamily, float> map)
    {
        var a = new float[Families.Length];
        if (map != null) foreach (var p in map) a[(int)p.Key] += Math.Max(0f, p.Value);
        return a;
    }

    private static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;

    public static string Percent(float share) => (share * 100f).ToString("0", CultureInfo.InvariantCulture) + "%";
}

/// <summary>
/// The culture's presence on the world map for the pure layers (the Culture lens reads it through
/// <see cref="WorldLenses"/>): the live system registers a reader; <see cref="Version"/> changes whenever it spreads.
/// </summary>
public static class CultureField
{
    private static Func<int, float> _reader;

    /// <summary>Bumped each time presence changes, so the world view knows to repaint the lens.</summary>
    public static int Version { get; private set; }

    /// <summary>What the culture is called in the lens's words ("Iridian"; "Your people's" before it is named).</summary>
    public static string Name { get; set; } = "Your people's";

    public static void Register(Func<int, float> reader)
    {
        _reader = reader;
        Version++;
    }

    public static void Unregister(Func<int, float> reader)
    {
        if (_reader == reader) _reader = null;
        Version++;
    }

    public static void Touch() => Version++;

    /// <summary>The culture's presence in a cell, 0-1 (0 with no culture yet).</summary>
    public static float Of(int cell) => _reader != null && cell >= 0 ? _reader(cell) : 0f;
}
