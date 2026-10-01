using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>At home, choose this expedition's two techniques to keep; existing Opus remains learned.</summary>
public sealed class BattleRecoveryWindow : MonoBehaviour
{
    private static BattleRecoveryWindow instance;
    public static bool IsOpen => instance != null;
    private void OnEnable() => GameInput.CancelPressed += Close;
    private void OnDisable() => GameInput.CancelPressed -= Close;
    private void Close() => Destroy(gameObject);
    private void OnDestroy() { if (instance == this) instance = null; }
    private readonly HashSet<string> selected = new HashSet<string>();
    private string legend;
    private RectTransform book, rows;
    private TextMeshProUGUI feedback;
    private TooltipTheme theme;
    public static void Show(string name)
    {
        if (instance != null) Destroy(instance.gameObject);
        var window = new GameObject("Legend Opus Recovery").AddComponent<BattleRecoveryWindow>(); instance = window; window.legend = name; window.Build();
    }
    private void Build()
    {
        theme = CodeUI.Theme(nameof(BattleRecoveryWindow)); var canvas = CodeUI.Canvas(transform, "Recovery Canvas", 3, out var scaler);
        var root = CodeUI.Panel(canvas.transform, "Root", Vector2.zero, Vector2.one); CodeUI.Solid(root, "Backdrop", new Color(0, 0, 0, .9f), true);
        book = CodeUI.Panel(root, "Recovery", new Vector2(.5f, .5f), new Vector2(.5f, .5f)); book.sizeDelta = new Vector2(1050, 700); CodeUI.Plate(book, theme, scaler);
        var title = CodeUI.Label(book, "Title", legend + " — Legend Opus", 28, theme.titleColor, FontStyles.Normal, theme);
        CodeUI.Place(title.rectTransform, new Vector2(0, 1), Vector2.one, new Vector2(30, -75), new Vector2(-30, -20));
        rows = CodeUI.Panel(book, "Choices", Vector2.zero, Vector2.one); rows.offsetMin = new Vector2(30, 170); rows.offsetMax = new Vector2(-30, -90);
        var layout = rows.gameObject.AddComponent<VerticalLayoutGroup>(); layout.spacing = 10; layout.childControlHeight = true; layout.childForceExpandHeight = false; layout.childForceExpandWidth = true;
        var choices = LegendProgress.Instance.FieldAlterations(legend).Where(a => !a.trauma && !a.opus).GroupBy(a => a.card).Select(g => g.First()).ToList();
        foreach (var choice in choices)
        {
            TextMeshProUGUI button = null;
            button = CodeUI.TextButton(rows, choice.name + " (" + choice.card + ")", () =>
            {
                if (!selected.Remove(choice.card)) { if (selected.Count >= 2) { feedback.text = "Choose up to two techniques from this expedition."; return; } selected.Add(choice.card); }
                button.text = (selected.Contains(choice.card) ? "✓ " : "") + choice.name + " (" + choice.card + ")";
            }, theme, 23); button.gameObject.AddComponent<LayoutElement>().preferredHeight = 55;
        }
        feedback = CodeUI.Label(book, "Explanation", "Choose up to two Field Alterations. Rest clears acute pollution and Trauma, restores body and normal Clouded Composure, and records the selected techniques in the biography.", 21, theme.bodyColor, FontStyles.Normal, theme);
        CodeUI.Place(feedback.rectTransform, Vector2.zero, new Vector2(1, 0), new Vector2(30, 85), new Vector2(-30, 160));
        var rest = CodeUI.TextButton(book, "Rest and integrate choices", () =>
        { string why = LegendProgress.Instance.IntegrateBattleDeckAtHome(legend, selected); if (why == null) Destroy(gameObject); else feedback.text = why; }, theme, 24);
        CodeUI.Place(rest.rectTransform, Vector2.zero, new Vector2(.75f, 0), new Vector2(30, 25), new Vector2(-15, 70));
        var close = CodeUI.TextButton(book, "Cancel", () => Destroy(gameObject), theme, 24);
        CodeUI.Place(close.rectTransform, new Vector2(.75f, 0), new Vector2(1, 0), new Vector2(15, 25), new Vector2(-30, 70));
    }
    private void LateUpdate() { CodeUI.FitModal(book); }
}
