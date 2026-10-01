using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// The persistent front door and pause: which worlds exist, entering one (new or saved), saving, autosaving, and whether
/// the menu holds the screen (<see cref="BlocksGameplay"/>, the game's time stopped). It draws nothing: <see cref="MenuView"/>
/// shows the title screen (no world yet: <see cref="Playable"/> false) or the quick menu (Esc in a world), and calls the
/// actions here. Uses unscaled time for the birth sequence.
/// </summary>
public sealed class SaveMenu : MonoBehaviour
{
    /// <summary>One save file as the Load page lists it.</summary>
    public sealed class Slot
    {
        public string id, name, savedUtc, error;
        public int anchors;
        public double playSeconds;
    }

    private static SaveMenu instance;
    public static SaveMenu Instance => instance;
    public static bool BlocksGameplay => instance != null && (instance.visible || instance.busy);
    private readonly List<UnityEngine.UI.GraphicRaycaster> blocked = new List<UnityEngine.UI.GraphicRaycaster>();
    // Play tests set `visible` false by reflection to dismiss the menu: keep the name.
    private bool visible = true, busy, playable;
    private string message = "";
    private float explosion = -1, nextScan, nextAutosave, savedAt = -1;
    private string scene;
    private float previousScale = 1;
    private readonly List<Slot> slots = new List<Slot>();

    /// <summary>The menu holds the screen.</summary>
    public bool Visible => visible;
    /// <summary>A world is loaded and running (false: the title screen).</summary>
    public bool Playable => playable;
    /// <summary>A world is being entered.</summary>
    public bool Busy => busy;
    /// <summary>The last outcome or error to show.</summary>
    public string Message => message;
    /// <summary>When the birth of a new universe began (unscaled seconds; negative: not now).</summary>
    public float ExplosionStart => explosion;
    /// <summary>Unscaled seconds since the world was last saved (negative: not in this session).</summary>
    public float SinceSaved => savedAt < 0 ? -1 : Time.unscaledTime - savedAt;
    public IReadOnlyList<Slot> Slots => slots;
    /// <summary>The world last played on this machine can be continued from the title.</summary>
    public bool CanContinue => !playable && SaveSession.Error == null && !string.IsNullOrEmpty(SaveSession.Profile?.lastWorld);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (instance != null || TimeSystemLogic.Instance == null) return;
        instance = new GameObject("Save and Main Menu").AddComponent<SaveMenu>();
        DontDestroyOnLoad(instance.gameObject);
        instance.gameObject.AddComponent<SettingsApplier>();
        instance.gameObject.AddComponent<MenuView>();
    }

    private void Awake()
    {
        scene = SceneManager.GetActiveScene().path;
        Time.timeScale = 0;
        try { SaveSession.Initialize(); RefreshSlots(); } catch (Exception e) { message = e.Message; SaveSession.Error = e.Message; }
        GameInput.CancelUnclaimed += OnCancelUnclaimed;
    }

    private void OnDestroy() => GameInput.CancelUnclaimed -= OnCancelUnclaimed;

    // Escape that no open window took: the quick menu opens (the menu's own Escape is MenuView's).
    private void OnCancelUnclaimed()
    {
        if (playable && !busy && !visible) Open();
    }

    private void Update()
    {
        if (BlocksGameplay)
        {
            // Everything behind the menu stops taking clicks; the menu's own canvas (a child of this object) keeps them.
            foreach (var raycaster in FindObjectsByType<UnityEngine.UI.GraphicRaycaster>())
                if (raycaster.enabled && !raycaster.transform.IsChildOf(transform)) { raycaster.enabled = false; blocked.Add(raycaster); }
        }
        else if (blocked.Count > 0) { foreach (var raycaster in blocked) if (raycaster != null) raycaster.enabled = true; blocked.Clear(); }
        // Only the menu shows restored copies of a severed world: it reads every file in the Saves folder, so never in play.
        if (visible && SaveSession.Storage != null && Time.unscaledTime >= nextScan)
        {
            nextScan = Time.unscaledTime + 2;
            try { SaveSession.ScanRetired(); }
            catch (Exception e) { message = "Cosmetic identity check: " + e.Message; }
        }
        if (autosave != null && autosave.IsCompleted) FinishAutosave();
        if (playable && !visible && !busy)
        {
            SaveSession.Current.playSeconds += Time.unscaledDeltaTime;
            int minutes = GameSettings.AutosaveMinutes;
            if (minutes > 0 && autosave == null && Time.unscaledTime >= nextAutosave && !(EventSystemLogic.Instance?.IsEventActive() ?? false))
            {
                nextAutosave = Time.unscaledTime + minutes * 60f;
                // The world is captured this frame; the disk write runs on a worker thread.
                try { autosave = SaveSession.SaveInBackground(); }
                catch (Exception e) { Open(); message = e.Message; }
            }
        }
    }

    // The autosave being written to disk (null: none).
    private System.Threading.Tasks.Task autosave;

    private void FinishAutosave()
    {
        var done = autosave;
        autosave = null;
        if (done.IsFaulted)
        {
            if (!visible) Open();
            message = done.Exception?.GetBaseException().Message ?? "The autosave failed.";
            return;
        }
        savedAt = Time.unscaledTime;
        RememberCurrentSlot();
    }

    // ===== ACTIONS (MenuView) =====

    /// <summary>Show the menu over the running world and stop its time.</summary>
    public void Open()
    {
        if (visible) return;
        visible = true;
        previousScale = Time.timeScale;
        Time.timeScale = 0;
        message = "";
        nextScan = 0;
        RefreshSlots();
    }

    /// <summary>Back to the world, its time running as it was.</summary>
    public void Resume()
    {
        if (!playable || busy) return;
        visible = false;
        Time.timeScale = previousScale;
    }

    public void SaveWorld() => Try(Save);

    private void Save()
    {
        SaveSession.Save();
        savedAt = Time.unscaledTime;
        RememberCurrentSlot();
    }

    public void NewUniverse(string name) => Begin(null, string.IsNullOrWhiteSpace(name) ? "Arcanoria" : name.Trim());

    public void Continue()
    {
        if (!CanContinue) return;
        try { Begin(SaveSession.Read(SaveSession.Profile.lastWorld), null); } catch (Exception e) { message = e.Message; }
    }

    public void Load(string id, bool backup = false)
    {
        try { Begin(SaveSession.Read(id, backup), null); } catch (Exception e) { message = e.Message; }
    }

    public void Delete(string id) => Try(() =>
    {
        SaveSession.FinishWriting();
        SaveSession.Storage.Delete(id);
        if (SaveSession.Current?.identity.id == id) { playable = false; SaveSession.Current = null; }
    });

    /// <summary>Save, then leave the world for the title screen (the world stays loaded, stopped, behind it).</summary>
    public void ReturnToTitle()
    {
        if (busy) return;
        if (playable)
        {
            try { Save(); }
            catch (Exception e) { message = "Could not save before leaving: " + e.Message; return; }
        }
        playable = false;
        visible = true;
        Time.timeScale = 0;
        message = "";
        RefreshSlots();
    }

    public void Quit()
    {
        try { if (playable) Save(); }
        catch (Exception e) { message = "Could not save before quitting: " + e.Message; return; }
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    public void RecoverProfileBackup() => Try(SaveSession.RecoverProfileBackup);

    public void RemoveRetiredCopies() => Try(SaveSession.RemoveRetiredCopies);

    // Each slot as last read, with its file's stamp: a file not written since is not read again.
    private readonly Dictionary<string, ((long, long) stamp, Slot slot)> slotCache = new Dictionary<string, ((long, long), Slot)>();

    // The world just saved is listed from memory (reading it back would decrypt and parse the whole file).
    private void RememberCurrentSlot()
    {
        var save = SaveSession.Current;
        if (save == null || SaveSession.Storage == null) return;
        string id = save.identity.id;
        try { slotCache[id] = (SaveSession.Storage.Stamp(id), new Slot { id = id, name = save.name, savedUtc = save.savedUtc, anchors = save.rewards.Balance, playSeconds = save.playSeconds }); }
        catch (Exception) { slotCache.Remove(id); }
    }

    public void RefreshSlots()
    {
        slots.Clear();
        if (SaveSession.Storage == null) return;
        SaveSession.FinishWriting();
        foreach (string id in SaveSession.Storage.Slots())
        {
            var stamp = SaveSession.Storage.Stamp(id);
            if (slotCache.TryGetValue(id, out var cached) && cached.stamp == stamp) { slots.Add(cached.slot); continue; }
            Slot slot;
            try
            {
                var save = SaveSession.ReadHeader(id);
                slot = new Slot { id = id, name = save.name, savedUtc = save.savedUtc, anchors = save.rewards.Balance, playSeconds = save.playSeconds };
            }
            catch (Exception e) { slot = new Slot { id = id, name = id, error = e.Message }; }
            slotCache[id] = (stamp, slot);
            slots.Add(slot);
        }
        // Newest first (saved times are ISO strings).
        slots.Sort((a, b) => string.CompareOrdinal(b.savedUtc ?? "", a.savedUtc ?? ""));
    }

    private void Try(Action action, bool quiet = false)
    {
        try
        {
            action();
            if (!quiet) message = "Done.";
            RefreshSlots();
        }
        catch (Exception e) { if (!visible) Open(); message = e.Message; }
    }

    private void Begin(SaveDocument saved, string name)
    {
        if (busy) return;
        StartCoroutine(Enter(saved, name));
    }

    private IEnumerator Enter(SaveDocument saved, string name)
    {
        if (saved != null && saved.scene != scene) { message = "This save belongs to another game scene."; yield break; }
        if (playable) { try { Save(); } catch (Exception e) { message = e.Message; yield break; } }
        busy = true; playable = false; SaveSession.Restoring = true; Time.timeScale = 0; message = "";
        try
        {
            if (saved == null) SaveSession.New(name); else SaveSession.Current = saved;
            foreach (string unknown in Achievements.Tracker.Restore(SaveSession.Current.rewards.unlocked))
                GameLog.Warning($"This world earned '{unknown}', which is no longer an achievement (add it to AchievementAliases if it was renamed); its Anchors are kept.", LogChannel.Events);
            GameAge.Set(GameAge.FirstAge, 0);
        }
        catch (Exception e) { message = e.Message; busy = false; SaveSession.Restoring = false; yield break; }
        var load = SceneManager.LoadSceneAsync(scene);
        while (!load.isDone) yield return null;
        yield return null; yield return null;
        if (saved != null)
        {
            try { GameSnapshot.PrepareSlots(saved); } catch (Exception e) { message = e.Message; busy = false; yield break; }
            yield return null; yield return null;
        }
        try
        {
            if (saved != null) GameSnapshot.Restore(saved);
            SaveSession.Restoring = false;
            // Birth is durable before its visual reveal. No half-created universe is offered as a load slot.
            if (saved == null) Save();
            else { SaveSession.Profile.Merge(saved.rewards.unlocked); SaveSession.Profile.lastWorld = saved.identity.id; SaveSession.WriteProfile(); savedAt = Time.unscaledTime; }
        }
        catch (Exception e) { message = "Could not enter world: " + e.Message; busy = false; yield break; }
        if (saved == null)
        {
            explosion = Time.unscaledTime;
            yield return new WaitForSecondsRealtime(3.5f);
            explosion = -1;
        }
        playable = true; visible = false; busy = false; previousScale = 1; Time.timeScale = 1;
        int minutes = GameSettings.AutosaveMinutes;
        nextAutosave = Time.unscaledTime + Mathf.Max(1, minutes) * 60f;
        RefreshSlots();
        // Research carries on where the save left it (the plan and the research under way).
        GameUnitsLogic.Instance?.ResumeResearch();
    }

    public void CompleteSacrifice()
    {
        try { SaveSession.Sacrifice(); playable = false; visible = true; Time.timeScale = 0; message = "Learn to love without possession."; RefreshSlots(); }
        catch (Exception e) { message = e.Message; playable = false; visible = true; Time.timeScale = 0; }
    }

    private void OnApplicationPause(bool pause) { if (pause && playable && !busy) Try(Save, quiet: true); }
    private void OnApplicationQuit() { SaveSession.FinishWriting(); if (playable && !busy) Try(Save, quiet: true); }
}
