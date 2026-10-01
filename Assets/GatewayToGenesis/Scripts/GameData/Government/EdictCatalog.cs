using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// The stances and edicts the government can decree (roadmap: the vault's Government Logic names Edicts beside Civics
/// and council seats, but has no Edicts note yet: every title, effect and number here is a proposal, listed in Canon
/// Gaps). Stances answer the standing questions of a realm (who enters, how it grows, how far it claims, and the two
/// axes of the Pillars); edicts are decrees sealed for a while.
///
/// The Pillar stances ask the vault's own questions (Pillars: "Is power found in us ... or in nature?" and "Should
/// this power belong in the virtuous solo of a few or in the resonant orchestra of the many?"), so the laws a nation
/// keeps pull its political compass, and through it its government type.
/// </summary>
public static class EdictCatalog
{
    /// <summary>Pillar points a leaning stance gives its pillar.</summary>
    public const float PillarLean = 3f;

    private static readonly string[] None = Array.Empty<string>();
    private static string[] Areas(params string[] areas) => areas;

    private static GameEffect Pillar(string pillar, float value) => new GameEffect(GameEffectType.PillarBonus, value, ModifierType.Add, pillar);
    private static GameEffect Substat(string substat, float value) => new GameEffect(GameEffectType.SubstatBonus, value, ModifierType.Add, substat);
    private static GameEffect Derived(string stat, float percent) => new GameEffect(GameEffectType.DerivedStatBonus, percent, ModifierType.Percentage, stat);
    private static GameEffect Output(string resource, float percent) => new GameEffect(GameEffectType.ResourceModifier, percent, ModifierType.Percentage, resource);
    private static GameEffect Morale(float value) => new GameEffect(GameEffectType.MoraleModifier, value, ModifierType.Add);
    private static GameEffect Efficiency(float percent) => new GameEffect(GameEffectType.ProductionModifier, percent, ModifierType.Percentage, null, ScopeType.Global);

    // ===== STANCES =====

    public static readonly StanceDefinition Roofless = new StanceDefinition("roofless", "The Roofless", "Who may enter the walls without a home waiting?", "open_gates",
        new StanceOption("homes_first", "Homes First", "Only those with a roof waiting are let in; the rest wait at the gates until a home frees up.",
            "regalia", Areas("security", "governance"), Areas("welfare"), Pillar("regalia", PillarLean), Substat("authority", 1f))
        { levers = new EdictLevers { births = 1f, caravans = 1f, arrivalRations = 1f, vagrantsHoused = 1f, turnAwayRoofless = true } },
        new StanceOption("open_gates", "Open Gates", "Anyone fed at the gates is let in, housed or not, once Shared Embers lets the roofless in: they wait in the streets for a home.",
            null, Areas("welfare"), Areas("security")),
        new StanceOption("almshouses", "Almshouse Charter", "The roofless are let in and housed first whenever homes free up; the granaries feed the almshouses.",
            "waltz", Areas("welfare", "faith"), Areas("economy"), Pillar("waltz", PillarLean), Substat("euphony", 1f), Output("Food", -5f))
        { requiresTechnology = "Shared Embers", levers = new EdictLevers { births = 1f, caravans = 1f, arrivalRations = 1f, vagrantsHoused = 2f } });

    public static readonly StanceDefinition Strangers = new StanceDefinition("strangers", "Strangers at the Gates", "How are those who come from beyond our lands received?", "measured_welcome",
        new StanceOption("welcome", "Welcome the Wanderers", "Word goes out that the gates are open: caravans come half again as often, and those who share the road need fewer rations to be let in.",
            "waltz", Areas("diplomacy", "welfare", "culture"), Areas("security"), Pillar("waltz", PillarLean), Substat("symphony", 1f), Substat("secrecy", -1f))
        { levers = new EdictLevers { births = 1f, caravans = 1.5f, arrivalRations = 0.75f, vagrantsHoused = 1f } },
        new StanceOption("measured_welcome", "Measured Welcome", "Strangers are let in as any survivor is: fed at the gates, housed as homes allow.",
            null, None, None),
        new StanceOption("sealed", "Sealed Gates", "No word goes out and no caravan is drawn. Bands your own expeditions find still come home.",
            "regalia", Areas("security", "defense"), Areas("diplomacy", "economy"), Pillar("regalia", PillarLean), Substat("secrecy", 1f), Substat("authority", 1f), Substat("symphony", -1f))
        { levers = new EdictLevers { births = 1f, caravans = 0f, arrivalRations = 1f, vagrantsHoused = 1f } });

    public static readonly StanceDefinition Cradle = new StanceDefinition("cradle", "Hearth and Cradle", "Should the people grow as fast as they can?", "natures_pace",
        new StanceOption("many_hearths", "Many Hearths", "Families are honored and fed first: a quarter more births, and fewer hands at study.",
            "waltz", Areas("welfare"), Areas("lore"), Pillar("waltz", PillarLean), Substat("innovation", -1f))
        { levers = new EdictLevers { births = 1.25f, caravans = 1f, arrivalRations = 1f, vagrantsHoused = 1f } },
        new StanceOption("natures_pace", "Nature's Pace", "The people grow as they will.",
            null, None, None),
        new StanceOption("measured_cradle", "Measured Cradle", "Fewer mouths while the stores are thin (the lesson of The Inescapable Hunger): a quarter fewer births, more hands at study.",
            "aureus", Areas("lore", "innovation"), Areas("welfare"), Pillar("aureus", PillarLean), Substat("innovation", 1f))
        { levers = new EdictLevers { births = 0.75f, caravans = 1f, arrivalRations = 1f, vagrantsHoused = 1f } });

    public static readonly StanceDefinition Borders = new StanceDefinition("borders", "The Borders", "How far should our people settle beyond the walls? (the Realm's border policy)", "measured",
        new StanceOption("expand", "Claim the Horizon", "Society adopts land until the administration is full, even when more land lowers efficiency everywhere.",
            "regalia", Areas("exploration", "defense"), Areas("governance"), Pillar("regalia", PillarLean), Substat("ambition", 1f))
        { levers = new EdictLevers { births = 1f, caravans = 1f, arrivalRations = 1f, vagrantsHoused = 1f, borders = BorderPolicy.Expand } },
        new StanceOption("measured", "Measured Borders", "Society adopts land while each new cell still raises the realm's total output, then stops.",
            null, None, None)
        { levers = new EdictLevers { births = 1f, caravans = 1f, arrivalRations = 1f, vagrantsHoused = 1f, borders = BorderPolicy.Measured } },
        new StanceOption("hold", "Hold the Borders", "Society adopts no new land by itself; only claims extend the borders.",
            "waltz", Areas("governance", "welfare"), Areas("exploration"), Pillar("waltz", PillarLean), Substat("euphony", 1f))
        { levers = new EdictLevers { births = 1f, caravans = 1f, arrivalRations = 1f, vagrantsHoused = 1f, borders = BorderPolicy.Hold } });

    public static readonly StanceDefinition Weaving = new StanceDefinition("weaving", "Who May Weave", "Should Spellweaving belong to the virtuous solo of a few, or to the orchestra of the many?", "unspoken",
        new StanceOption("guarded_solo", "The Guarded Solo", "Spellweaving is taught to the few who will lead: fewer weavers, each far stronger.",
            "regalia", Areas("justice", "security"), Areas("culture"), Pillar("regalia", PillarLean), Substat("authority", 1f)),
        new StanceOption("unspoken", "Left Unspoken", "The realm has not yet said who may weave.",
            null, None, None),
        new StanceOption("open_orchestra", "The Open Orchestra", "Anyone who can hear may learn to weave: many voices, resonating together.",
            "waltz", Areas("culture", "welfare"), Areas("justice"), Pillar("waltz", PillarLean), Substat("symphony", 1f)));

    public static readonly StanceDefinition Hearing = new StanceDefinition("hearing", "Where Power Is Heard", "Is power found in us, in the spirit of Humanity, or in nature and the unknown beyond?", "unspoken",
        new StanceOption("inward", "Hear Inward", "Power is found in Humanity and its will to master chaos.",
            "aureus", Areas("lore", "innovation"), Areas("mysticism"), Pillar("aureus", PillarLean), Substat("innovation", 1f)),
        new StanceOption("unspoken", "Left Unspoken", "The realm has not yet said where power is heard.",
            null, None, None),
        new StanceOption("outward", "Hear Outward", "Power is found in nature and in the beauty of the beyond.",
            "chorus", Areas("mysticism", "faith"), Areas("lore"), Pillar("chorus", PillarLean), Substat("arcane", 1f)));

    public static readonly IReadOnlyList<StanceDefinition> Stances = new[] { Roofless, Strangers, Cradle, Borders, Weaving, Hearing };

    // ===== EDICTS =====

    public static readonly IReadOnlyList<EdictDefinition> Edicts = new[]
    {
        new EdictDefinition("open_granaries", "Open the Granaries", "The stores are opened to all for a few Sevenths of plenty.", "welfare", "Food", 40f, 7, 21, Morale(8f)),
        new EdictDefinition("call_to_toil", "Call to Toil", "Every able hand to the works, rest or no rest.", "industry", null, 0f, 21, 21, Efficiency(15f), Morale(-6f)) { dissonant = true },
        new EdictDefinition("scholars_vigil", "Scholars' Vigil", "Lamps burn late in every study; the scholars are fed from the common stores.", "lore", "Food", 30f, 21, 21, Output("Research", 15f)),
        new EdictDefinition("wardens_muster", "Wardens' Muster", "The walls are watched and the roads patrolled.", "defense", "Food", 25f, 21, 21, Derived("savingRollChance", 10f)),
        new EdictDefinition("hunters_charter", "Hunters' Charter", "Hunting parties are chartered to range beyond the fields.", "exploration", "Food", 15f, 21, 21, Output("Game Meat", 20f), Output("Hides", 20f)),
        new EdictDefinition("rite_of_listening", "Rite of Listening", "The people gather each dusk to listen for what the world is singing.", "faith", "Food", 30f, 21, 21, Output("Faith", 20f), Substat("piety", 1f)),
        new EdictDefinition("mandate_of_the_seats", "Mandate of the Seats", "The council's word is carried to every hearth with the Head of State's seal.", "governance", "Research", 50f, 21, 42, Derived("legendEffectiveness", 10f)),
    };

    public static StanceDefinition Stance(string id) => Stances.FirstOrDefault(s => string.Equals(s.id, id, StringComparison.OrdinalIgnoreCase));
    public static EdictDefinition Edict(string id) => Edicts.FirstOrDefault(e => string.Equals(e.id, id, StringComparison.OrdinalIgnoreCase));

    /// <summary>The option of a stance that sets <paramref name="policy"/> (The Borders).</summary>
    public static StanceOption BorderOption(BorderPolicy policy) => Borders.options.FirstOrDefault(o => o.levers.borders == policy);
}
