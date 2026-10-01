using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>A battle on the map as the battle screen shows it: the strengths and what made them before a blow was struck, and how it ended.</summary>
public sealed class BattleRecord
{
    public int number;
    public string place;
    /// <summary>The strengths and modifiers on the field as the battle began (no forecast: <see cref="BattlePreview.forecast"/> is null).</summary>
    public BattlePreview preview;
    public BattleReport report;
    /// <summary>Which side was yours (true: the attacker; null: neither).</summary>
    public bool? yours;
    /// <summary>What happened on the map afterwards (the fallen, the captives, the legends who went missing).</summary>
    public List<string> aftermath = new List<string>();

    /// <summary>The verdict for your side (the attacker's when neither was yours).</summary>
    public BattleOutcome Outcome => report.OutcomeFor(yours ?? true);
}

/// <summary>
/// The Symphony of War on the map (the card layer, <see cref="SymphonyDecks"/>): every side that fights gets its
/// Symphony. An army plays its companies' basic decks, its legends' personal grimoires, its commander's orders and the
/// civilization's war score; every other party of yours plays its kit (chosen in a settlement) and its legends'
/// grimoires; a band plays its creatures' instincts. <see cref="PreviewBattle"/> weighs two units before they meet (the
/// pre-battle screen), and every battle leaves a <see cref="BattleRecord"/> for the result screen.
/// </summary>
public partial class WorldSystem
{
    // Saved: the Symphony Cards seated in the Grimoire's Symphony seats (the war score armies play; empty: chosen by itself).
    [SaveOptionalField] private List<string> _warScore = new List<string>();

    /// <summary>The card layer's settings.</summary>
    public SymphonySettings Symphony => Combat.Symphony;

    /// <summary>The last battle fought on the map, with its strengths and verdicts.</summary>
    public BattleRecord LastBattleRecord { get; private set; }

    /// <summary>A battle was fought and recorded (for the result screen).</summary>
    public event Action<BattleRecord> BattleRecorded;

    // ===== KITS =====

    /// <summary>The kits your parties can carry now.</summary>
    public IEnumerable<ExpeditionKit> Kits => SymphonyDecks.Kits(Symphony, Researched);

    /// <summary>The kit a party of yours fights with: its own, else the Wayfarer's Kit (armies and bands carry none).</summary>
    public ExpeditionKit KitOf(WorldUnit unit)
    {
        if (unit == null || !WorldBattles.IsPlayers(unit) || !string.IsNullOrEmpty(unit.armyStack)) return null;
        var available = Kits.ToList();
        return available.FirstOrDefault(k => k.id == unit.kit) ?? available.FirstOrDefault(k => k.id == SymphonyCards.DefaultKit) ?? available.FirstOrDefault();
    }

    /// <summary>Why a party cannot change to <paramref name="kitId"/> now, or null (kits are changed in one of your settlements).</summary>
    public string WhyNotEquip(WorldUnit unit, string kitId)
    {
        var kit = Symphony.Kit(kitId);
        if (kit == null) return "No such kit.";
        if (unit == null || !WorldBattles.IsPlayers(unit) || !string.IsNullOrEmpty(unit.armyStack)) return "Only a party carries a kit.";
        if (unit.Missing) return "The party is missing.";
        if (KitOf(unit) == kit) return $"It already carries the {kit.name}.";
        if (!string.IsNullOrEmpty(kit.technology) && !Researched(kit.technology)) return $"The {kit.name} needs {kit.technology}.";
        if ((Map.Get(unit.coord)?.settlement ?? -1) < 0) return "A party changes its kit in one of your settlements.";
        return null;
    }

    public bool EquipKit(WorldUnit unit, string kitId)
    {
        string why = WhyNotEquip(unit, kitId);
        if (why != null) { Say(why); return false; }
        var kit = Symphony.Kit(kitId);
        unit.kit = kit.id;
        unit.kitWeight = Math.Max(0f, kit.weight);
        _quickPreviews.Clear();
        Say($"{unit.name} takes up the {kit.name}.");
        Changed?.Invoke();
        return true;
    }

    /// <summary>The next kit a party could change to (cycling), or null when there is no other.</summary>
    public ExpeditionKit NextKit(WorldUnit unit, string after)
    {
        var kits = Kits.ToList();
        if (kits.Count == 0) return null;
        var current = KitOf(unit);
        int i = kits.FindIndex(k => k.id == (after ?? current?.id));
        for (int n = 1; n <= kits.Count; n++)
        {
            var k = kits[(i + n + kits.Count) % kits.Count];
            if (k != current) return k;
        }
        return null;
    }

    // ===== THE WAR SCORE =====

    /// <summary>The Symphony Cards your armies play: those seated (or, until you seat any, the first owned ones), as many as the Grimoire has Symphony seats.</summary>
    public List<CombatCard> WarScore()
    {
        var owned = Grimoire.Current();
        var chosen = (_warScore ?? new List<string>()).Select(id => owned.cards.FirstOrDefault(c => c != null && c.id == id)).Where(c => c != null);
        var cards = (_warScore != null && _warScore.Count > 0 ? chosen : owned.cards.Where(c => c != null && (c.uses & CardUse.Battle) != 0)).Take(owned.symphonySeats);
        return cards.Select(c => SymphonyCards.FromSymphonyCard(c, Symphony)).Where(c => c != null).ToList();
    }

    /// <summary>Seats Symphony Cards in the war score (ids; empty: let it choose).</summary>
    public void SetWarScore(IEnumerable<string> ids)
    {
        _warScore = (ids ?? Enumerable.Empty<string>()).Where(id => !string.IsNullOrEmpty(id)).Distinct().ToList();
        Changed?.Invoke();
    }

    // ===== SYMPHONIES =====

    private DeckSources SourcesFor(WorldUnit unit, SpeciesSpec species = null)
    {
        var gen = Settings.generation;
        var owned = Grimoire.Current();
        return new DeckSources
        {
            age = GameAge.Number,
            hasTechnology = Researched,
            party = WorldBattles.IsPlayers(unit) && string.IsNullOrEmpty(unit.armyStack),
            doctrine = WorldBattles.IsPlayers(unit) ? Combat.Doctrine(!string.IsNullOrEmpty(unit.armyStack) ? Army.Stack(unit.armyStack)?.doctrine : unit.battleDoctrine) : null,
            civicCards = WorldBattles.IsPlayers(unit) ? (Combat.civicDecks ?? new List<BattleCivicDeck>()).Where(c => ActiveBattleCivics().Contains(c.civic))
                .SelectMany(c => c.cards.Select(id => (c.civic, Symphony.Card(id)))).ToList() : new List<(string, CombatCard)>(),
            symphony = Symphony,
            species = id => species != null && species.id == id ? species : WorldPursuit.SpeciesById(gen, id),
            identity = unit?.identity ?? BandIdentity.Creature,
            kit = KitOf(unit),
            warScore = unit != null && !string.IsNullOrEmpty(unit.armyStack) ? WarScore() : new List<CombatCard>(),
            learned = id => SymphonyCards.FromSymphonyCard(owned.cards.FirstOrDefault(c => c != null && c.id == id), Symphony),
        };
    }

    /// <summary>A unit's side with its Symphony.</summary>
    private BattleSide Scored(WorldUnit unit, BattleSide side, SpeciesSpec species = null) => SymphonyDecks.Score(side, SourcesFor(unit, species));

    private static List<string> ActiveBattleCivics() => CivicManager.Instance == null ? new List<string>() :
        CivicManager.Instance.GetAllActiveCivics().Where(c => c != null).Select(c => c.civicName).ToList();

    private int BattleProductionTier(string name)
    {
        var slot = GameUnitsLogic.Instance?.GetProductionSlot(name);
        return slot == null || slot.maxAmount <= 0f ? 0 : Math.Max(1, slot.productionUnitData?.productionTier ?? 1);
    }

    public bool EquipCompany(string unitId, string equipmentId)
    {
        var company = Army.Unit(unitId);
        var marching = company == null ? null : ArmyOf(company.stack);
        if (marching != null && (Map?.Get(marching.coord)?.settlement ?? -1) < 0) return false;
        if (!BattleEquipmentLogic.Equip(company, Combat.Equipment(equipmentId), _bank, BattleProductionTier)) return false;
        _quickPreviews.Clear(); Changed?.Invoke();
        return true;
    }

    public bool ChooseBattleDoctrine(WorldUnit unit, string doctrineId)
    {
        var doctrine = Combat.Doctrine(doctrineId);
        if (unit == null || !WorldBattles.IsPlayers(unit) || unit.Missing || doctrine == null || doctrine.minAge > GameAge.Number ||
            !string.IsNullOrEmpty(doctrine.technology) && !Researched(doctrine.technology)) return false;
        if (!string.IsNullOrEmpty(unit.armyStack))
        {
            if (!Army.SetDoctrine(unit.armyStack, doctrine)) return false;
        }
        else { unit.battleDoctrine = doctrine.id; unit.battleStance = doctrine.stance; }
        _quickPreviews.Clear(); Changed?.Invoke();
        return true;
    }

    public IEnumerable<BattleDoctrineSpec> BattleDoctrines => (Combat.doctrines ?? new List<BattleDoctrineSpec>()).Concat(BattleDeploymentLogic.Defaults)
        .Where(d => d != null && d.minAge <= GameAge.Number && (string.IsNullOrEmpty(d.technology) || Researched(d.technology)))
        .GroupBy(d => d.id).Select(g => g.First());

    public bool AssignArmyCore(string stackId, string legend, BattleEliteRole role, string section = null)
    {
        if (ExpeditionOf(legend) != null) return false;
        var seat = SeatLeftBy(legend);
        if (seat != null && !GovernmentLogic.Instance.CanChangeSeat(seat.seatIndex)) return false;
        var spec = Combat.Section(section);
        if (LegendProgress.Instance?.BattleLegendOf(legend) == null || (!string.IsNullOrEmpty(section) &&
            (spec == null || spec.minAge > GameAge.Number || !string.IsNullOrEmpty(spec.technology) && !Researched(spec.technology)))) return false;
        if (!Army.AssignCore(stackId, legend, role, section)) return false;
        LeaveCouncil(legend);
        _quickPreviews.Clear(); Changed?.Invoke(); return true;
    }

    public bool ReleaseArmyCore(string stackId, string legend)
    {
        if (!Army.ReleaseCore(stackId, legend)) return false;
        _quickPreviews.Clear(); Changed?.Invoke(); return true;
    }

    private void ComposeBattleInputs(BattleSetup setup, WorldUnit attacker, WorldUnit defender)
    {
        foreach (var pair in new[] { (side: setup.attacker, unit: attacker), (side: setup.defender, unit: defender) })
        {
            var own = WorldBattles.IsPlayers(pair.unit);
            pair.side.scoreInputs = BattleCompositionLogic.Describe(pair.side, setup.field, Combat, pair.unit, SpecOf(pair.unit),
                own ? ActiveBattleCivics() : null, own && LegendProgress.Instance != null ? new Func<string, IReadOnlyList<LegendRelationship>>(LegendProgress.Instance.Relationships) : null);
        }
    }

    // ===== THE PRE-BATTLE SCREEN =====

    /// <summary>The two units as the battle would stand if they met now: who attacks, the field, both sides spent or bitten as they are.</summary>
    private BattleSetup SetupOf(WorldUnit one, WorldUnit other)
    {
        if (one == null || other == null) return null;
        var attackerUnit = WorldBattles.Attacker(one, other, WorldBattles.Angry);
        var defenderUnit = attackerUnit == one ? other : one;
        var attacker = SideOf(attackerUnit);
        var defender = SideOf(defenderUnit);
        if (attacker == null || defender == null || attacker.sections.Count == 0 || defender.sections.Count == 0) return null;
        WorldPursuit.Tire(attacker, WorldPursuit.Tiredness(attackerUnit));
        WorldPursuit.Tire(defender, WorldPursuit.Tiredness(defenderUnit));
        Bite(attackerUnit, defender);
        Bite(defenderUnit, attacker);
        var time = TimeSystemLogic.Instance;
        var field = WorldBattles.Field(Map, Settings.generation, Combat, WorldUnits.MicroPosition(attackerUnit), WorldUnits.MicroPosition(defenderUnit), GameAge.Number, time != null ? time.CurrentEcho : 0);
        var setup = new BattleSetup { attacker = attacker, defender = defender, field = field, seed = unchecked(Map.seed * 31 + attackerUnit.id * 7919 + defenderUnit.id * 104729) };
        ComposeBattleInputs(setup, attackerUnit, defenderUnit);
        return setup;
    }

    /// <summary>
    /// The pre-battle screen for two units (Total War's balance of power, Civ VI's strengths): who would attack, each
    /// side's strength and its modifiers, and what <paramref name="runs"/> forecast battles predict. Null when they
    /// cannot fight.
    /// </summary>
    public BattlePreview PreviewBattle(WorldUnit one, WorldUnit other, int runs = 12)
    {
        var setup = SetupOf(one, other);
        if (setup == null) return null;
        var at = Map.Get(HexHierarchy.Parent(WorldUnits.MicroPosition(WorldBattles.Attacker(one, other, WorldBattles.Angry) == one ? other : one)));
        return BattlePreview.Of(setup, Combat, runs, $"Battle of {Place(at)}");
    }

    private readonly Dictionary<(int, int), (float at, BattlePreview preview)> _quickPreviews = new Dictionary<(int, int), (float, BattlePreview)>();

    /// <summary>The strengths alone (no forecast), kept for a second: for cards and order labels drawn every frame.</summary>
    public BattlePreview QuickPreview(WorldUnit one, WorldUnit other)
    {
        if (one == null || other == null) return null;
        var key = (one.id, other.id);
        float now = UnityEngine.Time.unscaledTime;
        if (_quickPreviews.TryGetValue(key, out var kept) && now - kept.at < 1f) return kept.preview;
        if (_quickPreviews.Count > 64) _quickPreviews.Clear();
        var preview = PreviewBattle(one, other, 0);
        _quickPreviews[key] = (now, preview);
        return preview;
    }

    /// <summary>Whether the pre-battle screen for <paramref name="one"/> would show it as the attacker.</summary>
    public bool WouldAttack(WorldUnit one, WorldUnit other) => one != null && other != null && WorldBattles.Attacker(one, other, WorldBattles.Angry) == one;

    /// <summary>Your party (or army) nearest to <paramref name="enemy"/>, for its card's odds.</summary>
    public WorldUnit NearestOfYours(WorldUnit enemy)
    {
        if (Map == null || enemy == null) return null;
        var at = WorldUnits.MicroPosition(enemy);
        return Map.Units.Where(u => WorldBattles.IsPlayers(u) && !u.Missing && WorldBattles.Fights(u))
            .OrderBy(u => HexCoord.Distance(at, WorldUnits.MicroPosition(u))).ThenBy(u => u.id).FirstOrDefault();
    }

    // ===== THE RECORD =====

    private BattleRecord Record(BattleSetup setup, WorldUnit attackerUnit, WorldUnit defenderUnit, string place)
    {
        var record = new BattleRecord
        {
            number = _battlesFought, place = place,
            preview = BattlePreview.Of(setup, Combat, 0, $"Battle of {place}"),
            yours = WorldBattles.IsPlayers(attackerUnit) ? true : WorldBattles.IsPlayers(defenderUnit) ? false : (bool?)null,
        };
        return record;
    }
}
