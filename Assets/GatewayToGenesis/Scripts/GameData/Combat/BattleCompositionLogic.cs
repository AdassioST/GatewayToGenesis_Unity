using System;
using System.Collections.Generic;
using System.Linq;

public enum BattleEliteRole { None, Legend, Conductor, CommandStaff, Guard, Spellweaver, Specialist }
public enum BattleAbstraction { Individual, Squad, Company, Battalion, ArmySection }
public enum BattleScoreInput { Army, Material, Magic, Command, Condition, Relationships, Context }

[Serializable]
public sealed class ArmyCoreMember
{
    public string legend, section;
    public BattleEliteRole role = BattleEliteRole.Legend;
}

[Serializable]
public sealed class BattleCivicDeck
{
    public string civic;
    public List<string> cards = new List<string>();
}

/// <summary>Authored gear. Numbers and production requirements come from content, never inferred from a name.</summary>
[Serializable]
public sealed class BattleEquipmentSpec
{
    public string id, name, technology, productionUnit;
    public int minAge, productionTier = 1;
    public List<string> sections = new List<string>(), cards = new List<string>();
    public List<ResourceAmount> cost = new List<ResourceAmount>();
    public float attack, defense, armor, piercing, potency, ward;
}

public static class BattleEquipmentLogic
{
    public static string WhyNotEquip(ConscriptUnit unit, BattleEquipmentSpec gear, IConscriptionBank bank, Func<string, int> productionTier)
    {
        if (unit == null || gear == null || bank == null) return "No company or equipment.";
        if (unit.equipment == gear.id) return "The company already carries this equipment.";
        if (gear.sections.Count > 0 && !gear.sections.Contains(unit.specId)) return "This equipment does not fit the company.";
        if (bank.Age < gear.minAge || !string.IsNullOrEmpty(gear.technology) && !bank.Has(gear.technology)) return "This equipment is not unlocked.";
        if (!string.IsNullOrEmpty(gear.productionUnit) && (productionTier?.Invoke(gear.productionUnit) ?? 0) < Math.Max(1, gear.productionTier))
            return $"Requires {gear.productionUnit} at production tier {Math.Max(1, gear.productionTier)}.";
        // Sum duplicate resource lines before checking or spending, so an authored recipe cannot overspend.
        foreach (var cost in Costs(gear)) if (bank.Amount(cost.Key) < cost.Sum(c => c.amount)) return $"Not enough {cost.Key}.";
        return null;
    }

    private static IEnumerable<IGrouping<string, ResourceAmount>> Costs(BattleEquipmentSpec gear) =>
        (gear.cost ?? new List<ResourceAmount>()).Where(c => c != null && !string.IsNullOrEmpty(c.resource) && c.amount > 0f).GroupBy(c => c.resource);

    public static bool Equip(ConscriptUnit unit, BattleEquipmentSpec gear, IConscriptionBank bank, Func<string, int> productionTier)
    {
        if (WhyNotEquip(unit, gear, bank, productionTier) != null) return false;
        foreach (var cost in Costs(gear)) bank.Spend(cost.Key, cost.Sum(c => c.amount));
        unit.equipment = gear.id;
        unit.equipmentTier = string.IsNullOrEmpty(gear.productionUnit) ? Math.Max(1, gear.productionTier) : productionTier(gear.productionUnit);
        return true;
    }

    public static void Apply(CombatSection section, BattleEquipmentSpec gear, int tier)
    {
        if (gear == null) return;
        section.equipment = gear.id;
        section.productionUnit = gear.productionUnit;
        section.productionTier = Math.Max(1, tier);
        section.equipmentCards = new List<string>(gear.cards ?? new List<string>());
        section.attack += gear.attack; section.defense += gear.defense; section.armor += gear.armor;
        section.piercing += gear.piercing; section.potency += gear.potency; section.ward = Math.Max(0f, Math.Min(0.9f, section.ward + gear.ward));
    }
}

public sealed class BattleScoreFact
{
    public BattleScoreInput input;
    public string label, value;
    public BattleScoreFact Clone() => (BattleScoreFact)MemberwiseClone();
}

/// <summary>A frozen pre-contact explanation of actual state, shared by manual/auto and copied by forecasts.</summary>
public sealed class BattleScoreInputs
{
    public List<BattleScoreFact> facts = new List<BattleScoreFact>();
    public BattleScoreInputs Clone() => new BattleScoreInputs { facts = facts.Select(f => f.Clone()).ToList() };
    public void Add(BattleScoreInput input, string label, string value) => facts.Add(new BattleScoreFact { input = input, label = label, value = value });
}

public static class BattleCompositionLogic
{
    public static BattleAbstraction Abstraction(BattleScale scale) => scale == BattleScale.EliteEngagement ? BattleAbstraction.Individual :
        scale == BattleScale.MicroEngagement ? BattleAbstraction.Squad : scale == BattleScale.FormationEngagement ? BattleAbstraction.Company :
        scale == BattleScale.MediumArmy ? BattleAbstraction.Battalion : BattleAbstraction.ArmySection;

    public static void Classify(BattleSide side)
    {
        var scale = BattleScaleRules.ForSize(side.CombatantCount);
        foreach (var section in side.sections)
            section.abstraction = section.count <= 1 || section.eliteRole != BattleEliteRole.None ? BattleAbstraction.Individual :
                scale == BattleScale.EliteEngagement ? BattleAbstraction.Squad : Abstraction(scale);
    }

    /// <summary>Materializes every assigned named elite once, keeping command/company identity references intact.</summary>
    public static void AddEliteCore(BattleSide side, CombatSettings settings, IEnumerable<ArmyCoreMember> members = null, Func<string, BattleLegend> legendOf = null)
    {
        var roles = new Dictionary<BattleLegend, (BattleEliteRole role, string spec)>();
        foreach (var leader in side.Legends)
        {
            var piece = side.sections.FirstOrDefault(s => s.eliteRole != BattleEliteRole.None && s.leader == leader);
            roles[leader] = (leader == side.conductor ? BattleEliteRole.Conductor : piece?.eliteRole ?? BattleEliteRole.Legend, null);
        }
        foreach (var member in members ?? Enumerable.Empty<ArmyCoreMember>())
        {
            var legend = side.Legends.FirstOrDefault(l => l.name == member.legend) ?? legendOf?.Invoke(member.legend);
            if (legend != null) roles[legend] = (legend == side.conductor ? BattleEliteRole.Conductor : member.role, member.section);
        }
        foreach (var pair in roles)
        {
            var legend = pair.Key;
            var existing = side.sections.FirstOrDefault(s => s.count == 1 && s.leader == legend && string.IsNullOrEmpty(s.unitId));
            if (existing != null) { existing.eliteRole = pair.Value.role; continue; }
            var spec = settings.Section(pair.Value.spec);
            var piece = spec != null ? CombatSection.Raise(spec, legend.leitmotif, legend.ornaments) : new CombatSection
            {
                row = FormationRow.Support, kind = SectionKind.Support, maxIntegrity = WorldBattles.LegendIntegrity,
                maxComposure = settings.Tuning.legendComposure, attack = WorldBattles.LegendAttack, defense = WorldBattles.LegendDefense,
                primary = legend.leitmotif, soulWeaver = true,
            };
            piece.Reset();
            piece.name = legend.name; piece.count = 1; piece.leader = legend; piece.eliteRole = pair.Value.role;
            piece.primary = legend.leitmotif;
            piece.composure = Math.Min(piece.maxComposure, legend.BattleComposure(settings.Tuning));
            side.sections.Add(piece);
        }
        SplitEliteScale(side);
        Classify(side);
    }

    /// <summary>At Elite scale every body is a piece. Split before scoring so deck voice indices remain valid.</summary>
    public static void SplitEliteScale(BattleSide side)
    {
        if (side.CombatantCount > 10) return;
        var pieces = new List<CombatSection>();
        foreach (var section in side.sections)
        {
            int count = Math.Max(1, section.count);
            if (count == 1) { pieces.Add(section); continue; }
            for (int i = 0; i < count; i++)
            {
                var piece = section.Clone();
                piece.name = $"{section.name} ({i + 1}/{count})"; piece.count = 1; piece.leader = section.leader;
                piece.maxIntegrity /= count; piece.integrity /= count; piece.maxComposure /= count; piece.composure /= count;
                piece.attack /= count; piece.defense /= count; piece.breakthrough /= count; piece.potency /= count; piece.width /= count;
                piece.lost /= count; piece.dead /= count; piece.wounded /= count;
                pieces.Add(piece);
            }
        }
        side.sections = pieces;
    }

    /// <summary>Roster aftermath is recorded once per recruited company even when its soldiers fought individually.</summary>
    public static CombatSection CompanyResult(IEnumerable<CombatSection> pieces)
    {
        var group = pieces.ToList();
        if (group.Count == 1) return group[0];
        var combined = group[0].Clone(); combined.leader = group[0].leader;
        combined.count = group.Sum(s => s.count); combined.maxIntegrity = group.Sum(s => s.maxIntegrity);
        combined.integrity = group.Where(s => !s.captured).Sum(s => Math.Max(0f, s.integrity));
        combined.wounded = group.Where(s => !s.captured && !s.destroyed).Sum(s => s.wounded);
        combined.destroyed = group.All(s => s.destroyed);
        combined.captured = group.Any(s => s.captured) && group.All(s => s.captured || s.destroyed);
        if (combined.captured) combined.integrity = group.Sum(s => Math.Max(0f, s.integrity));
        combined.timesMindBroken = group.Max(s => s.timesMindBroken);
        return combined;
    }

    public static BattleScoreInputs Describe(BattleSide side, Battlefield field, CombatSettings settings, WorldUnit unit = null, UnitSpec spec = null,
        IEnumerable<string> civics = null, Func<string, IReadOnlyList<LegendRelationship>> relationships = null)
    {
        var score = new BattleScoreInputs();
        Classify(side);
        score.Add(BattleScoreInput.Army, "Force", $"{side.CombatantCount} combatants; {BattleScaleRules.ForSize(side.CombatantCount)}; {side.sections.Count} tactical pieces");
        score.Add(BattleScoreInput.Army, "Initial advance", BattleDeploymentLogic.Doctrine(settings, side).name);
        foreach (var section in side.sections)
        {
            score.Add(BattleScoreInput.Army, section.name, $"{Math.Max(1, section.count)}; {section.abstraction}; {section.row}; {section.eliteRole}");
            if (!string.IsNullOrEmpty(section.specId) || !string.IsNullOrEmpty(section.equipment))
                score.Add(BattleScoreInput.Material, section.name, $"{section.equipment ?? "standard outfit"}; production tier {section.productionTier}" +
                    (string.IsNullOrEmpty(section.productionUnit) ? "" : $" from {section.productionUnit}") +
                    (string.IsNullOrEmpty(section.trainingTechnology) ? "" : $"; trained through {section.trainingTechnology}"));
            score.Add(BattleScoreInput.Condition, section.name, $"Integrity {section.integrity:0.#}/{section.maxIntegrity:0.#}; Composure {section.composure:0.#}/{section.maxComposure:0.#}");
            foreach (var bond in section.bonds ?? new Dictionary<string, int>())
                score.Add(BattleScoreInput.Relationships, $"{section.name} / {bond.Key}", $"Company attachment {bond.Value} stars");
        }
        if (unit != null)
        {
            score.Add(BattleScoreInput.Condition, "Expedition", $"Supply {unit.supplies:0.#}/{spec?.supplyCapacity ?? 0f:0.#}; fatigue {unit.fatigue:0.#}; attrition {unit.attrition:0.#}; nerve lost {unit.nerveLost:P0}");
            if (!string.IsNullOrEmpty(unit.kit)) score.Add(BattleScoreInput.Material, "Expedition kit", unit.kit);
        }
        score.Add(BattleScoreInput.Command, "Conductor", side.conductor == null ? "Uncommanded" : $"{side.conductor.name}; {side.conductor.Title}");
        foreach (string civic in civics ?? Enumerable.Empty<string>()) score.Add(BattleScoreInput.Command, "Active civic", civic);
        var names = new HashSet<string>(side.Legends.Select(l => l.name));
        if (relationships != null) side.echoingBonds.Clear();
        foreach (var legend in side.Legends)
        {
            score.Add(BattleScoreInput.Condition, legend.name, $"{legend.leitmotif}; {legend.State}; strain {legend.strain:0.#}" +
                (legend.conditions.Count == 0 ? "" : "; " + string.Join(", ", legend.conditions)));
            score.Add(BattleScoreInput.Command, legend.name, legend.Title + (legend.traits.Count == 0 ? "" : "; " + string.Join(", ", legend.traits)));
            foreach (var bond in relationships?.Invoke(legend.name) ?? Array.Empty<LegendRelationship>())
                if (names.Contains(bond.other))
                {
                    score.Add(BattleScoreInput.Relationships, $"{legend.name} -> {bond.other}", $"{bond.StageName}; affection {bond.affection}; {(bond.severed ? "severed" : bond.thread ?? "no thread")}");
                    if (relationships != null && bond.Significant && bond.stage > 0) side.echoingBonds.Add(new BattleEchoingBond
                    { first = legend.name, second = bond.other, strength = Math.Min(1f, bond.stage / 3f * bond.affection / 100f) });
                }
        }
        foreach (var source in (side.deck ?? new List<DeckCard>()).Where(d => d?.card != null).GroupBy(d => d.source))
            score.Add(BattleScoreInput.Magic, source.Key ?? "Deck", string.Join(", ", source.Select(d => d.card.name).Distinct()));
        if (!score.facts.Any(f => f.input == BattleScoreInput.Material)) score.Add(BattleScoreInput.Material, "Outfit", "No additional equipment");
        if (!score.facts.Any(f => f.input == BattleScoreInput.Magic)) score.Add(BattleScoreInput.Magic, "Deck", "No scored cards");
        if (!score.facts.Any(f => f.input == BattleScoreInput.Relationships)) score.Add(BattleScoreInput.Relationships, "Bonds", "No recorded bonds in this force");
        field = field ?? new Battlefield();
        score.Add(BattleScoreInput.Context, "Battlefield", $"{field.place}; {field.ground}; settlement {field.settlement}; entrenchment {side.entrenchment:0.#}; recon {side.sections.Sum(s => s.recon):0.#}");
        score.Add(BattleScoreInput.Context, "Loom", $"Coherence {field.coherence:0.#}; Dissonance {field.dissonance:0.#}; leyline {field.leyline}");
        foreach (var hex in field.hexes)
        {
            var features = new List<string>();
            if (hex.blocked) features.Add("impassable"); if (hex.river) features.Add("river");
            if (hex.ford) features.Add("ford"); if (hex.bridge) features.Add("bridge"); if (hex.wall) features.Add("wall");
            if (hex.highGround) features.Add("high ground"); if (hex.forest) features.Add("forest"); if (hex.snow) features.Add("snow");
            if (hex.narrowPass) features.Add("narrow pass"); if (hex.leyline) features.Add("leyline"); if (hex.harmonicChannel) features.Add("harmonic channel");
            if (hex.dissonance > 0f) features.Add($"Dissonance {hex.dissonance:0.#}");
            if (features.Count > 0) score.Add(BattleScoreInput.Context, $"{BattleHexLayout.At(hex.hex).Lane} hex {hex.hex + 1}", string.Join(", ", features));
        }
        return score;
    }
}
