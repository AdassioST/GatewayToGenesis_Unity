using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

/// <summary>
/// The menus in the real scene (ClickerScreen): the title screen before any world; in a world, an Escape that no window
/// takes opens the quick menu and stops time, Escape again resumes; an open window (the Library) takes its own Escape;
/// every Options tab builds, and Escape backs out of a page before it resumes. The world is faked in memory (no save
/// file is written: autosave is pushed away and `playable` is cleared before leaving play mode). Escape is sent through
/// GameInput's own handler. Helpers are static: locals captured before Enter Play Mode are lost to the domain reload.
/// </summary>
public class MenuPlayTests
{
    private const string Scene = "Assets/GatewayToGenesis/Scenes/ClickerScreen.unity";
    private const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;

    [UnityTest]
    public IEnumerator TitleQuickMenuAndOptions()
    {
        EditorSceneManager.OpenScene(Scene);
        yield return new EnterPlayMode();
        for (int i = 0; i < 600 && !Ready(); i++) yield return null;
        for (int i = 0; i < 10; i++) yield return null;
        try
        {
            CheckTheTitle();
            EnterAFakeWorld();
        }
        catch { Leave(); throw; }
        yield return null;
        yield return null;
        try
        {
            EscapeOpensAndResumes();
            TheLibraryTakesItsOwnEscape();
            OpenOptions();
        }
        catch { Leave(); throw; }
        yield return null;
        try { EveryTabBuilds(); }
        catch { Leave(); throw; }
        yield return null;
        try { EscapeBacksOutThenResumes(); }
        finally { Leave(); }
        yield return null;
    }

    private static bool Ready() =>
        SaveMenu.Instance != null && GameInput.Instance != null && Root() != null && LibraryWindow.IsOpen == false;

    private static SaveMenu Menu => SaveMenu.Instance;

    private static RectTransform Root()
    {
        var canvas = SaveMenu.Instance != null ? SaveMenu.Instance.GetComponentsInChildren<Canvas>(true).FirstOrDefault(c => c.name == "Menu Canvas") : null;
        return canvas != null ? (RectTransform)canvas.transform.Find("Menu") : null;
    }

    private static void Escape() => typeof(GameInput).GetMethod("Cancel", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, null);

    private static void Click(string text)
    {
        var button = Root().GetComponentsInChildren<Button>(false)
            .FirstOrDefault(b => b.GetComponent<TMP_Text>() != null && b.GetComponent<TMP_Text>().text.StartsWith(text));
        Assert.IsNotNull(button, $"a button reads \"{text}\"");
        Assert.IsTrue(button.interactable, $"\"{text}\" can be clicked");
        button.onClick.Invoke();
    }

    private static string Shown() => string.Join("\n", Root().GetComponentsInChildren<TMP_Text>(false).Select(t => t.text));

    private static void CheckTheTitle()
    {
        Assert.IsTrue(Menu.Visible && !Menu.Playable, "the game opens on the title screen");
        Assert.IsTrue(SaveMenu.BlocksGameplay, "the title holds the screen");
        Assert.IsTrue(Root().gameObject.activeSelf, "the title is drawn");
        string shown = Shown();
        StringAssert.Contains("Gateway to Genesis", shown);
        StringAssert.Contains("New universe", shown);
        StringAssert.Contains("Options", shown);
        Assert.IsFalse(shown.Contains("Resume"), "no Resume without a world");
    }

    // A world in memory only: the menu's own flags, a document, autosave pushed far away.
    private static void EnterAFakeWorld()
    {
        SaveSession.Current = new SaveDocument { name = "Menu Test", identity = new WorldIdentity { id = "menu-test" }, rewards = new WorldRewards() };
        typeof(SaveMenu).GetField("nextAutosave", Private).SetValue(Menu, float.MaxValue);
        typeof(SaveMenu).GetField("playable", Private).SetValue(Menu, true);
        typeof(SaveMenu).GetField("visible", Private).SetValue(Menu, false);
        Time.timeScale = 1;
    }

    private static void EscapeOpensAndResumes()
    {
        Assert.IsFalse(Root().gameObject.activeSelf, "the menu steps aside in a world");
        Escape();
        Assert.IsTrue(Menu.Visible, "an Escape nothing took opens the quick menu");
        Assert.AreEqual(0f, Time.timeScale, "time stops behind the quick menu");
        Escape();
        Assert.IsFalse(Menu.Visible, "Escape on the quick menu resumes");
        Assert.AreEqual(1f, Time.timeScale, "time runs again as it was");
    }

    private static void TheLibraryTakesItsOwnEscape()
    {
        LibraryWindow.Open();
        Assert.IsTrue(LibraryWindow.IsOpen);
        Escape();
        Assert.IsFalse(LibraryWindow.IsOpen, "Escape closes the Library");
        Assert.IsFalse(Menu.Visible, "and does not also open the menu");
    }

    private static void OpenOptions()
    {
        Escape();
        Assert.IsTrue(Menu.Visible);
        Menu.GetComponent<MenuView>().SendMessage("Update");
        string shown = Shown();
        StringAssert.Contains("Resume", shown);
        StringAssert.Contains("Menu Test", shown, "the quick menu names the world");
        Click("Options");
        Assert.IsTrue(Menu.GetComponent<MenuView>().ShowingOptions, "Options opens as a page");
    }

    private static void EveryTabBuilds()
    {
        foreach (string tab in new[] { "Display", "Audio", "Controls", "General" })
        {
            Click(tab);
            string shown = Shown();
            switch (tab)
            {
                case "Display": StringAssert.Contains("Brightness", shown); StringAssert.Contains("VSync", shown); break;
                case "Audio": StringAssert.Contains("Sound effects", shown); StringAssert.Contains("Music", shown); break;
                case "Controls":
                    StringAssert.Contains("World map", shown);
                    StringAssert.Contains(KeyBindings.Keys("map"), shown, "each key shows what it is bound to");
                    break;
                default: StringAssert.Contains("Autosave", shown); StringAssert.Contains("Edge scrolling", shown); break;
            }
        }
    }

    private static void EscapeBacksOutThenResumes()
    {
        Escape();
        Assert.IsTrue(Menu.Visible, "Escape on a page goes back to the menu");
        Assert.IsFalse(Menu.GetComponent<MenuView>().ShowingOptions);
        Escape();
        Assert.IsFalse(Menu.Visible, "then resumes");
    }

    // Never leave play mode with a world marked playable: quitting saves it.
    private static void Leave()
    {
        if (Menu != null)
        {
            typeof(SaveMenu).GetField("playable", Private).SetValue(Menu, false);
            typeof(SaveMenu).GetField("visible", Private).SetValue(Menu, false);
        }
        SaveSession.Current = null;
        Time.timeScale = 1;
    }
}
