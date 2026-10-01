using System;
using System.Collections.Generic;
using System.Linq;

public static partial class BattleResolver
{
    public sealed partial class BattleRun
    {
        private readonly Dictionary<Side, HashSet<int>> pollutionDrawn = new Dictionary<Side, HashSet<int>>();

        public IReadOnlyList<DeckCard> DrawPile(bool attacker) => Pile(Of(attacker), false);
        public IReadOnlyList<DeckCard> DiscardPile(bool attacker) => Pile(Of(attacker), true);
        private static IReadOnlyList<DeckCard> Pile(Side s, bool discard) => s.perf == null ? Array.Empty<DeckCard>() :
            (IReadOnlyList<DeckCard>)(discard ? s.perf.discard : s.perf.draw).Select(i => s.perf.deck[i]).ToList();

        public string Pollute(bool attacker, int section, BattlePollution kind, int copies = 1, bool intoDiscard = false)
        {
            var s = Of(attacker); var track = TrackOf(s, section);
            if (kind == BattlePollution.None || copies < 1 || copies > 16 || track?.unit?.Standing != true) return "Choose a standing voice and one to sixteen Status cards.";
            if (s.perf == null) s.perf = new Performance { deck = new List<DeckCard>(), rng = new CombatRandom(report.seed + section) };
            var p = s.perf;
            for (int c = 0; c < copies; c++)
            {
                int index = p.deck.Count; p.deck.Add(new DeckCard { card = BattlePollutionCards.Of(kind), voice = section, legend = LegendOf(track), source = "Status Pollution" });
                if (intoDiscard) p.discard.Add(index); else { p.draw.Insert(p.draw.Count == 0 ? 0 : (int)(p.rng.NextFloat() * (p.draw.Count + 1)), index); }
            }
            return null;
        }

        private string Purge(bool attacker, int section, BattlePollution kind = BattlePollution.None)
        {
            var s = Of(attacker); if (s.perf == null || TrackOf(s, section) == null) return "No such voice.";
            var p = s.perf;
            var indices = p.hand.Concat(p.draw).Concat(p.discard).Distinct().Where(i => p.deck[i].voice == section && p.deck[i].card.pollution != BattlePollution.None &&
                (kind == BattlePollution.None || p.deck[i].card.pollution == kind)).ToList();
            foreach (int i in indices) { p.hand.Remove(i); p.draw.Remove(i); p.discard.Remove(i); p.exhausted.Add(i); }
            return null;
        }

        private void PollutionDraw(Side s)
        {
            if (s.perf == null) return;
            if (!pollutionDrawn.TryGetValue(s, out var seen)) pollutionDrawn[s] = seen = new HashSet<int>();
            foreach (int i in s.perf.hand.Where(i => s.perf.deck[i].card.pollution != BattlePollution.None).ToList())
            {
                if (!seen.Add(i)) continue; var dc = s.perf.deck[i]; var unit = VoiceOf(s, dc); if (unit?.Standing != true) continue;
                switch (dc.card.pollution)
                {
                    case BattlePollution.Burn: Resolve(unit, 3, 0, false, null); break;
                    case BattlePollution.Bleeding: Resolve(unit, 2, 0, false, null); break;
                    case BattlePollution.Panic: unit.composure -= 5; break;
                }
            }
        }

        private bool StatusInHand(Side s, CombatSection unit, BattlePollution kind) => s.perf != null && s.perf.hand.Any(i => s.perf.deck[i].card.pollution == kind && VoiceOf(s, s.perf.deck[i]) == unit);
        private int StatusCount(Side s, CombatSection unit, BattlePollution kind) => s.perf == null ? 0 : s.perf.hand.Count(i => s.perf.deck[i].card.pollution == kind && VoiceOf(s, s.perf.deck[i]) == unit);
        private void PollutionDiscard(Side s, int index)
        {
            var dc = s.perf.deck[index]; var unit = VoiceOf(s, dc);
            if (dc.card.pollution == BattlePollution.Agony && unit?.Standing == true) unit.composure -= 3;
        }

        private void RecordDeckEvolution()
        {
            foreach (var fate in report.legends)
            {
                var s = Of(fate.attacker); var unit = s.side.sections.FirstOrDefault(x => x.leader?.name == fate.name && x.eliteRole != BattleEliteRole.None);
                var legend = unit?.leader ?? (s.conductor?.name == fate.name ? s.conductor : null);
                if (legend == null) continue;
                fate.deckEvolution = (legend.deckEvolution ?? new BattleDeckEvolution()).Clone();
                if (unit != null && s.perf != null)
                {
                    int voice = s.side.sections.IndexOf(unit);
                    fate.deckEvolution.pollution = s.perf.hand.Concat(s.perf.draw).Concat(s.perf.discard).Distinct()
                        .Where(i => s.perf.deck[i].voice == voice && s.perf.deck[i].card.pollution != BattlePollution.None && !s.perf.exhausted.Contains(i))
                        .Select(i => s.perf.deck[i].card.pollution).ToList();
                    if (unit.timesMindBroken > 0)
                    {
                        string card = s.side.deck.FirstOrDefault(c => c.voice == voice && c.card?.purpose == SpellPurpose.Offensive)?.card.id;
                        if (card != null && !fate.deckEvolution.alterations.Any(a => a.card == card))
                            fate.deckEvolution.alterations.Add(new BattleDeckAlteration { card = card, name = "Defiance under pressure", trauma = unit.timesMindBroken > 1 });
                        else if (unit.timesMindBroken > 1)
                            foreach (var alteration in fate.deckEvolution.alterations.Where(a => a.card == card && !a.opus)) alteration.trauma = true;
                    }
                }
            }
        }
    }
}
