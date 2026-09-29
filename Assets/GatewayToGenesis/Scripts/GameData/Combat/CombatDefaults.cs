using System.Collections.Generic;

/// <summary>
/// The Symphony of War's content when World.asset lists none (<see cref="CombatSettings"/>). Every section, template,
/// ground and number is a proposal. The names the vault gives (Orchestral Formation, Battle Conductor, Dance, Strand
/// healing, Music as Catalyst, Pathfinder) are kept; the sections themselves are not in the vault.
/// </summary>
public static class CombatDefaults
{
    private static GroundModifier G(BattleGround ground, float attack = 1f, float defense = 1f) =>
        new GroundModifier { ground = ground, attack = attack, defense = defense };

    public static readonly IReadOnlyList<CombatSectionSpec> Sections = new List<CombatSectionSpec>
    {
        // The front lane.
        // The basic infantry, conscripted from the population (the owner's card: Melee Unit, 1 Attack, 2 Defense).
        new CombatSectionSpec
        {
            id = "grave-warden", name = "Grave Warden", row = FormationRow.Front, kind = SectionKind.Infantry, minAge = 0, technology = "The Rekindling",
            description = "Melee unit: 1 Attack, 2 Defense. Survivors who keep watch over the dead, and hold the line the way they hold vigil.",
            integrity = 110f, composure = 40f, attack = 10f, defense = 22f, breakthrough = 14f, armor = 6f, piercing = 5f, speed = 3f,
            conscripted = true, people = 2, cost = { new ResourceAmount { resource = "Elderwood", amount = 125f } },
            category = "Golden Hymn Citadel", quote = "In the shadow of the fallen, we stand.",
        },
        new CombatSectionSpec
        {
            id = "shieldwall", name = "Shieldwall", row = FormationRow.Front, kind = SectionKind.Infantry, minAge = 0,
            description = "Locked shields that hold ground. Slow to strike, slow to break; a wall cannot keep its shape among trees.",
            integrity = 110f, composure = 38f, attack = 8f, defense = 28f, breakthrough = 14f, armor = 12f, piercing = 4f, speed = 2f,
            grounds = new List<GroundModifier> { G(BattleGround.Open, defense: 1.1f), G(BattleGround.Forest, defense: 0.85f) },
        },
        new CombatSectionSpec
        {
            id = "warband", name = "Ash Warband", row = FormationRow.Front, kind = SectionKind.Shock, minAge = 0,
            description = "Fast raiders who win in the first rush or not at all. They run down whoever breaks.",
            integrity = 90f, composure = 32f, attack = 18f, defense = 12f, breakthrough = 16f, armor = 4f, piercing = 8f, speed = 5f, charge = 1.5f,
            grounds = new List<GroundModifier> { G(BattleGround.Forest, 0.85f), G(BattleGround.Marsh, 0.8f) },
        },
        new CombatSectionSpec
        {
            id = "vanguard", name = "Vanguard Blades", row = FormationRow.Front, kind = SectionKind.Infantry, minAge = 1,
            description = "Drilled blades in worked hide and bronze: the first true soldiers of the Ages of Ecological Rebirth.",
            integrity = 110f, composure = 45f, attack = 20f, defense = 22f, breakthrough = 20f, armor = 18f, piercing = 18f, speed = 3f,
        },
        new CombatSectionSpec
        {
            id = "blade-dancers", name = "Blade Dancers", row = FormationRow.Front, kind = SectionKind.Dancer, minAge = 2,
            description = "The melee archetype of Spellweaving: battle movements woven into a song. Without musicians they must carry the song in their own voice, and falter.",
            integrity = 80f, composure = 45f, attack = 16f, defense = 14f, breakthrough = 16f, armor = 4f, piercing = 10f, speed = 5f,
            potency = 10f, needsAccompaniment = true,
            grounds = new List<GroundModifier> { G(BattleGround.Forest, 1.1f) },
        },

        // The back lane.
        // The basic archers, conscripted from the population (the owner's card: Ranged Unit, 2 Attack, 1 Defense).
        new CombatSectionSpec
        {
            id = "wasteland-archer", name = "Wasteland Archer", row = FormationRow.Back, kind = SectionKind.Ranged, minAge = 0, technology = "The Rekindling",
            description = "Ranged unit: 2 Attack, 1 Defense. Hunters of the ash plains who shoot from behind the line and fold when the line reaches them.",
            integrity = 70f, composure = 26f, attack = 18f, defense = 9f, breakthrough = 5f, piercing = 9f, speed = 4f,
            grounds = new List<GroundModifier> { G(BattleGround.Rough, 1.05f), G(BattleGround.Hills, 1.1f) },
            conscripted = true, people = 1, cost = { new ResourceAmount { resource = "Duskstone", amount = 50f } },
            category = "Golden Hymn Citadel", quote = "From the ashes of ruin.",
        },
        new CombatSectionSpec
        {
            id = "longbows", name = "Longbows", row = FormationRow.Back, kind = SectionKind.Ranged, minAge = 1,
            description = "Archers who pierce hide and bronze, best from high ground.",
            integrity = 75f, composure = 28f, attack = 16f, defense = 6f, breakthrough = 4f, piercing = 14f, speed = 3f,
            grounds = new List<GroundModifier> { G(BattleGround.Hills, 1.1f) },
        },
        new CombatSectionSpec
        {
            id = "weaver-circle", name = "Spellweaver Circle", row = FormationRow.Back, kind = SectionKind.Spellweaver, minAge = 1,
            description = "Awakened Spellweavers casting in one binding. Their primary binding is also their weakness; Signal Loss dulls their spells across the field unless a Resonance, Flux or Void Minor Note carries them.",
            integrity = 60f, composure = 50f, attack = 2f, defense = 4f, breakthrough = 2f, speed = 3f, potency = 16f, ward = 0.1f,
        },

        // Support.
        new CombatSectionSpec
        {
            id = "musicians", name = "War Musicians", row = FormationRow.Support, kind = SectionKind.Support, minAge = 0,
            description = "Music as Catalyst: drums and voices that lift every spell and steady the line's heartbeat.",
            integrity = 30f, composure = 30f, defense = 2f, breakthrough = 2f, speed = 3f, catalyst = 0.15f, rally = 1f,
        },
        new CombatSectionSpec
        {
            id = "menders", name = "Strand Menders", row = FormationRow.Support, kind = SectionKind.Support, minAge = 1,
            description = "Strand healing reminds a body how it was before the wound: some of the fallen get up again.",
            integrity = 30f, composure = 30f, defense = 2f, breakthrough = 2f, speed = 3f, mending = 0.08f, wounded = 0.2f,
        },
        new CombatSectionSpec
        {
            id = "crystalwrights", name = "Crystalwrights", row = FormationRow.Support, kind = SectionKind.Support, minAge = 2,
            description = "Crystal shelters and palisades raised on demand: a defending formation digs in deeper.",
            integrity = 30f, composure = 30f, defense = 2f, breakthrough = 2f, speed = 2f, entrench = 2f,
        },
        new CombatSectionSpec
        {
            id = "pathfinders", name = "Pathfinders", row = FormationRow.Support, kind = SectionKind.Support, minAge = 0,
            technology = "Pathfinder Training",
            description = "Scouts who read the ground: they win the first measure and see through ambushes.",
            integrity = 30f, composure = 30f, defense = 2f, breakthrough = 2f, speed = 5f, recon = 2f,
        },
    };

    private static FormationSlot S(string section, SpellBinding binding = SpellBinding.Unattuned, params SpellBinding[] harmony) =>
        new FormationSlot { section = section, binding = binding, harmony = new List<SpellBinding>(harmony) };

    public static readonly IReadOnlyList<FormationTemplate> Templates = new List<FormationTemplate>
    {
        new FormationTemplate
        {
            id = "hearth-guard", name = "Hearth Guard", tempo = SpellTempo.Staccato,
            description = "Age 0: spears and a shieldwall around the hearth, stones from behind, drums to keep their nerve.",
            front = { S("grave-warden"), S("grave-warden"), S("shieldwall") }, back = { S("wasteland-archer") }, support = { S("musicians") },
        },
        new FormationTemplate
        {
            id = "ember-host", name = "Ember Host", tempo = SpellTempo.Staccato,
            description = "Age 0: a raiding host that means to win in the first rush.",
            front = { S("warband"), S("warband"), S("grave-warden") }, back = { S("wasteland-archer"), S("wasteland-archer") }, support = { S("pathfinders") },
        },
        new FormationTemplate
        {
            id = "tidebound-choir", name = "Tidebound Choir", tempo = SpellTempo.Legato,
            description = "Age I: a Flux choir behind a steady line. The Resonance Minor Note carries its song across the field.",
            front = { S("vanguard"), S("shieldwall"), S("shieldwall") },
            back = { S("weaver-circle", SpellBinding.Flux), S("weaver-circle", SpellBinding.Flux, SpellBinding.Resonance), S("longbows") },
            support = { S("musicians"), S("menders") },
        },
        new FormationTemplate
        {
            id = "cinder-vanguard", name = "Cinder Vanguard", tempo = SpellTempo.Staccato,
            description = "Age I: blades and a Cindergale circle that burn through the enemy's first line. Beware Flux.",
            front = { S("vanguard"), S("vanguard"), S("warband") },
            back = { S("weaver-circle", SpellBinding.Cindergale), S("longbows") },
            support = { S("musicians"), S("pathfinders") },
        },
        new FormationTemplate
        {
            id = "prism-bastion", name = "Prism Bastion", tempo = SpellTempo.Ritardando,
            description = "Age II: a Crystal fortress that digs in, with a rare Triad of Crystal, Luminance and Resonance.",
            front = { S("shieldwall"), S("shieldwall"), S("blade-dancers", SpellBinding.Crystal) },
            back = { S("weaver-circle", SpellBinding.Crystal, SpellBinding.Luminance, SpellBinding.Resonance), S("weaver-circle", SpellBinding.Luminance) },
            support = { S("crystalwrights"), S("musicians"), S("menders") },
        },
    };

    private static GroundSpec Gr(BattleGround ground, string name, float width, float defense, float assault, float missiles, bool charges, params string[] terrains) =>
        new GroundSpec { ground = ground, name = name, width = width, defense = defense, assault = assault, missiles = missiles, charges = charges, terrains = new List<string>(terrains) };

    public static readonly IReadOnlyList<GroundSpec> Grounds = new List<GroundSpec>
    {
        Gr(BattleGround.Open, "Open ground", 6f, 1f, 1f, 1f, true, "plains", "fallow-steppe", "expanse-steppe", "wind-plain", "auric-meadow", "violet-glade"),
        Gr(BattleGround.Rough, "Rough ground", 5f, 1.1f, 1f, 1f, true, "scrub", "expanse-scrub", "foothills", "ash-plains"),
        Gr(BattleGround.Forest, "Forest", 4f, 1.15f, 1f, 0.7f, false, "woodland", "taiga-forest", "violet-wood", "auric-copse", "moonlit-grove", "skeletal-orchard"),
        Gr(BattleGround.Marsh, "Marsh", 3f, 1.1f, 0.8f, 0.9f, false, "marsh", "taiga-bog"),
        Gr(BattleGround.Hills, "Hills", 4f, 1.2f, 0.9f, 1.1f, true, "highlands", "wind-mesa"),
        Gr(BattleGround.Mountains, "Mountains", 3f, 1.4f, 0.75f, 1f, false, "peaks"),
        Gr(BattleGround.Ruins, "Ruins", 4f, 1.25f, 1f, 0.8f, false, "ruin-field"),
        Gr(BattleGround.Waste, "Torn ground", 4f, 1.05f, 1f, 1f, true, "rift-scar", "leyline-ravine"),
        Gr(BattleGround.Water, "Water", 2f, 1f, 0.6f, 0.8f, false, "lake", "shallows", "deep-ocean"),
    };

    private static TerrainElement E(string terrain, SpellBinding binding) => new TerrainElement { terrain = terrain, binding = binding };

    /// <summary>Where the land sings one element (proposals read from each terrain's nature: wind, ash, water, stone, memory, moonlight, torn Loom).</summary>
    public static readonly IReadOnlyList<TerrainElement> Elements = new List<TerrainElement>
    {
        E("wind-plain", SpellBinding.Resonance), E("wind-mesa", SpellBinding.Resonance),
        E("ash-plains", SpellBinding.Cindergale),
        E("lake", SpellBinding.Flux), E("shallows", SpellBinding.Flux), E("deep-ocean", SpellBinding.Flux), E("marsh", SpellBinding.Flux), E("taiga-bog", SpellBinding.Flux),
        E("peaks", SpellBinding.Crystal), E("highlands", SpellBinding.Crystal),
        E("ruin-field", SpellBinding.Strand), E("skeletal-orchard", SpellBinding.Strand),
        E("moonlit-grove", SpellBinding.Luminance),
        E("rift-scar", SpellBinding.Void), E("leyline-ravine", SpellBinding.Void),
    };
}
