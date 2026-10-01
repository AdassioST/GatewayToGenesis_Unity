using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

/// <summary>
/// The Symphony of War's card layer with no scene: the default cards, each side's deck (section decks, kits, personal
/// grimoires, a commander's orders, creatures' instincts), performing them in battle (the field, determinism, chord
/// layering, a manual battle), and the power gauge.
/// </summary>
public class SymphonyTests
{
    private static readonly CombatSettings Settings = new CombatSettings();
    private static SymphonySettings Cards => Settings.Symphony;

    private static SpeciesSpec Wolf => new SpeciesSpec { id = "wolf", name = "Wolf", diet = CreatureDiet.Carnivore, subgroup = CreatureSubgroup.Social, size = CreatureSize.Medium, structure = 0.9f };

    private static SpeciesSpec Salamander => new SpeciesSpec
    {
        id = "salamander", name = "Salamander", diet = CreatureDiet.Herbivore, subgroup = CreatureSubgroup.Benign, size = CreatureSize.Medium,
        structure = 0.5f, binding = BindingOrgan.Hide, primaryBinding = SpellBinding.Cindergale,
    };

    private static BattleSide Formation(string template, int age, bool deck = true)
    {
        var side = OrchestralFormations.Raise(Settings.Template(template), Settings, age);
        return deck ? SymphonyDecks.Score(side, new DeckSources { age = age, symphony = Cards }) : side;
    }

    private static BattleSide Party(int legends, string kit, bool deck = true, SpellBinding leitmotif = SpellBinding.Flux)
    {
        var names = Enumerable.Range(0, legends).Select(i => "Legend " + i).ToList();
        var side = WorldBattles.PartySide(new WorldUnit { name = "Party" }, null, names, n => new BattleLegend { name = n, leitmotif = leitmotif });
        return deck ? SymphonyDecks.Score(side, new DeckSources { age = 0, symphony = Cards, kit = Cards.Kit(kit) }) : side;
    }

    private static BattleSide Pack(SpeciesSpec species, int n, bool deck = true, BandIdentity identity = BandIdentity.Creature)
    {
        var side = CreatureCombat.Side(species, n);
        return deck ? SymphonyDecks.Score(side, new DeckSources { age = 0, symphony = Cards, species = id => species, identity = identity }) : side;
    }

    // ---- The cards -------------------------------------------------------------------------------------------------

    [Test]
    public void DefaultContent_IsWellFormed()
    {
        var ids = Cards.Cards.Select(c => c.id).ToList();
        CollectionAssert.AllItemsAreUnique(ids);
        foreach (var card in Cards.Cards)
        {
            if (card.noteRole == BattleNoteRole.Minor) Assert.IsNotNull(card.chord, $"{card.id} modifies a Core");
            else Assert.IsNotEmpty(card.effects, $"{card.id} does something");
            Assert.That(card.cost, Is.InRange(0, 3), card.id);
            if (card.kind == CardKind.Spell && card.voice != CardVoice.Caster)
                Assert.AreNotEqual(SpellBinding.Unattuned, card.binding, $"{card.id}: a spell names its Root (or is voiced by a caster, whose root it takes)");
        }
        foreach (var spec in CombatDefaults.Sections)
            Assert.IsNotEmpty(Cards.SectionDeck(spec.id).ToList(), $"{spec.name} brings a basic deck");
        foreach (var kit in Cards.Kits)
            foreach (string id in kit.cards) Assert.IsNotNull(Cards.Card(id), $"{kit.name} lists {id}");
        foreach (var b in HarmonicCircle.Seven) Assert.IsNotNull(Cards.Card(SymphonyCards.LeitmotifId(b)), $"a leitmotif card for {b}");
        foreach (LegendClass great in System.Enum.GetValues(typeof(LegendClass))) Assert.IsNotNull(Cards.Card(SymphonyCards.GreatId(great)), $"an order for the Great {great}");
        // The seven Symphony Cards of Act I have their battle cards, by the same ids as their assets.
        foreach (string id in new[] { "coaxed-spring", "igniting-cooking-pot", "desperate-humming", "ash-clearing-zephyr", "swallowed-light", "conjured-shard", "hairs-breadth-truer" })
            Assert.IsNotNull(Cards.Card(id), id);
    }

    [Test]
    public void TheBasicUnits_BringTheirOwnCards()
    {
        var side = Formation("hearth-guard", 0);
        int warden = side.sections.FindIndex(s => s.specId == "grave-warden");
        int archer = side.sections.FindIndex(s => s.specId == "wasteland-archer");
        Assert.IsTrue(side.deck.Any(d => d.card.id == "grave-spade-strike" && d.voice == warden));
        Assert.IsTrue(side.deck.Any(d => d.card.id == "ash-volley" && d.voice == archer));
        Assert.IsFalse(side.deck.Any(d => d.card.voice == CardVoice.Front && side.sections[d.voice].row != FormationRow.Front), "front cards are voiced only by front sections");
    }

    [Test]
    public void AKit_IsSharedAmongTheParty()
    {
        var side = Party(2, "hunters-kit");
        var kit = Cards.Kit("hunters-kit");
        var kitCards = side.deck.Where(d => d.source == kit.name).ToList();
        Assert.AreEqual(kit.cards.Count, kitCards.Count);
        CollectionAssert.AreEquivalent(new[] { 0, 1 }, kitCards.Select(d => d.voice).Distinct().ToArray(), "each legend's circle carries part of it");
        Assert.IsTrue(side.deck.Any(d => d.card.id == SymphonyCards.RallyToMe && d.voice == -1), "the Director gives orders");
    }

    [Test]
    public void EverySpellweaver_CarriesItsOwnGrimoire()
    {
        var legend = new BattleLegend { name = "Aurelian", leitmotif = SpellBinding.Cindergale, ornaments = { SpellBinding.Crystal }, awakened = true, grimoire = { "igniting-cooking-pot", "conjured-shard" } };
        legend.greats[LegendClass.Vanguard] = 3;
        var cards = LegendGrimoires.Cards(legend, 0, Cards);
        Assert.AreEqual("leitmotif-cindergale", cards[0].card.id);
        Assert.IsTrue(cards[0].major, "an Awakened Legend sounds its own leitmotif as a Major Note");
        Assert.IsTrue(cards.Any(c => c.card.id == "conjured-shard"), "Major Notes are an Awakened Legend's");
        Assert.IsFalse(cards.Any(c => c.card.id == "leitmotif-crystal"), "Ornamental Magic waits for Age III");
        Assert.IsTrue(LegendGrimoires.Cards(legend, 3, Cards).Any(c => c.card.id == "leitmotif-crystal"));
        legend.awakened = false;
        Assert.IsFalse(LegendGrimoires.Cards(legend, 0, Cards).Any(c => c.card.id == "conjured-shard"), "an unawakened legend cannot keep a Major Note");
        var orders = LegendGrimoires.Commands(legend, Cards);
        Assert.AreEqual(1.5f, orders.First(o => o.card.id == "great-vanguard").scale, 1e-4f, "three stars: +25% a star beyond the first");

        // A commander leading no section voices its grimoire itself; a leader's is voiced by its section.
        var army = Formation("hearth-guard", 0, deck: false);
        army.conductor = legend;
        army.sections[0].leader = new BattleLegend { name = "Mira", leitmotif = SpellBinding.Strand };
        SymphonyDecks.Score(army, new DeckSources { age = 0, symphony = Cards });
        Assert.IsTrue(army.deck.Any(d => d.card.id == "leitmotif-cindergale" && d.voice == -1 && d.legend == legend));
        Assert.IsTrue(army.deck.Any(d => d.card.id == "leitmotif-strand" && d.voice == 0));
    }

    [Test]
    public void APersonalGrimoire_HasPagesAndRules()
    {
        var soul = new LegendSoul { leitmotif = "Flux" };
        Assert.AreEqual(LegendGrimoires.BasePages, LegendGrimoires.Pages(soul, null));
        soul.ornaments.Add("Strand");
        Assert.AreEqual(LegendGrimoires.BasePages + 1, LegendGrimoires.Pages(soul, new Dictionary<LegendClass, int> { [LegendClass.Seer] = 2 }));
        Assert.IsTrue(LegendGrimoires.Awakened(soul));
    }

    [Test]
    public void Creatures_FightWithTheirInstincts()
    {
        var wolves = SymphonyCards.Creature(Wolf);
        Assert.IsTrue(wolves.Any(c => c.name == "Rend"));
        Assert.IsFalse(wolves.Any(c => c.effects.Any(e => e.op == CardOp.Spell)), "an ordinary animal casts nothing");
        var salamanders = SymphonyCards.Creature(Salamander);
        Assert.IsTrue(salamanders.Any(c => c.binding == SpellBinding.Cindergale && c.effects.Any(e => e.op == CardOp.Spell)), "it casts its binding by instinct");
        Assert.IsTrue(salamanders.Any(c => c.name == "Harden Hide"));
        Assert.IsTrue(SymphonyCards.Creature(Wolf, BandIdentity.Atonalis).Any(c => c.name == "Static Hunger"));
        Assert.IsTrue(Pack(Wolf, 6).deck.All(d => d.voice >= 0));
    }

    // ---- Performing ------------------------------------------------------------------------------------------------

    [Test]
    public void ASideWithoutCards_FightsAsThePlainAutoResolve()
    {
        var setup = new BattleSetup { attacker = Formation("ember-host", 0, deck: false), defender = Formation("hearth-guard", 0, deck: false), field = new Battlefield { age = 0 }, seed = 4 };
        var report = BattleResolver.Resolve(setup.Clone(4), Settings);
        Assert.IsEmpty(report.plays);
        Assert.AreEqual(0, report.attacker.cardsPlayed);
        Assert.IsFalse(report.log.Any(l => l.Contains("perform a Symphony")));
    }

    [Test]
    public void ASymphony_IsPerformedAndDeterministic()
    {
        var setup = new BattleSetup { attacker = Party(2, "wayfarers-kit"), defender = Pack(Wolf, 6), field = new Battlefield { age = 0 }, seed = 9 };
        var first = BattleResolver.Resolve(setup.Clone(9), Settings);
        var again = BattleResolver.Resolve(setup.Clone(9), Settings);
        Assert.IsNotEmpty(first.plays);
        Assert.IsTrue(first.plays.Any(p => p.attacker) && first.plays.Any(p => !p.attacker), "both sides play their cards");
        CollectionAssert.AreEqual(first.log, again.log);
        Assert.AreEqual(first.plays.Count, again.plays.Count);
        Assert.AreEqual(first.attacker.cardsPlayed, first.plays.Count(p => p.attacker && !p.failed));
        var deckIds = setup.attacker.deck.Select(d => d.card.id).ToList();
        Assert.IsTrue(first.plays.Where(p => p.attacker).All(p => deckIds.Contains(p.card)), "only its own cards");
    }

    [Test]
    public void CardsAtHomeOnTheField_LandHarder()
    {
        // Archers defending from cover in the woods: Loose from Cover is at home on both counts.
        var field = new Battlefield { age = 0, ground = BattleGround.Forest, concealed = true, cover = "deep-forest" };
        var report = BattleResolver.Resolve(new BattleSetup { attacker = Formation("ember-host", 0), defender = Formation("hearth-guard", 0), field = field, seed = 2 }, Settings);
        var loose = report.plays.Where(p => !p.attacker && p.card == "loose-from-cover").ToList();
        Assert.IsNotEmpty(loose);
        Assert.AreEqual(1.2f * 1.4f, loose[0].field, 1e-3f);
        // A card that needs its field is never played off it.
        var settings = new CombatSettings();
        var only = new CombatCard { id = "only-in-town", name = "Only in Town", cost = 0, effects = { new CardEffect(CardOp.Strike, CardAim.Enemy, 1f) }, condition = CardCondition.Settlement, requires = true };
        settings.symphony.cards = Cards.Cards.Concat(new[] { only }).ToList();
        var side = Party(1, "wayfarers-kit");
        side.deck.Add(new DeckCard { card = only, voice = 0 });
        var open = BattleResolver.Resolve(new BattleSetup { attacker = Pack(Wolf, 4), defender = side, field = new Battlefield { age = 0 }, seed = 3 }, settings);
        Assert.IsFalse(open.plays.Any(p => p.card == "only-in-town"));
        var town = BattleResolver.Resolve(new BattleSetup { attacker = Pack(Wolf, 4), defender = side.Clone(), field = new Battlefield { age = 0, settlement = true }, seed = 3 }, settings);
        Assert.IsTrue(town.plays.Any(p => p.card == "only-in-town"));
    }

    [Test]
    public void SpellsOfDifferentVoicesJoinOnlyAsAnExplicitEnsemble()
    {
        // Two circles of different bindings: separate Unisons do not layer on their own; an Ensemble Minor on the second
        // circle's Track, synchronized by the Conductor, joins its binding into the first circle's working.
        BattleSide Choir(bool ensemble)
        {
            var side = new BattleSide { name = "Choir", manual = true, conductor = new BattleLegend { name = "Cantor" } };
            side.sections.Add(CombatSection.Raise(Settings.Section("shieldwall")));
            side.sections.Add(CombatSection.Raise(Settings.Section("weaver-circle"), SpellBinding.Flux));
            side.sections.Add(CombatSection.Raise(Settings.Section("weaver-circle"), SpellBinding.Resonance));
            var chant = Cards.Card("circle-chant");
            var join = new CombatCard { id = "join-the-chant", name = "Join the Chant", kind = CardKind.Spell, binding = SpellBinding.Resonance, purpose = SpellPurpose.Modulation,
                noteRole = BattleNoteRole.Minor, flicker = 0f, chord = new BattleChordModifier { binding = SpellBinding.Resonance, joinBinding = true, requiresAdjacent = false } };
            side.deck = new List<DeckCard> { new DeckCard { card = chant, voice = 1 }, new DeckCard { card = ensemble ? join : chant, voice = 2 } };
            return side;
        }
        foreach (bool ensemble in new[] { false, true })
        {
            var run = BattleResolver.Begin(new BattleSetup { attacker = Choir(ensemble), defender = Pack(Wolf, 8, deck: false), field = new Battlefield { age = 2 }, seed = 1 }, Settings);
            run.BeginMeasure();
            int core = run.Hand(true).ToList().FindIndex(d => d.voice == 1), other = run.Hand(true).ToList().FindIndex(d => d.voice == 2);
            if (ensemble) Assert.IsNull(run.CommitChord(true, core, new[] { other }));
            else { Assert.IsNull(run.CommitCard(true, core)); Assert.IsNull(run.CommitCard(true, run.Hand(true).ToList().FindIndex(d => d.voice == 2))); }
            run.ResolveMeasure();
            Assert.AreEqual(ensemble, run.Report.log.Any(l => l.Contains("layered into a Dyad with Resonance")), string.Join("\n", run.Report.log));
        }
    }

    [Test]
    public void AManualBattle_IsPlayedMeasureByMeasure()
    {
        var mine = Party(2, "wayfarers-kit");
        mine.manual = true;
        var run = BattleResolver.Begin(new BattleSetup { attacker = mine, defender = Pack(Wolf, 5), field = new Battlefield { age = 0 }, seed = 6 }, Settings);
        Assert.IsNotNull(run.Prediction, "a hand-played battle is forecast first");
        run.BeginMeasure();
        Assert.AreEqual(1, run.Measure);
        Assert.IsNotEmpty(run.Hand(true));
        Assert.IsNotEmpty(run.Intent(false), "the enemy's intent is shown before you play");
        Assert.IsEmpty(run.Intent(true), "the resolver does not play your hand for you");
        int playable = Enumerable.Range(0, run.Hand(true).Count).First(i => run.WhyNotPlay(true, i) == null && run.Hand(true)[i].card.reaction == null);
        Assert.IsNull(run.CommitCard(true, playable, rendition: Cards.Tuning.perfect));
        Assert.AreEqual(1, run.Intent(true).Count);
        Assert.IsNotNull(run.WhyNotPlay(true, 99));
        run.ResolveMeasure();
        Assert.AreEqual(1, run.Report.plays.Count(p => p.attacker));
        Assert.AreEqual(Cards.Tuning.perfect, run.Report.plays.First(p => p.attacker).rendition, 1e-4f);
        var report = run.Finish();
        Assert.IsTrue(run.Over);
        Assert.Greater(report.measures, 0);
    }

    // ---- The Spell Builder's format and the five Purposes -----------------------------------------------------------

    [Test]
    public void EveryCard_IsWrittenInTheSpellBuildersFormat()
    {
        foreach (var card in Cards.Cards)
        {
            Assert.IsFalse(string.IsNullOrEmpty(card.catchline), $"{card.id} prints a catchline on its face");
            if (card.kind == CardKind.Spell && card.binding != SpellBinding.Unattuned)
            {
                Assert.IsFalse(string.IsNullOrEmpty(card.practice), $"{card.id}: a spell names its practice");
                Assert.IsFalse(string.IsNullOrEmpty(card.vision), $"{card.id}: a spell names the image it asks for");
            }
            StringAssert.StartsWith("<b>What it does</b>", CardFace.Reading(card));
        }
        // All five Purposes are in the library.
        foreach (SpellPurpose p in System.Enum.GetValues(typeof(SpellPurpose)))
            Assert.IsTrue(Cards.Cards.Any(c => c.purpose == p), $"a {p} card");
    }

    [Test]
    public void EveryUnit_HasABasicDeckOfTwoOffensiveTwoDefensiveOneUtility()
    {
        foreach (var spec in CombatDefaults.Sections)
            Assert.IsTrue(SymphonyDecks.IsBasicShape(Cards.SectionDeck(spec.id)), $"{spec.name}: {CardFace.Mix(Cards.SectionDeck(spec.id))}");
        Assert.IsTrue(SymphonyDecks.IsBasicShape(Cards.SectionDeck(SymphonyCards.PartyDeck)), "an expedition's own");
        var species = new[]
        {
            Wolf, Salamander,
            new SpeciesSpec { id = "moth", name = "Moth", diet = CreatureDiet.Herbivore, subgroup = CreatureSubgroup.Frightful, size = CreatureSize.Small, structure = 0.3f, binding = BindingOrgan.Wings, primaryBinding = SpellBinding.Luminance },
            new SpeciesSpec { id = "giant", name = "Giant", diet = CreatureDiet.Herbivore, subgroup = CreatureSubgroup.Venerable, size = CreatureSize.Gargantuan, structure = 0.55f },
            new SpeciesSpec { id = "eel", name = "Eel", diet = CreatureDiet.Carnivore, subgroup = CreatureSubgroup.Trapper, size = CreatureSize.Large, binding = BindingOrgan.Fins, primaryBinding = SpellBinding.Flux },
        };
        foreach (var sp in species)
            foreach (var identity in new[] { BandIdentity.Creature, BandIdentity.Atonalis, BandIdentity.FormlessMass })
                Assert.IsTrue(SymphonyDecks.IsBasicShape(SymphonyCards.Creature(sp, identity)), $"{sp.name} ({identity}): {CardFace.Mix(SymphonyCards.Creature(sp, identity))}");
        // A party: each of its sections brings the basic deck; the kit adds its specialty on top.
        var party = Party(2, "hunters-kit");
        Assert.AreEqual(2 * 5 + Cards.Kit("hunters-kit").cards.Count + party.deck.Count(d => d.voice == -1 || d.legend != null), party.deck.Count);
    }

    [Test]
    public void WhatHasNoCard_FallsBackOnStruggleAndEndure()
    {
        var side = new BattleSide { name = "Stragglers" };
        side.sections.Add(new CombatSection { name = "Stragglers", row = FormationRow.Front, maxIntegrity = 60f, maxComposure = 30f, attack = 3f, defense = 6f, breakthrough = 4f });
        side.sections[0].Reset();
        SymphonyDecks.Score(side, new DeckSources { age = 0, symphony = Cards });
        CollectionAssert.AreEquivalent(new[] { SymphonyCards.Struggle, SymphonyCards.Endure }, side.deck.Select(d => d.card.id).ToArray());
        Assert.AreEqual(SpellPurpose.Offensive, Cards.Card(SymphonyCards.Struggle).purpose);
        Assert.AreEqual(SpellPurpose.Defensive, Cards.Card(SymphonyCards.Endure).purpose);
        // Settlers walking behind a party have no deck of their own either.
        var unit = new WorldUnit { name = "Settling party", settlers = 6 };
        var party = WorldBattles.PartySide(unit, null, new List<string> { "Mira" }, n => new BattleLegend { name = n });
        SymphonyDecks.Score(party, new DeckSources { age = 0, symphony = Cards, kit = Cards.Kit("wayfarers-kit") });
        int settlers = party.sections.FindIndex(s => s.name.StartsWith("Settlers"));
        Assert.IsTrue(party.deck.Any(d => d.voice == settlers && d.card.id == SymphonyCards.Struggle));
        // Struggle is played, and its voice pays for it.
        var report = BattleResolver.Resolve(new BattleSetup { attacker = side, defender = Pack(Wolf, 2, deck: false), field = new Battlefield { age = 0 }, seed = 4 }, Settings);
        Assert.IsTrue(report.plays.Any(p => p.card == SymphonyCards.Struggle || p.card == SymphonyCards.Endure));
    }

    [Test]
    public void Setup_IsGroundworkForTheOffensiveCardsAfterIt()
    {
        var mine = Party(2, "wayfarers-kit");
        mine.manual = true;
        var mark = Cards.Card("mark-the-weak-point");
        var staff = Cards.Card("walking-staff");
        // One Major per Track: the Setup sounds early on one Track, the attack at the Cadence on another.
        mine.deck = new List<DeckCard> { new DeckCard { card = mark, voice = 1 }, new DeckCard { card = staff, voice = 0 } };
        var wolves = Pack(Wolf, 6, deck: false);
        mine.sections[0].battleHex = 7; mine.sections[1].battleHex = 6;
        wolves.sections[0].battleHex = 8;
        wolves.manual = true; // This assertion concerns groundwork with an adjacent opponent, not movement.
        var run = BattleResolver.Begin(new BattleSetup { attacker = mine, defender = wolves, field = new Battlefield { age = 0 }, seed = 2 }, Settings);
        run.BeginMeasure();
        Assert.IsNull(run.CommitCard(true, run.Hand(true).ToList().FindIndex(d => d.card == mark), 0, beat: 2));
        Assert.IsNull(run.CommitCard(true, run.Hand(true).ToList().FindIndex(d => d.card == staff), 0));
        run.ResolveMeasure();
        var played = run.Report.plays.Where(p => p.attacker).ToList();
        Assert.AreEqual(SpellPurpose.Setup, played[0].purpose);
        Assert.AreEqual(1, played[1].groundwork, "the attack after a Setup lands on its groundwork");
    }

    [Test]
    public void ACardsChord_IsBoundByTheAge()
    {
        var triad = new CombatCard { id = "triad", name = "Triad", kind = CardKind.Spell, purpose = SpellPurpose.Offensive, binding = SpellBinding.Cindergale, catchline = "x",
            minors = { SpellBinding.Strand, SpellBinding.Flux }, effects = { new CardEffect(CardOp.Spell, CardAim.Enemy, 1f) } };
        Assert.AreEqual(ChordTier.Triad, CardFace.Tier(triad));
        Assert.AreEqual("Minor Triad Chord", CardFace.Rank(triad, false));
        foreach (int age in new[] { 0, 2 })
        {
            var mine = Party(1, "wayfarers-kit", leitmotif: SpellBinding.Cindergale);
            mine.manual = true;
            mine.deck = new List<DeckCard> { new DeckCard { card = triad, voice = 0 } };
            var run = BattleResolver.Begin(new BattleSetup { attacker = mine, defender = Pack(Wolf, 4, deck: false), field = new Battlefield { age = age }, seed = 1 }, Settings);
            run.BeginMeasure();
            string why = run.WhyNotPlay(true, 0);
            if (age == 0) StringAssert.Contains("cannot hold a Triad Chord", why);
            else Assert.IsNull(why, "Age II commands its first (rare) Triads");
        }
    }

    [Test]
    public void TheFace_ReadsLikeTheWebsitesCard()
    {
        Assert.AreEqual("F", CardFace.Note(SpellBinding.Cindergale));
        Assert.AreEqual("C", CardFace.Note(SpellBinding.Resonance));
        Assert.AreEqual("B", CardFace.Note(SpellBinding.Strand));
        // The circle of fifths: C at twelve o'clock, G at one ... F at eleven; the five rests are the flats.
        Assert.AreEqual(0, CardFace.Station(SpellBinding.Resonance));
        Assert.AreEqual(1, CardFace.Station(SpellBinding.Crystal));
        Assert.AreEqual(5, CardFace.Station(SpellBinding.Strand));
        Assert.AreEqual(11, CardFace.Station(SpellBinding.Cindergale));
        CollectionAssert.AreEqual(new[] { "Gb", "Db", "Ab", "Eb", "Bb" }, CardFace.Rests().Select(r => r.note).ToArray());
        var fire = new CombatCard { name = "Repeating Fire", kind = CardKind.Spell, binding = SpellBinding.Cindergale, minors = { SpellBinding.Strand } };
        Assert.AreEqual("Minor Dyad Chord", CardFace.Rank(fire, false));
        Assert.AreEqual("Cindergale • Strand", CardFace.SuitLine(fire));
        Assert.AreEqual("Steel", CardFace.Rank(Cards.Card("grave-spade-strike"), false));
        Assert.AreEqual("Steel", CardFace.SuitLine(Cards.Card("grave-spade-strike")));
        Assert.AreEqual("Major Unison", CardFace.Rank(Cards.Card("leitmotif-flux"), true));
        Assert.AreEqual("Flux", CardFace.SuitLine(Cards.Card("circle-chant"), SpellBinding.Flux), "a caster's card takes its caster's binding");
        // The five Purposes, each on its thread of the Pentatonic circle, in the website's words.
        Assert.AreEqual(SpellBinding.Cindergale, CardFace.PurposeBinding(SpellPurpose.Offensive));
        Assert.AreEqual(SpellBinding.Crystal, CardFace.PurposeBinding(SpellPurpose.Defensive));
        Assert.AreEqual(SpellBinding.Resonance, CardFace.PurposeBinding(SpellPurpose.Setup));
        Assert.AreEqual(SpellBinding.Strand, CardFace.PurposeBinding(SpellPurpose.Utility));
        Assert.AreEqual(SpellBinding.Flux, CardFace.PurposeBinding(SpellPurpose.Modulation));
        Assert.AreEqual("The Lead", CardFace.Role(SpellPurpose.Offensive));
        Assert.AreEqual("The Ornamentation", CardFace.Role(SpellPurpose.Modulation));
        Assert.AreEqual("Preparing the environment", CardFace.Group(SpellPurpose.Setup));
    }

    // ---- The gauge -------------------------------------------------------------------------------------------------

    [Test]
    public void Power_GrowsWithTheDeckAndTracksTheForecast()
    {
        var bare = SymphonyPower.Rate(Party(2, "wayfarers-kit", deck: false), Settings);
        var decked = SymphonyPower.Rate(Party(2, "wayfarers-kit"), Settings);
        Assert.AreEqual(0f, bare.melody);
        // A card replaces its Track's drilled Note only when worth more: a traveller's staff adds no offense, its guards add endurance.
        Assert.GreaterOrEqual(decked.melody, 0f);
        Assert.Greater(decked.power, bare.power);
        Assert.Greater(decked.cards, 0);
        // More wolves: more power, and the forecast agrees.
        var few = Pack(Wolf, 4);
        var many = Pack(Wolf, 14);
        Assert.Greater(SymphonyPower.Rate(many, Settings).power, SymphonyPower.Rate(few, Settings).power);
        var field = new Battlefield { age = 0 };
        var vsFew = BattleResolver.Forecast(new BattleSetup { attacker = Party(2, "wayfarers-kit"), defender = few, field = field, seed = 1 }, Settings, 15);
        var vsMany = BattleResolver.Forecast(new BattleSetup { attacker = Party(2, "wayfarers-kit"), defender = many, field = field, seed = 1 }, Settings, 15);
        Assert.Greater(SymphonyPower.Odds(Party(2, "wayfarers-kit"), few, Settings, field), SymphonyPower.Odds(Party(2, "wayfarers-kit"), many, Settings, field));
        Assert.GreaterOrEqual(vsFew.WinChance, vsMany.WinChance);
    }
}
