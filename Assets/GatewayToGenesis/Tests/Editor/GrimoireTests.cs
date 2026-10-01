using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// The Grimoire's rules (<see cref="Grimoire"/>, no scene): what the technologies grant, which card takes which seat,
/// Ceremonial Arts as three Spellweavers with one Unison each (the owner's rule), and what the Spell Maker allows.
/// </summary>
public class GrimoireTests
{
    private readonly List<Object> _made = new List<Object>();

    [TearDown]
    public void CleanUp()
    {
        foreach (var o in _made) if (o != null) Object.DestroyImmediate(o);
        _made.Clear();
    }

    private SymphonyCardData Card(string id, SpellBinding binding, NoteWeight weight = NoteWeight.Minor, ChordTier chord = ChordTier.Unison,
        SpellTempo tempo = SpellTempo.Staccato, CardUse uses = CardUse.Battle | CardUse.Field)
    {
        var card = ScriptableObject.CreateInstance<SymphonyCardData>();
        card.id = id; card.cardName = id; card.binding = binding; card.weight = weight; card.chord = chord; card.tempo = tempo; card.uses = uses;
        _made.Add(card);
        return card;
    }

    private TechUnlockable Unlock(TechUnlockableType type, float count = 1f, GrimoireSeat seat = GrimoireSeat.Symphony, SymphonyCardData card = null)
    {
        var u = ScriptableObject.CreateInstance<TechUnlockable>();
        u.unlockableType = type; u.resourceModifier = count; u.seat = seat; u.symphonyCard = card;
        _made.Add(u);
        return u;
    }

    [Test]
    public void TechnologiesGrantSeatsCeremoniesCardsAndWildcards()
    {
        var pot = Card("igniting-cooking-pot", SpellBinding.Cindergale);
        var seat = Unlock(TechUnlockableType.GrimoireSeat);
        var ceremony = Unlock(TechUnlockableType.GrimoireSeat, seat: GrimoireSeat.Ceremony);
        var state = Grimoire.Collect(new[]
        {
            seat, seat, ceremony, Unlock(TechUnlockableType.SpellWildcard, 2f), Unlock(TechUnlockableType.SymphonyCard, card: pot),
            Unlock(TechUnlockableType.SymphonyCard, card: pot), Unlock(TechUnlockableType.CouncilSeat), null,
        });
        Assert.AreEqual(2, state.symphonySeats, "the same seat granted by two technologies counts twice");
        Assert.AreEqual(1, state.ceremonies);
        Assert.AreEqual(3, state.CeremonySeats, "a Ceremony has three voices");
        Assert.AreEqual(5, state.Seats);
        Assert.AreEqual(2, state.wildcards);
        Assert.AreEqual(1, state.cards.Count, "a card is owned once");
        Assert.AreEqual(1f, Grimoire.Value(state, "card:Igniting-Cooking-Pot"));
        Assert.AreEqual(3f, Grimoire.Value(state, "ceremony_seats"));
        Assert.AreEqual(5f, Grimoire.Value(state, ""), "the domain alone counts every seat");
        Assert.IsFalse(Grimoire.Collect(null).IsOpen);
    }

    [Test]
    public void ACeremonyIsResonanceFluxAndStrandOneVoiceEach()
    {
        Assert.AreEqual(CeremonyVoice.Root, Grimoire.VoiceOf(SpellBinding.Resonance));
        Assert.AreEqual(CeremonyVoice.Third, Grimoire.VoiceOf(SpellBinding.Flux));
        Assert.AreEqual(CeremonyVoice.Fifth, Grimoire.VoiceOf(SpellBinding.Strand));
        Assert.AreEqual(CeremonyVoice.None, Grimoire.VoiceOf(SpellBinding.Cindergale));
        foreach (var voice in Grimoire.Voices) Assert.AreEqual(voice, Grimoire.VoiceOf(Grimoire.BindingOf(voice)));

        var zephyr = Card("zephyr", SpellBinding.Resonance, uses: CardUse.Battle | CardUse.Field | CardUse.Ceremony);
        Assert.IsNull(Grimoire.WhyNotSeat(zephyr, GrimoireSeat.Ceremony, CeremonyVoice.Root, 0));
        StringAssert.Contains("Flux", Grimoire.WhyNotSeat(zephyr, GrimoireSeat.Ceremony, CeremonyVoice.Third, 0), "each voice has its binding");
        Assert.IsNotNull(Grimoire.WhyNotSeat(zephyr, GrimoireSeat.Ceremony, CeremonyVoice.None, 0));
    }

    [Test]
    public void AnUnvoicedCeremonyStillSoundsWithThePeoplesHum()
    {
        float people = Grimoire.CeremonyStrength(null);
        Assert.Greater(people, 0f, "the people hum every voice");
        var voices = new Dictionary<CeremonyVoice, SymphonyCardData> { { CeremonyVoice.Root, Card("r", SpellBinding.Resonance) } };
        float one = Grimoire.CeremonyStrength(voices);
        voices[CeremonyVoice.Third] = Card("t", SpellBinding.Flux);
        voices[CeremonyVoice.Fifth] = Card("f", SpellBinding.Strand, NoteWeight.Major);
        float all = Grimoire.CeremonyStrength(voices);
        Assert.Less(people, one); Assert.Less(one, all);
        Assert.LessOrEqual(all, 1f);
    }

    [Test]
    public void SeatsTakeStaccatoUnisonsInAgeZero()
    {
        Assert.IsNull(Grimoire.WhyNotSeat(Card("pot", SpellBinding.Cindergale), GrimoireSeat.Symphony, CeremonyVoice.None, 0));
        StringAssert.Contains("Unison", Grimoire.WhyNotSeat(Card("dyad", SpellBinding.Flux, chord: ChordTier.Dyad), GrimoireSeat.Symphony, CeremonyVoice.None, 5),
            "only Unisons fit a seat for now, in any Age");
        Assert.IsNotNull(Grimoire.WhyNotSeat(Card("legato", SpellBinding.Flux, tempo: SpellTempo.Legato), GrimoireSeat.Symphony, CeremonyVoice.None, 0), "Age 0 knows only Staccato");
        Assert.IsNull(Grimoire.WhyNotSeat(Card("legato", SpellBinding.Flux, tempo: SpellTempo.Legato), GrimoireSeat.Symphony, CeremonyVoice.None, 1), "Legato arrives in Age I");
        Assert.IsNull(Grimoire.WhyNotSeat(Card("shard", SpellBinding.Crystal, NoteWeight.Major), GrimoireSeat.Symphony, CeremonyVoice.None, 0),
            "an Awakened Legend can sound a Major Unison in Age 0");
        Assert.IsNotNull(Grimoire.WhyNotSeat(Card("rite", SpellBinding.Strand, uses: CardUse.Ceremony), GrimoireSeat.Symphony, CeremonyVoice.None, 0));
    }

    [Test]
    public void TheSpellMakerComposesOnlyWhatABindingCanDo()
    {
        var heard = new[] { SpellBinding.Cindergale, SpellBinding.Strand };
        Assert.IsNull(Grimoire.WhyNotCompose(SpellBinding.Strand, NoteWeight.Minor, SpellTempo.Staccato, SpellIntent.Mend, heard, 0, false));
        StringAssert.Contains("never heard", Grimoire.WhyNotCompose(SpellBinding.Void, NoteWeight.Minor, SpellTempo.Staccato, SpellIntent.Conceal, heard, 0, false));
        StringAssert.Contains("cannot be composed", Grimoire.WhyNotCompose(SpellBinding.Cindergale, NoteWeight.Minor, SpellTempo.Staccato, SpellIntent.Mend, heard, 0, false));
        StringAssert.Contains("Awakened Legend", Grimoire.WhyNotCompose(SpellBinding.Cindergale, NoteWeight.Major, SpellTempo.Staccato, SpellIntent.Strike, heard, 0, false));
        Assert.IsNull(Grimoire.WhyNotCompose(SpellBinding.Cindergale, NoteWeight.Major, SpellTempo.Staccato, SpellIntent.Strike, heard, 0, true));
        Assert.IsNotNull(Grimoire.WhyNotCompose(SpellBinding.Cindergale, NoteWeight.Minor, SpellTempo.Legato, SpellIntent.Strike, heard, 0, false));
        foreach (var binding in HarmonicCircle.Seven) Assert.IsNotEmpty(Grimoire.IntentsOf(binding).ToList(), $"{binding} can be composed to do something");
    }
}
