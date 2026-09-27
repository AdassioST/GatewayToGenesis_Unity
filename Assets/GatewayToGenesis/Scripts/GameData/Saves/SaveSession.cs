using System;
using System.IO;
using System.Linq;
using UnityEngine;

/// <summary>World slots, lifetime unlocks and permanent local retirement records.</summary>
public static class SaveSession
{
    public static SaveDocument Current;
    public static LifetimeProfile Profile;
    public static SaveStorage Storage;
    public static bool Restoring;
    public static string Error;
    public static int? GenerationSeed => Current?.identity.seed;
    public static int Anchors => Current?.rewards.Balance ?? 0;
    public static string[] ForbiddenCopies = Array.Empty<string>();
    private static string retirementPath;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset() { Current = null; Profile = null; Storage = null; Error = null; Restoring = false; ForbiddenCopies = Array.Empty<string>(); }
    public static void Initialize()
    {
        if (Storage != null) return;
        Storage = new SaveStorage(Path.Combine(Application.persistentDataPath, "Saves"));
        retirementPath = Path.Combine(Application.persistentDataPath, "retired-worlds.arc");
        Profile = Storage.Exists("profile") ? JsonUtility.FromJson<LifetimeProfile>(Storage.Read("profile")) : new LifetimeProfile();
        if (Profile == null || Profile.version != 1) throw new InvalidDataException("Unsupported lifetime profile.");
        AchievementAliases.Migrate(Profile);
        // A second, append-only-by-policy ledger survives restoring an older profile or its backup.
        MergeLegacyRetirement(Profile);
        ScanRetired();
    }
    private static void MergeLegacyRetirement(LifetimeProfile profile)
    {
        if (!File.Exists(retirementPath)) return;
        try
        {
            var retired = JsonUtility.FromJson<LifetimeProfile>(Storage.Open(File.ReadAllText(retirementPath)));
            if (retired?.retiredWorlds == null) return;
            foreach (var id in retired.retiredWorlds)
                if (Guid.TryParseExact(id, "N", out _) && !profile.retiredWorlds.Contains(id)) profile.retiredWorlds.Add(id);
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
        catch (System.Security.Cryptography.CryptographicException) { }
        catch (FormatException) { }
        catch (ArgumentException) { }
    }
    public static void WriteProfile() => Storage.Write("profile", JsonUtility.ToJson(Profile));
    public static void RecoverProfileBackup()
    {
        if (Storage == null) throw new InvalidOperationException("The identity key must be accessible before recovering a profile.");
        var profile = JsonUtility.FromJson<LifetimeProfile>(Storage.Read("profile", true));
        if (profile == null || profile.version != 1 || profile.retiredWorlds == null || profile.unlocked == null)
            throw new InvalidDataException("Invalid profile backup.");
        MergeLegacyRetirement(profile);
        AchievementAliases.Migrate(profile);
        Profile = profile; WriteProfile(); ScanRetired(); Error = null;
    }
    public static string Catalog()
    {
        var settings = GameCatalog.World.All.First().generation;
        // Include the exact stencil as well as catalog data in the identity binding.
        string text = WorldGenerator.CatalogHash(settings, WorldSystem.LoadTiles(settings)) + "|" + GameCatalog.LoadText(settings.stencil);
        using (var hash = System.Security.Cryptography.SHA256.Create())
            return Convert.ToBase64String(hash.ComputeHash(System.Text.Encoding.UTF8.GetBytes(text)));
    }
    public static void New(string name)
    {
        Initialize();
        Current = new SaveDocument { identity = WorldIdentity.Create(Catalog(), WorldGenerator.Version, Storage), name = string.IsNullOrWhiteSpace(name) ? "Arcanoria" : name.Trim() };
        Achievements.Tracker.Restore(Array.Empty<string>());
    }
    public static SaveDocument Read(string id, bool backup = false)
    {
        Initialize();
        var save = JsonUtility.FromJson<SaveDocument>(Storage.Read(id, backup));
        if (save == null || save.version != 3 || save.identity == null) throw new InvalidDataException("Unsupported save version.");
        save.identity.Validate(Storage); save.rewards.Validate();
        if (save.identity.id != id) throw new InvalidDataException("Slot identity mismatch.");
        // Achievements renamed since this save carry their unlocks, Anchors and evidence over (never paying twice).
        if (AchievementAliases.Migrate(save.rewards)) save.rewards.Validate();

        if (save.identity.generator != WorldGenerator.Version || save.identity.catalog != Catalog())
            throw new InvalidOperationException("This save requires its original world catalog and generator version.");
        return save;
    }
    public static void Save()
    {
        if (Current == null || Restoring) return;

        if (EventSystemLogic.Instance != null && EventSystemLogic.Instance.IsEventActive()) throw new InvalidOperationException("Finish the current story before saving.");
        GameSnapshot.Capture(Current);
        Profile.Merge(Current.rewards.unlocked); Profile.lastWorld = Current.identity.id; WriteProfile();
        Storage.Write(Current.identity.id, JsonUtility.ToJson(Current));
    }
    /// <summary>Award an achievement to this world and the lifetime profile (<see cref="AchievementAward.Commit"/>).
    /// True when it is newly unlocked in this session: announce it then. Throws when the profile cannot be written.</summary>
    public static bool Earn(string id, AwardEvidence evidence = null)
    {
        if (Current == null || Restoring) return false;
        // Prototype reward; kept in each award so future balancing does not rewrite old balances.
        int reward = 10;
        var config = Resources.Load<AnchorRewards>("Saves/AnchorRewards");
        if (config != null) reward = config.Amount(id);
        return AchievementAward.Commit(id, reward, evidence, Achievements.Tracker, Current.rewards, Profile, WriteProfile);
    }
    public static bool SpendAnchors(int amount)
    {
        if (Current == null || Restoring || amount <= 0 || Anchors < amount) return false;
        Current.rewards.Spend(amount);
        try { Save(); return true; } catch { Current.rewards.spent -= amount; throw; }
    }
    private static bool RetiredJson(string json)
    {
        if (!json.TrimStart().StartsWith("{", StringComparison.Ordinal)) return false;
        var save = JsonUtility.FromJson<SaveDocument>(json);
        if (save?.identity == null) return false;
        save.identity.Validate(Storage);
        return Profile.retiredWorlds.Contains(save.identity.id);
    }
    public static void ScanRetired()
    {
        Profile.retiredWorlds = new System.Collections.Generic.List<string>(new RetirementWitnesses(Storage, retirementPath).Reconcile(Profile.retiredWorlds));
        ForbiddenCopies = Profile.retiredWorlds.Count == 0 ? Array.Empty<string>() : Storage.FindCopies(RetiredJson);
        Profile.restoredRetiredWorld = ForbiddenCopies.Length > 0;
    }
    public static void RemoveRetiredCopies() { Storage.RemoveCopies(RetiredJson); ScanRetired(); }
    /// <summary>Called only by the explicit final-choice action; no ordinary delete invokes sacrifice.</summary>
    public static void Sacrifice()
    {
        if (Current == null) throw new InvalidOperationException("No active world.");
        string id = Current.identity.id;
        if (!Profile.retiredWorlds.Contains(id)) Profile.retiredWorlds.Add(id);
        Profile.Merge(Current.rewards.unlocked);
        if (Profile.lastWorld == id) Profile.lastWorld = null;
        Profile.Merge(new[] { "the-purest-of-all-love" });
        // Commit tombstone before deleting anything. A crash can leave a forbidden copy, never resurrect it.
        Profile.retiredWorlds = new System.Collections.Generic.List<string>(new RetirementWitnesses(Storage, retirementPath).Reconcile(Profile.retiredWorlds, requireAll: true));
        WriteProfile();
        string temp = retirementPath + ".tmp";
        using (var stream = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            var bytes = System.Text.Encoding.UTF8.GetBytes(Storage.Seal(JsonUtility.ToJson(Profile)));
            stream.Write(bytes, 0, bytes.Length); stream.Flush(true);
        }
        if (File.Exists(retirementPath)) File.Replace(temp, retirementPath, null); else File.Move(temp, retirementPath);
        Current = null;
        Storage.Delete(id); RemoveRetiredCopies();
    }
}
