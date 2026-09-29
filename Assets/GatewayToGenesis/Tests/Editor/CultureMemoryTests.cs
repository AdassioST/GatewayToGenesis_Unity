using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

/// <summary>
/// Shared wounds, memorial places and inheritance (T03), with no scene (<see cref="MemoryRules"/>): causes learned once,
/// dedications by id, practice and its capped recovery, dormancy, the lineage an Age passage keeps, the memorial's
/// ground read from the blooms that actually live there, and the memory's save round trip.
/// </summary>
public class CultureMemoryTests
{
    private static CultureStamp At(int seventh, string age = "age-of-renewal") => new CultureStamp { cultureSeventh = seventh, cycle = 1, echo = 1, phase = 1, seventh = 1, ageId = age };

    private static CultureEntityRef AshLoaf(string label = "Ash-Loaf") => CultureEntityRef.Of(CultureEntityKind.Recipe, "ash-loaf", label);

    private static MemoryState WithHunger(out MemoryEvidence hunger)
    {
        var state = new MemoryState();
        hunger = MemoryRules.Learn(state, MemoryRules.Crisis("age-of-desolation", "Age of Desolation", "The Inescapable Hunger", 1, 40, 60, At(10), witnessed: true));
        return state;
    }

    // ===== CAUSES =====

    [Test]
    public void Learn_ACauseIsRememberedOnce_WitnessedOrReadLater()
    {
        var state = WithHunger(out var hunger);
        Assert.IsNotNull(hunger);
        Assert.AreEqual("crisis:age-of-desolation:1", hunger.id);
        Assert.AreEqual(40, hunger.deaths);
        Assert.IsFalse(hunger.reconstructed);

        // The same passage read back from AgeProgression's history after a load: already remembered, nothing changes.
        var again = MemoryRules.Crisis("age-of-desolation", "Age of Desolation", "The Inescapable Hunger", 1, 40, 60, At(99), witnessed: false);
        Assert.IsNull(MemoryRules.Learn(state, again));
        Assert.AreEqual(1, state.evidence.Count);
        Assert.AreSame(hunger, state.evidence[0]);
        Assert.AreEqual(10, state.evidence[0].recorded.cultureSeventh, "the record already kept is never rewritten");
        Assert.IsFalse(state.evidence[0].reconstructed);
    }

    [Test]
    public void Learn_AReconstructedCauseKeepsItsMomentUnknown()
    {
        var state = new MemoryState();
        var fall = MemoryRules.Learn(state, MemoryRules.Fall(7, "Emberhold", "overrun by danger", 12, "age-of-desolation", At(30), witnessed: false));
        Assert.IsTrue(fall.reconstructed);
        Assert.IsNull(fall.occurred, "learned from older records: when it happened is not known, and not invented");
        Assert.AreEqual("age-of-desolation", fall.occurredAge);
        Assert.AreEqual(30, fall.recorded.cultureSeventh);
    }

    [Test]
    public void LossReloadRecovery_NeverDuplicatesAMemorial()
    {
        var state = new MemoryState();
        MemoryRules.Learn(state, MemoryRules.Fall(7, "Emberhold", "pillaged", 12, "age-of-desolation", At(5), witnessed: true));
        // A reload reads the ruin again; a reclaim reports the fall once more before its recovery.
        var reloaded = RoundTrip(state);
        Assert.IsNull(MemoryRules.Learn(reloaded, MemoryRules.Fall(7, "Emberhold", "pillaged", 12, "age-of-desolation", At(9), witnessed: false)));
        var recovered = MemoryRules.Learn(reloaded, MemoryRules.Recovered(7, "Emberhold", "New Ember", 12, At(20), witnessed: true));
        Assert.IsNull(MemoryRules.Learn(reloaded, MemoryRules.Fall(7, "Emberhold", "pillaged", 12, "age-of-desolation", At(20), witnessed: false)));
        Assert.IsNull(MemoryRules.Learn(reloaded, MemoryRules.Recovered(7, "Emberhold", "New Ember", 12, At(21), witnessed: false)));

        Assert.AreEqual(2, reloaded.evidence.Count, "one fall and one recovery, however often they are reported");
        Assert.AreEqual(1, reloaded.evidence.Count(e => e.cause == MemoryCause.SettlementFall));
        Assert.AreEqual("fall:7", recovered.related, "the recovery points at its fall instead of replacing it");
    }

    [Test]
    public void LegendLost_IsAFutureWorldTruthLinkOnly()
    {
        var state = new MemoryState();
        var lost = MemoryRules.Learn(state, MemoryRules.LegendLost("Vaelia", At(3), witnessed: true));
        Assert.AreEqual("legend-lost:Vaelia", lost.id);
        Assert.IsTrue(lost.worldTruthCandidate);
        Assert.AreEqual(CultureEntityKind.Legend, lost.source.kind);
        // Nothing in the memory canonizes: there is no state for it beyond the link.
        Assert.AreEqual(0, state.dedications.Count);
    }

    // ===== DEDICATIONS =====

    [Test]
    public void Dedicate_OnlyExistingPractices_OneMemoryPerPractice()
    {
        var state = WithHunger(out var hunger);
        var tuning = new MemoryTuning();
        Assert.IsNotNull(MemoryRules.WhyNotDedicate(state, hunger.id, AshLoaf(), exists: false, tuning), "a dish the people do not have cannot be dedicated");
        Assert.IsNotNull(MemoryRules.WhyNotDedicate(state, hunger.id, CultureEntityRef.Of(CultureEntityKind.Resource, "Food"), exists: true, tuning), "only dishes, landmarks, rites and holidays");
        Assert.IsNotNull(MemoryRules.WhyNotDedicate(state, "crisis:nothing:1", AshLoaf(), exists: true, tuning));

        var d = MemoryRules.Dedicate(state, hunger.id, AshLoaf(), true, At(11), tuning);
        Assert.IsNotNull(d);
        Assert.AreEqual("ded-1", d.id);
        Assert.AreEqual(DedicationStatus.Active, d.status);
        CollectionAssert.AreEqual(new[] { "age-of-renewal" }, d.ages);

        var plague = MemoryRules.Learn(state, MemoryRules.Crisis("age-of-renewal", "Age of Renewal", "The Great Plague", 2, 10, 90, At(50), true));
        Assert.IsNull(MemoryRules.Dedicate(state, plague.id, AshLoaf(), true, At(51), tuning), "the Ash-Loaf already remembers the Hunger");
        StringAssert.Contains("release it first", MemoryRules.WhyNotDedicate(state, plague.id, AshLoaf(), true, tuning));
        Assert.AreEqual(1, state.dedications.Count, "dedicating makes a link, never a new recipe or a second record");

        Assert.IsTrue(MemoryRules.Release(state, d.id));
        var moved = MemoryRules.Dedicate(state, plague.id, AshLoaf(), true, At(52), tuning);
        Assert.IsNotNull(moved, "released, it can carry another memory");
        Assert.AreEqual("ded-2", moved.id, "ids are never reused");
        Assert.AreEqual(DedicationStatus.Released, state.Dedication("ded-1").status, "the released record stays in the history");
    }

    [Test]
    public void Dedicate_AWoundHoldsOnlySoManyPractices()
    {
        var state = WithHunger(out var hunger);
        var tuning = new MemoryTuning { maxDedicationsPerWound = 2 };
        Assert.IsNotNull(MemoryRules.Dedicate(state, hunger.id, AshLoaf(), true, At(1), tuning));
        Assert.IsNotNull(MemoryRules.Dedicate(state, hunger.id, CultureEntityRef.Of(CultureEntityKind.Activity, "remembrance", "Rite of Remembrance"), true, At(1), tuning));
        Assert.IsNull(MemoryRules.Dedicate(state, hunger.id, CultureEntityRef.Of(CultureEntityKind.Holiday, "Day of Ash", "Day of Ash"), true, At(1), tuning));
    }

    [Test]
    public void Dedication_SurvivesARenamedRecipeThroughItsId()
    {
        var state = WithHunger(out var hunger);
        var d = MemoryRules.Dedicate(state, hunger.id, CultureEntityRef.Of(CultureEntityKind.Recipe, "invented-1", "Grandmother's Loaf"), true, At(2), new MemoryTuning());
        // The recipe is renamed: a reference by its new name is still the same recipe.
        var renamed = CultureEntityRef.Of(CultureEntityKind.Recipe, "invented-1", "Loaf of the Starved");
        Assert.AreSame(d, MemoryRules.ActiveFor(state, renamed));
        Assert.IsTrue(MemoryRules.Practise(state, MemoryRules.ActiveFor(state, renamed), 3));
        Assert.AreEqual(1, d.practiced);
        Assert.AreEqual("Grandmother's Loaf", d.target.label, "the name it was dedicated under is kept for the history");
        // A save round trip keeps the link.
        var loaded = RoundTrip(state);
        Assert.IsNotNull(MemoryRules.ActiveFor(loaded, renamed));
        // A different recipe with the old name is not the same dish.
        Assert.IsNull(MemoryRules.ActiveFor(loaded, CultureEntityRef.Of(CultureEntityKind.Recipe, "invented-2", "Grandmother's Loaf")));
    }

    [Test]
    public void Settle_APracticeGoneSleeps_AndWakesWhenItReturns()
    {
        var state = WithHunger(out var hunger);
        var shrine = CultureEntityRef.Of(CultureEntityKind.Landmark, CultureIds.Landmark(4, "shrine"), "Shrine of Ash");
        var d = MemoryRules.Dedicate(state, hunger.id, shrine, true, At(1), new MemoryTuning());
        bool standing = false;
        var changed = MemoryRules.Settle(state, r => standing, r => "its settlement fell");
        Assert.AreEqual(1, changed.Count);
        Assert.AreEqual(DedicationStatus.Dormant, d.status);
        Assert.AreEqual("its settlement fell", d.dormantReason);
        Assert.IsNull(MemoryRules.ActiveFor(state, shrine), "a dormant dedication carries nothing");
        Assert.IsFalse(MemoryRules.Practise(state, d, 5));
        Assert.AreEqual(0, MemoryRules.Recovery(state, 5, new MemoryTuning()));
        Assert.AreEqual(1, state.dedications.Count, "nothing is deleted");

        standing = true;
        MemoryRules.Settle(state, r => standing);
        Assert.AreEqual(DedicationStatus.Active, d.status);
        Assert.IsNull(d.dormantReason);
    }

    // ===== PRACTICE AND RECOVERY =====

    [Test]
    public void Quiet_OrdinaryMourningNeedsNoFeastNorHighMorale()
    {
        var state = WithHunger(out var hunger);
        var tuning = new MemoryTuning();
        // The rule is asked with nothing: no stores, no Unity, no morale enter it at all.
        Assert.IsNull(MemoryRules.WhyNotQuiet(state, hunger.id, founded: true, seventh: 4, tuning));
        Assert.IsNotNull(MemoryRules.WhyNotQuiet(state, hunger.id, founded: false, seventh: 4, tuning));
        Assert.IsTrue(MemoryRules.Keep(state, hunger.id, 4, "a quiet remembrance", quiet: true));
        StringAssert.Contains("not long ago", MemoryRules.WhyNotQuiet(state, hunger.id, true, 5, tuning));
        Assert.IsNull(MemoryRules.WhyNotQuiet(state, hunger.id, true, 4 + tuning.quietCooldownSevenths, tuning));
        Assert.AreEqual(tuning.moralePerWound, MemoryRules.Recovery(state, 4, tuning));
    }

    [Test]
    public void Keep_CountsOncePerSeventh_HoweverManyPracticesKeepIt()
    {
        var state = WithHunger(out var hunger);
        var tuning = new MemoryTuning();
        var loaf = MemoryRules.Dedicate(state, hunger.id, AshLoaf(), true, At(1), tuning);
        var rite = MemoryRules.Dedicate(state, hunger.id, CultureEntityRef.Of(CultureEntityKind.Activity, "remembrance"), true, At(1), tuning);
        Assert.IsTrue(MemoryRules.Practise(state, loaf, 8));
        Assert.IsFalse(MemoryRules.Practise(state, loaf, 8), "a second batch the same Seventh is the same keeping");
        Assert.IsTrue(MemoryRules.Practise(state, rite, 8));
        MemoryRules.Keep(state, hunger.id, 8, "a quiet remembrance", quiet: true);
        Assert.AreEqual(1, state.PracticeOf(hunger.id).kept);
        Assert.AreEqual(tuning.moralePerWound, MemoryRules.Recovery(state, 8, tuning), "one wound, one share, however it was kept");
    }

    [Test]
    public void Recovery_RepeatedLossesCannotStackPastTheCap_AndTheLossAloneGivesNothing()
    {
        var state = new MemoryState();
        var tuning = new MemoryTuning { moralePerWound = 1, moraleCap = 3 };
        for (int i = 0; i < 12; i++) MemoryRules.Learn(state, MemoryRules.Fall(i, "Town " + i, "pillaged", i, null, At(1), true));
        Assert.AreEqual(0, MemoryRules.Recovery(state, 2, tuning), "twelve falls, none kept: nothing");
        foreach (var e in state.evidence) MemoryRules.Keep(state, e.id, 2, "a quiet remembrance", true);
        Assert.AreEqual(3, MemoryRules.Recovery(state, 2, tuning), "twelve kept wounds support no more than the cap");
        // Keeping them again and again over the Sevenths never raises it.
        for (int s = 3; s < 40; s++) foreach (var e in state.evidence) MemoryRules.Keep(state, e.id, s, "again");
        Assert.AreEqual(3, MemoryRules.Recovery(state, 39, tuning));
    }

    [Test]
    public void Recovery_Lapses_WhenTheMemoryIsNoLongerKept()
    {
        var state = WithHunger(out var hunger);
        var tuning = new MemoryTuning { practiceWindowSevenths = 21 };
        MemoryRules.Keep(state, hunger.id, 10, "a quiet remembrance", true);
        Assert.AreEqual(1, MemoryRules.Recovery(state, 30, tuning));
        Assert.AreEqual(0, MemoryRules.Recovery(state, 31, tuning), "a Phase later with no keeping, the support is gone");
        Assert.IsNotNull(state.Evidence(hunger.id), "the wound itself stays remembered");
    }

    // ===== THE AGE PASSES =====

    [Test]
    public void Pass_ANewAgeRetainsTheLineage_OncePerPassage()
    {
        var state = WithHunger(out var hunger);
        var tuning = new MemoryTuning();
        var loaf = MemoryRules.Dedicate(state, hunger.id, AshLoaf(), true, At(1, "age-of-renewal"), tuning);
        var shrine = MemoryRules.Dedicate(state, hunger.id, CultureEntityRef.Of(CultureEntityKind.Landmark, "4:shrine", "Shrine of Ash"), true, At(1, "age-of-renewal"), tuning);
        MemoryRules.Settle(state, r => r.kind != CultureEntityKind.Landmark, r => "its settlement fell");
        var practices = new[] { CultureEntityRef.Of(CultureEntityKind.Holiday, "Day of Ash"), CultureEntityRef.Of(CultureEntityKind.Tradition, "trad-1", "naming-of-the-lost"), CultureEntityRef.Of(CultureEntityKind.Holiday, "Day of Ash") };

        var lineage = MemoryRules.Pass(state, "age-of-renewal", "Age of Renewal", "age-of-embers", "Age of Embers", 2, At(80), practices);
        Assert.IsNotNull(lineage);
        Assert.AreEqual("lineage:age-of-renewal:2", lineage.id);
        CollectionAssert.AreEqual(new[] { hunger.id }, lineage.evidence);
        Assert.AreEqual(2, lineage.dedications.Count);
        Assert.AreEqual(2, lineage.practices.Count, "practices are kept once each");
        Assert.IsNull(MemoryRules.Pass(state, "age-of-renewal", "Age of Renewal", "age-of-embers", "Age of Embers", 2, At(81), practices), "a passage is kept once");

        CollectionAssert.AreEqual(new[] { "age-of-renewal", "age-of-embers" }, loaf.ages);
        Assert.IsTrue(loaf.inherited);
        Assert.AreEqual(DedicationStatus.Dormant, shrine.status, "a dormant memorial is carried as it is, not dropped");
        Assert.IsTrue(shrine.inherited);
        // The lineage is a copy: what happens after the passage does not rewrite it.
        MemoryRules.Release(state, loaf.id);
        Assert.AreEqual(DedicationStatus.Active, lineage.dedications.First(d => d.id == loaf.id).status);

        // The new Age may recognize what it inherited; nothing does so by itself.
        var active = state.Dedication(shrine.id);
        Assert.IsNull(active.recognizedAge);
        Assert.IsNull(MemoryRules.WhyNotRecognize(active, "age-of-embers"));
        Assert.IsTrue(MemoryRules.Recognize(active, "age-of-embers"));
        Assert.IsNotNull(MemoryRules.WhyNotRecognize(active, "age-of-embers"));
        Assert.IsNotNull(MemoryRules.WhyNotRecognize(loaf, "age-of-embers"), "a released memorial is not recognized");

        var fresh = MemoryRules.Dedicate(state, hunger.id, CultureEntityRef.Of(CultureEntityKind.Activity, "song"), true, At(90, "age-of-embers"), tuning);
        StringAssert.Contains("nothing to inherit", MemoryRules.WhyNotRecognize(fresh, "age-of-embers"));
    }

    // ===== THE MEMORIAL'S GROUND =====

    private static WorldGenSettings EcologySettings()
    {
        var s = WorldGenerationTests.Settings();
        s.resourceSites.Add(new ResourceSiteSpec { id = "candle", name = "Candle", kind = ResourceKind.Bloom, niche = BloomNiche.Healer, count = 0, size = 1, residueNeed = 0.4f, drawnBy = BloomDraw.Suffering, untouchable = "No." });
        s.resourceSites.Add(new ResourceSiteSpec { id = "marigold", name = "Marigold", kind = ResourceKind.Bloom, niche = BloomNiche.Listener, count = 0, size = 1, residueNeed = 0.2f, drawnBy = BloomDraw.Content, untouchable = "No." });
        return s;
    }

    private static WorldMap Map(WorldGenSettings settings) => WorldGenerator.Generate(7, settings, WorldGenerationTests.ParsedStencil(), WorldGenerationTests.Tiles());

    private static ResourceSite Plant(WorldMap map, string spec, WorldTile at, float vigor = 1f)
    {
        var site = new ResourceSite { index = map.ResourceSites.Count, spec = spec, name = spec, kind = ResourceKind.Bloom, center = at.index, vigor = vigor };
        site.cells.Add(at.index);
        map.ResourceSites.Add(site);
        return site;
    }

    private static WorldTile LandNear(WorldMap map, WorldTile from, int distance) =>
        map.Tiles.First(t => !t.water && t.resourceSite < 0 && HexCoord.Distance(t.coord, from.coord) == distance);

    [Test]
    public void Ground_WithoutASuitableBloom_IsNoGarden_AndChangesNothing()
    {
        var settings = EcologySettings();
        var map = Map(settings);
        map.ResourceSites.RemoveAll(r => r.kind == ResourceKind.Bloom);
        var centre = map.Tiles.First(t => !t.water && t.resourceSite < 0 && map.Tiles.Count(n => !n.water && HexCoord.Distance(n.coord, t.coord) <= 2) > 10);
        var residue = map.Tiles.Select(t => t.residue).ToArray();
        var vigor = map.ResourceSites.Select(r => r.vigor).ToArray();

        var ground = MemoryRules.Ground(map, settings, centre.index, "the ruins of Emberhold", new MemoryTuning());
        Assert.IsEmpty(ground.blooms);
        Assert.IsFalse(ground.Suitable);
        Assert.AreEqual(0f, ground.Bonus, "grief does not conjure a garden");

        // A grief bloom nearby whose own need is not met: still no garden, and nothing feeds it.
        var near = LandNear(map, centre, 1);
        near.residue = 0.1f;
        var candle = Plant(map, "candle", near);
        ground = MemoryRules.Ground(map, settings, centre.index, "the ruins of Emberhold", new MemoryTuning());
        Assert.AreEqual(1, ground.blooms.Count);
        Assert.IsFalse(ground.Suitable);
        Assert.IsTrue(ground.blooms[0].griefLinked);
        StringAssert.Contains("too faint", ground.blooms[0].why);
        Assert.AreEqual(0f, ground.Bonus);
        Assert.AreEqual(0.1f, near.residue, 1e-6f, "reading the ground never feeds a bloom");
        Assert.AreEqual(1f, candle.vigor);
        CollectionAssert.AreEqual(residue.Where((r, i) => i != near.index), map.Tiles.Where(t => t.index != near.index).Select(t => t.residue));
        CollectionAssert.AreEqual(vigor, map.ResourceSites.Take(vigor.Length).Select(r => r.vigor));
    }

    [Test]
    public void Ground_ABloomLivingByItsOwnNeeds_MakesTheGarden_EachSpeciesByItsOwnNeed()
    {
        var settings = EcologySettings();
        var map = Map(settings);
        map.ResourceSites.RemoveAll(r => r.kind == ResourceKind.Bloom);
        foreach (var t in map.Tiles) t.resourceSite = -1;
        var centre = map.Tiles.First(t => !t.water && map.Tiles.Count(n => !n.water && HexCoord.Distance(n.coord, t.coord) == 1) >= 3);
        var a = LandNear(map, centre, 1);
        var b = map.Tiles.First(t => !t.water && t != a && HexCoord.Distance(t.coord, centre.coord) == 1);
        a.residue = b.residue = 0.3f;
        a.coherence = b.coherence = 0.5f;
        Plant(map, "candle", a);
        Plant(map, "marigold", b);

        var ground = MemoryRules.Ground(map, settings, centre.index, "Shrine of Ash", new MemoryTuning());
        Assert.IsTrue(ground.Suitable, "the marigold lives well on this feeling");
        Assert.IsTrue(ground.blooms.Single(x => x.spec == "marigold").thriving);
        Assert.IsFalse(ground.blooms.Single(x => x.spec == "candle").thriving, "the same comfort does not feed the grief bloom: it needs more");
        Assert.AreEqual(0f, ground.Bonus, "the first release shows the garden; it gives no bonus");

        // A withering bloom is no garden even where its need is met.
        map.ResourceSites.Single(r => r.spec == "marigold").vigor = 0.2f;
        Assert.IsFalse(MemoryRules.Ground(map, settings, centre.index, "Shrine of Ash", new MemoryTuning()).Suitable);
        // Beyond the garden's reach, a bloom is not the memorial's.
        Assert.IsEmpty(MemoryRules.Ground(map, settings, centre.index, "Shrine of Ash", new MemoryTuning { gardenReach = 0 }).blooms);
    }

    // ===== CONTENT AND SAVES =====

    [Test]
    public void Suggestions_NameRealRecipesAndRites_WithTheirCanonStatus()
    {
        var life = new CultureLifeTuning();
        foreach (var s in MemorialSuggestion.All)
        {
            Assert.IsNotEmpty(s.source, "every authored memorial cites its vault note");
            if (s.kind == CultureEntityKind.Recipe) Assert.IsNotNull(life.Recipe(s.target), s.target);
            if (s.kind == CultureEntityKind.Activity) Assert.IsNotNull(life.Activity(s.target), s.target);
            if (s.kind == CultureEntityKind.Landmark) Assert.IsNotNull(life.Landmark(s.target), s.target);
        }
        var hunger = MemoryRules.Crisis("age-of-desolation", "Age of Desolation", "The Inescapable Hunger", 1, 0, 10, At(1), true);
        var ash = MemorialSuggestion.All.Single(s => s.Fits(hunger) && s.kind == CultureEntityKind.Recipe);
        Assert.AreEqual("ash-loaf", ash.target);
        Assert.AreEqual(CanonStatus.ExplicitCanon, ash.status);
        var plague = MemoryRules.Crisis("age-of-renewal", "Age of Renewal", "The Great Plague", 2, 0, 10, At(1), true);
        Assert.IsFalse(MemorialSuggestion.All.Any(s => s.Fits(plague) && s.target == "ash-loaf"), "the Ash-Loaf belongs to the Hunger");
    }

    [Test]
    public void Save_TheMemoryRoundTripsInsideTheCultureState_AndOlderStatesLoad()
    {
        var culture = new CultureState();
        CultureMigration.Ensure(culture);
        var state = culture.extensions.memory;
        var hunger = MemoryRules.Learn(state, MemoryRules.Crisis("age-of-desolation", "Age of Desolation", "The Inescapable Hunger", 1, 40, 60, At(10), true));
        MemoryRules.Learn(state, MemoryRules.Fall(3, "Emberhold", "pillaged", 5, null, At(12), false));
        var d = MemoryRules.Dedicate(state, hunger.id, AshLoaf(), true, At(11), new MemoryTuning());
        MemoryRules.Practise(state, d, 12);
        MemoryRules.Pass(state, "age-of-renewal", "Age of Renewal", "age-of-embers", "Age of Embers", 2, At(13), new[] { AshLoaf() });

        var node = SaveStateCodec.Write(culture, typeof(CultureState));
        var loaded = (CultureState)SaveStateCodec.Read(node, typeof(CultureState));
        var m = loaded.extensions.memory;
        Assert.AreEqual(2, m.evidence.Count);
        Assert.IsNull(m.Evidence("fall:3").occurred, "an unknown moment stays unknown after a load");
        Assert.AreEqual("ded-1", m.dedications.Single().id);
        Assert.AreEqual(2, m.nextDedication);
        Assert.AreEqual(1, m.PracticeOf(hunger.id).kept);
        Assert.AreEqual(1, m.lineage.Count);
        Assert.AreEqual(CultureEntityKind.Recipe, m.lineage[0].dedications[0].target.kind);
        // Loading twice changes nothing.
        var twice = (CultureState)SaveStateCodec.Read(SaveStateCodec.Write(loaded, typeof(CultureState)), typeof(CultureState));
        Assert.AreEqual(Flat(SaveStateCodec.Write(loaded, typeof(CultureState))), Flat(SaveStateCodec.Write(twice, typeof(CultureState))));

        // An envelope saved before the memory existed loads with a fresh, empty memory.
        var older = SaveStateCodec.Write(new CultureExtensionState(), typeof(CultureExtensionState));
        older.children.RemoveAll(c => c.name == "memory");
        var envelope = (CultureExtensionState)SaveStateCodec.Read(older, typeof(CultureExtensionState));
        Assert.IsNotNull(envelope.memory);
        Assert.IsEmpty(envelope.memory.evidence);
    }

    private static string Flat(StateNode n) => n == null ? "~" : $"{n.name}|{n.kind}|{n.value}(" + string.Join(",", n.children.Select(Flat)) + ")";

    private static MemoryState RoundTrip(MemoryState state) => (MemoryState)SaveStateCodec.Read(SaveStateCodec.Write(state, typeof(MemoryState)), typeof(MemoryState));
}
