using System;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public class UiAccessibilityTests
{
    private GameObject host;
    private TooltipTheme theme;
    private bool labels;
    private float targetSize;
    [SetUp]
    public void SetUp()
    {
        labels = GameSettings.ActionLabels; targetSize = GameSettings.ActionSize;
        GameSettings.ActionLabels = false; GameSettings.ActionSize = 1;
        host = new GameObject("UI Test Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        ((RectTransform)host.transform).sizeDelta = new Vector2(1280, 720);
        theme = ScriptableObject.CreateInstance<TooltipTheme>();
    }
    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(host); Object.DestroyImmediate(theme);
        GameSettings.ActionLabels = labels; GameSettings.ActionSize = targetSize;
    }
    [Test]
    public void CollapsedBarKeepsPrimaryActionsAndExpansionRespectsUnlocks()
    {
        bool unlocked = false;
        var bar = QuickActionBar.Create(host.transform, theme, host.GetComponent<CanvasScaler>());
        var main = bar.Add("World", PixelIcon.Compass, () => { }, "Open map", theme, true);
        var extra = bar.Add("Journal", PixelIcon.Book, () => { }, "Open journal", theme);
        var gated = bar.Add("Bestiary", PixelIcon.Beast, () => { }, "Open creatures", theme, visible: () => unlocked);
        bar.Reflow();
        Assert.IsTrue(main.gameObject.activeSelf); Assert.IsFalse(extra.gameObject.activeSelf);
        bar.Toggle();
        Assert.IsTrue(extra.gameObject.activeSelf); Assert.IsFalse(gated.gameObject.activeSelf);
        unlocked = true; bar.Reflow(); Assert.IsTrue(gated.gameObject.activeSelf);
        bar.Toggle(); Assert.IsFalse(extra.gameObject.activeSelf); Assert.IsTrue(main.gameObject.activeSelf);
    }
    [Test]
    public void LargeLabelledTargetsWrapWithinNarrowCanvas()
    {
        ((RectTransform)host.transform).sizeDelta = new Vector2(480, 720);
        GameSettings.ActionLabels = true; GameSettings.ActionSize = 1.5f;
        var bar = QuickActionBar.Create(host.transform, theme, host.GetComponent<CanvasScaler>());
        for (int i = 0; i < 6; i++) bar.Add("Action " + i, PixelIcon.Book, () => { }, "Description", theme);
        bar.Toggle();
        Assert.LessOrEqual(bar.Rect.rect.width, 432);
        foreach (var button in bar.GetComponentsInChildren<IconActionButton>())
        {
            Assert.GreaterOrEqual(button.Rect.rect.width, 56);
            Assert.GreaterOrEqual(button.Rect.rect.height, 56);
            Assert.LessOrEqual(button.Rect.anchoredPosition.x + button.Rect.rect.width, bar.Rect.rect.width);
            Assert.IsTrue(button.GetComponentsInChildren<TextMeshProUGUI>().Any(t => t.name == "Action name"));
        }
    }
    [Test]
    public void EveryIconHasCrispPixelsAndButtonsKeepSemanticTooltips()
    {
        foreach (PixelIcon icon in Enum.GetValues(typeof(PixelIcon)))
        {
            var sprite = PixelIcons.Get(icon);
            Assert.AreEqual(FilterMode.Point, sprite.texture.filterMode);
            Assert.AreEqual(1, sprite.texture.mipmapCount);
        }
        int clicked = 0;
        var action = IconActionButton.Create(host.transform, "Humanize the Score", PixelIcon.Music, () => clicked++, "Bring the written score to life.", theme);
        var data = new TooltipData();
        Assert.IsTrue(action.GetComponent<TooltipTrigger>().TryBuild(data));
        Assert.AreEqual("Humanize the Score", data.title);
        action.Button.onClick.Invoke(); Assert.AreEqual(1, clicked);
        Assert.IsNotNull(action.GetComponent<UiFocus>());
        action.GetComponent<UiFocus>().OnSelect(null);
        Assert.AreEqual(4, action.GetComponentsInChildren<Image>().Count(i => i.name == "Focus edge"));
        action.GetComponent<UiFocus>().OnDeselect(null);
        Assert.AreEqual(0, action.GetComponentsInChildren<Image>().Count(i => i.name == "Focus edge"));
    }
}
