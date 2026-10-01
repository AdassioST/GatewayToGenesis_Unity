using System;
using System.Collections.Generic;
using System.Linq;

[Serializable] public sealed class WorldIdentity
{
    public string id, createdUtc, catalog, serial;
    public int seed, generator;
    public string Binding => id + "|" + seed + "|" + generator + "|" + catalog + "|" + createdUtc;
    public static WorldIdentity Create(string catalog, int generator, SaveStorage storage)
    {
        var identity = new WorldIdentity { id = Guid.NewGuid().ToString("N"), createdUtc = DateTime.UtcNow.ToString("O"),
            seed = BitConverter.ToInt32(SaveStorage.RandomBytes(4), 0) & int.MaxValue, generator = generator, catalog = catalog };
        identity.serial = storage.Seal(identity.Binding);
        return identity;
    }
    public void Validate(SaveStorage storage)
    {
        if (!Guid.TryParseExact(id, "N", out _) || storage.Open(serial) != Binding)
            throw new System.IO.InvalidDataException("The world identity does not match its generation data.");
    }
}
[Serializable] public sealed class AnchorAward { public string achievement; public int amount; }
[Serializable] public sealed class WorldRewards
{
    public List<string> unlocked = new List<string>();
    public List<AnchorAward> awards = new List<AnchorAward>();
    // What earned each award (at most one per achievement). Awards from before evidence was kept have none.
    public List<AwardEvidence> evidence = new List<AwardEvidence>();
    public int spent;
    public int Balance => checked(awards.Sum(a => a.amount) - spent);
    public bool Earn(string id, int amount)
    {
        if (string.IsNullOrEmpty(id) || unlocked.Contains(id)) return false;
        if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
        unlocked.Add(id); awards.Add(new AnchorAward { achievement = id, amount = amount }); return true;
    }
    /// <summary>Keep what earned an award this world holds; the first record per achievement stands.</summary>
    public void Record(AwardEvidence record)
    {
        if (evidence == null) evidence = new List<AwardEvidence>();
        if (record == null || !unlocked.Contains(record.achievement) || evidence.Any(e => e.achievement == record.achievement)) return;
        evidence.Add(record);
    }
    public AwardEvidence EvidenceFor(string id) => evidence?.FirstOrDefault(e => e.achievement == id);
    public bool Spend(int amount)
    {
        if (amount <= 0 || amount > Balance) return false;
        spent = checked(spent + amount); return true;
    }
    public void Validate()
    {
        if (unlocked == null || awards == null || spent < 0 || unlocked.Distinct().Count() != unlocked.Count ||
            awards.Any(a => a == null || a.amount < 0 || !unlocked.Contains(a.achievement)) ||
            awards.Select(a => a.achievement).Distinct().Count() != awards.Count || awards.Count != unlocked.Count || Balance < 0)
            throw new System.IO.InvalidDataException("Invalid achievement reward ledger.");
        if (evidence == null) evidence = new List<AwardEvidence>();
        if (evidence.Any(e => e == null || !unlocked.Contains(e.achievement)) || evidence.Select(e => e.achievement).Distinct().Count() != evidence.Count)
            throw new System.IO.InvalidDataException("Invalid achievement evidence.");
    }
}
[Serializable] public sealed class LifetimeProfile
{
    public int version = 1;
    public List<string> unlocked = new List<string>();
    public List<string> retiredWorlds = new List<string>();
    public bool restoredRetiredWorld;
    public string lastWorld;
    public void Merge(IEnumerable<string> ids) { foreach (var id in ids) if (!unlocked.Contains(id)) unlocked.Add(id); }
}
/// <summary>One node of a flattened state tree: its parent is an index into the same list (-1: the root).</summary>
[Serializable] public sealed class FlatStateNode { public string name, kind, value; public int parent; }

/// <summary>
/// A saved value: a scalar, or an object/list/dictionary of child nodes. In memory it is a tree; on disk each root
/// writes its descendants as one flat list (<see cref="FlatStateNode"/>), because JsonUtility stops at nesting depth
/// 10 and a recursive type would silently lose deep state (units, their paths, roads).
/// </summary>
[Serializable] public sealed class StateNode : UnityEngine.ISerializationCallbackReceiver
{
    public string name, kind, value;
    [NonSerialized] public List<StateNode> children = new List<StateNode>();
    public List<FlatStateNode> tree = new List<FlatStateNode>();

    public void OnBeforeSerialize()
    {
        tree = new List<FlatStateNode>();
        var open = new Queue<(StateNode node, int parent)>();
        foreach (var child in children ?? new List<StateNode>()) open.Enqueue((child, -1));
        while (open.Count > 0)
        {
            var (node, parent) = open.Dequeue();
            int index = tree.Count;
            tree.Add(new FlatStateNode { name = node.name, kind = node.kind, value = node.value, parent = parent });
            foreach (var child in node.children ?? new List<StateNode>()) open.Enqueue((child, index));
        }
    }

    public void OnAfterDeserialize()
    {
        children = new List<StateNode>();
        if (tree == null) return;
        var built = new List<StateNode>(tree.Count);
        foreach (var flat in tree)
        {
            var node = new StateNode { name = flat.name, kind = flat.kind, value = flat.value };
            built.Add(node);
            if (flat.parent < 0) children.Add(node);
            else if (flat.parent < built.Count - 1) built[flat.parent].children.Add(node);
            else throw new System.IO.InvalidDataException("A saved state node comes before its parent.");
        }
        tree = new List<FlatStateNode>();
    }
}
[Serializable] public sealed class SavedSystem { public string type, key; public StateNode state; }
/// <summary>One saved field of many objects (every world tile), one value per object in order
/// (<see cref="SaveStateCodec.PackColumns{T}"/>); <see cref="nulls"/> lists the objects whose value is null.</summary>
[Serializable] public sealed class SavedColumn { public string field; public List<string> values = new List<string>(); public List<int> nulls = new List<int>(); }
[Serializable] public sealed class SavedText { public string key, value; }
/// <summary>The part of a <see cref="SaveDocument"/> a save slot shows, read without the world's state (<see cref="SaveSession.ReadHeader"/>).</summary>
[Serializable] public sealed class SaveHeader
{
    public int version;
    public WorldIdentity identity;
    public string name, savedUtc;
    public double playSeconds;
    public WorldRewards rewards;
}
[Serializable] public sealed class SaveDocument
{
    public int version = 3; // 2: state trees written flat (StateNode.tree); 3: known cells, forage, claims, stores
    public WorldIdentity identity;
    public string name, savedUtc, scene, ageId, randomState, pendingStory;
    public int ageNumber;
    public double playSeconds;
    public WorldRewards rewards = new WorldRewards();
    public List<SavedSystem> systems = new List<SavedSystem>();
    // The world's tiles, one column per saved field (a node tree per tile made the file ten times larger and took seconds
    // to write). Saves written before the columns hold one tree per tile in worldTiles instead; both load.
    public List<SavedColumn> tileColumns = new List<SavedColumn>();
    public List<StateNode> worldTiles = new List<StateNode>();
    public List<SavedText> ink = new List<SavedText>();
    public List<SavedText> storyLocks = new List<SavedText>();
    // The research plan (GameUnitsLogic.ResearchPlan), technology names in order. Kept out of the system snapshot so
    // saves written before it load with an empty plan.
    public List<string> researchPlan = new List<string>();
    // Resources made during play (the people's invented dishes and drinks, RuntimeUnits), made again before the slots load.
    public List<RuntimeUnitRecord> runtimeUnits = new List<RuntimeUnitRecord>();

    /// <summary>A copy to write while play goes on (<see cref="SaveSession.SaveInBackground"/>): its own lists and its own
    /// rewards (an achievement earned meanwhile changes only the original); the captured state it shares is never
    /// changed again.</summary>
    public SaveDocument ForWriting()
    {
        var copy = (SaveDocument)MemberwiseClone();
        copy.rewards = UnityEngine.JsonUtility.FromJson<WorldRewards>(UnityEngine.JsonUtility.ToJson(rewards));
        copy.systems = new List<SavedSystem>(systems);
        copy.tileColumns = new List<SavedColumn>(tileColumns);
        copy.worldTiles = new List<StateNode>(worldTiles);
        copy.ink = new List<SavedText>(ink);
        copy.storyLocks = new List<SavedText>(storyLocks);
        copy.researchPlan = new List<string>(researchPlan);
        copy.runtimeUnits = new List<RuntimeUnitRecord>(runtimeUnits);
        return copy;
    }
}
