using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// What <see cref="SaveMenu"/> shows, built in code (<see cref="CodeUI"/>) on its own canvas over everything. Two faces:
/// the title screen before any world (Continue, New universe, Load, Achievements, Options, Quit) and the quick menu
/// when Escape is pressed in a world with no window open to take it (Resume, Save, Load, Achievements, Options, Return
/// to title, Quit). Pages open on the right: the new universe's name, the save files, lifetime achievements, and the
/// Options (<see cref="GameSettings"/>: General, Display, Audio, Controls with <see cref="KeyBindings"/>).
/// Escape backs out a page, then resumes. It follows SaveMenu's state every frame, so a play test that hides the menu
/// by its field hides this too. Also draws the birth of a new universe and, in a world, a small Menu button in the corner.
/// </summary>
public class MenuView : MonoBehaviour
{
    private enum Page { Main, NewWorld, Load, Achievements, Options }
    private enum Tab { General, Display, Audio, Controls }

    private const float ColumnX = 96f, PanelLeft = 690f;

    private SaveMenu _menu;
    private TooltipTheme _theme;
    private CanvasScaler _scaler;
    private RectTransform _canvasRect, _root, _column, _panel, _wings, _birth, _corner;
    private Image _backdrop, _orb, _birthGlow;
    private TextMeshProUGUI _birthText, _message, _status, _note;
    private IconActionButton _cornerAction;
    private ScrollRect _scroll;
    private Texture2D _glowTexture;
    private Sprite _glowSprite;

    private Page _page;
    private Tab _tab;
    private string _state, _worldName = "Arcanoria", _confirmDelete, _shownMessage;
    private float _statusAt;
    private readonly Dictionary<string, TextMeshProUGUI> _keyLabels = new Dictionary<string, TextMeshProUGUI>();

    /// <summary>The Options page is showing (tests; other code asks <see cref="SaveMenu.BlocksGameplay"/>).</summary>
    public bool ShowingOptions => _page == Page.Options && _root != null && _root.gameObject.activeSelf;

    private void Awake()
    {
        _menu = GetComponent<SaveMenu>();
        GameInput.MenuCancelPressed += OnMenuCancel;
    }

    private void OnDestroy()
    {
        GameInput.MenuCancelPressed -= OnMenuCancel;
        KeyBindings.CancelRebind();
        if (_glowSprite != null) Destroy(_glowSprite);
        if (_glowTexture != null) Destroy(_glowTexture);
    }

    // ===== FOLLOWING THE MENU =====

    private void Update()
    {
        if (_menu == null) return;
        if (_root == null) { Build(); if (_root == null) return; }

        bool birth = _menu.ExplosionStart >= 0f || _menu.Busy;
        string state = $"{_menu.Visible}|{_menu.Playable}|{birth}|{SaveSession.ForbiddenCopies.Length}|{_menu.CanContinue}|{SaveSession.Error != null}";
        if (state != _state)
        {
            bool opened = _state == null || !_state.StartsWith("True") && _menu.Visible;
            _state = state;
            if (opened || !_menu.Visible) { _page = Page.Main; _confirmDelete = null; KeyBindings.CancelRebind(); }
            Show(birth);
        }
        if (birth) Birth();
        _corner.gameObject.SetActive(_menu.Playable && !_menu.Visible && !birth);
        if (_corner.gameObject.activeSelf)
        {
            _corner.sizeDelta = new Vector2(GameSettings.ActionLabels ? 126 : 56, GameSettings.ActionLabels ? 84 : 56) * GameSettings.ActionSize;
            _cornerAction.RefreshPresentation();
            _cornerAction.SetState(false, false, SaveSession.Anchors.ToString());
            TooltipTrigger.Ensure(_cornerAction.gameObject).SetCustom("Menu", $"Save, load, options and achievements. {SaveSession.Anchors} Anchors available.");
        }
        if (_message != null && _menu.Message != _shownMessage) { _shownMessage = _menu.Message; _message.text = _shownMessage; }
        if (_status != null && Time.unscaledTime >= _statusAt) { _statusAt = Time.unscaledTime + 0.5f; _status.text = Status(); }
    }

    private void OnMenuCancel()
    {
        if (_menu == null || !_menu.Visible || _menu.Busy) return;
        if (_page != Page.Main) { Open(Page.Main); return; }
        _menu.Resume();
    }

    private void Show(bool birth)
    {
        _root.gameObject.SetActive(_menu.Visible || birth);
        _birth.gameObject.SetActive(birth);
        if (!_menu.Visible && !birth) return;
        bool title = !_menu.Playable;
        // The title stands on the dark of nothing; the quick menu dims the world behind it.
        _backdrop.color = title || birth ? new Color(0.015f, 0.012f, 0.035f, 1f) : new Color(0.01f, 0.008f, 0.02f, 0.72f);
        _orb.gameObject.SetActive(title && !birth);
        _wings.gameObject.SetActive(title && !birth && SaveSession.ForbiddenCopies.Length > 0);
        _column.gameObject.SetActive(!birth);
        if (!birth) Rebuild();
        else _panel.gameObject.SetActive(false);
    }

    private void Open(Page page)
    {
        if (page != Page.Options) KeyBindings.CancelRebind();
        _page = page;
        _confirmDelete = null;
        if (page == Page.Load || page == Page.Main) _menu.RefreshSlots();
        Rebuild();
    }

    private void Birth()
    {
        float start = _menu.ExplosionStart;
        if (start < 0f)
        {
            _birthGlow.gameObject.SetActive(false);
            _birthText.text = "The world gathers itself...";
            return;
        }
        // The first ripple: a light from the centre filling everything, then fading.
        float t = Mathf.Clamp01((Time.unscaledTime - start) / 3.5f);
        float size = Mathf.Lerp(0f, _canvasRect.rect.width * 3f, t * t);
        _birthGlow.gameObject.SetActive(true);
        _birthGlow.rectTransform.sizeDelta = new Vector2(size, size);
        _birthGlow.color = new Color(1f, 1f, 1f, GameSettings.ReduceMotion ? 0.6f : Mathf.Sin(t * Mathf.PI));
        _birthText.text = "The first ripple\n" + TooltipText.Muted(SaveSession.Current?.identity.id ?? "");
    }

    // ===== BUILD =====

    private void Build()
    {
        _theme = CodeUI.Theme(nameof(MenuView));
        if (_theme == null) return;
        var canvas = CodeUI.Canvas(transform, "Menu Canvas", 0, out _scaler);
        // Over every window, the Library and the tooltips.
        canvas.sortingOrder = 32000;
        _canvasRect = (RectTransform)canvas.transform;

        _glowTexture = new Texture2D(128, 128, TextureFormat.RGBA32, false);
        for (int y = 0; y < 128; y++) for (int x = 0; x < 128; x++)
        {
            float r = Vector2.Distance(new Vector2(x, y), new Vector2(63.5f, 63.5f)) / 64f;
            _glowTexture.SetPixel(x, y, new Color(0.8f, 0.72f, 1f, Mathf.Pow(Mathf.Clamp01(1f - r), 2f)));
        }
        _glowTexture.Apply();
        _glowSprite = Sprite.Create(_glowTexture, new Rect(0, 0, 128, 128), new Vector2(0.5f, 0.5f));

        // The corner button over a running world.
        _corner = CodeUI.Panel(canvas.transform, "Menu Button", new Vector2(1f, 1f), new Vector2(1f, 1f));
        _corner.pivot = new Vector2(1f, 1f);
        _corner.anchoredPosition = new Vector2(-14f, -14f);
        _corner.sizeDelta = new Vector2(56f, 56f);
        _cornerAction = IconActionButton.Create(_corner, "Menu", PixelIcon.Settings, () => _menu.Open(), "Save, load, options and achievements.", _theme);
        CodeUI.Stretch(_cornerAction.Rect);

        _root = CodeUI.Panel(canvas.transform, "Menu", Vector2.zero, Vector2.one);
        _backdrop = CodeUI.Solid(_root, "Backdrop", Color.black, true);

        _orb = CodeUI.Solid(_root, "Glow", new Color(1f, 1f, 1f, 0.3f));
        _orb.sprite = _glowSprite;
        CodeUI.Place(_orb.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(-260f, -420f), new Vector2(1000f, 840f));

        _wings = CodeUI.Panel(_root, "Wings", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
        BuildWings();

        _column = CodeUI.Panel(_root, "Column", Vector2.zero, Vector2.one);
        _panel = CodeUI.Panel(_root, "Page", Vector2.zero, Vector2.one);
        _panel.offsetMin = new Vector2(PanelLeft, 64f);
        _panel.offsetMax = new Vector2(-80f, -64f);

        _birth = CodeUI.Panel(_root, "Birth", Vector2.zero, Vector2.one);
        _birthGlow = CodeUI.Solid(_birth, "Ripple", Color.white);
        _birthGlow.sprite = _glowSprite;
        CodeUI.Place(_birthGlow.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        _birthText = CodeUI.Label(_birth, "Words", "", _theme.titleSize + 4f, _theme.titleColor, FontStyles.SmallCaps, _theme);
        CodeUI.Place(_birthText.rectTransform, Vector2.zero, new Vector2(1f, 0f), new Vector2(48f, 40f), new Vector2(-48f, 150f));
        _birthText.alignment = TextAlignmentOptions.BottomLeft;
    }

    // Wounded feather silhouettes and watching eyes, while a sacrificed world's copies remain.
    private void BuildWings()
    {
        for (int side = -1; side <= 1; side += 2) for (int i = 0; i < 12; i++)
        {
            var at = new Vector2(side * (180f + i * 18f), -i * 9f);
            var feather = CodeUI.Solid(_wings, "Feather", new Color(0.35f, 0.1f, 0.16f, 0.7f));
            feather.sprite = _glowSprite;
            var rect = feather.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = at;
            rect.sizeDelta = new Vector2(16f, 180f - i * 7f);
            rect.localRotation = Quaternion.Euler(0f, 0f, -side * (25f + i * 3f));
            var eye = CodeUI.Solid(_wings, "Eye", new Color(0.9f, 0.65f, 0.6f, 0.8f));
            eye.sprite = _glowSprite;
            eye.rectTransform.anchorMin = eye.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            eye.rectTransform.pivot = new Vector2(0f, 1f);
            eye.rectTransform.anchoredPosition = at + new Vector2(-12f, 0f);
            eye.rectTransform.sizeDelta = new Vector2(24f, 12f);
        }
    }

    private static void Clear(Transform parent)
    {
        for (int i = parent.childCount - 1; i >= 0; i--)
        {
            var child = parent.GetChild(i).gameObject;
            child.SetActive(false);
            Destroy(child);
        }
    }

    private void Rebuild()
    {
        if (_root == null) return;
        _keyLabels.Clear();
        _message = _status = _note = null;
        _shownMessage = null;
        Clear(_column);
        Clear(_panel);
        BuildColumn();
        _panel.gameObject.SetActive(_page != Page.Main);
        if (_page != Page.Main) BuildPage();
    }

    // ===== THE COLUMN (title or quick menu) =====

    private void BuildColumn()
    {
        bool title = !_menu.Playable;
        bool worldOk = SaveSession.Error == null && !_menu.Busy;
        RectTransform list;
        if (title)
        {
            CodeUI.Place(_column, Vector2.zero, new Vector2(0f, 1f), new Vector2(ColumnX, 70f), new Vector2(ColumnX + 520f, -70f));
            list = Stack(_column, 0, 10f, TextAnchor.MiddleLeft);
            bool forbidden = SaveSession.ForbiddenCopies.Length > 0;
            Text(list, forbidden ? "THE PUREST OF LOVE" : "THE RELIC OF ARCANORIA", _theme.subtitleSize + 4f, _theme.subtitleColor, FontStyles.SmallCaps);
            Text(list, "Gateway to Genesis", 64f, _theme.titleColor, FontStyles.SmallCaps).lineSpacing = -20f;
            Text(list, forbidden ? "You are mine, and mine alone." : "Every world begins in nothing.", _theme.bodySize + 2f, _theme.flavorColor, FontStyles.Italic);
            Space(list, 34f);
            if (_menu.CanContinue)
            {
                string last = _menu.Slots.FirstOrDefault(s => s.id == SaveSession.Profile.lastWorld)?.name;
                MenuButton(list, "Continue" + (string.IsNullOrEmpty(last) ? "" : $"  <size=60%>{TooltipText.Muted(last)}</size>"), () => _menu.Continue(), worldOk);
            }
            MenuButton(list, "New universe", () => Open(Page.NewWorld), worldOk, _page == Page.NewWorld);
            MenuButton(list, "Load a world", () => Open(Page.Load), worldOk && _menu.Slots.Count > 0, _page == Page.Load);
            MenuButton(list, "Achievements", () => Open(Page.Achievements), true, _page == Page.Achievements);
            MenuButton(list, "Options", () => Open(Page.Options), true, _page == Page.Options);
            MenuButton(list, "Quit", () => _menu.Quit());
            if (SaveSession.Error != null && SaveSession.Storage != null)
                MenuButton(list, "Recover the lifetime profile's backup", () => _menu.RecoverProfileBackup(), true, false, 22f);
            if (forbidden)
            {
                Space(list, 16f);
                Text(list, TooltipText.Muted($"A severed world's memory remains: {SaveSession.ForbiddenCopies.Length} restored copies in the Saves folder. This screen persists while its save or backup is present; it changes only the title artwork."), _theme.bodySize - 1f, _theme.bodyColor);
                MenuButton(list, "Remove the sacrificed world's remaining copies", () => _menu.RemoveRetiredCopies(), true, false, 22f);
            }
        }
        else
        {
            CodeUI.Place(_column, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(ColumnX - 20f, -330f), new Vector2(ColumnX + 500f, 330f));
            CodeUI.Plate(_column, _theme, _scaler);
            list = Stack(_column, 40, 6f, TextAnchor.UpperLeft);
            Text(list, "Paused", _theme.subtitleSize + 4f, _theme.subtitleColor, FontStyles.SmallCaps);
            Text(list, SaveSession.Current?.name ?? "This world", _theme.titleSize + 16f, _theme.titleColor, FontStyles.SmallCaps);
            _status = Text(list, Status(), _theme.bodySize - 1f, _theme.bodyColor);
            Space(list, 18f);
            MenuButton(list, "Resume", () => _menu.Resume(), true, false, 32f);
            MenuButton(list, "Save the world", () => { _menu.SaveWorld(); _statusAt = 0f; }, worldOk);
            MenuButton(list, "Load a world", () => Open(Page.Load), worldOk, _page == Page.Load);
            MenuButton(list, "Achievements", () => Open(Page.Achievements), true, _page == Page.Achievements);
            MenuButton(list, "Options", () => Open(Page.Options), true, _page == Page.Options);
            Space(list, 10f);
            MenuButton(list, "Return to title", () => _menu.ReturnToTitle(), worldOk);
            MenuButton(list, "Quit to desktop", () => _menu.Quit(), true);
            Text(list, TooltipText.Muted("Both save the world first."), _theme.bodySize - 2f, _theme.bodyColor);
        }
        Space(list, 10f);
        _message = Text(list, _menu.Message, _theme.bodySize - 1f, _theme.flavorColor);
        _shownMessage = _menu.Message;
    }

    private string Status()
    {
        var current = SaveSession.Current;
        if (current == null) return "";
        var played = TimeSpan.FromSeconds(current.playSeconds);
        string time = played.TotalHours >= 1 ? $"{(int)played.TotalHours} h {played.Minutes} min" : $"{played.Minutes} min";
        float since = _menu.SinceSaved;
        string saved = since < 0f ? "not saved yet this session" : since < 60f ? "saved just now" : $"saved {Mathf.FloorToInt(since / 60f)} min ago";
        int autosave = GameSettings.AutosaveMinutes;
        return $"{SaveSession.Anchors} Resonance Anchors{TooltipText.Separator}played {time}\n"
            + TooltipText.Muted(saved + (autosave > 0 ? $"; autosaves every {autosave} min" : "; autosave is off"));
    }

    // ===== PAGES =====

    private void BuildPage()
    {
        CodeUI.Plate(_panel, _theme, _scaler);
        string heading = _page == Page.NewWorld ? "A new universe" : _page == Page.Load ? "Worlds" : _page == Page.Achievements ? "Lifetime achievements" : "Options";
        var title = CodeUI.Label(_panel, "Title", heading, _theme.titleSize + 8f, _theme.titleColor, FontStyles.SmallCaps, _theme);
        CodeUI.Place(title.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(40f, -86f), new Vector2(-200f, -26f));
        var back = CodeUI.TextButton(_panel, "Back", () => Open(Page.Main), _theme);
        back.alignment = TextAlignmentOptions.MidlineRight;
        CodeUI.Place(back.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-180f, -80f), new Vector2(-40f, -30f));

        float top = 96f, bottom = 36f;
        if (_page == Page.Options)
        {
            top = 150f;
            bottom = 84f;
            BuildTabs();
            BuildFooter();
        }
        var content = ScrollArea(top, bottom);
        switch (_page)
        {
            case Page.NewWorld: NewWorldPage(content); break;
            case Page.Load: LoadPage(content); break;
            case Page.Achievements: AchievementsPage(content); break;
            case Page.Options:
                switch (_tab)
                {
                    case Tab.General: GeneralTab(content); break;
                    case Tab.Display: DisplayTab(content); break;
                    case Tab.Audio: AudioTab(content); break;
                    default: ControlsTab(content); break;
                }
                break;
        }
    }

    private void NewWorldPage(RectTransform content)
    {
        Text(content, "Every world begins in nothing. Name the universe you are about to wake; the name is only yours, and shows on its save.", _theme.bodySize, _theme.bodyColor);
        Space(content, 12f);
        var row = Fixed(content, 56f);
        var field = InputField(row, _worldName, "Arcanoria");
        field.onValueChanged.AddListener(v => _worldName = v);
        field.onSubmit.AddListener(v => { _worldName = v; _menu.NewUniverse(_worldName); });
        Space(content, 16f);
        MenuButton(content, "Begin", () => _menu.NewUniverse(_worldName), SaveSession.Error == null && !_menu.Busy, false, 32f);
        if (_menu.Playable)
            Text(content, TooltipText.Muted("The world you are in is saved first."), _theme.bodySize - 1f, _theme.bodyColor);
    }

    private void LoadPage(RectTransform content)
    {
        if (_menu.Slots.Count == 0) { Text(content, TooltipText.Muted("No worlds yet. Every world is saved as you play, and each keeps its own backup."), _theme.bodySize, _theme.bodyColor); return; }
        Text(content, TooltipText.Muted(_menu.Playable ? "The world you are in is saved before another is entered." : "Newest first. A world whose file is damaged can be entered from its backup."), _theme.bodySize - 1f, _theme.bodyColor);
        foreach (var slot in _menu.Slots)
        {
            Space(content, 8f);
            bool here = SaveSession.Current?.identity.id == slot.id && _menu.Playable;
            string line = slot.error != null
                ? TooltipText.Bad("Could not be read: " + slot.error)
                : $"{Saved(slot.savedUtc)}{TooltipText.Separator}{slot.anchors} Anchors{TooltipText.Separator}played {(int)(slot.playSeconds / 3600)} h {(int)(slot.playSeconds / 60) % 60} min";
            Text(content, $"<size=125%><color=#{ColorUtility.ToHtmlStringRGB(_theme.titleColor)}>{slot.name}</color></size>{(here ? TooltipText.Muted("  (this world)") : "")}\n{line}\n<size=80%>{TooltipText.Muted(slot.id)}</size>", _theme.bodySize - 1f, _theme.bodyColor);
            var buttons = Fixed(content, 40f);
            var row = buttons.gameObject.AddComponent<HorizontalLayoutGroup>();
            row.spacing = 36f;
            row.childControlWidth = row.childControlHeight = true;
            row.childForceExpandWidth = false;
            string id = slot.id;
            bool ok = SaveSession.Error == null && !_menu.Busy;
            if (!here) SmallButton(buttons, "Load", () => _menu.Load(id), ok && slot.error == null);
            SmallButton(buttons, "Recover backup", () => _menu.Load(id, true), ok);
            SmallButton(buttons, _confirmDelete == id ? TooltipText.Bad("Delete for ever?") : "Delete", () =>
            {
                if (_confirmDelete == id) { _confirmDelete = null; _menu.Delete(id); }
                else _confirmDelete = id;
                Rebuild();
            }, ok);
        }
    }

    private static string Saved(string utc)
    {
        if (!DateTime.TryParse(utc, null, System.Globalization.DateTimeStyles.RoundtripKind, out var when)) return utc ?? "";
        return "saved " + when.ToLocalTime().ToString("d MMM yyyy, HH:mm");
    }

    private void AchievementsPage(RectTransform content)
    {
        var unlocked = SaveSession.Profile?.unlocked ?? new List<string>();
        Text(content, TooltipText.Muted($"{unlocked.Count} earned on this machine, across every world. Each world keeps its own Resonance Anchors."), _theme.bodySize - 1f, _theme.bodyColor);
        Space(content, 8f);
        if (unlocked.Count == 0) Text(content, TooltipText.Muted("None yet."), _theme.bodySize, _theme.bodyColor);
        foreach (string id in unlocked)
        {
            string name = AchievementNote.Plain(Achievements.Tracker.Get(id)?.title ?? id);
            bool here = SaveSession.Current?.rewards.unlocked.Contains(id) == true;
            Text(content, TooltipText.Bullet(name + (here ? TooltipText.Muted("  earned in this world") : "")), _theme.bodySize, _theme.bodyColor);
        }
    }

    // ===== OPTIONS =====

    private void BuildTabs()
    {
        var tabs = CodeUI.Panel(_panel, "Tabs", new Vector2(0f, 1f), new Vector2(1f, 1f));
        tabs.offsetMin = new Vector2(40f, -146f);
        tabs.offsetMax = new Vector2(-40f, -96f);
        var row = tabs.gameObject.AddComponent<HorizontalLayoutGroup>();
        row.spacing = 44f;
        row.childControlWidth = row.childControlHeight = true;
        row.childForceExpandWidth = false;
        foreach (Tab tab in Enum.GetValues(typeof(Tab)))
        {
            var t = tab;
            bool on = tab == _tab;
            var label = CodeUI.TextButton(tabs, on ? $"<u>{tab}</u>" : tab.ToString(), () => { KeyBindings.CancelRebind(); _tab = t; Rebuild(); }, _theme, _theme.subtitleSize + 10f);
            label.color = on ? _theme.titleColor : _theme.subtitleColor;
            label.textWrappingMode = TextWrappingModes.NoWrap;
        }
        var rule = CodeUI.Solid(_panel, "Rule", new Color(_theme.subtitleColor.r, _theme.subtitleColor.g, _theme.subtitleColor.b, 0.35f));
        CodeUI.Place(rule.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(40f, -149f), new Vector2(-40f, -147f));
    }

    private void BuildFooter()
    {
        var footer = CodeUI.Panel(_panel, "Footer", Vector2.zero, new Vector2(1f, 0f));
        footer.offsetMin = new Vector2(40f, 26f);
        footer.offsetMax = new Vector2(-40f, 70f);
        _note = CodeUI.Label(footer, "Note", "", _theme.bodySize - 1f, _theme.flavorColor, FontStyles.Normal, _theme);
        CodeUI.Place(_note.rectTransform, Vector2.zero, new Vector2(1f, 1f), Vector2.zero, new Vector2(-300f, 0f));
        _note.alignment = TextAlignmentOptions.MidlineLeft;
        var reset = CodeUI.TextButton(footer, _tab == Tab.Controls ? "Restore every key" : "Restore defaults", RestoreDefaults, _theme);
        reset.alignment = TextAlignmentOptions.MidlineRight;
        reset.textWrappingMode = TextWrappingModes.NoWrap;
        CodeUI.Place(reset.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(-290f, 0f), Vector2.zero);
    }

    private void RestoreDefaults()
    {
        if (_tab == Tab.Controls) KeyBindings.ResetAll();
        else
        {
            GameSettings.ResetSection(_tab.ToString().ToLowerInvariant());
            if (_tab == Tab.General) Tutorials.VoiceOn = true;
        }
        Rebuild();
        if (_note != null) _note.text = $"{_tab} is back to the game's own settings.";
    }

    private void GeneralTab(RectTransform content)
    {
        Group(content, "Saving");
        int[] autosaves = GameSettings.AutosaveChoices;
        Cycler(Row(content, "Autosave", "The world is saved as you play; stories finish before a save."),
            autosaves.Select(m => m == 0 ? "Never" : $"Every {m} minutes").ToArray(),
            Array.IndexOf(autosaves, GameSettings.AutosaveMinutes), i => GameSettings.AutosaveMinutes = autosaves[i]);
        Toggle(Row(content, "Pause in the background", "Time stops while the game's window is not in front."),
            GameSettings.PauseInBackground, v => GameSettings.PauseInBackground = v);

        Group(content, "Guidance");
        Toggle(Row(content, "Tutorial Voice", "The Auric Aria speaks when you pause: what to do next, in golden words that fade as soon as you act."),
            Tutorials.VoiceOn, v => Tutorials.VoiceOn = v);
        var again = Row(content, "Lessons already learnt", "Each lesson is learnt once on this machine, then she no longer speaks it.");
        var againLabel = CodeUI.TextButton(again, "Hear them again", () => { Tutorials.ResetSeen(); if (_note != null) _note.text = "The Auric Aria will speak every lesson again."; }, _theme, _theme.bodySize + 2f);
        CodeUI.Stretch(againLabel.rectTransform);
        againLabel.alignment = TextAlignmentOptions.Center;
        float[] holds = GameSettings.TooltipHoldChoices;
        Cycler(Row(content, "Tooltip hold", "How long the pointer rests on a tooltip before it turns solid and its keywords can be opened."),
            new[] { "Quick", "Normal", "Patient", "Slow" }, Nearest(holds, GameSettings.TooltipHold), i => GameSettings.TooltipHold = holds[i]);

        Group(content, "Accessibility");
        Toggle(Row(content, "Action labels", "Show names under quick-action icons. Tooltips are also available on keyboard focus."),
            GameSettings.ActionLabels, value => GameSettings.ActionLabels = value);
        Slide(Row(content, "Action target size", "Enlarge the quick-action bar's buttons."), 1f, 1.5f, GameSettings.ActionSize, Times, value => GameSettings.ActionSize = value);

        Group(content, "Camera");
        Toggle(Row(content, "Edge scrolling", "The capital's view drifts when the pointer rests at a screen edge."),
            GameSettings.EdgeScrolling, v => GameSettings.EdgeScrolling = v);
        Slide(Row(content, "Scrolling speed", null), 0.5f, 2f, GameSettings.PanSpeed, Times, v => GameSettings.PanSpeed = v);
        Slide(Row(content, "Zoom speed", "The mouse wheel, in the capital."), 0.5f, 2f, GameSettings.ZoomSpeed, Times, v => GameSettings.ZoomSpeed = v);
    }

    private void DisplayTab(RectTransform content)
    {
        bool editor = Application.isEditor;
        Group(content, "Window");
        var modes = (WindowMode[])Enum.GetValues(typeof(WindowMode));
        Cycler(Row(content, "Window mode", editor ? "Applies in a built game; the Editor keeps its Game view." : "Borderless is a window the size of the screen."),
            modes.Select(m => m == WindowMode.Borderless ? "Borderless window" : m.ToString()).ToArray(),
            Array.IndexOf(modes, GameSettings.Window), i => GameSettings.Window = modes[i]);
        var sizes = GameSettings.Resolutions();
        sizes.Reverse();
        var desktop = new Vector2Int(Screen.currentResolution.width, Screen.currentResolution.height);
        var names = new List<string> { $"Desktop ({desktop.x} x {desktop.y})" };
        names.AddRange(sizes.Select(s => $"{s.x} x {s.y}"));
        int at = sizes.IndexOf(GameSettings.Resolution);
        Cycler(Row(content, "Resolution", editor ? "Applies in a built game." : null), names.ToArray(), at < 0 ? 0 : at + 1,
            i => GameSettings.Resolution = i == 0 ? Vector2Int.zero : sizes[i - 1]);

        Group(content, "Frames");
        Toggle(Row(content, "VSync", "Frames keep time with the screen: no tearing."), GameSettings.VSync, v => { GameSettings.VSync = v; Rebuild(); });
        int[] caps = GameSettings.FrameCapChoices;
        var capRow = Row(content, "Frame limit", GameSettings.VSync ? "The screen sets the pace while VSync is on." : "Fewer frames keep a laptop cooler.");
        Cycler(capRow, caps.Select(c => c == 0 ? "Unlimited" : $"{c} a second").ToArray(),
            Math.Max(0, Array.IndexOf(caps, GameSettings.FrameCap)), i => GameSettings.FrameCap = caps[i], !GameSettings.VSync);

        Group(content, "Picture");
        Slide(Row(content, "Brightness", "The world and the capital; the menus and panels keep their own light."), -1f, 1f, GameSettings.Brightness,
            v => Mathf.Abs(v) < 0.01f ? "As drawn" : (v > 0 ? "+" : "") + Mathf.RoundToInt(v * 100f) + "%", v => GameSettings.Brightness = v);
        Toggle(Row(content, "Reduce motion", "Pulses, swells and breathing glows hold still."), GameSettings.ReduceMotion, v => GameSettings.ReduceMotion = v);
    }

    private void AudioTab(RectTransform content)
    {
        Group(content, "Volume");
        Slide(Row(content, "Master", null), 0f, 1f, GameSettings.MasterVolume, Percent, v => GameSettings.MasterVolume = v);
        Slide(Row(content, "Music", null), 0f, 1f, GameSettings.Volume(SoundChannel.Music), Percent, v => GameSettings.SetVolume(SoundChannel.Music, v));
        Slide(Row(content, "Sound effects", "Stories, work and the world's events."), 0f, 1f, GameSettings.Volume(SoundChannel.Effects), Percent, v => GameSettings.SetVolume(SoundChannel.Effects, v));
        Slide(Row(content, "Ambience", "Wind, weather and the capital's life."), 0f, 1f, GameSettings.Volume(SoundChannel.Ambience), Percent, v => GameSettings.SetVolume(SoundChannel.Ambience, v));
        Slide(Row(content, "Interface", "Clicks, pages and notices."), 0f, 1f, GameSettings.Volume(SoundChannel.Interface), Percent, v => GameSettings.SetVolume(SoundChannel.Interface, v));
        Slide(Row(content, "Voice", "The Auric Aria's spoken words."), 0f, 1f, GameSettings.Volume(SoundChannel.Voice), Percent, v => GameSettings.SetVolume(SoundChannel.Voice, v));
        Group(content, "Focus");
        Toggle(Row(content, "Mute in the background", "Silence while the game's window is not in front."), GameSettings.MuteInBackground, v => GameSettings.MuteInBackground = v);
        Group(content, "Combat rhythm");
        Slide(Row(content, "Input latency", "Positive when your taps arrive late. Match the echoes after the call."), -250f, 250f,
            GameSettings.RhythmLatency, v => $"{v:0} ms", v => GameSettings.RhythmLatency = v);
        Slide(Row(content, "Timing window", "Wider windows help with motor or device timing differences."), .5f, 2f,
            GameSettings.RhythmWindow, Times, v => GameSettings.RhythmWindow = v);
        Toggle(Row(content, "Rhythm assistance", "Performs your Notes and prepared defenses at Clean; Perfect Echo and Tempo Fever require measured input."),
            GameSettings.RhythmAssist, v => GameSettings.RhythmAssist = v);
    }

    private void ControlsTab(RectTransform content)
    {
        Text(content, TooltipText.Muted("Click a key, then press the new one (Escape keeps the old). A key already in use swaps places. Hotkeys wait while you type in a field, and while Ctrl or Alt is held."), _theme.bodySize - 1f, _theme.bodyColor);
        string group = null;
        foreach (var entry in KeyBindings.Entries)
        {
            if (entry.group != group) { group = entry.group; Group(content, group); }
            var row = Row(content, entry.name, null);
            if (entry.locked)
            {
                var fixedKeys = CodeUI.Label(row, "Keys", KeyBindings.Keys(entry.id), _theme.bodySize + 1f, _theme.subtitleColor, FontStyles.Normal, _theme);
                CodeUI.Stretch(fixedKeys.rectTransform);
                fixedKeys.alignment = TextAlignmentOptions.Center;
                continue;
            }
            CodeUI.Solid(row, "Well", new Color(0f, 0f, 0f, 0.28f));
            string id = entry.id;
            var key = CodeUI.TextButton(row, KeyBindings.Keys(id), () => Rebind(id), _theme, _theme.bodySize + 3f);
            CodeUI.Stretch(key.rectTransform);
            key.alignment = TextAlignmentOptions.Center;
            key.color = _theme.titleColor;
            _keyLabels[id] = key;
        }
    }

    private void Rebind(string id)
    {
        if (KeyBindings.Rebinding) KeyBindings.CancelRebind();
        if (!_keyLabels.TryGetValue(id, out var label)) return;
        bool started = KeyBindings.StartRebind(id, note =>
        {
            foreach (var pair in _keyLabels) if (pair.Value != null) pair.Value.text = KeyBindings.Keys(pair.Key);
            if (_note != null) _note.text = note ?? "";
        });
        if (started) label.text = TooltipText.Muted("Press a key...");
    }

    // ===== PIECES =====

    private static string Percent(float v) => Mathf.RoundToInt(v * 100f) + "%";
    private static string Times(float v) => "x" + v.ToString("0.0#");

    private static int Nearest(float[] values, float value)
    {
        int best = 0;
        for (int i = 1; i < values.Length; i++) if (Mathf.Abs(values[i] - value) < Mathf.Abs(values[best] - value)) best = i;
        return best;
    }

    private RectTransform Stack(RectTransform parent, int padding, float spacing, TextAnchor align)
    {
        var list = CodeUI.Panel(parent, "List", Vector2.zero, Vector2.one);
        var layout = list.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(padding, padding, padding, padding);
        layout.spacing = spacing;
        layout.childAlignment = align;
        layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        return list;
    }

    private RectTransform ScrollArea(float top, float bottom)
    {
        var area = CodeUI.Panel(_panel, "Scroll", Vector2.zero, Vector2.one);
        area.offsetMin = new Vector2(36f, bottom);
        area.offsetMax = new Vector2(-30f, -top);
        _scroll = area.gameObject.AddComponent<ScrollRect>();
        _scroll.horizontal = false;
        _scroll.scrollSensitivity = 40f;
        _scroll.movementType = ScrollRect.MovementType.Clamped;
        var viewport = CodeUI.Panel(area, "Viewport", Vector2.zero, Vector2.one);
        viewport.gameObject.AddComponent<RectMask2D>();
        // Something to catch the wheel between rows.
        viewport.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.001f);
        var content = CodeUI.Panel(viewport, "Content", new Vector2(0f, 1f), new Vector2(1f, 1f));
        content.pivot = new Vector2(0.5f, 1f);
        var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(8, 22, 8, 16);
        layout.spacing = 6f;
        layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandHeight = false;
        content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        _scroll.viewport = viewport;
        _scroll.content = content;
        return content;
    }

    private TextMeshProUGUI Text(Transform parent, string text, float size, Color color, FontStyles style = FontStyles.Normal)
    {
        var label = CodeUI.Label(parent, "Text", text ?? "", size, color, style, _theme);
        label.alignment = TextAlignmentOptions.TopLeft;
        return label;
    }

    private static void Space(Transform parent, float height) => Fixed(parent, height);

    private static RectTransform Fixed(Transform parent, float height)
    {
        var go = new GameObject("Space", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var element = go.AddComponent<LayoutElement>();
        element.minHeight = element.preferredHeight = height;
        return (RectTransform)go.transform;
    }

    private void MenuButton(Transform parent, string text, Action onClick, bool interactable = true, bool current = false, float size = 30f)
    {
        var label = CodeUI.TextButton(parent, text, onClick, _theme, size);
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.overflowMode = TextOverflowModes.Ellipsis;
        if (current) label.color = _theme.titleColor;
        label.GetComponent<Button>().interactable = interactable;
        var element = label.gameObject.AddComponent<LayoutElement>();
        element.minHeight = element.preferredHeight = size * 1.55f;
    }

    private void SmallButton(Transform parent, string text, Action onClick, bool interactable)
    {
        var label = CodeUI.TextButton(parent, text, onClick, _theme, _theme.bodySize + 3f);
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.GetComponent<Button>().interactable = interactable;
    }

    private void Group(Transform parent, string name)
    {
        Space(parent, 10f);
        Text(parent, name, _theme.subtitleSize + 6f, _theme.subtitleColor, FontStyles.SmallCaps);
    }

    /// <summary>A setting's line: its name (and a short explanation) on the left; returns the right half for its control.</summary>
    private RectTransform Row(Transform parent, string name, string hint)
    {
        var row = Fixed(parent, string.IsNullOrEmpty(hint) ? 50f : 74f);
        row.name = name;
        string text = string.IsNullOrEmpty(hint) ? name : $"{name}\n<size=80%>{TooltipText.Muted(hint)}</size>";
        var label = CodeUI.Label(row, "Name", text, _theme.bodySize + 3f, _theme.bodyColor, FontStyles.Normal, _theme);
        CodeUI.Place(label.rectTransform, Vector2.zero, new Vector2(0.56f, 1f), Vector2.zero, Vector2.zero);
        label.alignment = TextAlignmentOptions.MidlineLeft;
        var control = CodeUI.Panel(row, "Control", new Vector2(0.6f, 0f), new Vector2(1f, 1f));
        control.offsetMin = new Vector2(0f, string.IsNullOrEmpty(hint) ? 5f : 16f);
        control.offsetMax = new Vector2(0f, string.IsNullOrEmpty(hint) ? -5f : -16f);
        return control;
    }

    /// <summary>A choice among a few: the value between two arrows; the value itself steps forward too.</summary>
    private void Cycler(RectTransform at, string[] choices, int index, Action<int> set, bool interactable = true)
    {
        if (choices.Length == 0) return;
        CodeUI.Solid(at, "Well", new Color(0f, 0f, 0f, 0.28f));
        int current = Mathf.Clamp(index, 0, choices.Length - 1);
        TextMeshProUGUI value = null;
        void Step(int by)
        {
            current = (current + by + choices.Length) % choices.Length;
            value.text = choices[current];
            set(current);
        }
        var left = CodeUI.TextButton(at, "<", () => Step(-1), _theme, _theme.bodySize + 6f);
        CodeUI.Place(left.rectTransform, Vector2.zero, new Vector2(0f, 1f), Vector2.zero, new Vector2(46f, 0f));
        left.alignment = TextAlignmentOptions.Center;
        var right = CodeUI.TextButton(at, ">", () => Step(1), _theme, _theme.bodySize + 6f);
        CodeUI.Place(right.rectTransform, new Vector2(1f, 0f), Vector2.one, new Vector2(-46f, 0f), Vector2.zero);
        right.alignment = TextAlignmentOptions.Center;
        value = CodeUI.TextButton(at, choices[current], () => Step(1), _theme, _theme.bodySize + 2f);
        CodeUI.Place(value.rectTransform, Vector2.zero, Vector2.one, new Vector2(48f, 0f), new Vector2(-48f, 0f));
        value.alignment = TextAlignmentOptions.Center;
        value.textWrappingMode = TextWrappingModes.NoWrap;
        value.color = _theme.titleColor;
        foreach (var label in new[] { left, right, value }) label.GetComponent<Button>().interactable = interactable;
    }

    private void Toggle(RectTransform at, bool on, Action<bool> set) => Cycler(at, new[] { "Off", "On" }, on ? 1 : 0, i => set(i == 1));

    private void Slide(RectTransform at, float min, float max, float value, Func<float, string> format, Action<float> set)
    {
        var area = CodeUI.Panel(at, "Slider", Vector2.zero, Vector2.one);
        area.offsetMax = new Vector2(-104f, 0f);
        var track = CodeUI.Solid(area, "Track", new Color(0f, 0f, 0f, 0.45f), true);
        CodeUI.Place(track.rectTransform, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0f, -5f), new Vector2(0f, 5f));
        var fillArea = CodeUI.Panel(area, "Fill Area", new Vector2(0f, 0.5f), new Vector2(1f, 0.5f));
        fillArea.offsetMin = new Vector2(0f, -5f);
        fillArea.offsetMax = new Vector2(0f, 5f);
        var fill = CodeUI.Solid(fillArea, "Fill", _theme.ringFill);
        var handleArea = CodeUI.Panel(area, "Handle Area", Vector2.zero, Vector2.one);
        handleArea.offsetMin = new Vector2(9f, 0f);
        handleArea.offsetMax = new Vector2(-9f, 0f);
        var handle = CodeUI.Solid(handleArea, "Handle", _theme.ringSolid, true);
        handle.rectTransform.sizeDelta = new Vector2(18f, 0f);

        var slider = area.gameObject.AddComponent<Slider>();
        slider.fillRect = fill.rectTransform;
        slider.handleRect = handle.rectTransform;
        slider.targetGraphic = handle;
        slider.direction = UnityEngine.UI.Slider.Direction.LeftToRight;
        slider.minValue = min;
        slider.maxValue = max;
        slider.SetValueWithoutNotify(value);
        var colors = slider.colors;
        colors.highlightedColor = new Color(1f, 0.96f, 0.82f);
        slider.colors = colors;

        var shown = CodeUI.Label(at, "Value", format(value), _theme.bodySize + 2f, _theme.titleColor, FontStyles.Normal, _theme);
        CodeUI.Place(shown.rectTransform, new Vector2(1f, 0f), Vector2.one, new Vector2(-90f, 0f), Vector2.zero);
        shown.alignment = TextAlignmentOptions.MidlineRight;
        slider.onValueChanged.AddListener(v => { shown.text = format(v); set(v); });
    }

    private TMP_InputField InputField(RectTransform parent, string text, string placeholderText)
    {
        var root = CodeUI.Panel(parent, "Name", Vector2.zero, Vector2.one);
        var background = root.gameObject.AddComponent<Image>();
        background.color = new Color(0f, 0f, 0f, 0.4f);
        var field = root.gameObject.AddComponent<TMP_InputField>();
        var area = CodeUI.Panel(root, "Text Area", Vector2.zero, Vector2.one);
        area.offsetMin = new Vector2(14f, 4f);
        area.offsetMax = new Vector2(-14f, -4f);
        area.gameObject.AddComponent<RectMask2D>();
        var placeholder = CodeUI.Label(area, "Placeholder", placeholderText, _theme.bodySize + 4f, _theme.subtitleColor, FontStyles.Italic, _theme);
        placeholder.textWrappingMode = TextWrappingModes.NoWrap;
        placeholder.alignment = TextAlignmentOptions.MidlineLeft;
        CodeUI.Stretch(placeholder.rectTransform);
        var label = CodeUI.Label(area, "Text", string.Empty, _theme.bodySize + 4f, _theme.titleColor, FontStyles.Normal, _theme);
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.alignment = TextAlignmentOptions.MidlineLeft;
        label.richText = false;
        CodeUI.Stretch(label.rectTransform);
        field.targetGraphic = background;
        field.textViewport = area;
        field.textComponent = label;
        field.placeholder = placeholder;
        if (_theme.font != null) field.fontAsset = _theme.font;
        field.pointSize = _theme.bodySize + 4f;
        field.lineType = TMP_InputField.LineType.SingleLine;
        field.richText = false;
        field.characterLimit = 60;
        field.text = text;
        return field;
    }
}
