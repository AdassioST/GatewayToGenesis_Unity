using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// A rite or gathering the people hold from the Culture window (an evening of song, a rite of remembrance): what it
/// costs, the Unity and morale it gives, how long until it can be held again, and the way of living it leans toward.
/// </summary>
[Serializable]
public class CultureActivitySpec
{
    public string id;
    public string name;
    [TextArea(1, 3)] public string description;
    [Tooltip("Paid from storage (Faith, peaches...).")]
    public List<ResourceAmount> cost = new List<ResourceAmount>();
    [Tooltip("Food value drawn from the stores (the shared meal), most perishable first.")]
    public float foodValue;
    public float unity;
    [Tooltip("Morale while its afterglow lasts.")]
    public int morale;
    public int moraleSevenths = 3;
    [Tooltip("Joy it adds (0-1): the afterglow the people's happiness reads.")]
    public float joy;
    public int cooldownSevenths = 7;
    [Tooltip("Researched before it can be held (empty: once the culture is founded).")]
    public string technology;
    [Tooltip("A civic that must be in force (empty: none), unless the culture leans toward its family by orLeaning.")]
    public string civic;
    [Tooltip("Share of the culture (0-1) leaning toward its family that opens it without the civic (0: the civic is required).")]
    public float orLeaning;
    [Tooltip("The way of living holding it leans the culture toward (an EnclaveFamily name; empty: none).")]
    public string family;
}

/// <summary>
/// A landmark a district (or a Religious Haven) raises: a shrine, a temple, a cathedral, a hall of song. The culture
/// names each one in its own voice. Tiers come in order: a temple stands where a shrine already does.
/// </summary>
[Serializable]
public class LandmarkSpec
{
    public string id;
    [Tooltip("What it is (\"Temple\"): the culture's name is built around it.")]
    public string name;
    [Tooltip("The district's Enclave category it belongs to (an EnclaveFamily name).")]
    public string family;
    [Tooltip("Its place in its line (1 first); a tier needs the one before it standing.")]
    public int tier = 1;
    [Tooltip("A Religious Haven may raise it too (faith landmarks).")]
    public bool haven;
    public float minDevelopment = 10f;
    [Tooltip("Materials and Unity paid to raise it.")]
    public List<ResourceAmount> cost = new List<ResourceAmount>();
    public float unityPerSeventh, faithPerSeventh;
    [Tooltip("What it adds to the living half of the people's happiness (0-1, all landmarks capped at 1).")]
    public float living;
    [TextArea(1, 3)] public string description;
}

/// <summary>
/// How a recipe is made: cooked in the kitchen (a dish, an Edible that feeds more than went into it), or made in the
/// cellar: fermented (ales, meads, wines) or distilled from what was fermented (spirits). The cellar's recipes make
/// Beverages (<see cref="FoodClass.Beverage"/>), never from an Eleos Bloom (the validator refuses one).
/// </summary>
public enum KitchenMethod
{
    Cook,
    Ferment,
    Distill,
}

/// <summary>
/// A dish the kitchen cooks, or a drink the cellar brews or distils, from what the stores hold (the Culinary Alchemists'
/// Flavor Log, Civic.md): its inputs, how much it makes, and what unlocks it. A dish feeds more than its inputs did; a
/// drink need not (it is kept for joy, and a spirit keeps for ever). Every number is a proposal.
/// </summary>
[Serializable]
public class RecipeSpec
{
    public string id;
    [Tooltip("The dish or drink (a Stored Food resource and a Pantry kind: an Edible for Cook, a Beverage for Ferment and Distill).")]
    public string dish;
    [Tooltip("Cook (the kitchen: a dish) or Ferment / Distill (the cellar: a beverage).")]
    public KitchenMethod method = KitchenMethod.Cook;
    public List<ResourceAmount> inputs = new List<ResourceAmount>();
    [Tooltip("Dishes (or drinks) one batch makes.")]
    public float output = 2f;
    [Tooltip("Researched before it can be cooked (empty: once the culture is founded).")]
    public string technology;
    [Tooltip("Unity one batch gives (memorial recipes: food as remembrance).")]
    public float unity;
    [TextArea(1, 3)] public string description;

    /// <summary>Made in the cellar (a beverage) rather than cooked in the kitchen (a dish).</summary>
    public bool InCellar => method != KitchenMethod.Cook;

    /// <summary>"Cook", "Brew" (fermented) or "Distill".</summary>
    public string Verb => method == KitchenMethod.Ferment ? "Brew" : method == KitchenMethod.Distill ? "Distill" : "Cook";

    /// <summary>"Cooked", "Brewed" or "Distilled".</summary>
    public string Done => method == KitchenMethod.Ferment ? "Brewed" : method == KitchenMethod.Distill ? "Distilled" : "Cooked";
}

/// <summary>A category of luxury (sweets, brews, fine cloth...) and the resources that satisfy it.</summary>
[Serializable]
public class LuxurySpec
{
    public string category;
    public List<string> resources = new List<string>();
    [Tooltip("Amount wanted each Seventh per hundred citizens.")]
    public float perHundred = 1f;
}

/// <summary>A resource can be both a luxury and a material of faith. Amenities are enjoyed without using up stock.</summary>
[Serializable]
public class CulturalGoodSpec
{
    public string resource;
    public bool amenity;
    [Min(0f)] public float faithPerUnit;
}

/// <summary>A living place enjoyed where it is, never harvested for its cultural benefit.</summary>
[Serializable]
public class CulturalSiteSpec
{
    public string site;
    public string category = "Living Gardens";
    public float amenity = 3f;
    public float faithPerUnit;
}

/// <summary>
/// The life of the culture (<see cref="CultureSystem"/>, CultureSystem.Life.cs): Unity, rites and gatherings,
/// festivals, holidays, landmarks, the kitchen, luxuries and happiness. The vault names none of these numbers; every
/// one is a proposal (Canon Gaps "The life of the culture").
/// </summary>
[Serializable]
public class CultureLifeTuning
{
    // ----- Unity -----
    [Tooltip("The resource the culture's shared life makes (a Vital Resource).")]
    public string unityResource = "Unity";
    [Tooltip("Unity per Seventh from song: times the culture's Weaver share (a wholly Weaver people sings this much).")]
    public float songUnity = 4f;
    [Tooltip("Unity per Seventh a Song-myth people adds (\"so that someone would still be singing\").")]
    public float songMythUnity = 1f;
    [Tooltip("Unity per Seventh each civic of music, rite or feast in force adds.")]
    public float civicUnity = 1.5f;
    public List<string> unityCivics = new List<string>
    {
        "Ballad & Fantasy Plays", "Polyphonic Choral Singers", "Ritual Whistling Fans Dancers", "Moonlit Vigil",
        "Harmonic Quorum", "Feast of Abundance", "Carnival of Shifting Reflections", "Courting Grounds",
    };
    [Tooltip("Unity per Seventh a district of song, faith or pleasure (Weaver, Esoteric, Indulgent) adds per 50 development.")]
    public float districtUnityPer50 = 1f;
    public List<EnclaveFamily> unityDistricts = new List<EnclaveFamily> { EnclaveFamily.Weaver, EnclaveFamily.Esoteric, EnclaveFamily.Indulgent };
    [Tooltip("Unity made is multiplied by this at happiness 0, rising to 1 + (1 - this) at 100: happy people sing more.")]
    public float unhappyUnity = 0.5f;
    [Tooltip("Most Unity the people can hold at once.")]
    public float unityCapacity = 500f;
    [Tooltip("How much a festival, rite, landmark or holiday counts toward what the culture lives (its leanings drift toward its way of living), against a Seventh's work and meals.")]
    public float livedPerRite = 40f;

    // ----- Rites and gatherings -----
    public List<CultureActivitySpec> activities = new List<CultureActivitySpec>
    {
        // The simplest gathering: nothing spent but the evening, so even a hungry people can keep it (a new game rule;
        // it feeds the tradition "Tales at the Hearth").
        new CultureActivitySpec
        {
            id = "tales", name = "Tales at the Hearth", family = "Auric", unity = 2f, morale = 1, moraleSevenths = 2, joy = 0.05f, cooldownSevenths = 3,
            description = "When the lamps are lit the old ones tell what the world was before the golden dust, and the children ask for the same tale again. Nothing is spent but the evening.",
        },
        new CultureActivitySpec
        {
            id = "song", name = "An Evening of Song", family = "Weaver", foodValue = 4f, unity = 6f, morale = 3, moraleSevenths = 3, joy = 0.15f, cooldownSevenths = 7,
            description = "The people gather after the day's work to sing what they remember. A shared pot, a few voices, then everyone.",
        },
        new CultureActivitySpec
        {
            id = "remembrance", name = "Rite of Remembrance", family = "Esoteric", cost = { Res("Faith", 5f) }, unity = 9f, morale = 2, moraleSevenths = 2, joy = 0.1f, cooldownSevenths = 14,
            technology = "Rites of Harvest",
            description = "The names of those the Hunger took are spoken aloud, one by one, so that no one is ground into the dust unremembered.",
        },
        new CultureActivitySpec
        {
            id = "golden-fruit", name = "Rite of the First Golden Fruit", family = "Agromagical", cost = { Res("Dried Auric Peaches", 6f) }, unity = 7f, morale = 4, moraleSevenths = 2, joy = 0.2f,
            cooldownSevenths = 63,
            description = "The first golden fruit of the year is named, offered and shared: a tiny festival (The Inescapable Hunger). Once an Echo.",
        },
        new CultureActivitySpec
        {
            id = "feast-of-abundance", name = "Feast of Abundance", family = "Indulgent", civic = "Feast of Abundance", orLeaning = 0.15f, foodValue = 20f, unity = 16f, morale = 6, moraleSevenths = 3, joy = 0.35f,
            cooldownSevenths = 21,
            description = "Surplus goods and Auric peaches are thrown from the towers into the squares; wealth held past the feast is hoarding (Civic.md).",
        },
    };

    // ----- Cultural parties and festivals -----
    [Tooltip("Researched before cultural parties can set out (it also needs the culture founded).")]
    public string festivalTechnology = "The Rekindling";
    [Tooltip("Food value a festival's feast draws from the stores, plus this much per point of the settlement's development.")]
    public float festivalFoodValue = 8f;
    public float festivalFoodPerDevelopment = 0.1f;
    [Tooltip("Unity a festival gives, plus this much per legend in the party.")]
    public float festivalUnity = 10f;
    public float festivalUnityPerLegend = 3f;
    public int festivalMorale = 5;
    public int festivalMoraleSevenths = 3;
    public float festivalJoy = 0.35f;
    [Tooltip("Strain eased from the settlement's Composure.")]
    public float festivalRelief = 15f;
    [Tooltip("Culture presence added to held cells within festivalRadius cells.")]
    public float festivalPresence = 0.15f;
    public int festivalRadius = 2;
    [Tooltip("Sevenths before the same settlement can hold another festival (a Phase).")]
    public int festivalCooldownSevenths = 21;
    [Tooltip("Fragments of Meaning each legend of the party earns (\"acts of purpose and unity\").")]
    public int festivalFragments = 2;

    // ----- Holidays -----
    [Tooltip("Morale above its balance the people need before they will set a day apart.")]
    public int holidayMoraleMargin = 20;
    [Tooltip("Unity spent to establish the first holiday, and how much more each one after it costs.")]
    public float holidayUnityCost = 40f;
    public float holidayUnityCostStep = 20f;
    public int maxHolidays = 7;
    [Tooltip("Sevenths between establishing two holidays (an Echo).")]
    public int holidayCooldownSevenths = 63;
    [Tooltip("A festival must have been held before the people ask for a holiday.")]
    public bool holidayNeedsFestival = true;
    public float holidayUnity = 8f;
    public int holidayMorale = 4;
    public int holidayMoraleSevenths = 2;
    public float holidayJoy = 0.3f;
    [Tooltip("Food value the holiday's table draws per hundred citizens (at least holidayFeastMinimum); half the joy when the stores cannot.")]
    public float holidayFeastPerHundred = 2f;
    public float holidayFeastMinimum = 3f;
    [Tooltip("Max morale each holiday kept on the calendar adds for good.")]
    public float holidayMaxMorale = 2f;

    // ----- Landmarks -----
    public List<LandmarkSpec> landmarks = new List<LandmarkSpec>
    {
        new LandmarkSpec { id = "shrine", name = "Shrine", family = "Esoteric", tier = 1, haven = true, minDevelopment = 10f, cost = { Res("Duskstone", 10f), Res("Unity", 10f) },
            unityPerSeventh = 0.5f, faithPerSeventh = 0.5f, living = 0.05f, description = "A place set apart, where a candle is kept burning." },
        new LandmarkSpec { id = "temple", name = "Temple", family = "Esoteric", tier = 2, haven = true, minDevelopment = 35f, cost = { Res("Duskstone", 30f), Res("Elderwood", 10f), Res("Unity", 30f) },
            unityPerSeventh = 1f, faithPerSeventh = 1.5f, living = 0.1f, description = "Walls, a roof and rites kept at set hours: faith with a door." },
        new LandmarkSpec { id = "cathedral", name = "Cathedral", family = "Esoteric", tier = 3, haven = true, minDevelopment = 70f, cost = { Res("Duskstone", 80f), Res("Sky Glass", 10f), Res("Unity", 80f) },
            unityPerSeventh = 2f, faithPerSeventh = 3f, living = 0.2f, description = "A cathedral of sky glass, raised over generations: the whole people's work." },
        new LandmarkSpec { id = "song-hall", name = "Hall of Song", family = "Weaver", tier = 1, minDevelopment = 15f, cost = { Res("Elderwood", 20f), Res("Unity", 15f) },
            unityPerSeventh = 1.2f, living = 0.08f, description = "Where the orchestra practises (the Waltz: many voices over one soloist)." },
        new LandmarkSpec { id = "amphitheatre", name = "Amphitheatre", family = "Weaver", tier = 2, minDevelopment = 50f, cost = { Res("Duskstone", 40f), Res("Unity", 40f) },
            unityPerSeventh = 2.5f, living = 0.15f, description = "Stone tiers for the whole district to hear the ballads." },
        new LandmarkSpec { id = "feast-hall", name = "Feast Hall", family = "Indulgent", tier = 1, minDevelopment = 15f, cost = { Res("Elderwood", 20f), Res("Unity", 10f) },
            unityPerSeventh = 0.6f, living = 0.12f, description = "Long tables, and room at them for anyone." },
        new LandmarkSpec { id = "pleasure-garden", name = "Pleasure Garden", family = "Indulgent", tier = 2, minDevelopment = 50f, cost = { Res("Silverreed", 10f), Res("Duskstone", 20f), Res("Unity", 30f) },
            unityPerSeventh = 1f, living = 0.2f, description = "Paths, lanterns and blooms kept only because they are lovely." },
    };

    // ----- The kitchen -----
    public List<RecipeSpec> recipes = new List<RecipeSpec>
    {
        new RecipeSpec { id = "peach-soup", dish = "Peach Soup", inputs = { Res("Dried Auric Peaches", 2f), Res("Earth-Beans", 2f) }, output = 2f,
            description = "The field kitchen's first dish: dried peaches softened with beans." },
        new RecipeSpec { id = "pit-flatbread", dish = "Pit-Oil Flatbread", inputs = { Res("Peach Pits", 4f), Res("Deep-Rooted Grain", 1f) }, output = 2f,
            description = "Oil pressed from the pits the peaches leave, worked into grain: nothing wasted." },
        new RecipeSpec { id = "riverfish-rice", dish = "Riverfish Rice", inputs = { Res("River Fish", 1f), Res("Highland Rice", 1f) }, output = 2f,
            description = "Fish steamed over highland rice." },
        new RecipeSpec { id = "hunters-stew", dish = "Hunter's Stew", inputs = { Res("Game Meat", 1f), Res("Bitter Roots", 2f), Res("Glimmerfern", 0.5f) }, output = 3f,
            technology = "Efficient Rations", description = "Game stretched with bitter roots and lifted with a pinch of Glimmerfern: it keeps far longer than meat." },
        new RecipeSpec { id = "honeyed-porridge", dish = "Honeyed Grain Porridge", inputs = { Res("Deep-Rooted Grain", 2f), Res("Wild Honey", 1f) }, output = 3f,
            technology = "Rites of Harvest", description = "Grain sweetened with wild honey: the first sweet many children taste." },
        new RecipeSpec { id = "ash-loaf", dish = "Ash-Loaf", inputs = { Res("Ash-Bread", 4f), Res("Earth-Beans", 1f) }, output = 2f, unity = 1f,
            technology = "Echoes of Hunger", description = "Peachless bread baked for those who starved in the Hunger: food as memorial and refusal (The Inescapable Hunger)." },
        new RecipeSpec { id = "feast-roast", dish = "Behemoth Feast Roast", inputs = { Res("Behemoth Meat", 1f), Res("Bitter Roots", 2f), Res("Glimmerfern", 1f) }, output = 4f,
            technology = "The Rekindling", description = "A behemoth roasted whole for a settlement's table." },
        new RecipeSpec { id = "saffron-pilaf", dish = "Saffron Riverfish Pilaf", inputs = { Res("River Fish", 2f), Res("Highland Rice", 2f), Res("Auric Saffron", 0.5f), Res("Silver Salt", 0.25f) }, output = 3f,
            technology = "Rites of Harvest", description = "River fish and fragrant rice finished with saffron from flourishing land and salt from coherent shores." },
        new RecipeSpec { id = "peach-tart", dish = "Honeyed Peach Tart", inputs = { Res("Dried Auric Peaches", 2f), Res("Deep-Rooted Grain", 1f), Res("Wild Honey", 1f), Res("Glimmerfern", 0.25f) }, output = 3f,
            technology = "Efficient Rations", description = "A layered peach tart with honey and a bright Glimmerfern finish, made to share on days of plenty." },

        // The cellar: beverages fermented from grain, honey, rice and peaches, then distilled from what was fermented.
        // Never from an Eleos Bloom (a bloom steeped or distilled is a tea). Names and numbers are proposals.
        new RecipeSpec { id = "rootgrain-ale", dish = "Rootgrain Ale", method = KitchenMethod.Ferment, inputs = { Res("Deep-Rooted Grain", 2f) }, output = 3f,
            description = "Deep-rooted grain malted, mashed and left to work: the everyday drink of those who dig and haul. It sours within the Phase." },
        new RecipeSpec { id = "honey-mead", dish = "Wild Honey Mead", method = KitchenMethod.Ferment, inputs = { Res("Wild Honey", 2f) }, output = 3f,
            technology = "Rites of Harvest", description = "Wild honey thinned with water and left to sing in the crock: the oldest drink of harvest nights." },
        new RecipeSpec { id = "rice-wine", dish = "Highland Rice Wine", method = KitchenMethod.Ferment, inputs = { Res("Highland Rice", 2f) }, output = 2f,
            technology = "Resource Preservation", description = "Highland rice steamed and fermented into a clear, keeping wine." },
        new RecipeSpec { id = "peach-wine", dish = "Auric Peach Wine", method = KitchenMethod.Ferment, inputs = { Res("Dried Auric Peaches", 3f) }, output = 2f,
            technology = "Resource Preservation", description = "Dried Auric peaches pressed and fermented into a golden wine. Sweet on the tongue; still the peach." },
        new RecipeSpec { id = "bitterroot-spirit", dish = "Bitterroot Spirit", method = KitchenMethod.Distill, inputs = { Res("Rootgrain Ale", 3f), Res("Bitter Roots", 1f) }, output = 1f,
            technology = "Chants of Ash", description = "Ale run through the fire's still over bitter roots: a harsh, clear spirit that never spoils." },
        new RecipeSpec { id = "peach-brandy", dish = "Auric Peach Brandy", method = KitchenMethod.Distill, inputs = { Res("Auric Peach Wine", 3f), Res("Peach Pits", 1f) }, output = 1f,
            technology = "Chants of Ash", description = "Peach wine distilled with a handful of pits for their almond bite, then laid down: it only improves." },
    };
    [Tooltip("Civics of the kitchen (Civic.md): while one is in force, every batch makes this share more.")]
    public List<string> kitchenCivics = new List<string> { "Culinary Alchemists", "Cooking Guilds" };
    public float kitchenCivicBonus = 0.25f;
    [Tooltip("Most batches of a recipe a standing order cooks each Seventh.")]
    public int maxStandingBatches = 5;

    // ----- Invented dishes and drinks (CultureInvention) -----
    [Tooltip("Most dishes and drinks the people can invent and name.")]
    public int maxInvented = 12;
    [Tooltip("Most different ingredients in one invented dish or drink.")]
    public int maxInventedIngredients = 4;
    [Tooltip("Share of the batch (0-1) an ingredient must make up to lend an invention its luxury category (honey in a Sweet).")]
    public float inventedLuxuryShare = 0.3f;
    [Tooltip("Joy (0-1) the people take in a dish or drink of their own, when it is first made.")]
    public float inventionJoy = 0.05f;

    // ----- Kitchen trials (KitchenTrials: no one knows what a mix makes until a batch is tried) -----
    [Tooltip("Batches the kitchen may try in one Seventh; each uses up what goes into it.")]
    public int trialsPerSeventh = 3;
    [Tooltip("Recipes no one has written down, found only by trying batches: each trial says how near it came, and coming nearer brings the next hint to light.")]
    public List<HiddenRecipeSpec> hiddenRecipes = KitchenTrials.Defaults();

    // ----- Luxuries and happiness -----
    public List<LuxurySpec> luxuries = new List<LuxurySpec>
    {
        new LuxurySpec { category = "Sweets", resources = { "Wild Honey", "Honeyed Grain Porridge", "Honeyed Peach Tart" }, perHundred = 1f },
        new LuxurySpec { category = "Fine Dishes", resources = { "Saffron Riverfish Pilaf", "Behemoth Feast Roast", "Hunter's Stew", "Riverfish Rice", "Peach Soup" }, perHundred = 1.5f },
        new LuxurySpec { category = "Teas", resources = { "Eleos Tea", "Candlevein Grief Tea", "Lullroot Tea", "Hearthleaf Tea" }, perHundred = 1f },
        new LuxurySpec { category = "Wines & Spirits", resources = { "Rootgrain Ale", "Wild Honey Mead", "Highland Rice Wine", "Auric Peach Wine", "Bitterroot Spirit", "Auric Peach Brandy" }, perHundred = 1f },
        new LuxurySpec { category = "Spices", resources = { "Auric Saffron", "Silver Salt", "Glimmerfern" }, perHundred = 0.3f },
        new LuxurySpec { category = "Fine Cloth", resources = { "Lumenwool", "Silverreed" }, perHundred = 0.5f },
        new LuxurySpec { category = "Ornaments", resources = { "Sky Glass" }, perHundred = 0.3f },
        new LuxurySpec { category = "Living Gardens", perHundred = 1f },
    };
    public List<CulturalGoodSpec> goods = new List<CulturalGoodSpec>
    {
        new CulturalGoodSpec { resource = "Sky Glass", amenity = true, faithPerUnit = 0.4f },
        new CulturalGoodSpec { resource = "Eleos Tea", faithPerUnit = 0.25f },
        new CulturalGoodSpec { resource = "Candlevein Grief Tea", faithPerUnit = 0.4f },
        new CulturalGoodSpec { resource = "Lullroot Tea", faithPerUnit = 0.2f },
        new CulturalGoodSpec { resource = "Hearthleaf Tea" },
    };
    public List<CulturalSiteSpec> gardens = new List<CulturalSiteSpec>
    {
        new CulturalSiteSpec { site = "vow-orchids", faithPerUnit = 0.4f },
        new CulturalSiteSpec { site = "candlevein-bloom", faithPerUnit = 0.3f },
        new CulturalSiteSpec { site = "xochi-singers", faithPerUnit = 0.2f },
        new CulturalSiteSpec { site = "memory-marigolds", faithPerUnit = 0.2f },
        new CulturalSiteSpec { site = "lullroots", faithPerUnit = 0.15f },
    };
    public CulturalGoodSpec Good(string resource) => goods?.FirstOrDefault(g => g != null && string.Equals(g.resource, resource, StringComparison.OrdinalIgnoreCase));
    [Tooltip("Citizens from which living begins to weigh in happiness beside surviving (the frontier ends near 150).")]
    public int livingFrom = 150;
    [Tooltip("Citizens at which living weighs as much as surviving.")]
    public int livingFull = 1500;
    [Tooltip("One more luxury category is wanted for each this many citizens past livingFrom.")]
    public int peoplePerLuxury = 250;
    [Tooltip("Share of joy that fades each Seventh.")]
    public float joyFade = 0.15f;
    [Tooltip("Stored food value per citizen at which the stores feel secure.")]
    public float securePerCitizen = 0.25f;
    [Tooltip("Morale per 10 points of happiness above or below 50, capped at happinessMoraleCap.")]
    public float moralePer10Happiness = 2f;
    public int happinessMoraleCap = 10;

    public CultureActivitySpec Activity(string id) => activities.FirstOrDefault(a => a != null && string.Equals(a.id, id, StringComparison.OrdinalIgnoreCase));
    public RecipeSpec Recipe(string id) => recipes.FirstOrDefault(r => r != null && string.Equals(r.id, id, StringComparison.OrdinalIgnoreCase));
    public LandmarkSpec Landmark(string id) => landmarks.FirstOrDefault(l => l != null && string.Equals(l.id, id, StringComparison.OrdinalIgnoreCase));
    /// <summary>A dish the kitchen cooks (not a drink from the cellar).</summary>
    public bool IsDish(string resource) => recipes.Any(r => r != null && !r.InCellar && string.Equals(r.dish, resource, StringComparison.OrdinalIgnoreCase));

    /// <summary>A beverage the cellar brews or distils.</summary>
    public bool IsDrink(string resource) => recipes.Any(r => r != null && r.InCellar && string.Equals(r.dish, resource, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// A copy of these numbers with the people's invented dishes and drinks among the recipes, in the luxury categories
    /// they satisfy and among the goods that give Faith. The copy's lists are its own: the asset is never changed.
    /// </summary>
    public CultureLifeTuning WithInventions(IEnumerable<InventedRecipe> invented)
    {
        var copy = (CultureLifeTuning)MemberwiseClone();
        var list = (invented ?? Enumerable.Empty<InventedRecipe>()).Where(r => r != null && !string.IsNullOrEmpty(r.name) && !string.IsNullOrEmpty(r.id)).ToList();
        copy.recipes = (recipes ?? new List<RecipeSpec>()).Concat(list.Select(r => r.ToRecipe())).ToList();
        copy.luxuries = (luxuries ?? new List<LuxurySpec>()).Where(l => l != null).Select(l => new LuxurySpec
        {
            category = l.category, perHundred = l.perHundred,
            resources = (l.resources ?? new List<string>()).Concat(list.Where(r => r.luxuries != null && r.luxuries.Contains(l.category, StringComparer.OrdinalIgnoreCase)).Select(r => r.name)).ToList(),
        }).ToList();
        copy.goods = (goods ?? new List<CulturalGoodSpec>()).Concat(list.Where(r => r.faithPerUnit > 0f).Select(r => new CulturalGoodSpec { resource = r.name, faithPerUnit = r.faithPerUnit })).ToList();
        return copy;
    }

    public static ResourceAmount Res(string resource, float amount) => new ResourceAmount { resource = resource, amount = amount };
}
