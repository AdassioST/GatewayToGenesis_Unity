using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>Projects real world context and authored local geometry onto the primary combat board.</summary>
public static class BattleTerrainLogic
{
    public static void Project(Battlefield field, WorldTile attacker, WorldTile defender, WorldGenSettings gen, CombatSettings settings, bool bridge = false)
    {
        var hexes = BattleHexLayout.Hexes.Select(h =>
        {
            var tile = h.Territory == BattleTerritory.Attacker ? attacker : defender;
            var ground = h.Territory == BattleTerritory.Attacker ? field.attackerGround ?? field.ground : field.ground;
            return new BattleHexTerrain
            {
                hex = h.Id, ground = ground, forest = ground == BattleGround.Forest,
                elevation = tile?.elevation ?? 0f, coherence = tile?.coherence ?? field.coherence,
                dissonance = tile?.dissonance ?? field.dissonance, leyline = tile == null ? field.leyline : tile.leylines != 0,
                highGround = h.Lane == BattleLane.Top && (WorldUnits.HighGround(tile) ||
                    (h.Territory == BattleTerritory.Attacker ? field.downhill > 0f : field.height > 0f)),
            };
        }).ToArray();
        void NativeBlockers(WorldTile tile, bool isAttacker)
        {
            if (tile == null) return;
            // Tunable projection: six surrounding micro cells supply six local deployment obstructions.
            var native = BattleHexLayout.Native(isAttacker).ToList();
            for (int i = 0; i < native.Count; i++)
                if ((tile.microBlockedMask & (1 << (i + 1))) != 0) hexes[native[i].Id].blocked = true;
        }
        NativeBlockers(attacker, true); NativeBlockers(defender, false);
        if (field.riverCrossing || bridge)
        {
            // Both Middle neutral cells form the one continuous ford/bridge; wings cannot cross this river.
            // The river spans the central strip, not the armies' native deployment positions.
            foreach (var h in BattleHexLayout.Hexes.Where(h => h.Territory == BattleTerritory.Neutral))
            {
                var terrain = hexes[h.Id];
                terrain.river = true; terrain.ford = !bridge && h.Lane == BattleLane.Middle;
                terrain.bridge = bridge && h.Lane == BattleLane.Middle;
                terrain.blocked = h.Lane != BattleLane.Middle;
                terrain.narrowPass = h.Lane == BattleLane.Middle;
            }
        }
        void Apply(IEnumerable<BattleHexTerrain> overrides, bool fromAttacker, bool nativeOnly)
        {
            var seen = new HashSet<int>();
            foreach (var authored in overrides ?? Enumerable.Empty<BattleHexTerrain>())
            {
                if (authored == null || !seen.Add(authored.hex)) throw new ArgumentException("Invalid or duplicate authored combat terrain.");
                var position = BattleHexLayout.At(authored.hex);
                // Terrain/feature authoring uses defender-native coordinates; mirror the attacker's native geometry.
                if (nativeOnly && position.Territory != BattleTerritory.Defender) continue;
                int id = fromAttacker ? Mirror(authored.hex) : authored.hex;
                var record = authored.Clone(); record.hex = id;
                // An explicitly localized Loom overrides the world cell; otherwise use its actual values.
                if (!record.overrideLoom) { record.coherence = hexes[id].coherence; record.dissonance = hexes[id].dissonance; }
                record.leyline |= hexes[id].leyline; record.harmonicChannel |= hexes[id].harmonicChannel;
                record.elevation += hexes[id].elevation;
                hexes[id] = record;
            }
        }
        Apply(gen?.Terrain(attacker?.terrain)?.combatHexes, true, true);
        Apply(gen?.Feature(attacker?.feature)?.combatHexes, true, true);
        Apply(gen?.Terrain(defender?.terrain)?.combatHexes, false, false);
        Apply(gen?.Feature(defender?.feature)?.combatHexes, false, false);
        field.hexes = hexes.ToList();
    }

    public static int Mirror(int id)
    {
        var position = BattleHexLayout.At(id);
        var lane = BattleHexLayout.Hexes.Where(h => h.Lane == position.Lane).ToList();
        return lane[lane.Count - 1 - position.Column].Id;
    }
}
