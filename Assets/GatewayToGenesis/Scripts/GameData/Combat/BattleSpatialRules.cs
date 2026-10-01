using System;
using System.Collections.Generic;
using System.Linq;

public enum BattleProximity { Any, Nearest, Furthest, SameLane, AdjacentAlly, ForwardMost, RearMost, NeutralContact }
public enum BattleSpatialCause
{
    Movement, SkirmishLost, ForcedRetreat, Stacking, ForcedDisplacement, Flanking,
    CriticalPositionLost, EliteCasualty, ConductorDisrupted, SevereComposureShock,
    TerrainIncompatibility, FriendlyFire, Surrounded, MindBreak, AdjacentFormationCollapse,
    FailedMovementCommitment, StanceBreak, Rout, Subjugation, PreparationFailed, GuardLost,
    Contact, Clash, Interruption,
}

/// <summary>Provisional numbers where Combat System.md specifies a rule but no magnitude.</summary>
[Serializable]
public sealed class BattleSpatialTuning
{
    public int meleeReach = 1, rangedReach = 3, spellReach = 4, supportReach = 1, highGroundRange = 1;
    public float stackedAttack = .35f, stackedDefense = .35f;
    public float counterBonus = .35f, spearheadPressure = .15f, lineGuard = .15f, crescentFlank = .15f;
    public float lineResilience = .85f, flankAttack = .08f;
    public float retreatShock = 10f, eliteFallShock = .2f, stanceBreakShock = .25f, severeShockShare = .25f;
    public float skirmishLoss = 12f, forcedRetreat = 10f, stacking = 5f, forcedDisplacement = 10f;
    public float flanking = 6f, criticalPositionLoss = 10f, eliteCasualty = 18f, conductorDisruption = 25f;
    public float severeShock = 10f, terrainIncompatibility = 4f, friendlyFire = 10f, surrounded = 10f;
    public float mindBreak = 10f, adjacentCollapse = 8f, failedCommitment = 8f;

    public float Damage(BattleSpatialCause cause)
    {
        switch (cause)
        {
            case BattleSpatialCause.SkirmishLost: return skirmishLoss;
            case BattleSpatialCause.ForcedRetreat: return forcedRetreat;
            case BattleSpatialCause.Stacking: return stacking;
            case BattleSpatialCause.ForcedDisplacement: return forcedDisplacement;
            case BattleSpatialCause.Flanking: return flanking;
            case BattleSpatialCause.CriticalPositionLost: return criticalPositionLoss;
            case BattleSpatialCause.EliteCasualty: return eliteCasualty;
            case BattleSpatialCause.ConductorDisrupted: return conductorDisruption;
            case BattleSpatialCause.SevereComposureShock: return severeShock;
            case BattleSpatialCause.TerrainIncompatibility: return terrainIncompatibility;
            case BattleSpatialCause.FriendlyFire: return friendlyFire;
            case BattleSpatialCause.Surrounded: return surrounded;
            case BattleSpatialCause.MindBreak: return mindBreak;
            case BattleSpatialCause.AdjacentFormationCollapse: return adjacentCollapse;
            case BattleSpatialCause.PreparationFailed:
            case BattleSpatialCause.FailedMovementCommitment: return failedCommitment;
            default: return 0f;
        }
    }
}

public sealed class BattleSpatialEvent
{
    public int measure, beat, from = -1, to = -1;
    public bool attacker;
    public BattleSpatialCause cause;
    public string section, detail;
    public float stabilityDamage;
}

public sealed class BattlePosition
{
    public bool attacker, standing, mindBroken;
    public int section, hex;
}

public sealed class BattleMovementIntent
{
    public bool attacker;
    public int section, from, to, beat;
}

/// <summary>Shared geometry and doctrine rules. Queries never consume RNG or mutate combatants.</summary>
public sealed class BattleSpatialRules
{
    public BattleSpatialState State { get; }
    public BattleSpatialTuning Tuning { get; }
    public BattleSpatialRules(BattleSpatialState state, BattleSpatialTuning tuning)
    { State = state; Tuning = tuning ?? new BattleSpatialTuning(); }

    public bool Passable(int hex) => !State.Terrain[hex].blocked && !State.Terrain[hex].wall;
    public int Distance(int from, int to)
    {
        if (!Passable(from) || !Passable(to)) return int.MaxValue;
        var q = new Queue<int>();
        var distances = Enumerable.Repeat(-1, BattleHexLayout.HexCount).ToArray();
        q.Enqueue(from); distances[from] = 0;
        while (q.Count > 0)
        {
            int h = q.Dequeue();
            if (h == to) return distances[h];
            foreach (int n in BattleHexLayout.Adjacent(h))
                if (Passable(n) && distances[n] < 0) { distances[n] = distances[h] + 1; q.Enqueue(n); }
        }
        return int.MaxValue;
    }

    public bool Support(CombatSection source, CombatSection target, int range = -1, int from = -1) =>
        source != null && source.Standing && source.integrity > 0f && target != null && target.Standing &&
        Distance(from >= 0 ? from : source.battleHex, target.battleHex) <= (range >= 0 ? range : Tuning.supportReach);

    public int Reach(CombatSection source, bool spell = false)
    {
        int reach = spell ? Tuning.spellReach : source?.row == FormationRow.Back ? Tuning.rangedReach : Tuning.meleeReach;
        if (source != null && source.row == FormationRow.Back && State.Terrain[source.battleHex].highGround) reach += Tuning.highGroundRange;
        return reach;
    }

    public bool Protected(CombatSection target, bool targetAttacker, int from, Func<CombatSection, int> position = null)
    {
        if (target.row == FormationRow.Front || State.Side(targetAttacker).StanceBroken) return false;
        int Hex(CombatSection x) => position?.Invoke(x) ?? x.battleHex;
        return State.Side(targetAttacker).Standing.Any(g => g != target && g.row == FormationRow.Front && !g.mindBroken &&
            !State.IsStacked(Hex(g), targetAttacker) && BattleHexLayout.AreAdjacent(Hex(g), Hex(target)) &&
            Distance(from, Hex(g)) < Distance(from, Hex(target)));
    }

    public List<CombatSection> Targets(bool attacker, CombatSection source, bool enemy, int range,
        BattleProximity proximity = BattleProximity.Any, bool ignoreGuard = false, int from = -1, Func<CombatSection, int> position = null)
    {
        if (source == null || !source.Standing) return new List<CombatSection>();
        int origin = from >= 0 ? from : source.battleHex;
        bool targetSide = enemy ? !attacker : attacker;
        int Hex(CombatSection x) => position?.Invoke(x) ?? x.battleHex;
        var pool = State.Side(targetSide).Standing.Where(x => Distance(origin, Hex(x)) <= range &&
            (!enemy || ignoreGuard || !Protected(x, targetSide, origin, position))).ToList();
        var lane = BattleHexLayout.At(origin).Lane;
        if (proximity == BattleProximity.SameLane) pool = pool.Where(x => BattleHexLayout.At(Hex(x)).Lane == lane).ToList();
        if (proximity == BattleProximity.AdjacentAlly) pool = enemy ? new List<CombatSection>() : pool.Where(x => x != source && BattleHexLayout.AreAdjacent(origin, Hex(x))).ToList();
        if (proximity == BattleProximity.NeutralContact) pool = pool.Where(x => BattleHexLayout.At(Hex(x)).Territory == BattleTerritory.Neutral ||
            BattleHexLayout.Adjacent(Hex(x)).Any(n => BattleHexLayout.At(n).Territory == BattleTerritory.Neutral)).ToList();
        if (pool.Count == 0) return pool;
        if (proximity == BattleProximity.Nearest || proximity == BattleProximity.Furthest)
        {
            int distance = proximity == BattleProximity.Nearest ? pool.Min(x => Distance(origin, Hex(x))) : pool.Max(x => Distance(origin, Hex(x)));
            pool = pool.Where(x => Distance(origin, Hex(x)) == distance).ToList();
        }
        if (proximity == BattleProximity.ForwardMost || proximity == BattleProximity.RearMost)
        {
            int Progress(CombatSection x) => BattleHexLayout.At(Hex(x)).X * (targetSide ? 1 : -1);
            int p = proximity == BattleProximity.ForwardMost ? pool.Max(Progress) : pool.Min(Progress);
            pool = pool.Where(x => Progress(x) == p).ToList();
        }
        return pool;
    }

    public string WhyNotMove(bool attacker, CombatSection section, int to)
    {
        if (section == null || !section.Standing) return "The combatant is no longer on the field.";
        if (to < 0 || to >= BattleHexLayout.HexCount || !Passable(to)) return "That hex is blocked.";
        if (!BattleHexLayout.AreAdjacent(section.battleHex, to)) return "Movement must cross one shared hex edge.";
        if (State.IsEngaged(section.battleHex) && !Homeward(section.battleHex, to, attacker)) return "An engaged combatant must withdraw toward home before advancing.";
        if (State.IsEngaged(section.battleHex) && State.Occupants(to, !attacker).Any()) return "An enemy blocks that withdrawal.";
        return null;
    }

    public bool Homeward(int from, int to, bool attacker) =>
        (BattleHexLayout.At(to).X - BattleHexLayout.At(from).X) * (attacker ? 1 : -1) < 0;
    public List<int> Retreats(bool attacker, CombatSection section) => BattleHexLayout.Adjacent(section.battleHex)
        .Where(h => Homeward(section.battleHex, h, attacker) && Passable(h) && !State.Occupants(h, !attacker).Any())
        .OrderBy(h => State.Occupants(h, attacker).Any()).ThenBy(h => BattleHexLayout.At(h).Lane != BattleHexLayout.At(section.battleHex).Lane)
        .ThenBy(h => h).ToList();

    public bool Surrounded(bool attacker, CombatSection section) => BattleHexLayout.Adjacent(section.battleHex)
        .All(h => !Passable(h) || State.Occupants(h, !attacker).Any());

    public bool Flanked(bool attacker, CombatSection section)
    {
        var enemies = State.Side(!attacker).Standing.Where(e => e.battleHex == section.battleHex || BattleHexLayout.AreAdjacent(e.battleHex, section.battleHex)).ToList();
        return enemies.Any(e => Homeward(section.battleHex, e.battleHex, attacker)) || enemies.Select(e => BattleHexLayout.At(e.battleHex).Lane).Distinct().Count() >= 2;
    }

    public bool DoctrineHeld(bool attacker)
    {
        var side = State.Side(attacker);
        var front = side.Standing.Where(x => x.row == FormationRow.Front).ToList();
        if (side.StanceBroken || front.Count == 0 || front.Any(x => x.mindBroken)) return false;
        var positions = front.Select(x => BattleHexLayout.At(x.battleHex)).ToList();
        if (positions.Max(h => h.X) - positions.Min(h => h.X) > 2) return false;
        switch (side.stance)
        {
            case BattleStance.Spearhead: return positions.All(h => h.Lane == BattleLane.Middle);
            case BattleStance.Crescent: return positions.Any(h => h.Lane == BattleLane.Top) && positions.Any(h => h.Lane == BattleLane.Bottom) &&
                new[] { 2, 13 }.Any(Passable);
            default: return positions.Select(h => h.Lane).Distinct().Count() >= Math.Min(3, front.Count) &&
                positions.All(h => positions.Count == 1 || positions.Any(n => n.Id != h.Id && BattleHexLayout.AreAdjacent(n.Id, h.Id)));
        }
    }

    public float AttackFactor(bool attacker, CombatSection section)
    {
        float factor = State.IsStacked(section.battleHex, attacker) ? Tuning.stackedAttack : 1f;
        var side = State.Side(attacker);
        if (DoctrineHeld(attacker))
        {
            if (side.stance == BattleStance.Spearhead) factor *= 1f + Tuning.spearheadPressure;
            if (DoctrineHeld(!attacker) && BattleStances.Counters(side.stance, State.Side(!attacker).stance)) factor *= 1f + Tuning.counterBonus;
        }
        return factor;
    }

    public float TargetAttackFactor(bool attacker, CombatSection target) => Flanked(!attacker, target) ?
        (1f + Tuning.flankAttack) * (DoctrineHeld(attacker) && State.Side(attacker).stance == BattleStance.Crescent ? 1f + Tuning.crescentFlank : 1f) : 1f;

    public float DefenseFactor(bool attacker, CombatSection section)
    {
        float factor = State.IsStacked(section.battleHex, attacker) ? Tuning.stackedDefense : 1f;
        if (DoctrineHeld(attacker) && State.Side(attacker).stance == BattleStance.Line &&
            State.Side(attacker).Standing.Any(x => x != section && !x.mindBroken && BattleHexLayout.AreAdjacent(x.battleHex, section.battleHex))) factor *= 1f + Tuning.lineGuard;
        return factor;
    }
}
