using System.Linq;
using NUnit.Framework;

/// <summary>
/// The creatures' calendar with no scene (<see cref="WorldRhythm"/>; vault: Arcanorian Ecology.md, "The Creatures' Calendar"): the Echo just lived
/// shaping births, decay and spreading; live effects peaking in an Echo's middle Phase; hungry predators in Silence;
/// and the Ritual Seventh's attunement (Pure Light surge and overload, creatures calming, hunts wounding twice).
/// Uses the catalog of <see cref="EcologyTests"/> with the four Echoes of World.asset's proposal.
/// </summary>
public class RhythmTests
{
    private const int Resonance = 1, Crescendo = 2, Dissonance = 3, Silence = 4;
    private const string Enclave = "enclave:0";

    private static WorldGenSettings Settings()
    {
        var s = EcologyTests.Settings();
        s.rhythm.echoes.Add(new EchoRhythm { name = "Echo of Resonance", summary = "births", growth = 1.6f, dispersal = 0.6f, predation = 0.8f, ease = 2f });
        s.rhythm.echoes.Add(new EchoRhythm { name = "Echo of Crescendo", summary = "range", growth = 1.2f, dispersal = 1.4f, predation = 1.2f, yields = 1.3f });
        s.rhythm.echoes.Add(new EchoRhythm { name = "Echo of Dissonance", summary = "decay", growth = 0.8f, detritivoreGrowth = 1.8f, dispersal = 1.4f, harm = 1.5f, ease = 0.5f });
        s.rhythm.echoes.Add(new EchoRhythm { name = "Echo of Silence", summary = "dormancy", growth = 0.4f, detritivoreGrowth = 0.6f, dispersal = 0.6f, yields = 0.7f, hungerDanger = 0.25f });
        return s;
    }

    private static void At(WorldMap map, int echo, int phase, bool ritual = false, int phaseCount = 0)
    {
        map.echo = echo;
        map.echoPhase = phase;
        map.ritualSeventh = ritual;
        map.phaseCount = phaseCount;
    }

    [Test]
    public void Calendar_TheEchoJustLivedAndThePhaseShape()
    {
        Assert.AreEqual(Silence, WorldRhythm.Previous(Resonance), "Resonance begins when Silence ends");
        Assert.AreEqual(Resonance, WorldRhythm.Previous(Crescendo));
        Assert.AreEqual(0, WorldRhythm.Previous(0), "no calendar, no season");
        var settings = Settings();
        var map = EcologyTests.Fresh(42, settings);
        Assert.AreEqual(1f, WorldRhythm.Yields(map, settings), "no calendar: E2 as it was");
        At(map, Crescendo, 2);
        Assert.AreEqual(1.3f, WorldRhythm.Yields(map, settings), 1e-4, "the Phase of Zenith carries Crescendo fully");
        At(map, Crescendo, 1);
        Assert.AreEqual(1.15f, WorldRhythm.Yields(map, settings), 1e-4, "the Phase of Flourish opens it");
        At(map, Silence, 2);
        Assert.AreEqual(0.7f, WorldRhythm.Yields(map, settings), 1e-4, "dormant in the Phase of Repose");
    }

    [Test]
    public void Dens_YieldAndHuntByTheSeason()
    {
        var settings = Settings();
        var map = EcologyTests.Fresh(42, settings);
        var den = EcologyTests.DenOf(map, "herd");
        var tile = map[den.center];
        tile.explored = true;
        float plain = WorldResources.HarvestAt(map, settings, tile, 1f).Sum(a => a.amount);
        At(map, Crescendo, 2);
        Assert.AreEqual(plain * 1.3f, WorldResources.HarvestAt(map, settings, tile, 1f).Sum(a => a.amount), 0.01f);
        At(map, Silence, 2);
        Assert.AreEqual(plain * 0.7f, WorldResources.HarvestAt(map, settings, tile, 1f).Sum(a => a.amount), 0.01f);
    }

    // Groups of a species after one Echo lived through `season`, from the same start.
    private static float After(string spec, int season)
    {
        var settings = Settings();
        var e = settings.ecology;
        e.territorialDispersal = e.roamingDispersal = e.nomadicDispersal = e.expandingDispersal = 0f; // births alone
        var map = EcologyTests.Fresh(42, settings);
        WorldEcology.Tick(map, settings, 0, 1);
        var p = WorldEcology.PopulationAt(map, settings, EcologyTests.DenOf(map, spec));
        p.groups = p.capacity * 0.3f; // room to grow, below the spreading threshold
        WorldEcology.Tick(map, settings, 0, 2, season);
        return p.groups;
    }

    [Test]
    public void Tick_BirthsInResonanceDormancyInSilenceAndDecayFeedsTheDecomposers()
    {
        Assert.Greater(After("herd", Resonance), After("herd", 0), "a spring of births");
        Assert.Less(After("herd", Silence), After("herd", 0), "a dormant winter");
        Assert.Greater(After("burrow", Dissonance), After("burrow", Resonance), "the decomposers thrive on autumn's decay");
        Assert.Less(After("herd", Dissonance), After("herd", Resonance), "while the herds do not");
    }

    [Test]
    public void Silence_HungryPredatorsStrayOntoYourLand()
    {
        var settings = Settings();
        var map = EcologyTests.Fresh(42, settings);
        var pack = EcologyTests.DenOf(map, "pack");
        var herd = EcologyTests.DenOf(map, "herd");
        At(map, Silence, 2);
        Assert.AreEqual(0.25f, WorldRhythm.HungerDanger(map, settings, pack), 1e-4);
        Assert.AreEqual(0f, WorldRhythm.HungerDanger(map, settings, herd), "grazers do not hunt");
        var near = HexCoord.Spiral(map[pack.center].coord, 2).Select(c => map.Get(c)).Where(t => t != null && t.resourceSite < 0 && HexCoord.Distance(t.coord, map[pack.center].coord) == 2).Take(2).ToList();
        near[0].authorityId = WorldAuthority.Player;
        near[1].authorityId = WorldAuthority.Wilderness;
        WorldResources.Refresh(map, settings);
        Assert.Greater(near[0].siteDanger, 0f, "your land near the den");
        Assert.AreEqual(0f, near[1].siteDanger, 0.001f, "not the wilds around it");
        At(map, Crescendo, 2);
        Assert.AreEqual(0f, WorldRhythm.HungerDanger(map, settings, pack), "fed in summer");
    }

    // A deer population after one Ritual Seventh with its range's Coherence at `coherence`.
    private static (float before, float after, int changes) Ritual(float coherence, bool silver = false)
    {
        var settings = Settings();
        settings.Species("deer").structure = 0.3f; // 70% Pure Light: it attunes
        var map = EcologyTests.Fresh(42, settings);
        WorldEcology.Tick(map, settings, 0, 1);
        var p = WorldEcology.PopulationAt(map, settings, EcologyTests.DenOf(map, "herd"));
        p.groups = p.capacity * 0.5f;
        foreach (int i in WorldEcology.Habitat(map, settings, "deer", 0)[p.slot]) { map[i].coherence = coherence; map[i].silver = silver; }
        At(map, Resonance, 3, ritual: true);
        float before = p.groups;
        var changes = WorldRhythm.Attune(map, settings, 0);
        return (before, p.groups, changes.Count(c => c.population == p));
    }

    [Test]
    public void RitualSeventh_PureLightSurgesWhereCoherenceHoldsAndOverloadsWhereItIsThin()
    {
        var (before, after, told) = Ritual(0.9f);
        Assert.Greater(after, before, "attuned, it surges");
        Assert.AreEqual(1, told);
        (before, after, _) = Ritual(0.2f);
        Assert.Less(after, before, "on thin Coherence the attunement overloads it");
        (before, after, _) = Ritual(0.3f);
        Assert.Less(after, before, "0.3 is under the 0.42 a 70% Pure Light lineage needs");
        (before, after, _) = Ritual(0.3f, silver: true);
        Assert.Greater(after, before, "silver water (Lunehymn at its fullest) lifts it past the need");
        var settings = Settings();
        var map = EcologyTests.Fresh(42, settings);
        WorldEcology.Tick(map, settings, 0, 1);
        At(map, Resonance, 3, ritual: true);
        Assert.IsEmpty(WorldRhythm.Attune(map, settings, 0), "a 90% Structure beast does not attune");
    }

    [Test]
    public void RitualSeventh_CalmsTowardThoseWhoLetThemBeAndAHuntThenWoundsTwice()
    {
        var settings = Settings();
        var map = EcologyTests.Fresh(42, settings);
        var b = settings.behavior;
        At(map, Crescendo, 3, ritual: true, phaseCount: 5);
        var gentle = WorldBehavior.Meet(map, "deer", Enclave);
        WorldBehavior.Hunted(map, settings, "deer", WorldAuthority.Player, 1f);
        var hunter = WorldBehavior.Of(map, "deer").Bond(WorldAuthority.Player);
        Assert.AreEqual(-b.huntHarm * settings.rhythm.ritualHarm, hunter.temper, 1e-4, "a hunt on the Ritual Seventh");
        float hunted = hunter.temper;
        WorldRhythm.Attune(map, settings, 0);
        Assert.AreEqual(settings.rhythm.attuneEase, gentle.temper, 1e-4, "those who let it be are heard");
        Assert.AreEqual(hunted, hunter.temper, 1e-4, "not its hunter this Phase");
        At(map, Crescendo, 1, ritual: false, phaseCount: 6);
        WorldBehavior.Hunted(map, settings, "deer", WorldAuthority.Player, 1f);
        Assert.AreEqual(hunted - b.huntHarm, hunter.temper, 1e-4, "an ordinary hunt opening Crescendo (harm 1)");
    }

    [Test]
    public void Behavior_ResonanceForgivesAndDissonanceFractures()
    {
        float Eased(int season)
        {
            var settings = Settings();
            var map = EcologyTests.Fresh(42, settings);
            WorldEcology.Tick(map, settings, 0, 1);
            var den = EcologyTests.DenOf(map, "herd");
            int cell = WorldEcology.Habitat(map, settings, "deer", 0)[map[den.center].habitatSlot][0];
            map[cell].authorityId = Enclave;
            WorldBehavior.Tick(map, settings, 0, season);
            return WorldBehavior.Of(map, "deer").Bond(Enclave).temper;
        }
        Assert.AreEqual(2f * Eased(0), Eased(Resonance), 1e-4, "living beside it in the Echo of Resonance eases it twice as fast");
        Assert.AreEqual(0.5f * Eased(0), Eased(Dissonance), 1e-4);
        var s = Settings();
        var m = EcologyTests.Fresh(42, s);
        At(m, Dissonance, 2);
        WorldBehavior.Hunted(m, s, "deer", WorldAuthority.Player, 1f);
        Assert.AreEqual(-s.behavior.huntHarm * 1.5f, WorldBehavior.Of(m, "deer").Bond(WorldAuthority.Player).temper, 1e-4, "a hunt at Dissonance's height fractures trust half again");
    }
}
