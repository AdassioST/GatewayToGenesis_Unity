using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// The Tutorial Voice (built in code, <see cref="CodeUI"/>): the Auric Aria guiding a first playthrough. No cards, only
/// golden words that float and breathe over the game (<see cref="AuricVoice"/>) in three layers: a small whisper, a
/// large title and a small line saying what to do (<see cref="TutorialCard"/>).
/// She waits for a pause: nothing is said until the player has been idle <see cref="IdleSeconds"/> (no click, key,
/// scroll or drag, and the pointer at rest), and she falls silent the moment they act, returning at the next pause while
/// her lesson still applies. Two kinds (<see cref="TutorialLesson"/>):
/// - a <b>lesson</b> (the founding): larger words a little above the middle of the screen and its control ringed in
///   breathing gold (<see cref="TutorialGlow"/>), at every pause while the game state calls for it;
/// - <b>hints</b>: smaller words under the Age banner (or floating just above a gold ring on the world map), a softer
///   glow on the control, one at a time, spoken at pauses until learnt (done, answered by pressing what glows, or heard
///   long enough) and remembered in PlayerPrefs, so a second playthrough is left alone.
/// Nothing here catches clicks. Everything hides while a story is told, a window covers the view or the save menu is up;
/// each lesson says whether it belongs to the capital, the world map or either. Created by <see cref="GenesisLoop"/>.
/// The whole voice can be turned off in Options (General, "Tutorial Voice").
/// To add one: derive from <see cref="TutorialLesson"/> and list it in <see cref="Lessons"/>, most urgent first.
/// </summary>
public class Tutorials : MonoBehaviour
{
    /// <summary>Every lesson and hint, in order of priority: the first with something to say is spoken.</summary>
    public static readonly List<TutorialLesson> Lessons = Defaults();

    // Fresh lessons for every play session (their own state, such as the founders' farewell, must not leak between
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
    // She comes in slowly, eased like dawn light (the letters condense on their own clock, AuricVoice), and leaves
    // quicker when the player acts; changing words fade out faster still.
    private const float RefreshSeconds = 0.15f, FadeInSeconds = 2.4f, FadeOutSeconds = 0.6f, SwapSeconds = 0.35f;
    private const float LessonWidth = 860f, BodyWidth = 640f, LessonRise = 70f, HintMaxWidth = 520f, HintGlow = 0.55f;
    // A press this near a ringed spot of the map (canvas units) answers it. The pointer moving more than MovePixels in a
    // frame is not idle; once she speaks, it must wander WanderPixels before she falls silent (a hand resting on the
    // mouse does not hush her). A held button moved further than DragPixels is a drag.
    private const float SpotReach = 56f, MovePixels = 2f, WanderPixels = 90f, DragPixels = 3f;

    /// <summary>Real seconds of stillness before she speaks (tests shorten it).</summary>
    public static float IdleSeconds = 3f;

    /// <summary>Real seconds after a hint is learnt before the next may be spoken (tests shorten it).</summary>
    public static float HintGap = 3f;

    /// <summary>Remember learnt hints in PlayerPrefs (tests turn it off and use memory only).</summary>
    public static bool Persist = true;

    private static HashSet<string> _seen;
    private static bool _stirred;

    // One place she speaks from: three lines, a rule under the title, and what they say now.
    private class Voice
    {
        public RectTransform rect;
        public CanvasGroup group;
        public TextMeshProUGUI whisper, title, body;
        public Image rule;
        public AuricVoice speech;
        public string key, words;
        public TutorialLesson speaker;
        public float widest;
        // How far the voice has come in (0 silent, 1 fully there), eased into its alpha.
        public float fade;
    }

    private TooltipTheme _theme;
    private Canvas _canvas;
    private RectTransform _canvasRect, _ring;
    private Voice _lessonVoice, _hintVoice;
    private Image _ringImage;
    private TutorialGlow _glow;
    private float _refreshAt, _nextHintAt, _idle, _wander;
    private TutorialLesson _speaker, _spoke;
    private TutorialCard _card;
    private string _key;
    // Seconds each utterance has been heard (lesson id and title), and the lesson utterances heard out.
    private readonly Dictionary<string, float> _heard = new Dictionary<string, float>(StringComparer.Ordinal);
    private readonly HashSet<string> _retired = new HashSet<string>(StringComparer.Ordinal);

    /// <summary>The lesson or hint she is speaking now (null: she is silent).</summary>
    public static string ShowingId { get; private set; }

    /// <summary>The player did something (tests, or input the voice cannot see): she falls silent and waits for the next pause.</summary>
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
    /// The Tutorial Voice on (the default) or off, remembered on this machine (Options, General). Off silences every
    /// lesson and hint. Only the Options menu sets it, so it may be set from the title screen, before any world.
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
        _stirred = false;
        _theme = CodeUI.Theme(nameof(Tutorials));
        if (_theme == null) { enabled = false; return; }
        _canvas = CodeUI.Canvas(transform, "Tutorials", 6, out _);
        _canvasRect = (RectTransform)_canvas.transform;
        var root = _canvas.gameObject.AddComponent<CanvasGroup>();
        root.blocksRaycasts = root.interactable = false;

        float b = _theme.bodySize;
        // The lesson: her words a little above the middle of the screen, the title large.
        _lessonVoice = Build("Lesson", new Vector2(0.5f, 0.5f), b + 2f, b + 22f, b + 3f, LessonWidth);
        _lessonVoice.rect.anchoredPosition = new Vector2(0f, LessonRise);
        // A hint: smaller, under the Age banner (or over its spot on the map).
        _hintVoice = Build("Hint", new Vector2(0.5f, 1f), b - 1f, b + 8f, b, HintMaxWidth);

        // The ring on a spot of the world map.
        _ringImage = CodeUI.Solid(_canvas.transform, "World Ring", TutorialGlow.Gold);
        _ringImage.sprite = RingSprite();
        _ring = _ringImage.rectTransform;
        _ring.anchorMin = _ring.anchorMax = _ring.pivot = new Vector2(0.5f, 0.5f);
        _ring.sizeDelta = new Vector2(72f, 72f);
        _ring.gameObject.SetActive(false);

        _glow = gameObject.AddComponent<TutorialGlow>();
    }

    // Floating words: no plate and nothing behind them, three lines of light and a rule of light under the title.
    private Voice Build(string name, Vector2 anchor, float whisperSize, float titleSize, float bodySize, float widest)
    {
        var voice = new Voice { widest = widest };
        voice.rect = CodeUI.Panel(_canvas.transform, name, anchor, anchor);
        voice.rect.pivot = anchor;
        voice.whisper = Line(voice.rect, "Whisper", whisperSize, FontStyles.Italic, 3f);
        voice.title = Line(voice.rect, "Title", titleSize, FontStyles.Normal, 1f);
        voice.rule = CodeUI.Solid(voice.rect, "Rule", AuricVoice.DeepGold);
        voice.rule.sprite = AuricVoice.RuleSprite();
        voice.rule.rectTransform.anchorMin = voice.rule.rectTransform.anchorMax = voice.rule.rectTransform.pivot = new Vector2(0.5f, 1f);
        voice.body = Line(voice.rect, "Body", bodySize, FontStyles.Normal, 0f);
        voice.speech = voice.rect.gameObject.AddComponent<AuricVoice>();
        voice.speech.Bind(new TMP_Text[] { voice.whisper, voice.title, voice.body },
            new[] { new Color(1f, 1f, 1f, 0.72f), Color.white, new Color(1f, 0.98f, 0.93f, 0.9f) },
            new Graphic[] { voice.rule }, new[] { 1 });
        foreach (var g in voice.rect.GetComponentsInChildren<Graphic>(true)) g.raycastTarget = false;
        voice.group = voice.rect.gameObject.AddComponent<CanvasGroup>();
        voice.group.alpha = 0f;
        voice.group.blocksRaycasts = voice.group.interactable = false;
        voice.rect.gameObject.SetActive(false);
        return voice;
    }

    private TextMeshProUGUI Line(RectTransform parent, string name, float size, FontStyles style, float spacing)
    {
        var label = CodeUI.Label(parent, name, string.Empty, size, Color.white, style, _theme);
        label.alignment = TextAlignmentOptions.Center;
        label.lineSpacing = TooltipText.LineSpacing;
        label.characterSpacing = spacing;
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
        if (_lessonVoice == null) return;
        float now = Time.unscaledTime, dt = Time.unscaledDeltaTime;
        if (now >= _refreshAt)
        {
            _refreshAt = now + RefreshSeconds;
            Pick();
        }

        // The pause: any act resets it and silences her; a still pointer lets it grow.
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
        bool speak = _speaker != null && !quiet && _idle >= IdleSeconds && Here(_speaker);
        var voice = speak ? (_speaker.IsHint ? _hintVoice : _lessonVoice) : null;
        Drive(_lessonVoice, voice == _lessonVoice && voice != null);
        Drive(_hintVoice, voice == _hintVoice && voice != null);
        bool heard = voice != null && voice.key == _key && voice.fade > 0.5f;
        _spoke = heard ? _speaker : null;
        if (heard) Listen(dt);
        if (_hintVoice.rect.gameObject.activeSelf && _hintVoice.speaker != null && _hintVoice.key == _key) PlaceHint(_hintVoice);

        _glow.Follow(heard ? _speaker.Target() : null, heard && _speaker.IsHint ? HintGlow : 1f);
        PlaceRing(heard ? _speaker : null);
        ShowingId = speak ? _speaker.Id : null;
    }

    private void Hush()
    {
        _idle = 0f;
        _wander = 0f;
    }

    // The words she is speaking are heard a little longer: a hint heard long enough is learnt, a lesson's utterance with
    // a limit is retired.
    private void Listen(float dt)
    {
        _heard.TryGetValue(_key, out float heard);
        _heard[_key] = heard += dt;
        if (_speaker.IsHint)
        {
            if (heard >= _speaker.Seconds) Learnt(_speaker.Id);
        }
        else if (_card.retireAfter > 0f && heard >= _card.retireAfter) _retired.Add(_key);
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
    private void Drive(Voice voice, bool mine)
    {
        if (mine && voice.key != _key && voice.fade <= 0f) Show(voice);
        bool up = mine && voice.key == _key;
        // Back after a pause: the words are drawn out of the air again. The same utterance with other words (a hint's
        // line changes as the player moves): laid out anew.
        if (up && voice.fade <= 0f) voice.speech.Say();
        else if (up && voice.words != Words) Show(voice);
        float seconds = up ? FadeInSeconds : mine ? SwapSeconds : FadeOutSeconds;
        voice.fade = Mathf.MoveTowards(voice.fade, up ? 1f : 0f, Time.unscaledDeltaTime / seconds);
        // Eased both ways: she arrives like light gathering, not a switch.
        float t = voice.fade;
        voice.group.alpha = t * t * t * (t * (t * 6f - 15f) + 10f);
        bool visible = voice.fade > 0f;
        if (voice.rect.gameObject.activeSelf != visible) voice.rect.gameObject.SetActive(visible);
    }

    private static bool Here(TutorialLesson lesson)
    {
        if (lesson == null) return false;
        switch (lesson.Place)
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

    // A hint with a spot on the map floats just above its ring, as if spoken there; any other sits under the Age banner.
    private void PlaceHint(Voice voice)
    {
        var canvas = _canvasRect.rect;
        var size = voice.rect.sizeDelta;
        if (RingSpot(voice.speaker, out var screen) && RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvasRect, screen, null, out var local))
        {
            voice.rect.anchorMin = voice.rect.anchorMax = new Vector2(0.5f, 0.5f);
            voice.rect.pivot = new Vector2(0.5f, 0f);
            float halfWidth = canvas.width / 2f - size.x / 2f - 16f;
            float top = canvas.height / 2f - AgeBanner.ReservedHeight - size.y - 8f;
            voice.rect.anchoredPosition = new Vector2(Mathf.Clamp(local.x, -halfWidth, halfWidth), Mathf.Min(local.y + _ring.sizeDelta.y * 0.6f, top));
            return;
        }
        voice.rect.anchorMin = voice.rect.anchorMax = voice.rect.pivot = new Vector2(0.5f, 1f);
        voice.rect.anchoredPosition = new Vector2(0f, -(AgeBanner.ReservedHeight + 18f));
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
            if (lesson == null || lesson.IsHint || !Here(lesson) || !Speaks(lesson, out var card, out string key) || _retired.Contains(key)) continue;
            Choose(lesson, card, key);
            return;
        }
        if (Time.unscaledTime < _nextHintAt) return;
        foreach (var hint in Lessons)
        {
            if (hint == null || !hint.IsHint || Seen(hint.Id) || !Here(hint) || !Speaks(hint, out var card, out string key)) continue;
            Choose(hint, card, key);
            return;
        }
    }

    private static bool Speaks(TutorialLesson lesson, out TutorialCard card, out string key)
    {
        key = null;
        if (!lesson.Speak(out card) || string.IsNullOrEmpty(card.title)) return false;
        key = lesson.Id + "|" + card.title;
        return true;
    }

    private void Choose(TutorialLesson speaker, TutorialCard card, string key)
    {
        _speaker = speaker;
        _card = card;
        _key = key;
    }

    private string Words => _card.whisper + "\n" + _card.title + "\n" + _card.body;

    // The voice takes its new words: each line laid out under the one before, the rule under the title.
    private void Show(Voice voice)
    {
        voice.key = _key;
        voice.words = Words;
        voice.speaker = _speaker;
        voice.rect.gameObject.SetActive(true);
        voice.whisper.text = _card.whisper ?? string.Empty;
        voice.title.text = _card.title ?? string.Empty;
        voice.body.text = _card.body ?? string.Empty;
        // As wide as the whisper and the title need; the line under them wraps rather than stretch the voice past BodyWidth.
        float width = 0f;
        foreach (var line in new[] { voice.whisper, voice.title })
            if (line.text.Length > 0) width = Mathf.Max(width, line.GetPreferredValues(line.text).x + 8f);
        if (voice.body.text.Length > 0) width = Mathf.Max(width, Mathf.Min(BodyWidth, voice.body.GetPreferredValues(voice.body.text).x + 8f));
        width = Mathf.Min(voice.widest, width);

        float y = 0f;
        y = Place(voice.whisper, width, y, 4f);
        y = Place(voice.title, width, y, 2f);
        var rule = voice.rule.rectTransform;
        rule.sizeDelta = new Vector2(Mathf.Min(width * 0.6f, 380f), 12f);
        rule.anchoredPosition = new Vector2(0f, -y);
        y += 12f + 6f;
        y = Place(voice.body, width, y, 0f);
        voice.rect.sizeDelta = new Vector2(width, y);
        if (voice == _hintVoice) PlaceHint(voice);
        voice.speech.Say();
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
