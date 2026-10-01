using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// The Tutorial Voice (built in code, <see cref="CodeUI"/>): the Auric Aria, a god speaking in whispers. No cards, only
/// golden words that float and breathe over the game (<see cref="AuricVoice"/>), in two forms (<see cref="AuricForm"/>):
/// - an <b>announcement</b>, a little above the middle of the screen: a small whisper, a large title, a small line under
///   it, for what begins or is completed (the founding);
/// - a <b>line</b>, low on the screen like a subtitle: one or two sentences of hers, "(sob)" directions drawn fainter.
/// She says one thing at a time, from two sources:
/// - what happens in the moment (<see cref="AuricAria.Announce"/>, <see cref="AuricAria.Say"/>: the founders home, the
///   world opening, a first death) is said <b>at once</b>, held until read, and not hushed by the player's clicks;
/// - teaching waits for a pause: nothing is taught until the player has been idle <see cref="IdleSeconds"/> (no click,
///   key, scroll or drag, and the pointer at rest), and she falls silent the moment they act, returning at the next
///   pause while it still applies. A <b>lesson</b> (the founding) is an announcement with its control ringed in
///   breathing gold (<see cref="TutorialGlow"/>); <b>hints</b> are lines with a softer glow (or a gold ring on the world
///   map), one at a time, until learnt (done, answered by pressing what glows, or heard long enough) and remembered in
///   PlayerPrefs, so a second playthrough is left alone.
/// Her voice-over hooks are <see cref="AuricAria.Began"/> and <see cref="AuricAria.Ended"/> (<see cref="AuricVoiceOver"/>
/// plays recorded clips). Nothing here catches clicks. Everything waits while a story is told, a window covers the view
/// or the save menu is up; each lesson says whether it belongs to the capital, the world map or either. Created by
/// <see cref="GenesisLoop"/>. The whole voice can be turned off in Options (General, "Tutorial Voice").
/// To add a lesson: derive from <see cref="TutorialLesson"/> and list it in <see cref="Lessons"/>, most urgent first.
/// To answer a moment: call <see cref="AuricAria"/> (from a lesson's Watch, or <see cref="AuricMoments"/>).
/// </summary>
public class Tutorials : MonoBehaviour
{
    /// <summary>Every lesson and hint, in order of priority: the first with something to say is spoken.</summary>
    public static readonly List<TutorialLesson> Lessons = Defaults();

    // Fresh lessons for every play session (their own state, such as the founders' homecoming, must not leak between
    // sessions when the Editor keeps statics).
    private static List<TutorialLesson> Defaults() => new List<TutorialLesson>
    {
        new FoundingLesson(),
        new WorldOpensHint(),
        new WorldLookHint(),
        new LeadPartyHint(),
        new SendPartyHint(),
        new RationsHint(),
        new SurveyHint(),
        new HarvestHint(),
        new ClaimHint(),
        new FormExpeditionHint(),
        new RuinsHint(),
        new LensesHint(),
        new PartiesHint(),
        new RealmHint(),
        new EraScoreHint(),
    };

    private const string SeenKey = "g2g.tutorials.seen", OffKey = "g2g.tutorials.off";
    // Teaching comes in slowly, eased like dawn light (the letters condense on their own clock, AuricVoice), and leaves
    // quicker when the player acts; changing words fade out faster still. Words said at once answer a moment: they come
    // sooner and leave slowly, like a breath let out.
    private const float RefreshSeconds = 0.15f, FadeInSeconds = 2.4f, FadeOutSeconds = 0.6f, SwapSeconds = 0.35f;
    private const float SaidFadeIn = 1.2f, SaidFadeOut = 1.6f;
    private const float AnnouncementWidth = 860f, BodyWidth = 640f, AnnouncementRise = 70f, HintGlow = 0.55f;
    // A subtitle: its widest, how far above the bottom (share of the screen's height, never under the floor: clear of
    // the world's dock, or over the capital just above the ornament of its HUD along the bottom), and how faint its rule is.
    private const float LineWidth = 900f, LineHeight = 0.14f, LineFloor = 128f, CapitalLineFloor = 360f, LineRule = 0.5f, LineReveal = 0.022f;
    // A press this near a ringed spot of the map (canvas units) answers it. The pointer moving more than MovePixels in a
    // frame is not idle; once she speaks, it must wander WanderPixels before she falls silent (a hand resting on the
    // mouse does not hush her). A held button moved further than DragPixels is a drag.
    private const float SpotReach = 56f, MovePixels = 2f, WanderPixels = 90f, DragPixels = 3f;

    /// <summary>Real seconds of stillness before she teaches (tests shorten it).</summary>
    public static float IdleSeconds = 3f;

    /// <summary>Real seconds after a hint is learnt before the next may be spoken (tests shorten it).</summary>
    public static float HintGap = 3f;

    /// <summary>Remember learnt hints in PlayerPrefs (tests turn it off and use memory only).</summary>
    public static bool Persist = true;

    private static HashSet<string> _seen;
    private static bool _stirred;

    // One place she speaks from: its lines (an announcement's whisper, title and body; a subtitle's body alone), a rule
    // of light, and what they say now.
    private class Voice
    {
        public RectTransform rect;
        public CanvasGroup group;
        public TextMeshProUGUI whisper, title, body;
        public Image rule;
        public AuricVoice speech;
        public string key, text;
        public TutorialLesson speaker;
        // Said at once (not taught): it fades on the slower clock.
        public bool said;
        // How far the voice has come in (0 silent, 1 fully there), eased into its alpha.
        public float fade;
    }

    private TooltipTheme _theme;
    private Canvas _canvas;
    private RectTransform _canvasRect, _ring;
    private Voice _announcement, _line;
    private Image _ringImage;
    private TutorialGlow _glow;
    private AuricMoments _moments;
    private float _refreshAt, _nextHintAt, _idle, _wander;
    // Teaching: the lesson or hint that would speak at a pause, its words and their key.
    private TutorialLesson _speaker, _spoke;
    private AuricWords _teach;
    private string _key;
    // Said at once: the words, their key, how long they have been up and how long they hold.
    private AuricWords _said;
    private string _saidKey;
    private bool _saying;
    private float _saidHeld, _saidHold;
    private int _saidCount;
    // What her voice-over is saying (Began raised, Ended not yet).
    private AuricWords _voiced;
    private string _voicedKey;
    // Seconds each teaching utterance has been heard (lesson id and words id).
    private readonly Dictionary<string, float> _heard = new Dictionary<string, float>(StringComparer.Ordinal);

    /// <summary>What she is saying now: a lesson or hint's id, or the id of words said at once (null: she is silent).</summary>
    public static string ShowingId { get; private set; }

    /// <summary>The player did something (tests, or input the voice cannot see): she stops teaching and waits for the next pause.</summary>
    public static void Stir() => _stirred = true;

    // ===== WHAT HAS BEEN LEARNT =====

    private static HashSet<string> SeenSet
    {
        get
        {
            if (_seen != null) return _seen;
            _seen = new HashSet<string>(StringComparer.Ordinal);
            if (!Persist) return _seen;
            try { foreach (var id in PlayerPrefs.GetString(SeenKey, string.Empty).Split(',')) if (id.Length > 0) _seen.Add(id); }
            catch (Exception) { }
            return _seen;
        }
    }

    /// <summary>The hint was learnt (heard out, answered or done) in any playthrough on this machine.</summary>
    public static bool Seen(string id) => SeenSet.Contains(id);

    // Only a game begun from the save menu writes to PlayerPrefs, so play tests (which dismiss the menu) never mark a
    // player's hints as learnt.
    public static void MarkSeen(string id)
    {
        if (string.IsNullOrEmpty(id) || !SeenSet.Add(id) || !Persist || SaveSession.Current == null) return;
        try
        {
            PlayerPrefs.SetString(SeenKey, string.Join(",", SeenSet));
            PlayerPrefs.Save();
        }
        catch (Exception) { }
    }

    /// <summary>Forget every learnt hint (for a player who wants them again), or only the memory held now (tests).</summary>
    public static void ResetSeen()
    {
        _seen = new HashSet<string>(StringComparer.Ordinal);
        if (!Persist) return;
        try { PlayerPrefs.DeleteKey(SeenKey); } catch (Exception) { }
    }

    /// <summary>Drop the learnt hints held in memory: the next read reloads them from PlayerPrefs.</summary>
    public static void ResetSeenMemory() => _seen = null;

    /// <summary>
    /// The Tutorial Voice on (the default) or off, remembered on this machine (Options, General). Off silences all she
    /// says: lessons, hints and moments. Only the Options menu sets it, so it may be set from the title screen.
    /// </summary>
    public static bool VoiceOn
    {
        get { try { return !Persist || PlayerPrefs.GetInt(OffKey, 0) == 0; } catch (Exception) { return true; } }
        set { if (!Persist) return; try { PlayerPrefs.SetInt(OffKey, value ? 0 : 1); PlayerPrefs.Save(); } catch (Exception) { } }
    }

    // ===== BUILD =====

    private void Start()
    {
        Lessons.Clear();
        Lessons.AddRange(Defaults());
        AuricAria.Clear();
        _stirred = false;
        _theme = CodeUI.Theme(nameof(Tutorials));
        if (_theme == null) { enabled = false; return; }
        _canvas = CodeUI.Canvas(transform, "Tutorials", 6, out _);
        _canvasRect = (RectTransform)_canvas.transform;
        var root = _canvas.gameObject.AddComponent<CanvasGroup>();
        root.blocksRaycasts = root.interactable = false;

        float b = _theme.bodySize;
        // Announcements: a little above the middle of the screen, the title large.
        _announcement = Build("Announcement", new Vector2(0.5f, 0.5f));
        _announcement.whisper = Line(_announcement.rect, "Whisper", b + 2f, FontStyles.Italic, 3f);
        _announcement.title = Line(_announcement.rect, "Title", b + 22f, FontStyles.Normal, 1f);
        _announcement.rule = Rule(_announcement.rect);
        _announcement.body = Line(_announcement.rect, "Body", b + 3f, FontStyles.Normal, 0f);
        Bind(_announcement, new TMP_Text[] { _announcement.whisper, _announcement.title, _announcement.body },
            new[] { new Color(1f, 1f, 1f, 0.72f), Color.white, new Color(1f, 0.98f, 0.93f, 0.9f) }, 1);
        _announcement.rect.anchoredPosition = new Vector2(0f, AnnouncementRise);
        // Lines: a subtitle low on the screen, a faint rule of light unfolding under it as she begins.
        _line = Build("Line", new Vector2(0.5f, 0f));
        _line.body = Line(_line.rect, "Subtitle", b + 4f, FontStyles.Normal, 1f);
        _line.rule = Rule(_line.rect);
        Bind(_line, new TMP_Text[] { _line.body }, new[] { new Color(1f, 0.98f, 0.93f, 0.95f) }, -1);
        _line.speech.OrnamentLight = LineRule;
        _line.speech.RevealPerLetter = LineReveal;

        // The ring on a spot of the world map.
        _ringImage = CodeUI.Solid(_canvas.transform, "World Ring", TutorialGlow.Gold);
        _ringImage.sprite = RingSprite();
        _ring = _ringImage.rectTransform;
        _ring.anchorMin = _ring.anchorMax = _ring.pivot = new Vector2(0.5f, 0.5f);
        _ring.sizeDelta = new Vector2(72f, 72f);
        _ring.gameObject.SetActive(false);

        _glow = gameObject.AddComponent<TutorialGlow>();
        gameObject.AddComponent<AuricVoiceOver>();
        _moments = new AuricMoments();
    }

    private void OnDestroy()
    {
        _moments?.Dispose();
        Voiced(null, default);
    }

    // Floating words: no plate and nothing behind them.
    private Voice Build(string name, Vector2 anchor)
    {
        var voice = new Voice();
        voice.rect = CodeUI.Panel(_canvas.transform, name, anchor, anchor);
        voice.rect.pivot = anchor;
        return voice;
    }

    // Its lines share one glowing material; the rule unfolds after line <paramref name="ruleAfter"/> (-1: as she begins).
    private static void Bind(Voice voice, TMP_Text[] lines, Color[] tints, int ruleAfter)
    {
        voice.speech = voice.rect.gameObject.AddComponent<AuricVoice>();
        voice.speech.Bind(lines, tints, new Graphic[] { voice.rule }, new[] { ruleAfter });
        foreach (var g in voice.rect.GetComponentsInChildren<Graphic>(true)) g.raycastTarget = false;
        voice.group = voice.rect.gameObject.AddComponent<CanvasGroup>();
        voice.group.alpha = 0f;
        voice.group.blocksRaycasts = voice.group.interactable = false;
        voice.rect.gameObject.SetActive(false);
    }

    private static Image Rule(RectTransform parent)
    {
        var rule = CodeUI.Solid(parent, "Rule", AuricVoice.DeepGold);
        rule.sprite = AuricVoice.RuleSprite();
        rule.rectTransform.anchorMin = rule.rectTransform.anchorMax = rule.rectTransform.pivot = new Vector2(0.5f, 1f);
        return rule;
    }

    private TextMeshProUGUI Line(RectTransform parent, string name, float size, FontStyles style, float spacing)
    {
        var label = CodeUI.Label(parent, name, string.Empty, size, Color.white, style, _theme);
        label.alignment = TextAlignmentOptions.Center;
        label.lineSpacing = TooltipText.LineSpacing;
        label.characterSpacing = spacing;
        label.richText = true;
        var rect = label.rectTransform;
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        return label;
    }

    // A soft golden ring, drawn once.
    private static Sprite _ringSprite;

    private static Sprite RingSprite()
    {
        if (_ringSprite != null) return _ringSprite;
        const int size = 128;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        var pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(size / 2f, size / 2f)) / (size / 2f);
                float a = Mathf.Clamp01(1f - Mathf.Abs(d - 0.82f) / 0.09f) + 0.35f * Mathf.Clamp01(1f - Mathf.Abs(d - 0.82f) / 0.2f);
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)(255 * Mathf.Clamp01(a)));
            }
        texture.SetPixels32(pixels);
        texture.Apply();
        return _ringSprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
    }

    // ===== EACH FRAME =====

    private void Update()
    {
        if (_announcement == null) return;
        float now = Time.unscaledTime, dt = Time.unscaledDeltaTime;
        if (now >= _refreshAt)
        {
            _refreshAt = now + RefreshSeconds;
            Watch();
            Pick();
        }

        // The pause: any act resets it and stops her teaching; a still pointer lets it grow.
        bool acted = Acted(out bool press, out float moved);
        if (acted)
        {
            if (press && _spoke != null) Answer(_spoke);
            Hush();
        }
        else if (moved > MovePixels)
        {
            if (_spoke == null) _idle = 0f;
            else if ((_wander += moved) > WanderPixels) Hush();
        }
        else _idle += dt;

        bool quiet = (EventSystemLogic.Instance != null && EventSystemLogic.Instance.isEventActive) || SaveMenu.BlocksGameplay || OpenWindows.OverTheView;
        TakeSaid(quiet);

        // What she says now: words said at once first; else the lesson or hint, at a pause.
        TutorialLesson speaker = null;
        AuricWords words = default;
        string key = null;
        if (_saying)
        {
            words = _said;
            key = _saidKey;
        }
        else if (_speaker != null && !quiet && _idle >= IdleSeconds && Here(_speaker))
        {
            speaker = _speaker;
            words = _teach;
            key = _key;
        }
        var voice = key == null ? null : words.form == AuricForm.Announcement ? _announcement : _line;
        Drive(_announcement, voice == _announcement, key, words, speaker);
        Drive(_line, voice == _line, key, words, speaker);
        bool shown = voice != null && voice.key == key && voice.fade > 0f;
        // Said at once: held until read, then let go (it fades while the next is taken).
        if (_saying && shown && (_saidHeld += dt) >= _saidHold) _saying = false;
        bool heard = shown && voice.fade > 0.5f;
        _spoke = heard ? speaker : null;
        if (_spoke != null) Listen(dt);
        Voiced(shown ? key : null, words);
        if (_line.rect.gameObject.activeSelf) PlaceLine();

        _glow.Follow(_spoke != null ? _spoke.Target() : null, _spoke != null && _spoke.IsHint ? HintGlow : 1f);
        PlaceRing(_spoke);
        ShowingId = key == null ? null : speaker != null ? speaker.Id : words.id;
    }

    // Lessons and moments look at the game while it is played (not under the save menu or while a save is restored).
    private void Watch()
    {
        if (SaveMenu.BlocksGameplay || SaveSession.Restoring) return;
        _moments?.Watch();
        foreach (var lesson in Lessons) lesson?.Watch();
    }

    // Words said at once are taken in turn whenever she may speak. A story or a window cuts them short: they are said
    // again afterwards unless most of them was heard. With the voice off nothing waits.
    private void TakeSaid(bool quiet)
    {
        if (!VoiceOn)
        {
            AuricAria.Clear();
            _saying = false;
            return;
        }
        if (_saying && quiet)
        {
            if (_saidHeld < _saidHold * 0.5f) AuricAria.Resume(_said);
            _saying = false;
        }
        if (_saying || quiet || !Here(null) || !AuricAria.TryNext(out _said)) return;
        _saying = true;
        _saidHeld = 0f;
        _saidHold = AuricAria.HoldSeconds(_said);
        _saidKey = "said#" + (++_saidCount) + "|" + _said.id;
    }

    // Her voice-over follows what is shown: Began when new words appear, Ended when they go.
    private void Voiced(string key, AuricWords words)
    {
        if (key == _voicedKey) return;
        if (_voicedKey != null) AuricAria.RaiseEnded(_voiced);
        _voicedKey = key;
        _voiced = words;
        if (key != null) AuricAria.RaiseBegan(words);
    }

    private void Hush()
    {
        _idle = 0f;
        _wander = 0f;
    }

    // The hint she is teaching is heard a little longer: heard long enough, it is learnt.
    private void Listen(float dt)
    {
        _heard.TryGetValue(_key, out float heard);
        _heard[_key] = heard += dt;
        if (_speaker.IsHint && heard >= _speaker.Seconds) Learnt(_speaker.Id);
    }

    // A press while she spoke: on the glowing control or the ringed spot (or anywhere, for a hint about any action)
    // answers a hint.
    private void Answer(TutorialLesson speaker)
    {
        if (!speaker.IsHint) return;
        var pointer = InputUtils.MousePosition;
        var target = speaker.Target();
        bool answered = speaker.AnsweredByAnyInput;
        if (!answered && target != null && target.isActiveAndEnabled)
        {
            var canvas = target.canvas;
            var eye = canvas == null || canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            answered = RectTransformUtility.RectangleContainsScreenPoint(target.rectTransform, pointer, eye);
        }
        if (!answered && RingSpot(speaker, out var spot)) answered = Vector2.Distance(spot, pointer) <= SpotReach * Mathf.Max(0.01f, _canvas.scaleFactor);
        if (answered) Learnt(speaker.Id);
    }

    private void Learnt(string id)
    {
        if (Seen(id)) return;
        MarkSeen(id);
        _nextHintAt = Time.unscaledTime + HintGap;
    }

    // The player acted this frame: a press, a key, a scroll or a drag (or Stir). Also says how far the pointer moved.
    private static bool Acted(out bool press, out float moved)
    {
        var mouse = Mouse.current;
        bool menu = SaveMenu.BlocksGameplay;
        press = !menu && mouse != null && (mouse.leftButton.wasPressedThisFrame || mouse.rightButton.wasPressedThisFrame || mouse.middleButton.wasPressedThisFrame);
        moved = mouse != null ? mouse.delta.ReadValue().magnitude : 0f;
        bool held = mouse != null && (mouse.leftButton.isPressed || mouse.rightButton.isPressed || mouse.middleButton.isPressed);
        bool acted = press || (held && moved > DragPixels) || InputUtils.MouseScrollDelta.sqrMagnitude > 0f || InputUtils.AnyKeyDown || _stirred;
        _stirred = false;
        return acted;
    }

    // A voice fades in when it is the one speaking and already holds these words. Otherwise it fades out (quickly when
    // it is about to take other words), and takes the new words once silent.
    private void Drive(Voice voice, bool mine, string key, AuricWords words, TutorialLesson speaker)
    {
        if (mine && voice.key != key && voice.fade <= 0f) Show(voice, key, words, speaker);
        bool up = mine && voice.key == key;
        // Back after a pause: the words are drawn out of the air again. The same utterance with other words (a hint's
        // line changes as the player moves): laid out anew.
        if (up && voice.fade <= 0f) voice.speech.Say();
        else if (up && voice.text != words.Transcript) Show(voice, key, words, speaker);
        float seconds = up ? (voice.said ? SaidFadeIn : FadeInSeconds) : mine ? SwapSeconds : voice.said ? SaidFadeOut : FadeOutSeconds;
        voice.fade = Mathf.MoveTowards(voice.fade, up ? 1f : 0f, Time.unscaledDeltaTime / seconds);
        // Eased both ways: she arrives like light gathering, not a switch.
        float t = voice.fade;
        voice.group.alpha = t * t * t * (t * (t * 6f - 15f) + 10f);
        bool visible = voice.fade > 0f;
        if (voice.rect.gameObject.activeSelf != visible) voice.rect.gameObject.SetActive(visible);
    }

    // Where a lesson may speak (null: words said at once, over the capital or the map alike).
    private static bool Here(TutorialLesson lesson)
    {
        switch (lesson != null ? lesson.Place : TutorialPlace.Anywhere)
        {
            case TutorialPlace.Capital: return !WorldView.IsOpen;
            case TutorialPlace.World: return WorldView.Current == WorldView.Mode.World;
            default: return WorldView.Current == WorldView.Mode.Capital || WorldView.Current == WorldView.Mode.World;
        }
    }

    // Where the lesson's spot of the world map is on screen, when it has one and it is in view.
    private bool RingSpot(TutorialLesson lesson, out Vector2 screen)
    {
        screen = default;
        return lesson != null && lesson.WorldTarget(out var point) && WorldView.ScreenPoint(point.x, point.y, out screen)
            && RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvasRect, screen, null, out var local)
            && _canvasRect.rect.Contains(local);
    }

    // The ring breathes over the lesson's spot on the world map, when it has one and it is on screen.
    private void PlaceRing(TutorialLesson lesson)
    {
        bool on = RingSpot(lesson, out var screen);
        if (_ring.gameObject.activeSelf != on) _ring.gameObject.SetActive(on);
        if (!on) return;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvasRect, screen, null, out var local);
        float breath = GameSettings.ReduceMotion ? 0.7f : 0.5f - 0.5f * Mathf.Cos(Time.unscaledTime * 2f * Mathf.PI / 1.6f);
        _ring.anchoredPosition = local;
        _ring.localScale = Vector3.one * Mathf.Lerp(0.95f, 1.12f, breath);
        _ringImage.color = new Color(TutorialGlow.Gold.r, TutorialGlow.Gold.g, TutorialGlow.Gold.b, Mathf.Lerp(0.25f, 0.85f, breath));
    }

    // A subtitle sits low in the middle of the screen, above the HUD along the bottom.
    private void PlaceLine()
    {
        float floor = WorldView.Current == WorldView.Mode.World ? LineFloor : CapitalLineFloor;
        _line.rect.anchoredPosition = new Vector2(0f, Mathf.Max(floor, _canvasRect.rect.height * LineHeight));
    }

    // ===== CHOOSING =====

    private void Pick()
    {
        // Hints learnt by doing are never spoken.
        foreach (var hint in Lessons)
            if (hint != null && hint.IsHint && !Seen(hint.Id) && hint.Done()) Learnt(hint.Id);

        _speaker = null;
        _key = null;
        if (!VoiceOn) return;
        // The lesson: the first that speaks, while the game calls for it.
        foreach (var lesson in Lessons)
        {
            if (lesson == null || lesson.IsHint || !Here(lesson) || !Speaks(lesson, out var words, out string key)) continue;
            Choose(lesson, words, key);
            return;
        }
        if (Time.unscaledTime < _nextHintAt) return;
        foreach (var hint in Lessons)
        {
            if (hint == null || !hint.IsHint || Seen(hint.Id) || !Here(hint) || !Speaks(hint, out var words, out string key)) continue;
            Choose(hint, words, key);
            return;
        }
    }

    private static bool Speaks(TutorialLesson lesson, out AuricWords words, out string key)
    {
        key = null;
        if (!lesson.Speak(out words) || words.IsEmpty) return false;
        key = lesson.Id + "|" + words.id;
        return true;
    }

    private void Choose(TutorialLesson speaker, AuricWords words, string key)
    {
        _speaker = speaker;
        _teach = words;
        _key = key;
    }

    // The voice takes its new words and lays them out, then speaks them.
    private void Show(Voice voice, string key, AuricWords words, TutorialLesson speaker)
    {
        voice.key = key;
        voice.text = words.Transcript;
        voice.speaker = speaker;
        voice.said = speaker == null;
        voice.rect.gameObject.SetActive(true);
        if (voice == _announcement) LayOutAnnouncement(voice, words);
        else LayOutLine(voice, words);
        voice.speech.Say();
    }

    // Each line under the one before, the rule under the title.
    private void LayOutAnnouncement(Voice voice, AuricWords words)
    {
        voice.whisper.text = AuricWords.Styled(words.whisper);
        voice.title.text = words.title ?? string.Empty;
        voice.body.text = AuricWords.Styled(words.text);
        // As wide as the whisper and the title need; the line under them wraps rather than stretch the voice past BodyWidth.
        float width = 0f;
        foreach (var line in new[] { voice.whisper, voice.title })
            if (line.text.Length > 0) width = Mathf.Max(width, line.GetPreferredValues(line.text).x + 8f);
        if (voice.body.text.Length > 0) width = Mathf.Max(width, Mathf.Min(BodyWidth, voice.body.GetPreferredValues(voice.body.text).x + 8f));
        width = Mathf.Min(AnnouncementWidth, width);

        float y = 0f;
        y = Place(voice.whisper, width, y, 4f);
        y = Place(voice.title, width, y, 2f);
        var rule = voice.rule.rectTransform;
        rule.sizeDelta = new Vector2(Mathf.Min(width * 0.6f, 380f), 12f);
        rule.anchoredPosition = new Vector2(0f, -y);
        y += 12f + 6f;
        y = Place(voice.body, width, y, 0f);
        voice.rect.sizeDelta = new Vector2(width, y);
    }

    // The subtitle, wrapping onto a second line rather than running wide, and a short faint rule under it.
    private void LayOutLine(Voice voice, AuricWords words)
    {
        voice.body.text = AuricWords.Styled(words.text);
        float widest = Mathf.Min(LineWidth, _canvasRect.rect.width - 64f);
        float width = Mathf.Min(widest, voice.body.GetPreferredValues(voice.body.text).x + 8f);
        float y = Place(voice.body, width, 0f, 6f);
        var rule = voice.rule.rectTransform;
        rule.sizeDelta = new Vector2(Mathf.Min(width * 0.45f, 260f), 10f);
        rule.anchoredPosition = new Vector2(0f, -y);
        voice.rect.sizeDelta = new Vector2(width, y + 10f);
        PlaceLine();
    }

    // One line at <paramref name="y"/> from the top, as tall as its words wrap; returns where the next begins.
    private static float Place(TMP_Text line, float width, float y, float gap)
    {
        bool on = line.text.Length > 0;
        line.gameObject.SetActive(on);
        if (!on) return y;
        float height = line.GetPreferredValues(line.text, width, 0f).y;
        line.rectTransform.sizeDelta = new Vector2(0f, height);
        line.rectTransform.anchoredPosition = new Vector2(0f, -y);
        return y + height + gap;
    }
}
