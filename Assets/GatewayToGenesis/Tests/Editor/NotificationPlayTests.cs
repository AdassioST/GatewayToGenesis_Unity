using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;

/// <summary>
/// The capital's notices in the real scene: they pop into the HUD's NotificationGrid (its placeholder rhombuses
/// hidden), news goes with a right click, issues (an empty council while legends wait) stay whatever is clicked.
/// </summary>
public class NotificationPlayTests
{
    private const string Scene = "Assets/GatewayToGenesis/Scenes/ClickerScreen.unity";

    private static Transform Grid() => TabHotkeys.Instance.Hud.transform.Find("NotificationGrid");

    private static NoticeClicks[] Shown() => Grid().GetComponentsInChildren<NoticeClicks>(false);

    private static string TitleOf(NoticeClicks notice) => notice.GetComponent<TooltipTrigger>().customTitle;

    private static void RightClick(NoticeClicks notice) =>
        notice.OnPointerClick(new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Right });

    [UnityTest]
    public IEnumerator NewsPopsIntoTheGridAndGoesWithARightClickWhileIssuesStay()
    {
        EditorSceneManager.OpenScene(Scene);
        yield return new EnterPlayMode();
        for (int i = 0; i < 600 && (TabHotkeys.Instance == null || Object.FindAnyObjectByType<NotificationFeed>() == null || GovernmentLogic.Instance == null); i++) yield return null;
        var menu = Object.FindAnyObjectByType<SaveMenu>();
        if (menu != null) typeof(SaveMenu).GetField("visible", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(menu, false);
        Time.timeScale = 1;
        for (float until = Time.realtimeSinceStartup + 2.5f; Time.realtimeSinceStartup < until;) yield return null;

        var grid = Grid();
        Assert.IsNotNull(grid, "the HUD's NotificationGrid");
        Assert.IsFalse(grid.Cast<Transform>().Any(c => c.gameObject.activeSelf && c.GetComponent<NoticeClicks>() == null), "the scene's placeholder rhombuses are hidden");
        var council = Shown().FirstOrDefault(n => TitleOf(n) == "Council seats stand empty");
        Assert.IsNotNull(council, "the legends known at the start wait for seats: an issue shows");

        NotificationFeed.Push("A test discovery", "Something was found.", NotificationFeed.Topic.Discovery, key: "test");
        for (float until = Time.realtimeSinceStartup + 1f; Time.realtimeSinceStartup < until;) yield return null;
        var news = Shown().FirstOrDefault(n => TitleOf(n) == "A test discovery");
        Assert.IsNotNull(news, "news pops into the grid");
        Assert.Greater(news.transform.localScale.x, 0.9f, "and has finished popping in");
        Assert.Less(news.transform.GetSiblingIndex(), council.transform.GetSiblingIndex(), "news stacks above the issues");

        RightClick(council);
        RightClick(news);
        for (float until = Time.realtimeSinceStartup + 1.5f; Time.realtimeSinceStartup < until;) yield return null;
        Assert.IsFalse(Shown().Any(n => n != null && TitleOf(n) == "A test discovery"), "a right click dismisses news");
        Assert.IsTrue(Shown().Any(n => TitleOf(n) == "Council seats stand empty"), "an issue stays until it is solved");
        yield return new ExitPlayMode();
    }
}
