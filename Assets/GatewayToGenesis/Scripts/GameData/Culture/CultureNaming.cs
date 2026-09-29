using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>Who is giving a name: the culture's voice (its character when it names), its myth, and what it calls itself.</summary>
public struct NamingVoice
{
    /// <summary>The culture's leading way of living: the words it reaches for.</summary>
    public EnclaveFamily voice;
    /// <summary>The founding myth's id (song, hearth, ruins): the memory its names return to.</summary>
    public string myth;
    /// <summary>What its ways are called (Iridian), or null before it is named.</summary>
    public string adjective;
    public string nation;

    public NamingVoice(EnclaveFamily voice, string myth, string adjective = null, string nation = null)
    {
        this.voice = voice; this.myth = myth; this.adjective = adjective; this.nation = nation;
    }
}

/// <summary>
/// How a culture names what it builds and keeps, with no scene state (tested in <c>CultureLifeTests</c>): its hamlets,
/// its districts, the landmarks in them (shrines, temples, cathedrals, halls of song) and its holidays. The culture's
/// character is its voice (a Weaver people names in verses and canticles, an Esoteric one in veils and moons), its
/// founding myth the memory its names return to (the Last Verse, the Shared Fire, the Standing Wall), and the kind of
/// district or landmark the thing named. Names are chosen by a stable seed, so the same culture naming the same place
/// always says the same thing. The vocabularies are the game's proposals, not vault canon.
/// </summary>
public static class CultureNaming
{
    // The words each way of living reaches for ("The {word} Market").
    private static readonly Dictionary<EnclaveFamily, string[]> Words = new Dictionary<EnclaveFamily, string[]>
    {
        { EnclaveFamily.Agromagical, new[] { "Orchard", "Furrow", "Sheaf", "Seedbed", "Harvest", "Bough" } },
        { EnclaveFamily.Militant, new[] { "Shield", "Vigil", "Spear", "Rampart", "Oath", "Watchfire" } },
        { EnclaveFamily.Auric, new[] { "Lantern", "Oracle", "Codex", "Relic", "Prism", "Gilded Page" } },
        { EnclaveFamily.Weaver, new[] { "Verse", "Refrain", "Canticle", "Chorus", "Loom", "Lullaby" } },
        { EnclaveFamily.Domestication, new[] { "Hearth", "Fold", "Herd", "Paddock", "Burrow", "Saddle" } },
        { EnclaveFamily.Trading, new[] { "Scale", "Crossing", "Caravan", "Ledger", "Toll", "Waystone" } },
        { EnclaveFamily.Industrious, new[] { "Anvil", "Beam", "Forge", "Kiln", "Quarry", "Keystone" } },
        { EnclaveFamily.Regal, new[] { "Crown", "Banner", "Seal", "Throne", "Mantle", "Herald" } },
        { EnclaveFamily.Indulgent, new[] { "Honey", "Revel", "Silk", "Feast", "Carnival", "Nectar" } },
        { EnclaveFamily.Esoteric, new[] { "Veil", "Moon", "Star", "Whisper", "Leyline", "Silver Tide" } },
    };

    // The same, as the first half of a place name ("Lanternford").
    private static readonly Dictionary<EnclaveFamily, string[]> Prefixes = new Dictionary<EnclaveFamily, string[]>
    {
        { EnclaveFamily.Agromagical, new[] { "Sheaf", "Orchard", "Bloom", "Furrow", "Green" } },
        { EnclaveFamily.Militant, new[] { "Shield", "Iron", "Oath", "Watch" } },
        { EnclaveFamily.Auric, new[] { "Gold", "Lantern", "Gilt", "Oracle" } },
        { EnclaveFamily.Weaver, new[] { "Chorus", "Verse", "Lute", "Loom" } },
        { EnclaveFamily.Domestication, new[] { "Fold", "Herd", "Hearth", "Hide" } },
        { EnclaveFamily.Trading, new[] { "Coin", "Caravan", "Scale", "Ledger" } },
        { EnclaveFamily.Industrious, new[] { "Anvil", "Stone", "Kiln", "Forge" } },
        { EnclaveFamily.Regal, new[] { "Crown", "Banner", "High", "Seal" } },
        { EnclaveFamily.Indulgent, new[] { "Honey", "Silk", "Revel", "Feast" } },
        { EnclaveFamily.Esoteric, new[] { "Moon", "Star", "Veil", "Silver" } },
    };

    private static readonly string[] PlaceEndings = { "ford", "vale", "hold", "reach", "wick", "mere", "stead", "gate", "rest", "hollow" };

    // What each kind of district is called ("the Canticle Market"); the generalist is a hamlet.
    private static readonly Dictionary<EnclaveFamily, string[]> Kinds = new Dictionary<EnclaveFamily, string[]>
    {
        { EnclaveFamily.Agromagical, new[] { "Fields", "Terraces" } },
        { EnclaveFamily.Militant, new[] { "Watch", "Garrison" } },
        { EnclaveFamily.Auric, new[] { "Academy", "Athenaeum" } },
        { EnclaveFamily.Weaver, new[] { "Quarter", "Looms" } },
        { EnclaveFamily.Domestication, new[] { "Pastures", "Folds" } },
        { EnclaveFamily.Trading, new[] { "Market", "Bazaar" } },
        { EnclaveFamily.Industrious, new[] { "Works", "Foundry" } },
        { EnclaveFamily.Regal, new[] { "Court", "Precinct" } },
        { EnclaveFamily.Indulgent, new[] { "Gardens", "Pleasances" } },
        { EnclaveFamily.Esoteric, new[] { "Sanctum", "Cloister" } },
    };

    // How each way of living keeps a holiday ("the Vigil of the Shared Fire").
    private static readonly Dictionary<EnclaveFamily, string> Festive = new Dictionary<EnclaveFamily, string>
    {
        { EnclaveFamily.Agromagical, "Harvest Feast" }, { EnclaveFamily.Militant, "Muster" }, { EnclaveFamily.Auric, "Illumination" },
        { EnclaveFamily.Weaver, "Songfeast" }, { EnclaveFamily.Domestication, "Hearthgathering" }, { EnclaveFamily.Trading, "Fair" },
        { EnclaveFamily.Industrious, "Kindling" }, { EnclaveFamily.Regal, "Coronation" }, { EnclaveFamily.Indulgent, "Revel" },
        { EnclaveFamily.Esoteric, "Vigil" },
    };

    // What each founding myth remembers ("of the Last Verse").
    private static readonly Dictionary<string, string[]> Motifs = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
    {
        { "song", new[] { "Last Verse", "Remembered Song", "Unbroken Chorus", "First Singers" } },
        { "hearth", new[] { "Shared Fire", "One Pot", "Last Seed", "Kept Flame" } },
        { "ruins", new[] { "Standing Wall", "Old Gate", "Broken Arch", "First Stones" } },
    };
    private static readonly string[] NoMyth = { "First Dawn", "Gathering", "Survivors" };

    public static IReadOnlyList<string> WordsOf(EnclaveFamily family) => Words[family];
    public static string FestiveOf(EnclaveFamily family) => Festive[family];
    public static IReadOnlyList<string> MotifsOf(string myth) => !string.IsNullOrEmpty(myth) && Motifs.TryGetValue(myth, out var m) ? m : NoMyth;

    // ===== NAMES =====

    /// <summary>
    /// What the culture calls a new hamlet (<paramref name="district"/> null) or a district of <paramref name="district"/>'s
    /// category: "Chorusgate", "The Canticle Market", "Market of the Last Verse", "The Iridian Market". Never one of
    /// <paramref name="taken"/>.
    /// </summary>
    public static string District(NamingVoice v, EnclaveFamily? district, int seed, ICollection<string> taken = null)
    {
        return Unique(seed, taken, s =>
        {
            if (!district.HasValue) return Place(v, s);
            string kind = Pick(Kinds[district.Value], s >> 3);
            switch (Mod(s, v.adjective != null ? 4 : 3))
            {
                case 0: return $"The {Pick(Words[v.voice], s >> 5)} {kind}";
                case 1: return $"{kind} of the {Pick(MotifsOf(v.myth), s >> 7)}";
                case 2: return $"{Place(v, s >> 2)} {kind}";
                default: return $"The {v.adjective} {kind}";
            }
        });
    }

    /// <summary>What the culture calls a landmark (<paramref name="noun"/>: "Temple"): "The Temple of the Shared Fire", "The Moon Temple", "The Iridian Cathedral".</summary>
    public static string Landmark(NamingVoice v, string noun, int seed, ICollection<string> taken = null)
    {
        noun = string.IsNullOrWhiteSpace(noun) ? "Hall" : noun.Trim();
        return Unique(seed, taken, s =>
        {
            switch (Mod(s, v.adjective != null ? 4 : 3))
            {
                case 0: return $"The {noun} of the {Pick(MotifsOf(v.myth), s >> 4)}";
                case 1: return $"The {noun} of the {Pick(Words[v.voice], s >> 6)}";
                case 2: return $"The {Pick(Words[v.voice], s >> 6)} {noun}";
                default: return $"The {v.adjective} {noun}";
            }
        });
    }

    /// <summary>
    /// What the culture calls a holiday commemorating <paramref name="occasion"/> (a heritage moment's kind: founded,
    /// named, national-food, festival, reform, landmark; anything else is the people's joy) whose subject is
    /// <paramref name="subject"/> (the food, the settlement, the landmark): "The Feast of Peach Soup", "The Vigil of the
    /// Shared Fire", "Iridia Founding Day".
    /// </summary>
    public static string Holiday(NamingVoice v, string occasion, string subject, int seed, ICollection<string> taken = null)
    {
        string festive = Festive[v.voice];
        string nation = string.IsNullOrWhiteSpace(v.nation) ? null : v.nation;
        string about = string.IsNullOrWhiteSpace(subject) ? null : Bare(subject);
        return Unique(seed, taken, s =>
        {
            switch ((occasion ?? string.Empty).ToLowerInvariant())
            {
                case "founded": return nation != null && Mod(s, 2) == 0 ? $"{nation} Founding Day" : $"The Day of the {Pick(MotifsOf(v.myth), s >> 3)}";
                case "named": return nation != null ? $"{nation} Naming Day" : "The Day of Names";
                case "national-food": return about != null ? $"The Feast of {about}" : $"The {festive} of the Table";
                case "festival": return about != null ? $"The {festive} of {about}" : $"The {festive} of the Streets";
                case "reform": return $"The {festive} of Renewal";
                case "landmark": return about != null ? $"The Consecration of {about}" : "The Consecration";
                default:
                    return Mod(s, 2) == 0 ? $"The {festive} of the {Pick(MotifsOf(v.myth), s >> 3)}" : $"The {festive} of the {Pick(Words[v.voice], s >> 5)}";
            }
        });
    }

    // "Lanternford": a place name in the culture's voice.
    private static string Place(NamingVoice v, int s) => Pick(Prefixes[v.voice], s >> 1) + Pick(PlaceEndings, s >> 4);

    // "The Moon Temple" is spoken of as "Moon Temple" after "of".
    private static string Bare(string name) => name.StartsWith("The ", StringComparison.Ordinal) ? name.Substring(4) : name;

    // Try the seed and the next ones until a name is free; then number it.
    private static string Unique(int seed, ICollection<string> taken, Func<int, string> make)
    {
        bool Free(string n) => taken == null || !taken.Contains(n, StringComparer.OrdinalIgnoreCase);
        string first = null;
        for (int i = 0; i < 24; i++)
        {
            string name = make(Mix(seed, i));
            first = first ?? name;
            if (Free(name)) return name;
        }
        for (int n = 2; ; n++)
        {
            string numbered = $"{first} {AgeRules.Roman(n)}";
            if (Free(numbered)) return numbered;
        }
    }

    // ===== SEEDS =====

    /// <summary>A stable seed from words and numbers (string.GetHashCode differs between runs, so it is not used).</summary>
    public static int Seed(params object[] parts)
    {
        unchecked
        {
            uint h = 2166136261;
            foreach (var p in parts)
            {
                foreach (char c in Convert.ToString(p, System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty) { h ^= c; h *= 16777619; }
                h ^= '|';
                h *= 16777619;
            }
            return (int)(h & 0x7fffffff);
        }
    }

    private static int Mix(int seed, int i)
    {
        unchecked
        {
            uint x = (uint)seed + (uint)i * 0x9E3779B9;
            x ^= x >> 16; x *= 0x85EBCA6B; x ^= x >> 13; x *= 0xC2B2AE35; x ^= x >> 16;
            return (int)(x & 0x7fffffff);
        }
    }

    private static int Mod(int s, int n) => n <= 0 ? 0 : (int)((uint)s % (uint)n);

    private static string Pick(IReadOnlyList<string> list, int s) => list[Mod(s, list.Count)];
}

/// <summary>
/// The calendar of holidays, with no scene state: a holiday is kept on one Seventh (1-21) of one Phase of the Echo
/// (1-3), so it comes round every Echo (63 Sevenths). The Ritual Seventh is the 21st.
/// </summary>
public static class CultureCalendar
{
    public const int SeventhsPerEcho = TimeSystemLogic.SeventhsPerPhase * TimeSystemLogic.PhasesPerEcho;

    /// <summary>"1st", "2nd", "3rd", "11th", "21st".</summary>
    public static string Ordinal(int n)
    {
        int tens = n % 100;
        string suffix = tens >= 11 && tens <= 13 ? "th" : (n % 10) == 1 ? "st" : (n % 10) == 2 ? "nd" : (n % 10) == 3 ? "rd" : "th";
        return n + suffix;
    }

    public static string PhaseWord(int phase) => phase == 1 ? "first" : phase == 2 ? "second" : phase == 3 ? "third" : Ordinal(phase);

    /// <summary>"the 20th Seventh of the second Phase of every Echo" (the Ritual Seventh is called so).</summary>
    public static string Day(int seventh, int phase) =>
        seventh == TimeSystemLogic.SeventhsPerPhase ? $"the Ritual Seventh of the {PhaseWord(phase)} Phase of every Echo" : $"the {Ordinal(seventh)} Seventh of the {PhaseWord(phase)} Phase of every Echo";

    public static string Day(Holiday h) => h == null ? string.Empty : Day(h.seventh, h.phase);

    /// <summary>"the 20th Seventh of the Phase of Flourish, Echo of Resonance, Cycle 2": the day it was first set apart.</summary>
    public static string Established(Holiday h) =>
        h == null ? string.Empty : $"the {Ordinal(h.seventh)} Seventh of the {(string.IsNullOrEmpty(h.phaseName) ? $"{PhaseWord(h.phase)} Phase" : h.phaseName)}{(string.IsNullOrEmpty(h.echoName) ? $", Echo {h.echo}" : ", " + h.echoName)}, Cycle {h.cycle}";

    /// <summary>A Seventh's place in its Echo, 1-63.</summary>
    public static int InEcho(int seventh, int phase) => (Math.Max(1, phase) - 1) * TimeSystemLogic.SeventhsPerPhase + Math.Max(1, seventh);

    /// <summary>Sevenths from now (<paramref name="seventh"/> of <paramref name="phase"/>) until the holiday's day: 0 on the day itself.</summary>
    public static int SeventhsUntil(Holiday h, int seventh, int phase)
    {
        if (h == null) return int.MaxValue;
        int d = InEcho(h.seventh, h.phase) - InEcho(seventh, phase);
        return ((d % SeventhsPerEcho) + SeventhsPerEcho) % SeventhsPerEcho;
    }

    /// <summary>One key per Echo (cycle * 10 + echo): a holiday is kept once in each.</summary>
    public static int EchoKey(int cycle, int echo) => cycle * 10 + echo;
}
