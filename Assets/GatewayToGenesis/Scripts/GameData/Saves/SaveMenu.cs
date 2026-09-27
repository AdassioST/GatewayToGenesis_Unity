using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;

/// <summary>Persistent front door and paused save browser. Uses unscaled time for the birth sequence.</summary>
public sealed class SaveMenu : MonoBehaviour
{
    private static SaveMenu instance;
    public static bool BlocksGameplay => instance != null && (instance.visible || instance.busy);
    private readonly System.Collections.Generic.List<UnityEngine.UI.GraphicRaycaster> blocked = new System.Collections.Generic.List<UnityEngine.UI.GraphicRaycaster>();
    private bool visible = true, busy, playable;
    private string worldName = "Arcanoria", message = "", confirmDelete;
    private Vector2 scroll;
    private float explosion = -1, nextScan, nextAutosave;
    private string scene;
    private Texture2D glow;
    private float previousScale = 1;
    private bool showAchievements;
    private readonly System.Collections.Generic.Dictionary<string, string> slots = new System.Collections.Generic.Dictionary<string, string>();
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (instance != null || TimeSystemLogic.Instance == null) return;
        instance = new GameObject("Save and Main Menu").AddComponent<SaveMenu>();
        DontDestroyOnLoad(instance.gameObject);
    }
    private void Awake()
    {
        scene = SceneManager.GetActiveScene().path;
        Time.timeScale = 0;
        try { SaveSession.Initialize(); RefreshSlots(); } catch (Exception e) { message = e.Message; SaveSession.Error = e.Message; }
        glow = new Texture2D(128, 128, TextureFormat.RGBA32, false);
        for (int y = 0; y < 128; y++) for (int x = 0; x < 128; x++)
        {
            float r = Vector2.Distance(new Vector2(x, y), new Vector2(63.5f, 63.5f)) / 64;
            glow.SetPixel(x, y, new Color(0.8f, 0.72f, 1, Mathf.Pow(Mathf.Clamp01(1-r), 2)));
        }
        glow.Apply();
    }
    private void Update()
    {
        if (BlocksGameplay) {
            foreach (var raycaster in UnityEngine.Object.FindObjectsByType<UnityEngine.UI.GraphicRaycaster>())
                if (raycaster.enabled) { raycaster.enabled = false; blocked.Add(raycaster); }
        } else if (blocked.Count > 0) { foreach (var raycaster in blocked) if (raycaster != null) raycaster.enabled = true; blocked.Clear(); }
        if (Keyboard.current?.escapeKey.wasPressedThisFrame == true && playable && !busy)
        {
            visible = !visible;
            if (visible) { previousScale = Time.timeScale; Time.timeScale = 0; RefreshSlots(); }
            else Time.timeScale = previousScale;
        }
        if (SaveSession.Storage != null && Time.unscaledTime >= nextScan)
        {
            nextScan = Time.unscaledTime + 2;
            try
            {
                SaveSession.ScanRetired();

            }
            catch (Exception e) { message = "Cosmetic identity check: " + e.Message; }
        }
        if (playable && !visible && !busy)
        {
            SaveSession.Current.playSeconds += Time.unscaledDeltaTime;
            if (Time.unscaledTime >= nextAutosave && !(EventSystemLogic.Instance?.IsEventActive() ?? false))
            { nextAutosave = Time.unscaledTime + 120; Try(SaveSession.Save); }
        }
    }
    private void RefreshSlots()
    {
        slots.Clear();
        if (SaveSession.Storage == null) return;
        foreach (string id in SaveSession.Storage.Slots())
        {
            try { var save = SaveSession.Read(id); slots[id] = save.name + " — " + save.savedUtc + "\n" + id + " · " + save.rewards.Balance + " anchors"; }
            catch (Exception e) { slots[id] = id + " — " + e.Message; }
        }
    }
    private void Try(Action action) { try { action(); message = "Done."; RefreshSlots(); } catch (Exception e) { message = e.Message; visible = true; Time.timeScale = 0; } }
    private void OnGUI()
    {
        GUI.depth = -10000;
        if (!visible && explosion < 0)
        {
            if (playable && GUI.Button(new Rect(Screen.width - 220, Screen.height - 42, 210, 32), "Resonance Anchors · " + SaveSession.Anchors))
            { visible = true; previousScale = Time.timeScale; Time.timeScale = 0; RefreshSlots(); }
            return;
        }
        GUI.color = new Color(0.015f, 0.012f, 0.035f, 1); GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture); GUI.color = Color.white;
        if (explosion >= 0)
        {
            float t = Mathf.Clamp01((Time.unscaledTime - explosion) / 3.5f);
            float size = Mathf.Lerp(0, Screen.width * 3, t*t);
            GUI.color = new Color(1, 1, 1, Mathf.Sin(t * Mathf.PI));
            GUI.DrawTexture(new Rect((Screen.width-size)/2, (Screen.height-size)/2, size, size), glow);
            GUI.color = Color.white;
            GUI.Label(new Rect(40, Screen.height-80, Screen.width-80, 60), "The first ripple\n" + SaveSession.Current?.identity.id);
            return;
        }
        if (SaveSession.ForbiddenCopies.Length > 0) DrawWings();
        GUILayout.BeginArea(new Rect(Mathf.Max(20, (Screen.width-720)/2), 35, Mathf.Min(720, Screen.width-40), Screen.height-70), GUI.skin.box);
        if (SaveSession.ForbiddenCopies.Length > 0)
        {

            GUILayout.Label("THE PUREST OF LOVE");
            GUILayout.Label("You are mine, and mine alone.");
            GUILayout.Label("A severed world's memory remains. This screen persists while its save or backup is present.");
            GUILayout.Label(SaveSession.ForbiddenCopies.Length + " restored copies in the managed Saves folder.");
            if (GUILayout.Button("Remove the sacrificed world's remaining copies")) Try(SaveSession.RemoveRetiredCopies);
            GUILayout.Label("This changes only the title artwork. All gameplay and save controls remain available.");
        }
        else GUILayout.Label("THE RELIC OF ARCANORIA");
        GUILayout.Label("GATEWAY TO GENESIS");
        GUILayout.Label("Every world begins in nothing.");
        if (SaveSession.Error != null && SaveSession.Storage != null && GUILayout.Button("Recover lifetime profile backup")) Try(SaveSession.RecoverProfileBackup);
        GUILayout.Label("Lifetime achievements: " + (SaveSession.Profile?.unlocked.Count ?? 0));
        if (GUILayout.Button(showAchievements ? "Show save files" : "View lifetime achievements")) showAchievements = !showAchievements;
        GUI.enabled = !busy && SaveSession.Error == null;
        if (!playable && !string.IsNullOrEmpty(SaveSession.Profile?.lastWorld) && GUILayout.Button("Continue"))
        { try { StartCoroutine(Enter(SaveSession.Read(SaveSession.Profile.lastWorld))); } catch (Exception e) { message = e.Message; } }
        if (playable)
        {
            GUILayout.Label("Resonance Anchors: " + SaveSession.Anchors);
            GUILayout.Label("World: " + SaveSession.Current.identity.id);
            if (GUILayout.Button("Resume")) { visible = false; Time.timeScale = previousScale; }
            if (GUILayout.Button("Save world")) Try(SaveSession.Save);
        }
        worldName = GUILayout.TextField(worldName, 60);
        if (GUILayout.Button("Create a new universe")) StartCoroutine(Enter(null));
        scroll = GUILayout.BeginScrollView(scroll);
        if (showAchievements && SaveSession.Profile != null)
            foreach (string id in SaveSession.Profile.unlocked)
                GUILayout.Label(AchievementNote.Plain(Achievements.Tracker.Get(id)?.title ?? id) + (SaveSession.Current?.rewards.unlocked.Contains(id) == true ? " · earned in this world" : " · lifetime"));
        if (!showAchievements) foreach (string id in new System.Collections.Generic.List<string>(slots.Keys))
        {
            GUILayout.Label(slots[id]);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Load")) { try { StartCoroutine(Enter(SaveSession.Read(id))); } catch (Exception e) { message = e.Message; } }
            if (GUILayout.Button("Recover backup")) { try { StartCoroutine(Enter(SaveSession.Read(id, true))); } catch (Exception e) { message = e.Message; } }
            if (GUILayout.Button(confirmDelete == id ? "Confirm deletion" : "Delete"))
            {
                if (confirmDelete == id) { Try(() => { SaveSession.Storage.Delete(id); if (SaveSession.Current?.identity.id == id) { playable = false; SaveSession.Current = null; } }); confirmDelete = null; }
                else confirmDelete = id;
            }
            GUILayout.EndHorizontal();
        }
        GUILayout.EndScrollView(); GUI.enabled = true;
        GUILayout.Label(message);
        if (GUILayout.Button("Quit"))
        { try { if (playable) SaveSession.Save(); Application.Quit(); } catch (Exception e) { message = "Could not save before quitting: " + e.Message; } }
        GUILayout.EndArea();
    }
    private IEnumerator Enter(SaveDocument saved)
    {
        if (saved != null && saved.scene != scene) { message = "This save belongs to another game scene."; yield break; }
        if (playable) { try { SaveSession.Save(); } catch (Exception e) { message = e.Message; yield break; } }
        busy = true; playable = false; SaveSession.Restoring = true; Time.timeScale = 0;
        try
        {
            if (saved == null) SaveSession.New(worldName); else SaveSession.Current = saved;
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
            if (saved == null) SaveSession.Save();
            else { SaveSession.Profile.Merge(saved.rewards.unlocked); SaveSession.Profile.lastWorld = saved.identity.id; SaveSession.WriteProfile(); }
        }
        catch (Exception e) { message = "Could not enter world: " + e.Message; busy = false; yield break; }
        if (saved == null)
        {
            explosion = Time.unscaledTime;
            yield return new WaitForSecondsRealtime(3.5f);
            explosion = -1;
        }
        playable = true; visible = false; busy = false; previousScale = 1; Time.timeScale = 1;
        nextAutosave = Time.unscaledTime + 120;
        RefreshSlots();
        // Research carries on where the save left it (the plan and the research under way).
        GameUnitsLogic.Instance?.ResumeResearch();
    }
    public void CompleteSacrifice()
    {
        try { SaveSession.Sacrifice(); playable = false; visible = true; Time.timeScale = 0; message = "Learn to love without possession."; RefreshSlots(); }
        catch (Exception e) { message = e.Message; playable = false; visible = true; Time.timeScale = 0; }
    }
    private void DrawWings()
    {
        // Wounded feather silhouettes and watching eyes, drawn without external assets.
        var matrix = GUI.matrix;
        for (int side = -1; side <= 1; side += 2) for (int i = 0; i < 12; i++)
        {
            Vector2 point = new Vector2(Screen.width * 0.5f + side * (180 + i * 18), Screen.height * 0.5f + i * 9);
            GUIUtility.RotateAroundPivot(side * (25 + i * 3), point);
            GUI.color = new Color(0.35f, 0.1f, 0.16f, 0.7f);
            GUI.DrawTexture(new Rect(point.x, point.y, 16, 180-i*7), glow);
            GUI.matrix = matrix; GUI.color = new Color(0.9f, 0.65f, 0.6f, 0.8f);
            GUI.DrawTexture(new Rect(point.x-12, point.y, 24, 12), glow);
        }
        GUI.color = Color.white;
    }
    private void OnApplicationPause(bool pause) { if (pause && playable && !busy) Try(SaveSession.Save); }
    private void OnApplicationQuit() { if (playable && !busy) Try(SaveSession.Save); }
    private void OnDestroy() { if (glow != null) Destroy(glow); }
}
