using System;
using System.Collections.Generic;
using System.Linq;

public static partial class BattleResolver
{
    private sealed class Veil { public bool attacker; public int hex, strength, expires; }
    public sealed partial class BattleRun
    {
        private readonly List<Veil> veils = new List<Veil>();
        private readonly List<BattleActionIntent> phantoms = new List<BattleActionIntent>();
        public int SeerVoice(bool attacker)
        {
            var side = Of(attacker).side;
            if (side.seerVoice < 0) side.seerVoice = side.sections.Select((unit, index) => new { unit, index })
                .Where(x => x.unit.eliteRole != BattleEliteRole.None && x.unit.leader != null && x.unit.Standing)
                .OrderByDescending(x => x.unit.leader.Score(SpellBinding.Luminance)).Select(x => x.index).DefaultIfEmpty(-1).First();
            return side.seerVoice;
        }
        public string DesignateSeer(bool attacker, int voice)
        {
            var side = Of(attacker).side;
            if (!open || m != 1 || MeasureBeat != 0 || scoresCommitted) return "Designate the Seer during the first Measure's Composition.";
            if (voice < 0 || voice >= side.sections.Count || !side.sections[voice].Standing || side.sections[voice].leader == null || side.sections[voice].eliteRole == BattleEliteRole.None)
                return "Choose a present elite with a Soul Leitmotif.";
            side.seerVoice = voice; return null;
        }
        public int Clarity(bool attacker)
        {
            var side = Of(attacker).side; int voice = SeerVoice(attacker);
            if (voice < 0 || voice >= side.sections.Count) return 0;
            var seer = side.sections[voice];
            if (!seer.Standing || seer.mindBroken || seer.deathKnell || seer.leader == null) return 0;
            int rank = BattleMeasureMath.Mastery(seer.leader.Score(SpellBinding.Luminance));
            if (Of(!attacker).side.Standing.Any(enemy => enemy.battleHex == seer.battleHex)) rank--;
            return Math.Max(0, rank);
        }
        public int IntentClarity(bool observer, bool owner, int voice, int from = -1)
        {
            int rank = Clarity(observer); int veil = IntentVeil(owner, voice, from);
            return rank >= 4 ? rank : rank - Math.Max(0, veil - (rank >= 3 ? 1 : 0));
        }
        private int IntentVeil(bool owner, int voice, int from)
        {
            var unit = voice >= 0 && voice < Of(owner).side.sections.Count ? Of(owner).side.sections[voice] : null;
            int hex = unit?.battleHex ?? from;
            int local = hex < 0 ? 0 : Math.Max(0, Spatial.Terrain[hex].veil);
            int camouflage = veils.Where(v => v.attacker == owner && v.hex == hex && v.expires >= beat).Select(v => v.strength).DefaultIfEmpty(0).Max();
            int terrain = field.ground == BattleGround.Forest ? 1 : field.concealed ? 1 : 0;
            return Math.Max(local, Math.Max(camouflage, terrain));
        }
        public IReadOnlyList<BattleActionIntent> IntentsFor(bool attacker) => BattleIntentReading.Read(Countdowns.Concat(phantoms.Where(p => p.dueBeat >= beat)), attacker, Clarity(attacker),
            intent => Clarity(attacker) - IntentClarity(attacker, intent.attacker, intent.voice, intent.from));
        private void ConcealIntent(Side side, CombatSection unit, int strength, int duration)
        { if (unit?.Standing == true) veils.Add(new Veil { attacker = side.attacker, hex = unit.battleHex, strength = Math.Max(0, strength), expires = beat + Math.Max(1, duration) }); }
        private void Phantom(Side side, CombatSection unit, CardEffect effect)
        {
            if (unit?.Standing != true) return;
            phantoms.Add(new BattleActionIntent { id = -phantoms.Count - 1L, attacker = side.attacker, voice = side.side.sections.IndexOf(unit), from = unit.battleHex,
                target = -1, name = "Unverified working", phantom = true, confirmed = false, dueBeat = beat + Math.Max(1, (int)effect.amount), kind = BattleActionKind.Card });
        }
    }
}
