using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>When a threat site sends out its next band (saved).</summary>
[Serializable]
public class ThreatTimer
{
    public string threat;
    public int cell;
    /// <summary>Sevenths until it sends a band out, once it has fewer than it may.</summary>
    public float sevenths;
}

/// <summary>
/// The Symphony of War on the world map: the civilization's army roster (conscripted companies and their stacks,
/// <see cref="ArmyRoster"/>), armies marching as map units, the threats' red and orange enemy bands (sent out from
/// their sites, hunting or wary, roaming around home), and battles when hostile units stand on adjacent micro hexes
/// (<see cref="WorldBattles"/>). Every one of your units can be attacked: armies fight with their companies, every other
/// party as itself (<see cref="WorldBattles.PartySide"/>).
/// </summary>
public partial class WorldSystem
{
    // Saved (GameSnapshot): the roster, how many battles have been fought (seeds each one), the threats' timers.
    [SaveOptionalField] private ArmyRoster _army = new ArmyRoster();
    [SaveOptionalField] private int _battlesFought;
    [SaveOptionalField] private List<ThreatTimer> _threatTimers = new List<ThreatTimer>();

    private readonly LiveConscriptionBank _bank = new LiveConscriptionBank();
    private readonly Dictionary<(string spec, int tone), UnitSpec> _tinted = new Dictionary<(string, int), UnitSpec>();

    /// <summary>Composure share a party recovers per Seventh after a battle (three times as fast in camp or a settlement).</summary>
    public const float NerveRecovery = 0.05f;

    /// <summary>The civilization's conscripted companies and stacks.</summary>
    public ArmyRoster Army => _army ?? (_army = new ArmyRoster());

    /// <summary>The Symphony of War's settings (World.asset; defaults when it lists none).</summary>
    public CombatSettings Combat => Settings.combat ?? (Settings.combat = new CombatSettings());

    /// <summary>The last battle fought on the map, for a battle screen.</summary>
    public BattleReport LastBattle { get; private set; }

    /// <summary>A battle was fought: (report, attacker, defender).</summary>
    public event Action<BattleReport, WorldUnit, WorldUnit> BattleFought;

    /// <summary>Your side took a captive (a creature group to bring home, or prisoners).</summary>
    public event Action<BattleCaptive> CaptiveTaken;

    // An enemy's kind of unit in its stance's colour (red hostile, orange wary, muted orange timid), so the map shows which is which.
    private UnitSpec Tinted(UnitSpec spec, WorldUnit unit)
    {
        int tone = WorldBattles.Angry(unit) ? 0 : unit.stance == EnemyStance.Timid ? 2 : 1;
        if (_tinted.TryGetValue((spec.id, tone), out var tinted) && tinted.stamina == spec.stamina && tinted.sight == spec.sight) return tinted;
        tinted = spec.Clone();
        tinted.color = WorldBattles.ColorOf(unit);
        _tinted[(spec.id, tone)] = tinted;
        return tinted;
    }

    // ===== THE ROSTER =====

    public string WhyNotConscript(string specId) => ArmyRoster.WhyNotConscript(specId, _bank, Combat);

    /// <summary>Raises a company from the population (null when it cannot: <see cref="WhyNotConscript"/>).</summary>
    public ConscriptUnit Conscript(string specId)
    {
        var unit = Army.Conscript(specId, _bank, Combat);
        if (unit != null)
        {
            // Families part at the Capital: longing for the conscripted, and lives torn from their own.
            var capital = Map?.Get(Map.Capital);
            if (capital != null)
            {
                AddSuffering(capital.index, Feeling.Longing, WorldSuffering.ConscriptionLonging);
                AddSuffering(capital.index, Feeling.Estrangement, WorldSuffering.ConscriptionLonging * 0.5f);
            }
            Say($"{unit.name} are raised.");
            Changed?.Invoke();
        }
        return unit;
    }

    /// <summary>The army on the map carrying a stack, or null while it stays at home.</summary>
    public WorldUnit ArmyOf(string stackId) => Map?.Units.FirstOrDefault(u => u.armyStack == stackId);

    /// <summary>Marches a stack out of a settlement (the Capital by default) as an army on the map.</summary>
    public WorldUnit MarchArmy(string stackId, Settlement from = null)
    {
        var stack = Army.Stack(stackId);
        if (Map == null || stack == null || stack.units.Count == 0) return null;
        var existing = ArmyOf(stackId);
        if (existing != null) return existing;
        from = from ?? WorldCivilization.Capital(Map);
        var spec = Settings.Unit(WorldBattles.ArmySpec);
        if (from == null || spec == null) return null;
        var unit = Deploy(spec, from.coord);
        unit.name = stack.name;
        unit.armyStack = stack.id;
        Say($"{stack.name} marches out of {from.name}.");
        Changed?.Invoke();
        return unit;
    }

    /// <summary>
    /// An enemy band on the map at a micro hex: a species' creatures, or a threat's beings (<paramref name="threat"/>)
    /// carrying <paramref name="binding"/>. Red hunt your units; orange wait until provoked.
    /// </summary>
    public WorldUnit SpawnBand(string speciesId, int count, HexCoord micro, EnemyStance stance = EnemyStance.Hostile, string threat = null,
        int homeCell = -1, SpellBinding binding = SpellBinding.Unattuned)
    {
        var threatSpec = WorldPursuit.ThreatOf(Settings.generation, threat);
        var species = threatSpec != null && string.IsNullOrEmpty(speciesId) ? WorldBattles.Being(threatSpec, binding) : WorldPursuit.SpeciesById(Settings.generation, speciesId);
        var spec = Settings.Unit(WorldBattles.BandSpec);
        if (Map == null || species == null || spec == null || count <= 0 || MicroNavigation.Parent(Map, micro) == null) return null;
        _nextUnitId = Math.Max(_nextUnitId, Map.Units.Count == 0 ? 0 : Map.Units.Max(u => u.id) + 1);
        var unit = new WorldUnit
        {
            id = _nextUnitId++, spec = spec.id, name = $"{species.name} ({count})", faction = WorldBattles.Wild,
            species = threatSpec != null && string.IsNullOrEmpty(speciesId) ? null : species.id, creatures = count, stance = stance,
            threat = threatSpec?.id, homeCell = homeCell >= 0 ? homeCell : MicroNavigation.Parent(Map, micro).index, binding = binding,
            coord = HexHierarchy.Parent(micro), homeHex = micro, homeHexSet = true,
        };
        WorldUnits.Place(unit, micro);
        WorldUnits.Initialize(unit, spec);
        WorldPursuit.Initialize(Settings.generation, unit);
        Map.Units.Add(unit);
        Changed?.Invoke();
        return unit;
    }

    // ===== THE THREATS SEND THEIR BANDS OUT =====

    // Each threat site keeps its bands out: once it has fewer than it may and its timer has run, a new band rises at
    // the site (its first as soon as the site is known). A threat's beings each carry a binding drawn for the band.
    private bool TickThreats(float sevenths)
    {
        if (Map == null || Map.Threats.Count == 0) return false;
        if (_threatTimers == null) _threatTimers = new List<ThreatTimer>();
        bool spawned = false;
        foreach (var site in Map.Threats)
        {
            var threat = Settings.generation.Threat(site.spec);
            if (threat == null || threat.bands <= 0 || !Map[site.cell].revealed) continue;
            int out_ = Map.Units.Count(u => u.threat == threat.id && u.homeCell == site.cell && u.creatures > 0);
            var timer = _threatTimers.FirstOrDefault(t => t.threat == threat.id && t.cell == site.cell);
            if (timer == null) _threatTimers.Add(timer = new ThreatTimer { threat = threat.id, cell = site.cell, sevenths = 0f });
            if (out_ >= threat.bands) { timer.sevenths = Math.Max(timer.sevenths, 0f); continue; }
            timer.sevenths -= sevenths;
            if (timer.sevenths > 0f) continue;
            timer.sevenths = Math.Max(0.5f, threat.respawnSevenths);
            var at = FreeHexNear(MicroNavigation.Center(Map[site.cell].coord));
            if (!at.HasValue) continue;
            int roll = _battlesFought + Map.Units.Count;
            int count = threat.bandMin + (int)(WorldNoise.Hash01(Map.seed, site.cell, roll) * (Math.Max(threat.bandMin, threat.bandMax) - threat.bandMin + 1));
            var binding = string.IsNullOrEmpty(threat.species) ? WorldBattles.DrawBinding(threat, WorldNoise.Hash01(Map.seed ^ 0x5bd1, site.cell, roll)) : SpellBinding.Unattuned;
            var band = SpawnBand(threat.species, Math.Max(1, count), at.Value, threat.stance, threat.id, site.cell, binding);
            if (band == null) continue;
            // A threat's own beings are Atonalis: each band walks one of the Eight-Born Paths, which sets how it hunts.
            if (string.IsNullOrEmpty(threat.species))
            {
                band.atonalPath = AtonalPaths.Draw(threat.beingPaths, WorldNoise.Hash01(Map.seed ^ 0x3a71, site.cell, roll));
                WorldPursuit.ApplyPath(band);
                Rename(band);
            }
            spawned = true;
        }
        return spawned;
    }

    // A micro hex at or around <paramref name="around"/> that no unit stands on and where a creature of <paramref name="habitat"/>
    // can stand (land for what walks, water for what swims), within <paramref name="radius"/>.
    private HexCoord? FreeHexNear(HexCoord around, CreatureHabitat habitat = CreatureHabitat.Land, int radius = 3)
    {
        var grid = MicroNavigation.Grid(Map, Settings.generation);
        var probe = new WorldUnit { faction = WorldBattles.Wild, habitat = habitat };
        for (int r = 0; r <= radius; r++)
            foreach (var hex in HexCoord.Spiral(around, r).Where(h => HexCoord.Distance(h, around) == r))
            {
                int id = MicroNavigation.Index(Map, hex);
                if (!WorldPursuit.CanStand(Map, grid, probe, id)) continue;
                if (Map.Units.Any(u => !u.Missing && WorldUnits.MicroPosition(u) == hex)) continue;
                return hex;
            }
        return null;
    }

    // ===== ON THE MOVE =====

    // A band's moment (WorldSystem.Encounters): it flees, chases, stalks, searches or roams as it decides. It walks unseen
    // by the map's knowledge (it reveals nothing) and never onto a hex another unit holds.
    private bool StepBand(WorldUnit band, UnitSpec spec, float sevenths, List<string> notice) => StepEncounter(band, spec, sevenths, notice);

    // Your units that come adjacent to a wary band, pass close by it or walk into its territory turn it hostile for a
    // while (red until it calms down or gives up the chase).
    private bool Provoke(List<string> notice)
    {
        bool changed = false;
        var yours = Map.Units.Where(u => WorldBattles.IsPlayers(u) && !u.Missing).ToList();
        if (yours.Count == 0) return false;
        foreach (var enemy in Map.Units.Where(u => !WorldBattles.IsPlayers(u) && u.stance == EnemyStance.Wary && !u.Missing).ToList())
        {
            // One that has learned you mean no harm lets you pass; one heading home after a chase has had enough.
            if (enemy.response == ThreatResponse.Tolerates || enemy.response == ThreatResponse.Unmoved || enemy.bandActivity == BandActivity.Returning) continue;
            int sight = Math.Max(1, SpecOf(enemy)?.sight ?? 4);
            var by = yours.FirstOrDefault(u => WorldPursuit.Provokes(Map, u, enemy, sight));
            if (by == null) continue;
            if (enemy.provoked <= 0f) notice.Add($"{enemy.name} turn on {by.name}!");
            enemy.provoked = WorldBattles.ProvokedSevenths;
            changed = true;
        }
        return changed;
    }

    // ===== BATTLES =====

    // Wary bands provoked first and predators catch their prey; then every hostile pair in contact fights once this moment.
    private bool FightClashes(List<string> notice)
    {
        if (_pendingEncounter != null) return false;
        bool changed = Provoke(notice);
        if (Predation(notice)) changed = true;
        var clashes = WorldBattles.Clashes(Map.Units);
        foreach (var (attacker, defender) in clashes)
        { Fight(attacker, defender, notice); if (_pendingEncounter != null) break; }
        return changed || clashes.Count > 0;
    }

    /// <summary>The side a map unit fights as: an army's stack with its legends, any other party of yours as itself, or an enemy band's creatures.</summary>
    public BattleSide SideOf(WorldUnit unit)
    {
        if (unit == null) return null;
        BattleLegend LegendOf(string n) => LegendProgress.Instance != null ? LegendProgress.Instance.BattleLegendOf(n) : null;
        // Every side plays its Symphony (WorldSystem.Symphony): an army its companies, legends, orders and war score; a
        // party its kit and its legends' grimoires; a band its creatures' instincts.
        if (!string.IsNullOrEmpty(unit.armyStack)) return Scored(unit, Army.Muster(unit.armyStack, Combat, LegendOf));
        if (WorldBattles.IsPlayers(unit)) return Scored(unit, WorldBattles.PartySide(unit, SpecOf(unit), Expeditions.Members(unit).ToList(), LegendOf));
        var species = WorldPursuit.Species(Settings.generation, unit);
        if (species == null || unit.creatures <= 0) return null;
        var side = CreatureCombat.Side(species, unit.creatures);
        side.name = unit.name;
        return Scored(unit, side, species);
    }

    private void Fight(WorldUnit attackerUnit, WorldUnit defenderUnit, List<string> notice)
    {
        var attacker = SideOf(attackerUnit);
        var defender = SideOf(defenderUnit);
        if (attacker == null || defender == null || attacker.sections.Count == 0 || defender.sections.Count == 0) return;
        // Whoever ran itself out fights spent: a quarry run down is half beaten (one way to hunt), and so is a pursuer
        // that chased too long.
        WorldPursuit.Tire(attacker, WorldPursuit.Tiredness(attackerUnit));
        WorldPursuit.Tire(defender, WorldPursuit.Tiredness(defenderUnit));
        // An Atonalis takes its foe's nerve before the first blow (despair, fear, doubt: its Path's bite).
        Bite(attackerUnit, defender);
        Bite(defenderUnit, attacker);
        var wildUnit = !WorldBattles.IsPlayers(attackerUnit) ? attackerUnit : !WorldBattles.IsPlayers(defenderUnit) ? defenderUnit : null;
        int wildBefore = wildUnit?.creatures ?? 0;
        var time = TimeSystemLogic.Instance;
        var atHex = WorldUnits.MicroPosition(defenderUnit);
        var field = WorldBattles.Field(Map, Settings.generation, Combat, WorldUnits.MicroPosition(attackerUnit), atHex, GameAge.Number, time != null ? time.CurrentEcho : 0);
        int seed = unchecked(Map.seed * 31 + attackerUnit.id * 7919 + defenderUnit.id * 104729 + ++_battlesFought * 15485863);
        var setup = new BattleSetup { attacker = attacker, defender = defender, field = field, seed = seed,
            majorEncounter = attackerUnit.majorEncounter || defenderUnit.majorEncounter, boss = attackerUnit.boss || defenderUnit.boss,
            decisive = attackerUnit.decisiveEncounter || defenderUnit.decisiveEncounter, originalEight = attackerUnit.originalEight || defenderUnit.originalEight,
            objective = defenderUnit.battleObjective ?? attackerUnit.battleObjective };
        ComposeBattleInputs(setup, attackerUnit, defenderUnit);
        if (WorldBattles.IsPlayers(attackerUnit) || WorldBattles.IsPlayers(defenderUnit))
        { PrepareEncounter(setup, attackerUnit, defenderUnit); notice.Add("Battle prepared: choose automatic resolution or conduct the Symphony."); return; }
        if (setup.RequiresManual) return;
        // The strengths and what made them, before a blow is struck (the battle screen's two columns).
        var record = Record(setup, attackerUnit, defenderUnit, Place(Map.Get(HexHierarchy.Parent(atHex))));
        var report = BattleResolver.Resolve(setup, Combat);
        ApplyEncounterResult(attackerUnit, defenderUnit, setup, record, report, notice);
    }

    private void ApplyEncounterResult(WorldUnit attackerUnit, WorldUnit defenderUnit, BattleSetup setup, BattleRecord record, BattleReport report, List<string> notice)
    {
        var attacker = setup.attacker; var defender = setup.defender;
        var atHex = WorldUnits.MicroPosition(defenderUnit);
        var wildUnit = !WorldBattles.IsPlayers(attackerUnit) ? attackerUnit : !WorldBattles.IsPlayers(defenderUnit) ? defenderUnit : null;
        int wildBefore = wildUnit?.creatures ?? 0;
        record.report = report;
        LastBattle = report;

        WorldUnits.Stop(attackerUnit);
        WorldUnits.Stop(defenderUnit);
        attackerUnit.truce = defenderUnit.truce = WorldBattles.TruceSevenths;
        var lines = new List<string>();
        foreach (var spent in new[] { attackerUnit, defenderUnit }.Where(u => u.winded))
            lines.Add($"{spent.name} {(WorldBattles.IsPlayers(spent) ? "was" : "were")} run down, too spent to fight well.");
        // Your legends carry the battle home (before the parties scatter, so a doomed party's legends go missing first).
        // An Atonalis of any Path but Carnalix takes the ones that broke or were left behind captive, if it still stands.
        var progress = LegendProgress.Instance;
        foreach (var fate in report.legends)
        {
            var own = fate.attacker ? attackerUnit : defenderUnit;
            var foe = fate.attacker ? defenderUnit : attackerUnit;
            var foeSide = fate.attacker ? defender : attacker;
            if (progress == null || !WorldBattles.IsPlayers(own)) continue;
            if (fate.dead) { progress.ApplyBattleFate(fate); lines.Add($"{fate.name} died in battle."); continue; }
            bool captor = foe.identity == BandIdentity.Atonalis && AtonalPaths.Of(foe.atonalPath).captures && foeSide.sections.Any(s => s.Standing && s.Alive > 0);
            if (fate.captured || captor && (fate.missing || fate.mindBroken && !fate.won))
            {
                float strain = fate.strain;
                fate.strain = 0f;
                fate.missing = false;
                progress.ApplyBattleFate(fate);
                progress.TakeCaptive(fate.name, CaptorKey(foe), foe.identity == BandIdentity.Atonalis ? AtonalPaths.Of(foe.atonalPath).feedOnCaptive : 0f, strain, foe.name);
                (foe.captives = foe.captives ?? new List<string>()).Add(fate.name);
                lines.Add($"{fate.name} is dragged away by {foe.name}.");
                continue;
            }
            progress.ApplyBattleFate(fate);
        }
        var battleCell = Map.Get(HexHierarchy.Parent(atHex));
        int fallen = attacker.sections.Concat(defender.sections).Sum(s => Math.Max(0, s.count - (s.destroyed ? 0 : s.Alive)));
        Aftermath(attackerUnit, attacker, report, true, lines, defenderUnit);
        Aftermath(defenderUnit, defender, report, false, lines, attackerUnit);
        // A creature band that lost creatures to one of your parties: what fell or was taken is the hunt's.
        var hunter = wildUnit == null ? null : wildUnit == attackerUnit ? defenderUnit : attackerUnit;
        bool hunt = hunter != null && WorldBattles.IsPlayers(hunter) && wildUnit.identity == BandIdentity.Creature;
        if (hunt && Map.Units.Contains(hunter))
            HuntSpoils(hunter, wildUnit, wildBefore - (Map.Units.Contains(wildUnit) ? wildUnit.creatures : 0), lines);
        // A Carnalix drains what it fought: deeper wounds and strain, and it grows on what it took.
        Drain(attackerUnit, defenderUnit, lines);
        Drain(defenderUnit, attackerUnit, lines);
        // The field remembers: a battle leaves suffering (a hunt's is slaughter, any other fight's violence).
        if (battleCell != null)
        {
            // Fear on the field (a hunt's is the quarry's), and the body's pain for everyone who fell.
            AddSuffering(battleCell.index, Feeling.Dread, hunt ? WorldSuffering.BattleDread * 0.5f : WorldSuffering.BattleDread);
            AddSuffering(battleCell.index, Feeling.Pain, (hunt ? WorldSuffering.HuntPain : 0f) + WorldSuffering.PerSlain * fallen);
            // And the healthy feeling of those who stood: courage in a fight, a hunt's meal won.
            AddSuffering(battleCell.index, hunt ? Feeling.Vitality : Feeling.Courage, hunt ? WorldSuffering.HuntVitality : WorldSuffering.BattleCourage);
        }
        // Either way the chase is over.
        foreach (var u in new[] { attackerUnit, defenderUnit })
        {
            u.quarryId = -1;
            u.sprinting = false;
            u.chaseTiles = 0;
        }
        foreach (var captive in report.captives.Where(c => WorldBattles.IsPlayers(c.takenByAttacker ? attackerUnit : defenderUnit)))
        {
            lines.Add($"{captive.individuals} of {captive.name} are taken alive.");
            CaptiveTaken?.Invoke(captive);
        }

        string where = Place(battleCell);
        // Told from your side when one of the two is yours: "Decisive Victory at the Ash Plains".
        bool? yours = record.yours;
        var verdict = report.OutcomeFor(yours ?? true);
        string summary = yours != null
            ? $"{BattleReport.Words(verdict)} at {where}: {(yours.Value ? attacker.name : defender.name)} against {(yours.Value ? defender.name : attacker.name)} ({record.preview.Side(yours.Value).Strength} against {record.preview.Side(!yours.Value).Strength})."
            : $"{BattleReport.Words(report.outcome)} for {attacker.name} against {defender.name} at {where}.";
        notice.Add(summary);
        NotificationFeed.Push($"{BattleReport.Words(verdict)} at {where}", summary + $"\n{BattleVerdicts.Meaning(verdict)}" + (lines.Count > 0 ? "\n" + string.Join("\n", lines) : string.Empty),
            NotificationFeed.Topic.World, key: $"battle:{_battlesFought}");
        GameLog.Event($"Battle {_battlesFought} at {where}: {string.Join(" ", report.log.Skip(Math.Max(0, report.log.Count - 3)))}", Log);
        record.aftermath = lines;
        LastBattleRecord = record;
        BattleFought?.Invoke(report, attackerUnit, defenderUnit);
        BattleRecorded?.Invoke(record);
    }

    // The key a captive legend's record holds for the band that took it.
    private static string CaptorKey(WorldUnit band) => "band:" + band.id;

    // An Atonalis's Path takes a share of the other side's Composure before the first blow.
    private static void Bite(WorldUnit biter, BattleSide victim)
    {
        if (biter == null || victim == null || biter.identity != BandIdentity.Atonalis) return;
        float bite = AtonalPaths.Of(biter.atonalPath).nerveBite;
        if (bite <= 0f) return;
        foreach (var s in victim.sections) s.composure *= 1f - bite;
    }

    // A Carnalix that still stands after fighting one of your parties drains it: wounds and strain beyond the battle's,
    // and it grows by one on what it took (it is never sated).
    private void Drain(WorldUnit carnalix, WorldUnit victim, List<string> lines)
    {
        if (carnalix == null || victim == null || !Map.Units.Contains(carnalix) || carnalix.creatures <= 0) return;
        if (carnalix.identity != BandIdentity.Atonalis || !AtonalPaths.Of(carnalix.atonalPath).drains || !WorldBattles.IsPlayers(victim)) return;
        if (Map.Units.Contains(victim))
        {
            victim.attrition = Math.Min(100f, victim.attrition + 12f);
            var progress = LegendProgress.Instance;
            if (progress != null) foreach (string legend in Expeditions.Members(victim).ToList()) progress.Strain(legend, 4f, $"drained by {carnalix.name}");
        }
        carnalix.creatures = Math.Min(8, carnalix.creatures + 1);
        Rename(carnalix);
        lines.Add($"{carnalix.name} drains {victim.name} and grows on what it took.");
    }

    // What the battle leaves of one side's map unit (<paramref name="foe"/>: whom it fought).
    private void Aftermath(WorldUnit unit, BattleSide side, BattleReport report, bool attacker, List<string> lines, WorldUnit foe)
    {
        bool lost = attacker ? report.winner <= 0 : report.winner > 0;
        bool beaten = attacker ? report.winner < 0 : report.winner > 0;
        if (!string.IsNullOrEmpty(unit.armyStack))
        {
            // An army: its roster written back; beaten, it falls back toward your settlements.
            lines.AddRange(Army.Record(unit.armyStack, side, report, attacker, _bank, Combat));
            var stack = Army.Stack(unit.armyStack);
            if (stack == null || stack.units.Count == 0)
            {
                lines.Add($"{unit.name} is no more.");
                RemoveFromMap(unit);
                return;
            }
            if (lost) FallBack(unit, lines);
            return;
        }
        if (WorldBattles.IsPlayers(unit))
        {
            PartyAftermath(unit, side, beaten, lines, report.population.Where(f => f.attacker == attacker).ToList());
            return;
        }
        // An enemy band keeps its survivors; slain Atonalis leave a Rose Seed behind, a slain Formless Mass nothing at all.
        int survivors = side.sections.Where(s => !s.destroyed && !s.captured).Sum(s => s.Alive);
        int slain = side.sections.Where(s => s.destroyed).Sum(s => s.count);
        if (unit.identity == BandIdentity.Atonalis && slain > 0)
            lines.Add($"{slain} {(slain == 1 ? "falls" : "fall")} to static and leave{(slain == 1 ? "s" : "")} a Rose Seed behind.");
        if (unit.identity == BandIdentity.FormlessMass && survivors <= 0)
            lines.Add(unit.bandActivity == BandActivity.Cocooned ? "The cocoon bursts empty: no Atonalis will hatch from it." : "The Formless Mass sinks back into the ground, spent.");
        unit.creatures = survivors;
        if (survivors <= 0)
        {
            FreeCaptives(unit, foe != null && WorldBattles.IsPlayers(foe) ? foe.name : null, lines);
            RemoveFromMap(unit);
            return;
        }
        Rename(unit);
        if (lost)
        {
            // Beaten, it limps home and leaves you be for a while.
            unit.truce = Math.Max(unit.truce, WorldBattles.TruceSevenths * 2f);
            unit.provoked = 0f;
            unit.chaseCooldown = WorldPursuit.ChaseCooldown;
            unit.bandActivity = BandActivity.Returning;
        }
    }

    // A band that held legends of yours is gone: they are free and make for home.
    private void FreeCaptives(WorldUnit band, string by, List<string> lines)
    {
        if (band?.captives == null || band.captives.Count == 0) return;
        var progress = LegendProgress.Instance;
        foreach (string legend in band.captives.ToList())
        {
            if (progress == null || progress.CaptorOf(legend) != CaptorKey(band)) continue;
            progress.Free(legend, 2, by);
            lines?.Add($"{legend} is free and makes for home.");
        }
        band.captives.Clear();
    }

    // A party of yours that is not an army: beaten, it scatters (its legends are already missing; surviving settlers walk
    // home, the fallen are counted); otherwise it carries its wounds (attrition) and lost nerve into its next battle.
    private void PartyAftermath(WorldUnit unit, BattleSide side, bool beaten, List<string> lines, List<BattlePopulationFate> population)
    {
        var settlers = side.sections.Where(s => s.kind == SectionKind.Support && s.count > 0 && s.name.StartsWith("Settlers")).ToList();
        int settlersHome = settlers.Where(s => !s.captured).Sum(s => s.Alive);
        int settlersHeld = settlers.Where(s => s.captured).Sum(s => s.Alive);
        var settlerFates = population.Where(f => settlers.Any(s => s.name == f.section)).ToList();
        int recordedDead = settlerFates.Sum(f => f.dead);
        if (settlerFates.Count > 0)
        {
            settlersHome = settlerFates.Sum(f => f.healthy + f.wounded + f.recoverable); settlersHeld = settlerFates.Sum(f => f.captured);
            lines.Add($"Settlers: {recordedDead} dead, {settlerFates.Sum(f => f.wounded)} wounded, {settlerFates.Sum(f => f.recoverable)} recoverable, {settlerFates.Sum(f => f.missing)} missing, {settlersHeld} captured.");
        }
        if (beaten)
        {
            int home = settlersHome;
            int fallen = settlerFates.Count > 0 ? recordedDead : unit.settlers - home - settlersHeld;
            if (home > 0 && PopGrowthLogic.Instance != null) PopGrowthLogic.Instance.Discharge(home, unit.name);
            if (fallen > 0 && PopGrowthLogic.Instance != null) PopGrowthLogic.Instance.ReviseDeathRecords(fallen);
            lines.Add($"{unit.name} is scattered{(home > 0 ? $"; {home} settler{(home == 1 ? "" : "s")} find their way home" : string.Empty)}.");
            // Those lost and scattered leave grief on the ground where it happened.
            if (Map.Get(unit.coord) is WorldTile scatteredAt)
            {
                AddSuffering(scatteredAt.index, Feeling.Tumult, WorldSuffering.ScatterGrief);
                AddSuffering(scatteredAt.index, Feeling.Estrangement, WorldSuffering.ScatterGrief * 0.6f);
            }
            RemoveFromMap(unit);
            return;
        }
        // A legend whose section fell left the field alone: it is no longer with the party.
        var progress = LegendProgress.Instance;
        if (progress != null)
        {
            unit.companions?.RemoveAll(name => progress.IsMissing(name) || progress.IsLost(name));
            if (unit.leader != null && (progress.IsMissing(unit.leader) || progress.IsLost(unit.leader)))
            {
                unit.leader = unit.companions != null && unit.companions.Count > 0 ? unit.companions[0] : null;
                if (unit.leader != null) unit.companions.RemoveAt(0);
            }
        }
        float max = side.sections.Sum(s => s.maxIntegrity), left = side.sections.Where(s => !s.captured).Sum(s => Math.Max(0f, s.integrity) + s.wounded);
        float maxNerve = side.sections.Sum(s => s.maxComposure), nerve = side.sections.Sum(s => Math.Max(0f, s.composure));
        if (max > 0f) unit.attrition = Math.Max(unit.attrition, Math.Min(100f, 100f * (1f - left / max)));
        if (maxNerve > 0f) unit.nerveLost = Math.Max(0f, Math.Min(1f, 1f - nerve / maxNerve));
        if (settlers.Count > 0 && unit.settlers > settlersHome)
        {
            int fallen = settlerFates.Count > 0 ? recordedDead : Math.Max(0, unit.settlers - settlersHome - settlersHeld);
            unit.settlers = settlersHome;
            if (PopGrowthLogic.Instance != null) PopGrowthLogic.Instance.ReviseDeathRecords(fallen);
            lines.Add($"{unit.name} lost {fallen} settler{(fallen == 1 ? "" : "s")}.");
        }
    }

    private void FallBack(WorldUnit unit, List<string> lines)
    {
        if (!WorldUnits.WayToResupply(Map, Settings.generation, unit, out var path, out _) || path.Count == 0) return;
        WorldUnits.Order(Map, unit, path);
        unit.retreating = true;
        lines.Add($"{unit.name} falls back toward your settlements.");
    }

    private void RemoveFromMap(WorldUnit unit)
    {
        // Whatever took it off the map, the legends it held go free.
        FreeCaptives(unit, null, null);
        Map.Units.Remove(unit);
        _partySpecs.Remove(unit.id);
    }
}
