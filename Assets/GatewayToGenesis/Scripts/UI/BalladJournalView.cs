using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Accessible between stories: ongoing ballads and the legends' directional relationship histories.</summary>
public sealed class BalladJournalView : MonoBehaviour
{
    public void OpenJournal(bool relationships = false)
    {
        if (_root == null) return;
        _relationships = relationships; _root.gameObject.SetActive(true); Refresh(true);
    }
    private static BalladJournalView _current;
    /// <summary>The journal is open (Escape closes it: <see cref="OpenWindows"/>).</summary>
    public static bool IsOpen => _current != null && _current._root != null && _current._root.gameObject.activeSelf;
    public static void ToggleJournal() => _current?.Toggle();

    private TooltipTheme _theme;
    private RectTransform _root, _body;
    private ScrollRect _scroll;
    private bool _relationships;
    private string _signature, _confirm;
    private float _refreshAt;

    private void Start()
    {
        _current = this;
        _theme = CodeUI.Theme(nameof(BalladJournalView));
        var canvas = CodeUI.Canvas(transform, "Ballads and Bonds", 5, out var scaler);
        _root = CodeUI.Panel(canvas.transform, "Journal", new Vector2(0.14f, 0.13f), new Vector2(0.86f, 0.88f));
        CodeUI.Plate(_root, _theme, scaler);
        // The plate's art is decorative; this surface prevents clicks reaching the world underneath.
        _root.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.01f);
        var tabs = CodeUI.Panel(_root, "Navigation", new Vector2(0f, 1f), Vector2.one);
        tabs.pivot = new Vector2(0.5f, 1f);
        tabs.offsetMin = new Vector2(24f, -64f);
        tabs.offsetMax = new Vector2(-24f, -16f);
        var navigation = tabs.gameObject.AddComponent<HorizontalLayoutGroup>();
        navigation.spacing = 24f;
        navigation.childControlWidth = navigation.childControlHeight = true;
        navigation.childForceExpandWidth = false;
        CodeUI.TextButton(tabs, "Ongoing Ballads", () => { _relationships = false; Refresh(true); }, _theme);
        CodeUI.TextButton(tabs, "Legend Relationships", () => { _relationships = true; Refresh(true); }, _theme);
        CodeUI.TextButton(tabs, "Close", Toggle, _theme);

        var area = CodeUI.Panel(_root, "Scroll", Vector2.zero, Vector2.one);
        area.offsetMin = new Vector2(24f, 24f);
        area.offsetMax = new Vector2(-24f, -76f);
        area.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.08f);
        area.gameObject.AddComponent<RectMask2D>();
        _scroll = area.gameObject.AddComponent<ScrollRect>();
        _scroll.horizontal = false;
        _scroll.scrollSensitivity = 40f;
        _body = CodeUI.Panel(area, "Entries", new Vector2(0f, 1f), Vector2.one);
        _body.pivot = new Vector2(0.5f, 1f);
        var layout = _body.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(14, 14, 12, 12);
        layout.spacing = 14f;
        layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandHeight = false;
        _body.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        _scroll.viewport = area;
        _scroll.content = _body;
        _root.gameObject.SetActive(false);
    }

    private void OnEnable() => GameInput.CancelPressed += Close;
    private void OnDisable() => GameInput.CancelPressed -= Close;
    private void Close() { if (_root != null) _root.gameObject.SetActive(false); _confirm = null; }
    private void Toggle()
    {
        if (_root == null) return;
        _root.gameObject.SetActive(!_root.gameObject.activeSelf);
        _confirm = null;
        if (_root.gameObject.activeSelf) Refresh(true);
    }

    private void Update()
    {
        if (_root == null) return;
        bool telling = EventSystemLogic.Instance != null && EventSystemLogic.Instance.isEventActive;
        if (telling) Close();
        if (!_root.gameObject.activeSelf || Time.unscaledTime < _refreshAt) return;
        _refreshAt = Time.unscaledTime + 0.5f;
        Refresh(false);
    }

    private void Refresh(bool reset)
    {
        var events = EventSystemLogic.Instance;
        var volumes = EventVolumeManager.Instance;
        var legends = LegendProgress.Instance;
        string signature = _relationships
            ? (legends == null ? "" : string.Join("\n", legends.RecruitedNames.Concat(legends.LostNames).Select(n => n + legends.RelationshipText(n))))
            : (events == null ? "" : string.Join("\n", events.Ballads.OrderBy(b => b.complete).Reverse().Select(b => BalladJournal.Describe(b, volumes))));
        signature += _confirm;
        if (!reset && _signature == signature) return;
        _signature = signature;
        float position = _scroll.verticalNormalizedPosition;
        foreach (Transform child in _body) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
        if (_relationships)
        {
            Label("Each legend has their own reading. Affection runs from 0 to 100; 50 is halfway. At 85+ or below 15, further shared experiences can upgrade or fray the stage. A stage change resets affection to 50. Affection does not imply romance.");
            if (legends != null)
                foreach (var name in legends.RecruitedNames.Concat(legends.LostNames).OrderBy(n => n))
                {
                    Label(TooltipText.Heading(name) + (legends.IsLost(name) ? " · Lost to Dissonance" : "") + "\n" + legends.RelationshipText(name));
                    foreach (var bond in legends.Relationships(name).Where(b => b.Significant && legends.IsRecruited(name)))
                    {
                        string key = name + "|" + bond.other;
                        string label = _confirm == key ? $"Confirm severance: {name} → {bond.other} (+5 strain; history and their feelings remain)" : $"Sever {name}'s tie to {bond.other}";
                        CodeUI.TextButton(_body, label, () =>
                        {
                            if (_confirm == key) { legends.SeverRelationship(name, bond.other); _confirm = null; }
                            else _confirm = key;
                            Refresh(false);
                        }, _theme);
                    }
                }
        }
        else
        {
            Label("Ongoing Ballads · the people, the last development, and the way forward");
            if (events == null || events.Ballads.Count == 0) Label("No ballads recorded yet. Discover stories through expeditions and events; their verses will appear here.");
            if (events != null)
                foreach (var ballad in events.Ballads.Reverse().OrderBy(b => b.complete))
                {
                    Label(BalladJournal.Describe(ballad, volumes));
                    if (volumes == null) continue;
                    foreach (var next in BalladJournal.NextVerses(ballad, volumes.GetVolumes().SelectMany(v => v.storyNodes)))
                        if (volumes.StoryBarriers(next).Count == 0)
                            CodeUI.TextButton(_body, $"Continue {ballad.title ?? ballad.id} · Verse {next.verse}", () => { if (volumes.ContinueBallad(next)) Close(); }, _theme);
                }
        }
        LayoutRebuilder.ForceRebuildLayoutImmediate(_body);
        _scroll.verticalNormalizedPosition = reset ? 1f : position;
    }

    private void Label(string text) => CodeUI.Label(_body, "Entry", text, _theme.bodySize, _theme.bodyColor, FontStyles.Normal, _theme);
}
