using System.Linq;
using NUnit.Framework;

/// <summary>
/// Behavior, the species' memory, with no scene (<see cref="WorldBehavior"/>; vault: Arcanorian Ecology.md, "Behavior: The Memory of a Lineage"): the response
/// ladders, hunting and habitat taken, first encounters, living beside a species, the word spreading, scar spectra, and
/// what behavior changes (pressure, hunts, danger). Uses the catalog of <see cref="EcologyTests"/>.
/// </summary>
public class BehaviorTests
{
    private const string Enclave = "enclave:0";

    [Test]
    public void Response_MovesAStepAlongItsLadderPerStepOfTemper()
    {
        var b = new BehaviorSettings();
        Assert.AreEqual(ThreatResponse.FleesWhenThreatened, WorldBehavior.Response(b, ThreatResponse.FleesWhenThreatened, 0.3f), "under a step, its nature");
        Assert.AreEqual(ThreatResponse.FleesOnSight, WorldBehavior.Response(b, ThreatResponse.FleesWhenThreatened, -0.4f), "a docile herd hunted learns to flee on sight");
        Assert.AreEqual(ThreatResponse.Tolerates, WorldBehavior.Response(b, ThreatResponse.FleesWhenThreatened, 0.4f), "and one left in peace lets you near");
        Assert.AreEqual(ThreatResponse.FleesOnSight, WorldBehavior.Response(b, ThreatResponse.FleesOnSight, -1f), "the top of the fear ladder is as far as fear goes");
        Assert.AreEqual(ThreatResponse.Tolerates, WorldBehavior.Response(b, ThreatResponse.FleesOnSight, 0.7f), "two steps gentler");
        Assert.AreEqual(ThreatResponse.AttacksOnSight, WorldBehavior.Response(b, ThreatResponse.DefendsTerritory, -0.4f), "a territorial herd harmed attacks");
        Assert.AreEqual(ThreatResponse.DefendsTerritory, WorldBehavior.Response(b, ThreatResponse.Hunts, 0.4f), "a pack that learned you mean no harm only defends its ground");
        Assert.AreEqual(ThreatResponse.DefendsWhenThreatened, WorldBehavior.Response(b, ThreatResponse.Hunts, 1f));
        Assert.IsTrue(WorldBehavior.Harsher(ThreatResponse.FleesWhenThreatened, ThreatResponse.FleesOnSight));
        Assert.IsFalse(WorldBehavior.Harsher(ThreatResponse.FleesWhenThreatened, ThreatResponse.Tolerates));
    }

    [Test]
    public void Hunting_TheHunterIsRememberedAndTheWordSpreads()
    {
        var settings = EcologyTests.Settings();
        var map = EcologyTests.Fresh(42, settings);
        var b = settings.behavior;
        WorldBehavior.Meet(map, "deer", Enclave);
        for (int i = 0; i < 7; i++) WorldBehavior.Hunted(map, settings, "deer", WorldAuthority.Player, 1f);
        var memory = WorldBehavior.Of(map, "deer");
        Assert.AreEqual(-7 * b.huntHarm, memory.Bond(WorldAuthority.Player).temper, 0.001f);
        Assert.AreEqual(7, memory.Bond(WorldAuthority.Player).hunts);
        Assert.AreEqual(-7 * b.huntHarm * b.spread, memory.overall, 0.001f, "the lineage learns a share of it");
        Assert.AreEqual(0f, memory.Bond(Enclave).temper, "an authority already met keeps its own bond");
        Assert.AreEqual(ThreatResponse.FleesOnSight, WorldBehavior.Toward(map, settings, settings.Species("deer"), WorldAuthority.Player));
        Assert.AreEqual(ThreatResponse.FleesWhenThreatened, WorldBehavior.Toward(map, settings, settings.Species("deer"), Enclave), "it tells its hunters from the others");
        Assert.AreEqual(WorldBehavior.Key(WorldAuthority.Outpost), WorldAuthority.Player, "your outposts are you");
        // A newcomer meets what the lineage has learned.
        var stranger = WorldBehavior.Meet(map, "deer", "enclave:9");
        Assert.AreEqual(memory.overall, stranger.temper, 0.001f);
    }

    [Test]
    public void Tick_YouMeetASpeciesByIdentifyingItsDenAndHoldersByLivingInItsRange()
    {
        var settings = EcologyTests.Settings();
        var map = EcologyTests.Fresh(42, settings);
        WorldEcology.Tick(map, settings, 0, 1);
        var den = EcologyTests.DenOf(map, "herd");
        foreach (var site in map.ResourceSites.Where(s => s.spec == "herd")) foreach (int c in site.cells) map[c].explored = false;
        // Nothing of yours in its range either (living there is also an encounter).
        foreach (int i in WorldEcology.Habitat(map, settings, "deer", 0).Values.SelectMany(c => c))
            if (WorldAuthority.IsPlayers(map[i].authorityId)) map[i].authorityId = WorldAuthority.Wilderness;
        WorldBehavior.Tick(map, settings, 0);
        Assert.IsFalse(WorldBehavior.Met(map, "deer", WorldAuthority.Player), "unseen, not met");
        map[den.center].explored = true;
        WorldBehavior.Tick(map, settings, 0);
        Assert.IsTrue(WorldBehavior.Met(map, "deer", WorldAuthority.Player));
        var cells = WorldEcology.Habitat(map, settings, "deer", 0)[map[den.center].habitatSlot];
        foreach (int i in cells.Take(cells.Count / 2)) map[i].authorityId = Enclave;
        WorldBehavior.Tick(map, settings, 0);
        var bond = WorldBehavior.Of(map, "deer").Bond(Enclave);
        Assert.IsNotNull(bond, "an authority holding its land meets it");
        Assert.Greater(bond.heldShare, 0f);
        Assert.Greater(bond.temper, 0f, "land already held when they met is not counted as taken; living beside it eases it");
    }

    [Test]
    public void Habitat_TakingItCostsTemperAndLivingBesideItUnhuntedEasesIt()
    {
        var settings = EcologyTests.Settings();
        var map = EcologyTests.Fresh(42, settings);
        var b = settings.behavior;
        WorldEcology.Tick(map, settings, 0, 1);
        var den = EcologyTests.DenOf(map, "herd");
        var cells = WorldEcology.Habitat(map, settings, "deer", 0)[map[den.center].habitatSlot];
        map[cells[0]].authorityId = Enclave;
        WorldBehavior.Tick(map, settings, 0);
        var bond = WorldBehavior.Of(map, "deer").Bond(Enclave);
        float before = bond.temper, share = bond.heldShare;
        foreach (int i in cells) map[i].authorityId = Enclave;
        WorldBehavior.Tick(map, settings, 0);
        Assert.Less(bond.temper, before, "taking the rest of its habitat hurts it");
        Assert.AreEqual(before - (bond.heldShare - share) * b.habitatHarm + b.coexist, bond.temper, 0.02f);
        for (int echo = 0; echo < 40; echo++) WorldBehavior.Tick(map, settings, 0);
        Assert.AreEqual(b.coexistCap, bond.temper, 0.001f, "living beside it calms it, up to one step");
        Assert.AreEqual(ThreatResponse.Tolerates, WorldBehavior.Toward(map, settings, settings.Species("deer"), Enclave));
        float calm = bond.temper;
        WorldBehavior.Hunted(map, settings, "deer", Enclave, 1f);
        WorldBehavior.Tick(map, settings, 0);
        Assert.AreEqual(calm - b.huntHarm, bond.temper, 0.001f, "an Echo with a hunt brings no easing");
        WorldBehavior.Befriend(map, settings, "deer", Enclave, 0.5f);
        Assert.Greater(bond.temper, b.coexistCap, "befriending goes past what living beside it can");
    }

    [Test]
    public void Tick_TellsYouWhenItsBehaviorTowardYouMovesAStep()
    {
        var settings = EcologyTests.Settings();
        var map = EcologyTests.Fresh(42, settings);
        WorldEcology.Tick(map, settings, 0, 1);
        var den = EcologyTests.DenOf(map, "herd");
        map[den.center].explored = true;
        WorldBehavior.Tick(map, settings, 0);
        foreach (int i in WorldEcology.Habitat(map, settings, "deer", 0).Values.SelectMany(c => c)) map[i].authorityId = WorldAuthority.Player;
        var changes = WorldBehavior.Tick(map, settings, 0);
        var change = changes.FirstOrDefault(c => c.species.id == "deer");
        Assert.IsNotNull(change, "taking all its land at once turns it");
        Assert.IsFalse(change.gentler);
        Assert.AreEqual(ThreatResponse.FleesOnSight, change.to);
    }

    [Test]
    public void AgePassed_ScarSpectraKeepAShareOfTheMemory()
    {
        var settings = EcologyTests.Settings();
        var map = EcologyTests.Fresh(42, settings);
        WorldBehavior.Befriend(map, settings, "wolf", Enclave, 1f);
        var memory = WorldBehavior.Of(map, "wolf");
        float overall = memory.overall;
        WorldBehavior.AgePassed(map, settings);
        Assert.AreEqual(settings.behavior.memoryKept, memory.Bond(Enclave).temper, 0.001f);
        Assert.AreEqual(overall * settings.behavior.memoryKept, memory.overall, 0.001f);
    }

    [Test]
    public void Consequences_FleeingHerdsYieldLessHostilePacksAreDangerousAndGentleOnesMindPeopleLess()
    {
        var settings = EcologyTests.Settings();
        var map = EcologyTests.Fresh(42, settings);
        var b = settings.behavior;
        WorldEcology.Tick(map, settings, 0, 1);
        var herd = EcologyTests.DenOf(map, "herd");
        var pack = EcologyTests.DenOf(map, "pack");
        Assert.AreEqual(1f, WorldBehavior.HuntShare(map, settings, herd, WorldAuthority.Player));
        Assert.AreEqual(0f, WorldBehavior.DenDanger(map, settings, pack), "a pack is not a danger for its nature alone");
        for (int i = 0; i < 7; i++) WorldBehavior.Hunted(map, settings, "deer", WorldAuthority.Player, 1f);
        Assert.AreEqual(b.warySpoils, WorldBehavior.HuntShare(map, settings, herd, WorldAuthority.Player), "it has learned to flee your hunters");
        Assert.AreEqual(1f, WorldBehavior.HuntShare(map, settings, herd, Enclave), "not the others'");
        // A pack that has learned to defend itself from you, then turned against you.
        WorldBehavior.Befriend(map, settings, "wolf", WorldAuthority.Player, 0.8f);
        Assert.AreEqual(0f, WorldBehavior.DenDanger(map, settings, pack), "calmer than its nature, it is no danger");
        for (int i = 0; i < 30; i++) WorldBehavior.Hunted(map, settings, "wolf", WorldAuthority.Player, 1f);
        Assert.AreEqual(ThreatResponse.Hunts, WorldBehavior.Toward(map, settings, settings.Species("wolf"), WorldAuthority.Player), "a hunter cannot grow harsher than hunting");
        Assert.AreEqual(0f, WorldBehavior.DenDanger(map, settings, pack));
        // A territorial herd that attacks you on sight casts danger around its den.
        settings.Species("mole").diet = CreatureDiet.Detritivore;
        settings.Species("mole").subgroup = CreatureSubgroup.Territorial;
        var burrow = EcologyTests.DenOf(map, "burrow");
        for (int i = 0; i < 7; i++) WorldBehavior.Hunted(map, settings, "mole", WorldAuthority.Player, 1f);
        Assert.AreEqual(ThreatResponse.AttacksOnSight, WorldBehavior.Toward(map, settings, settings.Species("mole"), WorldAuthority.Player));
        Assert.AreEqual(b.hostileDanger, WorldBehavior.DenDanger(map, settings, burrow));
        WorldResources.Refresh(map, settings);
        Assert.GreaterOrEqual(map[burrow.center].siteDanger, b.hostileDanger - 0.001f, "the den's own cell carries it");
        // A species that tolerates whoever presses its land minds them as little as the unmoved.
        var deer = settings.Species("deer");
        var ground = new WorldTile { coherence = 0.9f };
        Assert.Greater(WorldEcology.Quality(settings.ecology, deer, ground, 0.4f, ThreatResponse.Tolerates),
            WorldEcology.Quality(settings.ecology, deer, ground, 0.4f, ThreatResponse.FleesOnSight));
    }
}
