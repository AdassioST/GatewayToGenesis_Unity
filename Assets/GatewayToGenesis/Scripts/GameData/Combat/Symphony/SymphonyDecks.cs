using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// A legend's personal grimoire, with no scene state (the owner, Sept 29, 2026: every Spellweaver has one of its own).
/// It always holds the legend's Soul Leitmotif as a card (its binding's Principle), the orders of the Greats it has
/// earned, and its Ornaments once the Age allows Ornamental Magic; beyond those it holds the Symphony Cards it has
/// learned, as many as it has pages (<see cref="Pages"/>), chosen from the cards the civilization owns
/// (<see cref="Grimoire"/>). A legend that never chose any learns the cards of its own bindings by itself
/// (<see cref="Default"/>). Every number is a proposal.
/// </summary>
public static class LegendGrimoires
{
    /// <summary>Pages every legend has; each Ornament and the Awakened State add one, and every three stars in the Greats another.</summary>
    public const int BasePages = 2, StarsPerPage = 3;

    /// <summary>Awakened: it has an Ornament, or has reached the Awakened State. Only an Awakened Legend sounds a Major Note in Age 0.</summary>
    public static bool Awakened(LegendSoul soul) => soul != null && (soul.awakenedState || (soul.ornaments?.Count ?? 0) > 0);

    public static int Pages(LegendSoul soul, IReadOnlyDictionary<LegendClass, int> greats)
    {
        int stars = greats == null ? 0 : greats.Values.Sum(v => Math.Max(0, v));
        return BasePages + (soul?.ornaments?.Count ?? 0) + (soul != null && soul.awakenedState ? 1 : 0) + stars / StarsPerPage;
    }

    /// <summary>Why a legend cannot learn <paramref name="card"/> now, or null when it can.</summary>
    public static string WhyNotLearn(SymphonyCardData card, LegendSoul soul, IEnumerable<string> learned, int pages, GrimoireState owned)
    {
        if (card == null) return "No card.";
        var known = (learned ?? Enumerable.Empty<string>()).ToList();
        if (known.Any(id => string.Equals(id, card.id, StringComparison.OrdinalIgnoreCase))) return $"{card.DisplayName} is already in its grimoire.";
        if (owned == null || !owned.cards.Any(c => c != null && c.id == card.id)) return $"Your people do not know {card.DisplayName} yet.";
        if ((card.uses & CardUse.Battle) == 0) return $"{card.DisplayName} is not played in battle.";
        if (card.weight == NoteWeight.Major && !Awakened(soul)) return "Only an Awakened Legend can sound a Major Note.";
        if (known.Count >= pages) return $"Its grimoire is full ({pages} pages): forget a card first.";
        return null;
    }

    /// <summary>
    /// What a legend learns by itself when it never chose: the owned cards of its Soul Leitmotif's binding, then of its
    /// Ornaments', as many as its pages hold (Major Notes only when Awakened).
    /// </summary>
    public static List<string> Default(LegendSoul soul, GrimoireState owned, int pages)
    {
        var list = new List<string>();
        if (soul == null || owned == null) return list;
        var bindings = new[] { soul.leitmotif }.Concat(soul.ornaments ?? new List<string>()).Select(HarmonicCircle.Of).Where(b => b != SpellBinding.Unattuned).ToList();
        foreach (var b in bindings)
            foreach (var card in owned.cards.Where(c => c != null && c.binding == b && (c.uses & CardUse.Battle) != 0))
            {
                if (list.Count >= pages) return list;
                if (card.weight == NoteWeight.Major && !Awakened(soul)) continue;
                if (!list.Contains(card.id)) list.Add(card.id);
            }
        return list;
    }

    /// <summary>
    /// The cards a legend brings to battle in <paramref name="age"/>: its leitmotif (a Major Note once Awakened), its
    /// Ornaments when the Age allows Ornamental Magic, and its learned cards (<paramref name="learned"/> turns an id into a
    /// card). Its Greats' orders are the commander's (<see cref="Commands"/>).
    /// </summary>
    public static List<(CombatCard card, bool major)> Cards(BattleLegend legend, int age, SymphonySettings settings, Func<string, CombatCard> learned = null)
    {
        var list = new List<(CombatCard, bool)>();
        if (legend == null) return list;
        settings = settings ?? new SymphonySettings();
        bool major = legend.awakened && (AgeMagic.MajorNotes(age) || AgeCapabilities.IsAvailable(AgeCapabilities.UnisonMajorAwakened, age));
        var own = settings.Card(SymphonyCards.LeitmotifId(legend.leitmotif));
        if (own != null) list.Add((own, major));
        if (AgeCapabilities.IsAvailable(AgeCapabilities.OrnamentalMagic, age))
            foreach (var ornament in (legend.ornaments ?? new List<SpellBinding>()).Where(o => o != legend.leitmotif).Distinct())
            {
                var card = settings.Card(SymphonyCards.LeitmotifId(ornament));
                if (card != null) list.Add((card, false));
            }
        foreach (string id in legend.grimoire ?? new List<string>())
        {
            var card = learned == null ? settings.Card(id) : learned(id);
            if (card == null || list.Any(c => c.Item1.id == card.id)) continue;
            if (card.weight == NoteWeight.Major && !legend.awakened) continue;
            list.Add((card, false));
        }
        return list;
    }

    /// <summary>A commander's orders: Rally to Me, and one Command per Great it holds a star in (stronger with more stars).</summary>
    public static List<(CombatCard card, float scale)> Commands(BattleLegend legend, SymphonySettings settings)
    {
        var list = new List<(CombatCard, float)>();
        if (legend == null) return list;
        settings = settings ?? new SymphonySettings();
        var rally = settings.Card(SymphonyCards.RallyToMe);
        if (rally != null) list.Add((rally, 1f));
        foreach (LegendClass great in Enum.GetValues(typeof(LegendClass)))
        {
            int stars = legend.Stars(great);
            var card = stars > 0 ? settings.Card(SymphonyCards.GreatId(great)) : null;
            if (card != null) list.Add((card, 1f + settings.Tuning.perStar * (stars - 1)));
        }
        return list;
    }
}

/// <summary>What a side's Symphony is built from beyond its sections (<see cref="SymphonyDecks.Build"/>).</summary>
public sealed class DeckSources
{
    public bool party;
    public Func<string, bool> hasTechnology;
    public BattleDoctrineSpec doctrine;
    public List<(string source, CombatCard card)> civicCards = new List<(string, CombatCard)>();
    public int age;
    public SymphonySettings symphony;
    /// <summary>A creature section's species by id (null: creatures bring no cards).</summary>
    public Func<string, SpeciesSpec> species;
    /// <summary>A band's identity (an Atonalis, a Formless Mass...).</summary>
    public BandIdentity identity = BandIdentity.Creature;
    /// <summary>The kit a party carries (null: it is not a party, or carries none).</summary>
    public ExpeditionKit kit;
    /// <summary>An army's war score: the Symphony Cards the civilization has seated in the Grimoire's Symphony seats.</summary>
    public List<CombatCard> warScore = new List<CombatCard>();
    /// <summary>A learned Symphony Card's battle card by id (null: the library's own).</summary>
    public Func<string, CombatCard> learned;
}

/// <summary>
/// Builds a side's Symphony (its deck) from what fights in it, with no scene state (tested in <c>SymphonyTests</c>):
/// every unit brings its basic deck of five, 2 Offensive, 2 Defensive, 1 Utility (<see cref="SymphonySettings.SectionDeck"/>:
/// a section kind's, an expedition's own for each of a party's sections, a creature group's instincts from
/// <see cref="SymphonyCards.Creature"/>); a party's kit adds its specialty, shared out among its sections; every legend
/// brings its personal grimoire, voiced by the section it leads (or, a commander leading none, by itself), and the
/// commander its orders; an army adds the civilization's war score, voiced by its best caster. A section left with no
/// card at all falls back on Struggle and Endure.
/// </summary>
public static class SymphonyDecks
{
    /// <summary>Whether <paramref name="sec"/> can ever voice <paramref name="card"/>.</summary>
    public static bool CanVoice(CombatCard card, CombatSection sec)
    {
        if (card == null || sec == null) return false;
        switch (card.voice)
        {
            case CardVoice.Front: return sec.row == FormationRow.Front;
            case CardVoice.Back: return sec.row == FormationRow.Back;
            case CardVoice.Caster: return sec.Casts;
            case CardVoice.Commander: return false;
            default: return true;
        }
    }

    public static List<DeckCard> Build(BattleSide side, DeckSources src)
    {
        var deck = new List<DeckCard>();
        if (side == null) return deck;
        src = src ?? new DeckSources();
        var symphony = src.symphony ?? new SymphonySettings();
        var voiced = new HashSet<BattleLegend>();

        void Add(CombatCard card, int voice, string source, BattleLegend legend = null, float scale = 1f, bool major = false)
        {
            if (card == null) return;
            if (voice >= 0 && !CanVoice(card, side.sections[voice]) && legend == null) return;
            deck.Add(new DeckCard { card = card, voice = voice, legend = legend, scale = scale, major = major, source = source });
        }

        var partySections = new List<int>();
        for (int i = 0; i < side.sections.Count; i++)
        {
            var sec = side.sections[i];
            bool trained = string.IsNullOrEmpty(sec.trainingTechnology) || src.hasTechnology == null || src.hasTechnology(sec.trainingTechnology);
            if (!string.IsNullOrEmpty(sec.specId) && trained)
            {
                foreach (var card in symphony.SectionDeck(sec.specId)) Add(card, i, sec.name);
                foreach (var card in sec.trainingCards.Select(symphony.Card)) Add(card, i, $"{sec.name}'s training");
            }
            else if (!string.IsNullOrEmpty(sec.speciesId))
            {
                var species = src.species?.Invoke(sec.speciesId);
                foreach (var card in SymphonyCards.Creature(species, src.identity)) Add(card, i, species?.name ?? sec.name);
            }
            else if (sec.row == FormationRow.Front && (src.party || src.kit != null))
            {
                // A party's own sections (the Director's, each companion's circle) bring an expedition's basic deck.
                partySections.Add(i);
                foreach (var card in symphony.SectionDeck(SymphonyCards.PartyDeck)) Add(card, i, "the road");
            }

            foreach (var card in sec.equipmentCards.Select(symphony.Card)) Add(card, i, sec.equipment);
            var leader = sec.leader;
            // Company command remains linked, but an army's personal grimoire is voiced by its separate elite piece.
            if (sec.eliteRole == BattleEliteRole.None && leader != null && side.sections.Any(s => s.eliteRole != BattleEliteRole.None && s.leader == leader)) continue;
            if (leader != null && leader.State != ComposureState.Surrender && voiced.Add(leader))
                foreach (var (card, major) in LegendGrimoires.Cards(leader, src.age, symphony, src.learned))
                    Add(card, i, $"{leader.name}'s grimoire", leader, 1f, major);
        }

        // A party's kit, shared out among its sections in turn.
        var kit = src.kit;
        if (kit != null && partySections.Count > 0 && (string.IsNullOrEmpty(kit.technology) || src.hasTechnology == null || src.hasTechnology(kit.technology)))
        {
            int n = 0;
            foreach (var card in kit.cards.Select(symphony.Card).Where(c => c != null))
                Add(card, partySections[n++ % partySections.Count], kit.name);
        }

        var conductor = side.conductor;
        if (conductor != null && conductor.State != ComposureState.Surrender)
        {
            if (voiced.Add(conductor))
                foreach (var (card, major) in LegendGrimoires.Cards(conductor, src.age, symphony, src.learned))
                    Add(card, -1, $"{conductor.name}'s grimoire", conductor, 1f, major);
            foreach (var (card, scale) in LegendGrimoires.Commands(conductor, symphony))
                Add(card, -1, conductor.name, conductor, scale);
        }

        // The war score: sounded by the best caster of its binding, else any caster, else the commander, else the front.
        foreach (var card in src.warScore ?? new List<CombatCard>())
        {
            if (card == null) continue;
            int voice = BestVoice(side, card);
            if (voice == -1 && (conductor == null || conductor.State == ComposureState.Surrender)) continue;
            Add(card, voice, "the war score");
        }
        var doctrine = src.doctrine;
        if (doctrine != null && src.age >= doctrine.minAge && (string.IsNullOrEmpty(doctrine.technology) || src.hasTechnology == null || src.hasTechnology(doctrine.technology)))
            foreach (var card in doctrine.cards.Select(symphony.Card).Where(c => c != null))
            {
                int voice = BestVoice(side, card);
                if (voice >= 0 || conductor != null) Add(card, voice, doctrine.name);
            }
        foreach (var civic in src.civicCards ?? new List<(string source, CombatCard card)>())
        {
            if (civic.card == null) continue;
            int voice = BestVoice(side, civic.card);
            if (voice >= 0 || conductor != null) Add(civic.card, voice, civic.source);
        }

        // The fallback: a section that came out of all that with no card of its own (settlers walking behind, a unit
        // no deck names) still fights: Struggle (Offensive) and Endure (Defensive).
        var struggle = symphony.Card(SymphonyCards.Struggle);
        var endure = symphony.Card(SymphonyCards.Endure);
        for (int i = 0; i < side.sections.Count; i++)
        {
            if (deck.Any(d => d.voice == i)) continue;
            Add(struggle, i, "the fallback");
            Add(endure, i, "the fallback");
        }
        return deck;
    }

    /// <summary>Whether a list of cards is a basic deck's shape (<see cref="SymphonyCards.BasicShape"/>: 2 Offensive, 2 Defensive, 1 Utility).</summary>
    public static bool IsBasicShape(IEnumerable<CombatCard> cards)
    {
        var list = (cards ?? Enumerable.Empty<CombatCard>()).Where(c => c != null).ToList();
        return list.Count == SymphonyCards.BasicShape.Values.Sum() &&
               SymphonyCards.BasicShape.All(p => list.Count(c => c.purpose == p.Key) == p.Value);
    }

    private static int BestVoice(BattleSide side, CombatCard card)
    {
        var casters = side.sections.Select((s, i) => (s, i)).Where(x => x.s.Casts).ToList();
        var same = casters.Where(x => x.s.primary == card.binding).OrderByDescending(x => x.s.potency).ToList();
        if (same.Count > 0) return same[0].i;
        if (casters.Count > 0) return casters.OrderByDescending(x => x.s.potency).First().i;
        if (side.conductor != null) return -1;
        var front = side.sections.Select((s, i) => (s, i)).FirstOrDefault(x => x.s.row == FormationRow.Front);
        return front.s != null ? front.i : side.sections.Count > 0 ? 0 : -1;
    }

    /// <summary>Gives <paramref name="side"/> its Symphony and returns it.</summary>
    public static BattleSide Score(BattleSide side, DeckSources src)
    {
        if (side != null) side.deck = Build(side, src);
        return side;
    }

    /// <summary>The kits a party can carry now (<paramref name="hasTechnology"/> null: all).</summary>
    public static IEnumerable<ExpeditionKit> Kits(SymphonySettings settings, Func<string, bool> hasTechnology) =>
        (settings ?? new SymphonySettings()).Kits.Where(k => k != null && (string.IsNullOrEmpty(k.technology) || hasTechnology == null || hasTechnology(k.technology)));

    /// <summary>"12 cards: 5 Offensive, 4 Defensive, 2 Utility, 1 Setup" (by the Spell Builder's Purposes).</summary>
    public static string Describe(IReadOnlyCollection<DeckCard> deck)
    {
        if (deck == null || deck.Count == 0) return "no cards";
        return $"{deck.Count} card{(deck.Count == 1 ? "" : "s")}: {CardFace.Mix(deck.Select(d => d.card))}";
    }
}
