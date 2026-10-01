using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// A creature group as the Symphony of War sees it, read from its species (<see cref="SpeciesSpec"/>) with no scene
/// state. Size and stance give its steel (AECOR's taxonomy); how it answers a threat gives its nerve; Auric Structure
/// gives its toughness and Pure Light its magic and its fragility to spells (vault: Pure Light.md, "The Tradeoff of
/// Specialization"); its Coherence-Binding organ decides how the magic comes out; its primary binding is its weakness.
/// Every number is a proposal.
/// </summary>
public static class CreatureCombat
{
    // Per individual: strength, attack, defense, armor, piercing, potency.
    private static (float hp, float atk, float def, float armor, float pierce, float magic) Body(CreatureSize size)
    {
        switch (size)
        {
            case CreatureSize.Small: return (1.5f, 0.6f, 0.5f, 0f, 2f, 0.8f);
            case CreatureSize.Large: return (30f, 6f, 4.5f, 14f, 14f, 6f);
            case CreatureSize.Gargantuan: return (150f, 16f, 12f, 30f, 30f, 14f);
            default: return (6f, 2.2f, 1.8f, 4f, 6f, 2.5f);
        }
    }

    private static float Fierceness(CreatureStance stance)
    {
        switch (stance)
        {
            case CreatureStance.Passive: return 0.35f;
            case CreatureStance.Neutral: return 0.7f;
            case CreatureStance.Territorial: return 1.05f;
            case CreatureStance.Apex: return 1.5f;
            default: return 1f;
        }
    }

    private static float Guard(CreatureStance stance)
    {
        switch (stance)
        {
            case CreatureStance.Passive: return 0.6f;
            case CreatureStance.Territorial: return 1.1f;
            case CreatureStance.Apex: return 1.2f;
            default: return 0.9f;
        }
    }

    /// <summary>A section's Composure by how the creature answers a threat.</summary>
    public static float Nerve(ThreatResponse response)
    {
        switch (response)
        {
            case ThreatResponse.FleesOnSight: return 10f;
            case ThreatResponse.FleesWhenThreatened: return 15f;
            case ThreatResponse.Hides: return 12f;
            case ThreatResponse.Unmoved: return 60f;
            case ThreatResponse.AvoidsConflict: return 20f;
            case ThreatResponse.DefendsWhenThreatened: return 30f;
            case ThreatResponse.DefendsTerritory: return 40f;
            case ThreatResponse.Expands: return 35f;
            case ThreatResponse.Unpredictable: return 30f;
            case ThreatResponse.AttacksOnSight: return 40f;
            case ThreatResponse.Hunts: return 35f;
            case ThreatResponse.HuntsLoudly: return 45f;
            case ThreatResponse.Lures: return 40f;
            default: return 20f;
        }
    }

    /// <summary>Nerve by size: a giant shrugs off what scatters a swarm.</summary>
    private static float Bulk(CreatureSize size) => size == CreatureSize.Small ? 0.8f : size == CreatureSize.Large ? 1.3f : size == CreatureSize.Gargantuan ? 2f : 1f;

    /// <summary>When a group of this kind gives up the fight (share of its Composure): the shy leave early, a territory's keepers hold.</summary>
    public static float WithdrawAt(ThreatResponse response)
    {
        switch (response)
        {
            case ThreatResponse.FleesOnSight: return 0.85f;
            case ThreatResponse.FleesWhenThreatened:
            case ThreatResponse.AvoidsConflict: return 0.6f;
            case ThreatResponse.Hides: return 0.7f;
            case ThreatResponse.Unmoved:
            case ThreatResponse.DefendsTerritory: return 0.12f;
            default: return -1f;
        }
    }

    /// <summary>The primary binding a species carries (Unattuned for an ordinary animal).</summary>
    public static SpellBinding PrimaryOf(SpeciesSpec species) => species?.primaryBinding ?? SpellBinding.Unattuned;

    /// <summary>"Cindergale (weak to Flux; resists Crystal); also casts Crystal", or null for an ordinary animal.</summary>
    public static string ElementWords(SpeciesSpec species)
    {
        var primary = PrimaryOf(species);
        if (primary == SpellBinding.Unattuned) return null;
        var more = (species.secondaryBindings ?? new List<SpellBinding>()).Where(b => b != SpellBinding.Unattuned && b != primary).ToList();
        return HarmonicCircle.Describe(primary) + (more.Count > 0 ? "; also casts " + string.Join(", ", more.Select(HarmonicCircle.Name)) : string.Empty);
    }

    /// <summary>
    /// The sections <paramref name="individuals"/> of <paramref name="species"/> fight as: about 100 strength each, at
    /// most <paramref name="maxSections"/> (a huge swarm packs denser sections instead).
    /// </summary>
    public static List<CombatSection> Sections(SpeciesSpec species, int individuals, int maxSections = 8)
    {
        var list = new List<CombatSection>();
        if (species == null || individuals <= 0) return list;
        var body = Body(species.size);
        var profile = CreatureTaxonomy.Profile(species.diet, species.subgroup);
        var stance = profile?.stance ?? CreatureStance.Neutral;
        var response = profile?.response ?? ThreatResponse.DefendsWhenThreatened;
        float pureLight = 1f - species.structure;
        var primary = PrimaryOf(species);

        int perSection = Math.Max(1, (int)Math.Ceiling(100f / body.hp));
        int count = Math.Max(1, Math.Min(maxSections, (int)Math.Ceiling(individuals / (float)perSection)));
        if (individuals <= 10) count = individuals; // Elite encounters retain individual bodies, including small creatures.
        int left = individuals;
        for (int i = 0; i < count; i++)
        {
            int n = i == count - 1 ? left : Math.Min(left, (int)Math.Ceiling(individuals / (float)count));
            left -= n;
            if (n <= 0) break;
            var s = new CombatSection
            {
                name = $"{species.name} ({n})",
                speciesId = species.id,
                count = n,
                kind = SectionKind.Creature,
                row = species.binding == BindingOrgan.Wings || (species.binding == BindingOrgan.None && primary != SpellBinding.Unattuned) ? FormationRow.Back : FormationRow.Front,
                structure = species.structure,
                // Auric Structure is permanence: tougher bodies, harder hides.
                maxIntegrity = n * body.hp * (0.75f + 0.4f * species.structure),
                maxComposure = Nerve(response) * Bulk(species.size),
                attack = n * body.atk * Fierceness(stance),
                defense = n * body.def * Guard(stance),
                breakthrough = n * body.def * Guard(stance) * 0.8f,
                armor = Math.Max(0f, body.armor + 10f * (species.structure - 0.5f)),
                piercing = body.pierce,
                width = species.size == CreatureSize.Gargantuan ? 2f : 1f,
                speed = species.size == CreatureSize.Gargantuan ? 3f : species.size == CreatureSize.Large ? 4f : 5f,
                charge = species.subgroup == CreatureSubgroup.Wrathful ? 1.4f : species.subgroup == CreatureSubgroup.Marauder ? 1.3f : 1f,
                primary = primary,
                secondary = (species.secondaryBindings ?? new List<SpellBinding>()).Where(b => b != SpellBinding.Unattuned && b != primary).Distinct().Take(2).ToList(),
                organ = species.binding,
                niche = species.niche,
                // Only a being with an interface to the Loom casts; a mundane beast fights with teeth alone.
                potency = primary == SpellBinding.Unattuned ? 0f : n * body.magic * pureLight,
            };
            if (response == ThreatResponse.FleesOnSight || response == ThreatResponse.FleesWhenThreatened) s.speed += 2f;
            switch (species.binding)
            {
                case BindingOrgan.Hide: s.ward = 0.15f; s.armor += 8f; break;            // armored CBT fields deflect both
                case BindingOrgan.Voice: s.potency *= 1.2f; s.dread = 1.5f; break;       // a song that frays nerves
                case BindingOrgan.Gland: s.dread = 1.4f; break;                          // resonance-charged roars
                case BindingOrgan.Matrix: s.armor = 0f; s.maxIntegrity *= 0.85f; s.potency *= 1.1f; break; // no structure to buffer it
                case BindingOrgan.Fins:
                    s.grounds.Add(new GroundModifier { ground = BattleGround.Water, attack = 1.3f, defense = 1.2f });
                    s.grounds.Add(new GroundModifier { ground = BattleGround.Marsh, attack = 1.15f, defense = 1.1f });
                    break;
            }
            if (species.subgroup == CreatureSubgroup.Marauder) s.dread = Math.Max(s.dread, 1.3f); // its screeching
            s.Reset();
            list.Add(s);
        }
        return list;
    }

    /// <summary>A wild group as a side of its own ("Grey Wolf", 6 of them).</summary>
    public static BattleSide Side(SpeciesSpec species, int individuals, int maxSections = 8)
    {
        var profile = species == null ? null : CreatureTaxonomy.Profile(species.diet, species.subgroup);
        return new BattleSide
        {
            name = species?.name ?? "Creatures",
            wild = true,
            takesCaptives = false,
            tempo = SpellTempo.Staccato,
            withdrawAt = WithdrawAt(profile?.response ?? ThreatResponse.DefendsWhenThreatened),
            sections = Sections(species, individuals, maxSections),
        };
    }
}
