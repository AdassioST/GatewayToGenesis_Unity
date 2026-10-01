using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>A native position, expressed from either army's perspective.</summary>
[Serializable]
public sealed class BattleDeploymentSlot
{
    public FormationRow role;
    public BattleLane lane;
    public int rank = 1;
}

/// <summary>Authorable initial advance. Repeated slots deliberately concentrate sections; empty positions stay empty.</summary>
[Serializable]
public sealed class BattleDoctrineSpec
{
    public BattleCommandCulture commandCulture;
    public string id, name, technology;
    public int minAge;
    public BattleStance stance;
    public List<BattleDeploymentSlot> slots = new List<BattleDeploymentSlot>();
    public List<string> cards = new List<string>();
}

public enum BattleCommandCulture { Standard, Disciplined, Fanatical, Decentralized }

public static class BattleDeploymentLogic
{
    // These coordinates are tunable choreography, not additional canon. The three qualitative shapes are required.
    private static readonly IReadOnlyList<BattleDoctrineSpec> defaults = BuildDefaults();
    public static IReadOnlyList<BattleDoctrineSpec> Defaults => defaults;

    private static BattleDoctrineSpec[] BuildDefaults()
    {
        var result = new List<BattleDoctrineSpec>();
        foreach (BattleStance stance in Enum.GetValues(typeof(BattleStance)))
        {
            var doctrine = new BattleDoctrineSpec { id = stance.ToString().ToLowerInvariant(), name = stance.ToString(), stance = stance };
            foreach (FormationRow role in Enum.GetValues(typeof(FormationRow)))
            {
                int rank = role == FormationRow.Front ? 1 : 2;
                var lanes = stance == BattleStance.Spearhead ? new[] { BattleLane.Middle } :
                    stance == BattleStance.Crescent ? new[] { BattleLane.Top, BattleLane.Bottom } :
                    new[] { BattleLane.Middle, BattleLane.Top, BattleLane.Bottom };
                foreach (var lane in lanes) doctrine.slots.Add(new BattleDeploymentSlot { role = role, lane = lane, rank = rank });
            }
            result.Add(doctrine);
        }
        return result.ToArray();
    }

    public static BattleDoctrineSpec Doctrine(CombatSettings settings, BattleSide side) =>
        settings?.Doctrine(side.doctrine) ?? defaults.First(d => d.stance == side.stance);

    public static void Deploy(BattleSide side, bool attacker, IReadOnlyList<BattleHexTerrain> terrain, BattleDoctrineSpec doctrine)
    {
        if (doctrine != null) side.stance = doctrine.stance;
        var available = BattleHexLayout.Native(attacker).Where(h => !terrain[h.Id].blocked && !terrain[h.Id].wall).ToList();
        if (side.Standing.Any() && available.Count == 0) throw new ArgumentException($"{side.name} has no passable native deployment hex.");
        var ordinals = new Dictionary<FormationRow, int>();
        foreach (var section in side.Standing.Where(s => s.battleHex == -1))
        {
            int rank = section.eliteRole != BattleEliteRole.None || section.row != FormationRow.Front ? 2 : 1;
            var slots = (doctrine?.slots ?? new List<BattleDeploymentSlot>()).Where(s => s != null && s.role == section.row).ToList();
            ordinals.TryGetValue(section.row, out int ordinal);
            ordinals[section.row] = ordinal + 1;
            var slot = slots.Count > 0 ? slots[ordinal % slots.Count] : null;
            BattleLane lane = section.deployment?.lane ?? slot?.lane ?? BattleLane.Middle;
            rank = section.deployment?.rank ?? (section.eliteRole == BattleEliteRole.None ? slot?.rank ?? rank : 2);
            if (rank != 1 && rank != 2) throw new ArgumentException("A native deployment rank must be 1 or 2.");
            var preferred = available.FirstOrDefault(h => h.Rank == rank && h.Lane == lane);
            if (section.deployment == null && defaults.Contains(doctrine) && doctrine.stance != BattleStance.Spearhead)
            {
                var lanes = slots.Select(s => s.lane).ToList();
                preferred = available.Where(h => h.Rank == rank && lanes.Contains(h.Lane))
                    .OrderBy(h => side.sections.Count(s => s.Standing && s.battleHex == h.Id))
                    .ThenBy(h => h.Lane == lane ? 0 : 1).FirstOrDefault();
            }
            // A blocked wing falls toward the nearest legal position, even when that position is occupied.
            // Do not spread concentrated doctrines merely to erase their deliberate stacking.
            section.battleHex = (preferred ?? available.OrderBy(h => Math.Abs((int)h.Lane - (int)lane))
                .ThenBy(h => h.Rank == rank ? 0 : 1)
                .ThenBy(h => side.sections.Count(s => s.Standing && s.battleHex == h.Id)).First()).Id;
        }
    }
}
