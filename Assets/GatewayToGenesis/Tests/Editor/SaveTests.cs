using System;
using System.IO;
using NUnit.Framework;

public class SaveTests
{
    private string directory;
    private SaveStorage storage;
    [SetUp] public void Setup()
    {
        directory = Path.Combine(Path.GetTempPath(), "ArcanoriaSaveTests-" + Guid.NewGuid().ToString("N"));
        storage = new SaveStorage(directory);
    }
    [TearDown] public void Cleanup() { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    [Test] public void IdentityBindsSeedCatalogAndUniqueWorld()
    {
        var a = WorldIdentity.Create("catalog", 3, storage); var b = WorldIdentity.Create("catalog", 3, storage);
        Assert.AreNotEqual(a.id, b.id); Assert.AreNotEqual(a.serial, b.serial); a.Validate(storage);
        a.seed++; Assert.Throws<InvalidDataException>(() => a.Validate(storage));
    }
    [Test] public void CiphertextTamperingAndUnsafePathsAreRejected()
    {
        var bytes = Convert.FromBase64String(storage.Seal("state")); bytes[24] ^= 1;
        Assert.Throws<System.Security.Cryptography.CryptographicException>(() => storage.Open(Convert.ToBase64String(bytes)));
        Assert.Throws<ArgumentException>(() => storage.Read("../profile"));
    }
    [Test] public void ReplacementsKeepLastGoodBackup()
    {
        string id = Guid.NewGuid().ToString("N"); storage.Write(id, "first"); storage.Write(id, "second");
        Assert.AreEqual("second", storage.Read(id)); Assert.AreEqual("first", storage.Read(id, true));
    }
    [Test] public void RetiredCopyDetectionPersistsUntilAllRenamedCopiesAreRemoved()
    {
        string id = Guid.NewGuid().ToString("N"); storage.Write(id, "retired"); storage.Write(id, "retired");
        string copy = Path.Combine(directory, "renamed.old"); File.WriteAllText(copy, storage.Seal("retired"));
        Assert.AreEqual(3, storage.FindCopies(s => s == "retired").Length);
        storage.RemoveCopies(s => s == "retired"); Assert.IsEmpty(storage.FindCopies(s => s == "retired"));
        File.WriteAllText(copy, storage.Seal("retired")); Assert.AreEqual(1, storage.FindCopies(s => s == "retired").Length);
        File.Delete(copy); Assert.IsEmpty(storage.FindCopies(s => s == "retired"));
    }
    [Test] public void RewardsAreOncePerWorldAndLifetimeUnlocksAreAUnion()
    {
        var a = new WorldRewards(); var b = new WorldRewards(); var lifetime = new LifetimeProfile();
        Assert.IsTrue(a.Earn("first", 10)); Assert.IsFalse(a.Earn("first", 10)); Assert.IsTrue(b.Earn("first", 10));
        lifetime.Merge(a.unlocked); lifetime.Merge(b.unlocked); Assert.AreEqual(1, lifetime.unlocked.Count);
        Assert.IsTrue(a.Spend(3)); Assert.AreEqual(7, a.Balance); Assert.AreEqual(10, b.Balance);
        Assert.IsFalse(a.Spend(-1)); Assert.IsFalse(a.Spend(8)); a.Validate();
        var restored = (WorldRewards)SaveStateCodec.Read(SaveStateCodec.Write(a, typeof(WorldRewards)), typeof(WorldRewards));
        Assert.AreEqual(7, restored.Balance); Assert.IsFalse(restored.Earn("first", 10));
    }
    [Test] public void AllStateContractsResolve() => GameSnapshot.ValidateSchema();
    [Test] public void StateTreesDeeperThanJsonUtilityNestingRoundTrip()
    {
        // JsonUtility stops at depth 10; StateNode writes its descendants flat so nothing past it is lost.
        var root = new StateNode { kind = "object" };
        var node = root;
        for (int depth = 0; depth < 25; depth++)
        {
            var child = new StateNode { name = "level" + depth, kind = "object" };
            node.children.Add(child); node.children.Add(new StateNode { name = "leaf" + depth, kind = "value", value = depth.ToString() });
            node = child;
        }
        var system = new SavedSystem { type = "Deep", key = "k", state = root };
        var copy = UnityEngine.JsonUtility.FromJson<SavedSystem>(UnityEngine.JsonUtility.ToJson(system)).state;
        for (int depth = 0; depth < 25; depth++)
        {
            Assert.AreEqual(2, copy.children.Count, "depth " + depth);
            Assert.AreEqual(depth.ToString(), copy.children[1].value);
            copy = copy.children[0];
            Assert.AreEqual("level" + depth, copy.name);
        }
    }
    [Test] public void ThreeWitnessesRepairDeletionCorruptionAndRejectSwappedDomains()
    {
        var witnesses = new RetirementWitnesses(storage, Path.Combine(directory, "record"));
        string id = Guid.NewGuid().ToString("N");
        witnesses.Reconcile(new[] { id }, true);
        File.Delete(witnesses.PathFor(0));
        File.WriteAllText(witnesses.PathFor(1), "corrupt");
        CollectionAssert.Contains(witnesses.Reconcile(Array.Empty<string>()), id);
        Assert.IsTrue(storage.Open(File.ReadAllText(witnesses.PathFor(0))).StartsWith("ARC-WITNESS-1:0"));
        Assert.IsTrue(storage.Open(File.ReadAllText(witnesses.PathFor(1))).StartsWith("ARC-WITNESS-1:1"));
        string first = File.ReadAllText(witnesses.PathFor(0));
        File.WriteAllText(witnesses.PathFor(1), first);
        witnesses.Reconcile(Array.Empty<string>());
        Assert.IsTrue(storage.Open(File.ReadAllText(witnesses.PathFor(1))).StartsWith("ARC-WITNESS-1:1"));
        // Persistent retirement alone is not a cosmetic trigger: only an actual matching save is.
        Assert.IsEmpty(storage.FindCopies(text => text == "restored-save"));
    }
    [Test] public void SacrificeCommitsRetirementBeforeRemovingCopiesAndRestoredCopiesKeepTheEndingActive()
    {
        var oldStorage = SaveSession.Storage; var oldProfile = SaveSession.Profile; var oldCurrent = SaveSession.Current;
        var field = typeof(SaveSession).GetField("retirementPath", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
        var oldPath = field.GetValue(null);
        try
        {
            SaveSession.Storage = storage; SaveSession.Profile = new LifetimeProfile();
            field.SetValue(null, Path.Combine(directory, "retirement-ledger"));
            var identity = WorldIdentity.Create("catalog", 3, storage);
            SaveSession.Current = new SaveDocument { identity = identity };
            SaveSession.Current.rewards.Earn("first", 10);
            string payload = UnityEngine.JsonUtility.ToJson(SaveSession.Current);
            storage.Write(identity.id, payload); storage.Write(identity.id, payload);
            string copy = storage.Seal(payload);
            SaveSession.Sacrifice();
            Assert.IsFalse(storage.Exists(identity.id));
            Assert.IsTrue(SaveSession.Profile.retiredWorlds.Contains(identity.id));
            Assert.IsTrue(SaveSession.Profile.unlocked.Contains("first"));
            string renamed = Path.Combine(directory, "returned-copy"); File.WriteAllText(renamed, copy);
            SaveSession.ScanRetired(); Assert.AreEqual(1, SaveSession.ForbiddenCopies.Length);
            SaveSession.ScanRetired(); Assert.AreEqual(1, SaveSession.ForbiddenCopies.Length);
            File.Delete(renamed); SaveSession.ScanRetired(); Assert.IsEmpty(SaveSession.ForbiddenCopies);
        }
        finally { SaveSession.Storage = oldStorage; SaveSession.Profile = oldProfile; SaveSession.Current = oldCurrent; field.SetValue(null, oldPath); SaveSession.ForbiddenCopies = Array.Empty<string>(); }
    }
    [Test] public void LedgersAndReadonlyAgeBeatsRoundTripWithoutDuplicatingSources()
    {
        var ledger = new ModifierLedger(); ledger.Set("food", "technology", new ModifierValue(2, 30));
        var restored = (ModifierLedger)SaveStateCodec.Read(SaveStateCodec.Write(ledger, typeof(ModifierLedger)), typeof(ModifierLedger));
        Assert.AreEqual(2, restored.Get("food", "technology").Flat);
        Assert.AreEqual(30, restored.Get("food", "technology").Percent);
        var beats = new System.Collections.Generic.Queue<AgeBeat>(); beats.Enqueue(new AgeBeat(AgeBeatKind.AgeEnds, 42, 1));
        var copy = (System.Collections.Generic.Queue<AgeBeat>)SaveStateCodec.Read(SaveStateCodec.Write(beats, beats.GetType()), beats.GetType());
        Assert.AreEqual(42, copy.Dequeue().at);
    }
}
