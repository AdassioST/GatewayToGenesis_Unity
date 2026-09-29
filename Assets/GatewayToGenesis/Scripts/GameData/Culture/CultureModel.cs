using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>What a stored food is to the kitchen (the labels of <see cref="FoodKind.cuisine"/>).</summary>
public enum FoodClass
{
    /// <summary>Actual food: it feeds the people on its own.</summary>
    Edible,
    /// <summary>What a dish is made from: oils, seeds, pits, sweeteners. Eaten raw only when the edibles run out.</summary>
    Ingredient,
    /// <summary>What gives a dish its voice: some Eleos Blooms and other savours. Never eaten to fill a belly.</summary>
    Spice,
    /// <summary>
    /// Teas, including Eleos brews and ordinary infusions: drunk like food, so they feed the people alongside
    /// the edibles, but they are a category of their own at the table (a nation can have a national tea).
    /// The legacy EleosTea name and index are retained for saves; the table's display label is Tea.
    /// </summary>
    EleosTea,
    /// <summary>
    /// Drinks brewed or distilled in the cellar: ales, meads, wines and spirits, never made from an Eleos Bloom (those
    /// are teas). They are drunk for joy, not hunger: a luxury, a national drink, and the last thing opened when the
    /// stores are drawn on. Appended, so saved indices keep their meaning.
    /// </summary>
    Beverage,
}

/// <summary>
/// The names the civilization gives itself once its founding myth is told: the nation (Iridia), what its people are
/// called (Citizens: Iridian) and what its ways are called (Culture: Iridian).
/// </summary>
[Serializable]
public class CultureIdentity
{
    public string name;
    public string demonym;
    public string adjective;

    public bool IsNamed => !string.IsNullOrWhiteSpace(name);

    public CultureIdentity Copy() => new CultureIdentity { name = name, demonym = demonym, adjective = adjective };
}

/// <summary>How much of the culture leans toward one of the ten ways of living (the vault's civic families).</summary>
[Serializable]
public class CultureLeaning
{
    public EnclaveFamily family;
    /// <summary>Share of the culture, 0-1; all leanings sum to 1.</summary>
    public float share;
}

/// <summary>A food (or tea, drink, ingredient or spice) the people know: how accustomed they are to it, and whether it is theirs.</summary>
[Serializable]
public class Foodway
{
    public string resource;
    public FoodClass cuisine;
    /// <summary>How accustomed the people are to it, 0-1: it follows its share of the diet of its class, slowly.</summary>
    public float familiarity;
    /// <summary>How much of it the people have eaten or used, all told.</summary>
    public float eaten;
    /// <summary>Sevenths in a row it has been the first of its class at the table (reset when another overtakes it).</summary>
    public int firstSevenths;
    /// <summary>It is a national food (or tea, ingredient or spice) of the culture: it stays in the heritage.</summary>
    public bool national;
    /// <summary>The Age it became national in, and the Seventh (counted from the founding).</summary>
    public string nationalAge;
    public int nationalSeventh;
    /// <summary>The people chose to keep their table varied instead: it is not offered again until its familiarity falls and rises again.</summary>
    public bool declined;
    /// <summary>A dish or drink the people invented themselves: it grows familiar sooner and is the first offered as national.</summary>
    [SaveOptionalField] public bool invented;
    /// <summary>The tradition record of a national food (<see cref="TraditionInstance.id"/>): where it came from and whether it is still eaten.</summary>
    [SaveOptionalField] public string tradition;
}

/// <summary>A moment in the culture's own history (founded, named, a national food embraced, a reform), shown in its window.</summary>
[Serializable]
public class CultureMoment
{
    public string kind;
    public string title;
    public string text;
    public string ageId;
    /// <summary>Sevenths since the founding when it happened.</summary>
    public int seventh;
}

/// <summary>
/// Everything the culture remembers, saved whole with <see cref="CultureSystem"/> (GameSnapshot.Schema). Presence is
/// kept per world cell index (only cells the culture has reached). Not Unity-serialized (the save codec reads it; its
/// dictionary is no Unity field), so it carries no [Serializable].
/// </summary>
public class CultureState
{
    public bool founded;
    public string myth;
    public CultureIdentity identity = new CultureIdentity();
    public string foundedAge;
    /// <summary>Sevenths since the founding.</summary>
    public int sevenths;
    public List<CultureLeaning> leanings = new List<CultureLeaning>();
    public List<Foodway> foodways = new List<Foodway>();
    public Dictionary<int, float> presence = new Dictionary<int, float>();
    public List<CultureMoment> moments = new List<CultureMoment>();
    public int reforms;
    public int seventhsSinceReform;
    /// <summary>Ruins whose ways the culture has already digested (Digestive Rebirth reforms), by ruin id.</summary>
    public List<int> digestedRuins = new List<int>();
    /// <summary>The civics in force when the culture was founded (Ship of Theseus asks when none of them is left).</summary>
    public List<string> foundingCivics = new List<string>();
    public bool foundingCivicsGone;
    /// <summary>A food waiting for the people to decide whether it is theirs (the national food story), or null.</summary>
    public string pendingFood;
    /// <summary>The culture's name was asked for and not yet given.</summary>
    public bool namingPending;

    // ----- The life of the culture (CultureSystem.Life.cs); optional so saves made before them still load. -----
    /// <summary>Names the culture gave its hamlets and districts (the newest per settlement is the one it goes by).</summary>
    [SaveOptionalField] public List<CulturalName> names = new List<CulturalName>();
    /// <summary>Landmarks raised in its districts and havens (shrines, temples, cathedrals, halls of song...).</summary>
    [SaveOptionalField] public List<Landmark> landmarks = new List<Landmark>();
    /// <summary>The holidays it keeps, each on its day of every Echo.</summary>
    [SaveOptionalField] public List<Holiday> holidays = new List<Holiday>();
    /// <summary>Festivals held by cultural parties, one record per settlement.</summary>
    [SaveOptionalField] public List<FestivalRecord> festivals = new List<FestivalRecord>();
    /// <summary>When each rite or gathering was last held (by activity id).</summary>
    [SaveOptionalField] public List<ActivityRecord> activities = new List<ActivityRecord>();
    /// <summary>The kitchen's standing orders: batches of a recipe cooked every Seventh while the stores allow.</summary>
    [SaveOptionalField] public List<KitchenOrder> orders = new List<KitchenOrder>();
    /// <summary>How well the people live, 0-100 (recomputed each Seventh), and its two halves, 0-1.</summary>
    [SaveOptionalField] public float happiness;
    [SaveOptionalField] public float survival;
    [SaveOptionalField] public float living;
    /// <summary>The afterglow of festivals, holidays and gatherings, 0-1 (it fades each Seventh).</summary>
    [SaveOptionalField] public float joy;
    /// <summary>How well the luxuries the people want were met last Seventh, 0-1, and each category's share met.</summary>
    [SaveOptionalField] public float luxury;
    [SaveOptionalField] public float culturalFaith;
    [SaveOptionalField] public List<LuxuryMet> luxuriesMet = new List<LuxuryMet>();
    /// <summary>The Seventh (since the founding) the last holiday was established; festivals and dishes all told.</summary>
    [SaveOptionalField] public int lastHolidaySeventh;
    [SaveOptionalField] public int festivalsHeld;
    [SaveOptionalField] public float dishesCooked;
    /// <summary>Drinks the cellar has brewed and distilled, all told.</summary>
    [SaveOptionalField] public float drinksMade;
    /// <summary>The dishes and drinks the people invented and named (<see cref="CultureInvention"/>), in the order they were made.</summary>
    [SaveOptionalField] public List<InventedRecipe> invented = new List<InventedRecipe>();
    /// <summary>The living weighed in the people's happiness was announced (it is said once).</summary>
    [SaveOptionalField] public bool livingAnnounced;

    /// <summary>
    /// The culture's extension envelope (<see cref="CultureExtensionState"/>): the occurrence ledger, the traditions and
    /// each later culture feature's own record. Optional: a save made before it loads with a fresh one.
    /// </summary>
    [SaveOptionalField] public CultureExtensionState extensions = new CultureExtensionState();
}

/// <summary>A name the culture gave a settlement of its own (a hamlet, or a district when it was upgraded).</summary>
[Serializable]
public class CulturalName
{
    public int settlement;
    public string name;
    /// <summary>What it was called before.</summary>
    public string former;
    /// <summary>The district it was named for (empty: the hamlet).</summary>
    public string district;
    /// <summary>The culture's character when it gave the name (its voice).</summary>
    public EnclaveFamily voice;
    public int seventh;
}

/// <summary>A landmark raised in a settlement (<see cref="LandmarkSpec"/>): what it is and what the culture called it.</summary>
[Serializable]
public class Landmark
{
    public int settlement;
    public string spec;
    public string name;
    public int seventh;
    public string ageId;
}

/// <summary>
/// A holiday: the day it was established (the Seventh of the Phase, and which Phase of the Echo, Echo and Cycle it was)
/// and what it remembers. It is kept on that Seventh of that Phase of every Echo.
/// </summary>
[Serializable]
public class Holiday
{
    public string name;
    /// <summary>What it commemorates (the heritage moment's title, or the people's joy).</summary>
    public string occasion;
    /// <summary>Its day: the Seventh (1-21) and the Phase of the Echo (1-3).</summary>
    public int seventh, phase;
    /// <summary>When it was established: the Echo (1-4) and Cycle, and the names of that Phase and Echo.</summary>
    public int echo, cycle;
    public string phaseName, echoName;
    /// <summary>Sevenths since the founding when it was established.</summary>
    public int established;
    /// <summary>Times it has been kept, and the last Echo it was kept in (cycle * 10 + echo).</summary>
    public int kept;
    public int lastKept;
}

/// <summary>Festivals a settlement has held: how many and the last Seventh (since the founding).</summary>
[Serializable]
public class FestivalRecord
{
    public int settlement;
    public int count;
    public int lastSeventh;
}

/// <summary>A rite or gathering: how many times it was held and the last Seventh (since the founding).</summary>
[Serializable]
public class ActivityRecord
{
    public string id;
    public int count;
    public int lastSeventh;
}

/// <summary>A standing order in the kitchen: batches of a recipe cooked each Seventh as far as the stores allow.</summary>
[Serializable]
public class KitchenOrder
{
    public string recipe;
    public int batches;
}

/// <summary>How much of what the people wanted of a luxury category was met last Seventh, 0-1.</summary>
[Serializable]
public class LuxuryMet
{
    public string category;
    public float met;
}

/// <summary>
/// One answer to "Why were we founded?" (the founding myth, told when Horology first lets the people count the
/// Sevenths): the chorus stance it is chosen with, the leanings the culture begins from, and what it gives for as long
/// as the myth is told. Every number is a proposal.
/// </summary>
public sealed class FoundingMyth
{
    public readonly string id, title, stance, answer, summary;
    public readonly IReadOnlyDictionary<EnclaveFamily, float> baseline;
    public readonly IReadOnlyList<GameEffect> effects;

    public FoundingMyth(string id, string title, string stance, string answer, string summary, IDictionary<EnclaveFamily, float> baseline, params GameEffect[] effects)
    {
        this.id = id;
        this.title = title;
        this.stance = stance;
        this.answer = answer;
        this.summary = summary;
        this.baseline = new Dictionary<EnclaveFamily, float>(baseline);
        this.effects = effects ?? new GameEffect[0];
    }
}

/// <summary>The three founding myths (Culture.ink tells them; the chorus stance picks one).</summary>
public static class FoundingMyths
{
    public static readonly FoundingMyth Song = new FoundingMyth("song", "To Keep the Song", "Idealism",
        "We were founded so that someone would still be singing.",
        "When the world ended, the song went quiet. The first of us gathered around those who still remembered a verse, so that what the world forgot would have somewhere to live.",
        new Dictionary<EnclaveFamily, float>
        {
            { EnclaveFamily.Weaver, 0.34f }, { EnclaveFamily.Auric, 0.22f }, { EnclaveFamily.Agromagical, 0.14f }, { EnclaveFamily.Esoteric, 0.1f },
            { EnclaveFamily.Indulgent, 0.08f }, { EnclaveFamily.Regal, 0.06f }, { EnclaveFamily.Trading, 0.06f },
        },
        new GameEffect(GameEffectType.PillarBonus, 1f, ModifierType.Add, "waltz"),
        new GameEffect(GameEffectType.MaxMoraleModifier, 5f, ModifierType.Add));

    public static readonly FoundingMyth Hearth = new FoundingMyth("hearth", "To Share the Hearth", "Realism",
        "We were founded because alone, we starved.",
        "No one could keep a fire alone through the golden dust. The first of us pooled the last of the seed and the last of the fuel, and ate from one pot. Whoever came to the fire was fed; whoever was fed kept the fire.",
        new Dictionary<EnclaveFamily, float>
        {
            { EnclaveFamily.Agromagical, 0.34f }, { EnclaveFamily.Domestication, 0.16f }, { EnclaveFamily.Industrious, 0.14f }, { EnclaveFamily.Militant, 0.12f },
            { EnclaveFamily.Trading, 0.1f }, { EnclaveFamily.Weaver, 0.08f }, { EnclaveFamily.Indulgent, 0.06f },
        },
        new GameEffect(GameEffectType.ResourceModifier, 5f, ModifierType.Percentage, "Food"),
        new GameEffect(GameEffectType.PillarBonus, 1f, ModifierType.Add, "aureus"));

    public static readonly FoundingMyth Ruins = new FoundingMyth("ruins", "To Build on the Ruins", "Pragmatism",
        "We were founded where the old walls still stood.",
        "The Old World fell, but not all of it. The first of us settled in the lee of a broken wall because it was a wall, and took what the ruins still held. What was left behind would become what came next.",
        new Dictionary<EnclaveFamily, float>
        {
            { EnclaveFamily.Industrious, 0.28f }, { EnclaveFamily.Regal, 0.18f }, { EnclaveFamily.Trading, 0.16f }, { EnclaveFamily.Auric, 0.14f },
            { EnclaveFamily.Militant, 0.12f }, { EnclaveFamily.Agromagical, 0.12f },
        },
        new GameEffect(GameEffectType.ConstructionCostModifier, -5f, ModifierType.Percentage, null, ScopeType.Global),
        new GameEffect(GameEffectType.PillarBonus, 1f, ModifierType.Add, "regalia"));

    public static readonly IReadOnlyList<FoundingMyth> All = new[] { Song, Hearth, Ruins };

    public static FoundingMyth Find(string id) =>
        string.IsNullOrEmpty(id) ? null : All.FirstOrDefault(m => string.Equals(m.id, id.Trim(), StringComparison.OrdinalIgnoreCase)
            || string.Equals(m.stance, id.Trim(), StringComparison.OrdinalIgnoreCase));
}

/// <summary>A name or keyword and the family it leans the culture toward (civics, resources, land).</summary>
[Serializable]
public class FamilyTag
{
    public string key;
    public EnclaveFamily family;

    public FamilyTag() { }
    public FamilyTag(string key, EnclaveFamily family) { this.key = key; this.family = family; }
}

/// <summary>What the culture's leaning toward a family gives while it is the culture's character (the leading leaning).</summary>
[Serializable]
public class CharacterEffect
{
    public EnclaveFamily family;
    public GameEffectType type;
    public float value;
    public ModifierType modifierType;
    public string target;
    public ScopeType scope;

    public CharacterEffect() { }
    public CharacterEffect(EnclaveFamily family, GameEffectType type, float value, ModifierType modifierType, string target, ScopeType scope = ScopeType.Individual)
    {
        this.family = family; this.type = type; this.value = value; this.modifierType = modifierType; this.target = target; this.scope = scope;
    }

    public GameEffect ToEffect() => new GameEffect(type, value, modifierType, target, scope);
}

/// <summary>
/// The culture's numbers and tables (Resources/Culture/Culture.asset holds a copy to tune). Every number is a proposal;
/// the civic table is the vault's (Civic.md: each civic's family), the rest are the game's own guesses.
/// </summary>
[Serializable]
public class CultureTuning
{
    // ----- Leanings -----
    /// <summary>Weight of the founding myth's baseline in what the culture drifts toward (it never fades).</summary>
    public float mythWeight = 3f;
    /// <summary>Weight of the four pillars (Aureus: Auric, Regalia: Regal, Waltz: Weaver, Chorus: Esoteric), shared by their values.</summary>
    public float pillarWeight = 2f;
    /// <summary>Weight of each civic in force.</summary>
    public float civicWeight = 1.25f;
    /// <summary>Weight of what the people eat and work, shared by amount.</summary>
    public float usedWeight = 2f;
    /// <summary>Weight of the land the culture lives on, shared by presence.</summary>
    public float landWeight = 1.5f;
    /// <summary>Weight of each tributary district (one family each).</summary>
    public float districtWeight = 0.5f;
    /// <summary>Share of the way the culture moves toward what it is living each Seventh (slow: heritage has inertia).</summary>
    public float driftPerSeventh = 0.04f;

    // ----- Reforms (Digestive Rebirth) -----
    /// <summary>Share of the culture a reform moves to the chosen family.</summary>
    public float reformShift = 0.2f;
    /// <summary>Sevenths between reforms.</summary>
    public int reformCooldownSevenths = 21;
    /// <summary>Share of its presence the culture keeps in every cell when it reforms without ruins to digest (the people take a while to follow).</summary>
    public float reformPresenceKept = 0.7f;

    // ----- Foodways -----
    /// <summary>Share of the way familiarity moves toward the food's share of its class each Seventh.</summary>
    public float familiarityRate = 0.08f;
    /// <summary>Weight of what the cellars hold (seen and eaten from every day) against what was drawn this Seventh.</summary>
    public float heldWeight = 0.5f;
    /// <summary>Familiarity at which a food can become national.</summary>
    public float nationalFamiliarity = 0.5f;
    /// <summary>Sevenths in a row it must be the first of its class at the table (21: a whole Phase).</summary>
    public int nationalSevenths = 21;
    /// <summary>Most national foods per class.</summary>
    public int maxNationalPerClass = 3;
    /// <summary>A declined food may be offered again once its familiarity has fallen below this.</summary>
    public float declineReset = 0.2f;
    /// <summary>Output bonus (%) to each national food, ingredient or spice.</summary>
    public float nationalFoodBonusPercent = 10f;
    /// <summary>Invented dishes and drinks (the people's own) grow familiar this many times faster...</summary>
    public float inventedFamiliarityRate = 1.5f;
    /// <summary>...count this many times their share when the table decides what comes first in their class...</summary>
    public float inventedPreference = 1.5f;
    /// <summary>...and need only this share of the familiarity and Sevenths first before they are offered as national (and are offered first).</summary>
    public float inventedNationalEase = 0.7f;

    // ----- Spread -----
    /// <summary>Presence a held cell gains each Seventh, before nearness.</summary>
    public float spreadPerSeventh = 0.06f;
    /// <summary>Extra growth per Seventh at a settlement (nearness 1), less further away.</summary>
    public float seatSpreadBonus = 0.12f;
    /// <summary>Settlement reach in cells: nearness falls to 0 this many cells away.</summary>
    public int seatReach = 4;
    /// <summary>Share of its presence a cell no longer held loses each Seventh.</summary>
    public float fadePerSeventh = 0.1f;
    /// <summary>Presence below this is forgotten (the cell leaves the culture's map).</summary>
    public float forgetBelow = 0.02f;

    /// <summary>Resources and the family working or eating them leans the culture toward (proposals).</summary>
    public List<FamilyTag> resources = new List<FamilyTag>
    {
        new FamilyTag("Food", EnclaveFamily.Agromagical), new FamilyTag("Dried Auric Peaches", EnclaveFamily.Agromagical),
        new FamilyTag("Earth-Beans", EnclaveFamily.Agromagical), new FamilyTag("Bitter Roots", EnclaveFamily.Agromagical),
        new FamilyTag("Deep-Rooted Grain", EnclaveFamily.Agromagical), new FamilyTag("Ash-Bread", EnclaveFamily.Agromagical),
        new FamilyTag("Highland Rice", EnclaveFamily.Agromagical), new FamilyTag("Peach Pits", EnclaveFamily.Agromagical),
        new FamilyTag("Glimmerfern", EnclaveFamily.Agromagical), new FamilyTag("Eleos Tea", EnclaveFamily.Agromagical),
        new FamilyTag("River Fish", EnclaveFamily.Trading),
        new FamilyTag("Game Meat", EnclaveFamily.Domestication), new FamilyTag("Behemoth Meat", EnclaveFamily.Militant),
        new FamilyTag("Hides", EnclaveFamily.Domestication), new FamilyTag("Lumenwool", EnclaveFamily.Domestication),
        new FamilyTag("Wild Honey", EnclaveFamily.Indulgent), new FamilyTag("Silverreed", EnclaveFamily.Weaver),
        new FamilyTag("Research", EnclaveFamily.Auric), new FamilyTag("Faith", EnclaveFamily.Auric),
        new FamilyTag("Elderwood", EnclaveFamily.Industrious), new FamilyTag("Duskstone", EnclaveFamily.Industrious),
        new FamilyTag("Peat", EnclaveFamily.Industrious), new FamilyTag("Emberwhisper", EnclaveFamily.Industrious),
        new FamilyTag("Sky Glass", EnclaveFamily.Esoteric), new FamilyTag("Aetherlight", EnclaveFamily.Esoteric),
        new FamilyTag("Lunehymn", EnclaveFamily.Esoteric),
        // The kitchen's dishes and the shared life (CultureLifeTuning).
        new FamilyTag("Peach Soup", EnclaveFamily.Agromagical), new FamilyTag("Pit-Oil Flatbread", EnclaveFamily.Agromagical),
        new FamilyTag("Riverfish Rice", EnclaveFamily.Trading), new FamilyTag("Hunter's Stew", EnclaveFamily.Domestication),
        new FamilyTag("Honeyed Grain Porridge", EnclaveFamily.Indulgent), new FamilyTag("Ash-Loaf", EnclaveFamily.Weaver),
        new FamilyTag("Behemoth Feast Roast", EnclaveFamily.Indulgent), new FamilyTag("Unity", EnclaveFamily.Weaver),
        new FamilyTag("Auric Saffron", EnclaveFamily.Indulgent), new FamilyTag("Silver Salt", EnclaveFamily.Trading),
        new FamilyTag("Candlevein Grief Tea", EnclaveFamily.Esoteric), new FamilyTag("Lullroot Tea", EnclaveFamily.Esoteric),
        new FamilyTag("Hearthleaf Tea", EnclaveFamily.Agromagical), new FamilyTag("Saffron Riverfish Pilaf", EnclaveFamily.Trading),
        new FamilyTag("Honeyed Peach Tart", EnclaveFamily.Indulgent),
        // The cellar's beverages.
        new FamilyTag("Rootgrain Ale", EnclaveFamily.Industrious), new FamilyTag("Wild Honey Mead", EnclaveFamily.Indulgent),
        new FamilyTag("Highland Rice Wine", EnclaveFamily.Trading), new FamilyTag("Auric Peach Wine", EnclaveFamily.Indulgent),
        new FamilyTag("Bitterroot Spirit", EnclaveFamily.Militant), new FamilyTag("Auric Peach Brandy", EnclaveFamily.Regal),
    };

    /// <summary>Civics and their family: the vault's (Civic.md) and the game's four.</summary>
    public List<FamilyTag> civics = new List<FamilyTag>
    {
        new FamilyTag("Arcane Scholars", EnclaveFamily.Auric), new FamilyTag("Innovation Council", EnclaveFamily.Auric),
        new FamilyTag("Guild Masters", EnclaveFamily.Industrious), new FamilyTag("Military Academy", EnclaveFamily.Militant),
        new FamilyTag("Culinary Alchemists", EnclaveFamily.Agromagical), new FamilyTag("Cooking Guilds", EnclaveFamily.Agromagical),
        new FamilyTag("Feast of Abundance", EnclaveFamily.Indulgent), new FamilyTag("Ballad & Fantasy Plays", EnclaveFamily.Weaver),
        new FamilyTag("Ritual Whistling Fans Dancers", EnclaveFamily.Weaver), new FamilyTag("Moonlit Vigil", EnclaveFamily.Weaver),
        new FamilyTag("The Ball Fluff Runs", EnclaveFamily.Domestication), new FamilyTag("Horned Flag Stampedes", EnclaveFamily.Domestication),
        new FamilyTag("Truth Soul Leitmotif Testimonies", EnclaveFamily.Regal), new FamilyTag("Harmonic Quorum", EnclaveFamily.Regal),
        new FamilyTag("Bridal Conquest", EnclaveFamily.Militant), new FamilyTag("Grand Hunts of Legends", EnclaveFamily.Militant),
        new FamilyTag("Duels of Severance", EnclaveFamily.Militant), new FamilyTag("Leylines Nomads", EnclaveFamily.Esoteric),
        new FamilyTag("Sky Glass Burials", EnclaveFamily.Esoteric), new FamilyTag("Chaos Reverence Sacrifices", EnclaveFamily.Esoteric),
        new FamilyTag("Carnival of Shifting Reflections", EnclaveFamily.Indulgent), new FamilyTag("Polyphonic Choral Singers", EnclaveFamily.Weaver),
        new FamilyTag("Courting Grounds", EnclaveFamily.Weaver), new FamilyTag("Underground Pleasure Colosseums", EnclaveFamily.Indulgent),
        new FamilyTag("Memory Markets", EnclaveFamily.Trading), new FamilyTag("Slave & Servitude Auctions", EnclaveFamily.Trading),
        new FamilyTag("Trafficking Rings Conquests", EnclaveFamily.Militant), new FamilyTag("Species Caste System", EnclaveFamily.Regal),
        new FamilyTag("Legendary Hunting Charters", EnclaveFamily.Regal), new FamilyTag("Slayer's Amphitheater", EnclaveFamily.Militant),
        new FamilyTag("Atonalis Bombs", EnclaveFamily.Militant), new FamilyTag("Spiraling Conscription", EnclaveFamily.Militant),
    };

    /// <summary>Words in a cell's terrain, biome or landform and the family living there leans the culture toward (first match wins).</summary>
    public List<FamilyTag> land = new List<FamilyTag>
    {
        new FamilyTag("leyline", EnclaveFamily.Esoteric), new FamilyTag("moonlit", EnclaveFamily.Weaver), new FamilyTag("auric", EnclaveFamily.Auric),
        new FamilyTag("ruin", EnclaveFamily.Auric), new FamilyTag("orchard", EnclaveFamily.Agromagical), new FamilyTag("meadow", EnclaveFamily.Agromagical),
        new FamilyTag("plain", EnclaveFamily.Agromagical), new FamilyTag("fallow", EnclaveFamily.Agromagical), new FamilyTag("steppe", EnclaveFamily.Agromagical),
        new FamilyTag("wood", EnclaveFamily.Domestication), new FamilyTag("forest", EnclaveFamily.Domestication), new FamilyTag("copse", EnclaveFamily.Domestication),
        new FamilyTag("grove", EnclaveFamily.Domestication), new FamilyTag("glade", EnclaveFamily.Domestication), new FamilyTag("taiga", EnclaveFamily.Domestication),
        new FamilyTag("scrub", EnclaveFamily.Domestication), new FamilyTag("peak", EnclaveFamily.Industrious), new FamilyTag("highland", EnclaveFamily.Industrious),
        new FamilyTag("foothill", EnclaveFamily.Industrious), new FamilyTag("mesa", EnclaveFamily.Industrious), new FamilyTag("ash", EnclaveFamily.Militant),
        new FamilyTag("rift", EnclaveFamily.Militant), new FamilyTag("scar", EnclaveFamily.Militant), new FamilyTag("marsh", EnclaveFamily.Esoteric),
        new FamilyTag("bog", EnclaveFamily.Esoteric),
    };

    /// <summary>What the culture's character (its leading leaning) gives, per family.</summary>
    public List<CharacterEffect> character = new List<CharacterEffect>
    {
        new CharacterEffect(EnclaveFamily.Agromagical, GameEffectType.ResourceModifier, 6f, ModifierType.Percentage, "Food"),
        new CharacterEffect(EnclaveFamily.Militant, GameEffectType.ClickPowerBonus, 5f, ModifierType.Percentage, null, ScopeType.Global),
        new CharacterEffect(EnclaveFamily.Auric, GameEffectType.PillarBonus, 1f, ModifierType.Add, "aureus"),
        new CharacterEffect(EnclaveFamily.Weaver, GameEffectType.PillarBonus, 1f, ModifierType.Add, "waltz"),
        new CharacterEffect(EnclaveFamily.Domestication, GameEffectType.ResourceModifier, 6f, ModifierType.Percentage, "Stored Food", ScopeType.Section),
        new CharacterEffect(EnclaveFamily.Trading, GameEffectType.ResourceModifier, 6f, ModifierType.Percentage, "Valuables", ScopeType.Section),
        new CharacterEffect(EnclaveFamily.Industrious, GameEffectType.ConstructionCostModifier, -5f, ModifierType.Percentage, null, ScopeType.Global),
        new CharacterEffect(EnclaveFamily.Regal, GameEffectType.PillarBonus, 1f, ModifierType.Add, "regalia"),
        new CharacterEffect(EnclaveFamily.Indulgent, GameEffectType.MaxMoraleModifier, 5f, ModifierType.Add, null),
        new CharacterEffect(EnclaveFamily.Esoteric, GameEffectType.PillarBonus, 1f, ModifierType.Add, "chorus"),
    };

    public EnclaveFamily? FamilyOfResource(string resource) => Find(resources, resource);
    public EnclaveFamily? FamilyOfCivic(string civic) => Find(civics, civic);

    /// <summary>The family a cell's ground leans toward: the first land word found in its terrain, biome or landform.</summary>
    public EnclaveFamily? FamilyOfLand(string terrain, string biome = null, string landform = null)
    {
        string text = $"{terrain} {biome} {landform}".ToLowerInvariant();
        foreach (var tag in land)
            if (tag != null && !string.IsNullOrEmpty(tag.key) && text.Contains(tag.key.ToLowerInvariant())) return tag.family;
        return null;
    }

    private static EnclaveFamily? Find(List<FamilyTag> tags, string key)
    {
        if (tags == null || string.IsNullOrEmpty(key)) return null;
        foreach (var tag in tags) if (tag != null && string.Equals(tag.key, key.Trim(), StringComparison.OrdinalIgnoreCase)) return tag.family;
        return null;
    }
}
