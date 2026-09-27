using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

/// <summary>
/// Shows event screens. Every screen is built the same way: instantiate the screen prefab, fill its named
/// children (Title, Content, Button, ButtonText, ...) from the knot in <see cref="EventStoryIndex"/>, fade it in
/// and tint the vignette. Single-button screens (splash, verse, bridge, outro) queue the consequences written
/// on their button and continue to the knot it leads to; the chorus hands over to <see cref="ChorusScreenManager"/>.
/// Every button acts once per screen, however often it is clicked.
/// </summary>
public class EventScreenManager : MonoBehaviour
{
    private const LogChannel Log = LogChannel.EventScreens;

    [Header("Screen Prefabs")]
    [SerializeField] private GameObject splashScreenPrefab;
    [SerializeField] private GameObject verseScreenPrefab;
    [SerializeField] private GameObject chorusScreenPrefab;
    [SerializeField] private GameObject bridgeScreenPrefab;
    [SerializeField] private GameObject outroScreenPrefab;

    [Header("UI References")]
    [SerializeField] private Canvas eventCanvas;
    [SerializeField] private Transform screenContainer;
    [SerializeField] private Image vignette; // Previously named "fade" component

    [Header("Type Text Colors By Event Type")]
    [SerializeField] private Color environmentalTextColor = new Color(0.7f, 1f, 0.7f);
    [SerializeField] private Color mysticalTextColor = new Color(0.95f, 0.75f, 0.95f);
    [SerializeField] private Color socialTextColor = new Color(0.75f, 0.8f, 1f);
    [SerializeField] private Color crisisTextColor = new Color(1f, 0.75f, 0.75f);

    [Header("Animation Settings")]
    [SerializeField] private float screenFadeInDuration = 0.3f;
    [SerializeField] private float screenFadeOutDuration = 0.15f;
    [SerializeField] private Ease screenFadeInEase = Ease.OutQuad;

    [Header("Vignette Transparency Settings")]
    [SerializeField] private float vignetteTransitionDuration = 0.3f;
    [SerializeField] private Ease vignetteTransitionEase = Ease.OutQuad;

    [Tooltip("Seconds a bridge ignores input, so the click that opened it does not skip it.")]
    [SerializeField] private float bridgeInputDelay = 0.25f;

    private static EventSystemLogic Events => EventSystemLogic.Instance;
    private static EventVolumeManager Volumes => EventVolumeManager.Instance;

    private GameObject currentScreen;
    private EventScreen currentEventScreen;
    private EventStoryIndex.Knot currentKnot;
    private bool isScreenComplete;
    private ProgressiveSentenceRevealLogic progressiveRevealLogic;

    private readonly Dictionary<GameObject, Tween> screenFadeTweens = new Dictionary<GameObject, Tween>();
    private Tween currentVignetteTween;

    private void OnDestroy()
    {
        KillAllScreenTweens();
        if (currentVignetteTween != null && currentVignetteTween.IsActive()) currentVignetteTween.Kill();
    }

    // ===== SHOWING SCREENS =====

    public void ShowScreen(EventScreen screen)
    {
        if (screen == null || screenContainer == null) return;

        var prefab = PrefabFor(screen.screenType);
        if (prefab == null)
        {
            GameLog.Error($"No prefab assigned for {screen.screenType} screens.", Log);
            return;
        }

        DetachReveal();
        ClearScreensExceptFade();
        currentEventScreen = screen;
        currentKnot = EventStoryIndex.Get(screen.inkKnot);
        isScreenComplete = false;
        currentScreen = Instantiate(prefab, screenContainer);

        switch (screen.screenType)
        {
            case ScreenType.Splash: SetupSplash(screen); break;
            case ScreenType.Verse: SetupVerse(screen); break;
            case ScreenType.Chorus: SetupChorus(screen); break;
            case ScreenType.Bridge: SetupBridge(screen); break;
            case ScreenType.Outro: SetupOutro(screen); break;
        }

        AnimateVignetteForScreenType(screen.screenType);
        FadeScreenIn(currentScreen);
    }

    private GameObject PrefabFor(ScreenType type)
    {
        switch (type)
        {
            case ScreenType.Splash: return splashScreenPrefab;
            case ScreenType.Verse: return verseScreenPrefab;
            case ScreenType.Chorus: return chorusScreenPrefab;
            case ScreenType.Bridge: return bridgeScreenPrefab;
            default: return outroScreenPrefab;
        }
    }

    private void SetupSplash(EventScreen screen)
    {
        var story = CurrentStory;
        var metadata = CurrentMetadata;

        SetText("Title", StoryTitle(screen, "Story Begin"));
        SetText("Subtitle", screen.description ?? string.Empty);
        SetText("Name", StoryTitle(screen, string.Empty));
        string splashText = KnotText(screen, story != null ? story.storyDescription : null, string.Empty);
        SetText("Content", $"\"{splashText}\"");

        var background = FindChild<Image>("Background");
        if (background != null && screen.splashImage != null) background.sprite = screen.splashImage;

        var image = FindChild<Image>("Image");
        if (image != null)
        {
            Sprite art = null;
            if (story != null) art = FindArt(story.nodeName);
            if (art == null && metadata.TryGetValue("splash_art", out string splashArt)) art = FindNamedArt(splashArt);
            if (art == null) art = screen.splashImage;
            if (art != null) image.sprite = art;
        }

        var eventColor = FindChild<Image>("EventColor");
        if (eventColor != null)
        {
            var typeSprite = FindArt($"EventColor_{EventTypeName(metadata, screen)}");
            if (typeSprite != null)
            {
                eventColor.sprite = typeSprite;
                eventColor.color = Color.white;
            }
            else
            {
                eventColor.color = metadata.TryGetValue("event_color", out string color) ? ParseColorFromMetadata(color) : GetEventColor(screen.eventType);
            }
        }

        var typeText = FindChild<TextMeshProUGUI>("Type");
        if (typeText != null)
        {
            typeText.text = EventTypeName(metadata, screen);
            typeText.color = GetHardTextColorByEventType(EventTypeOf(metadata, screen.eventType));
        }

        string label = FirstChoiceLabel();
        if (string.IsNullOrEmpty(label)) label = metadata.TryGetValue("button_text", out string buttonText) ? buttonText : (screen.buttonText ?? "Continue");
        SetText("ButtonText", label);
        BindButton(ContinueFromButton);

        // Hovering the art shows the story's description.
        var tooltipTarget = image != null ? image.gameObject : (background != null ? background.gameObject : null);
        if (tooltipTarget != null)
        {
            string description = story != null && !string.IsNullOrEmpty(story.storyDescription) ? story.storyDescription : screen.description;
            SetCustomTooltip(tooltipTarget, description, null);
        }
    }

    private void SetupVerse(EventScreen screen)
    {
        SetText("Title", StoryTitle(screen, "Verse"));
        string content = KnotText(screen, null, "Verse content");
        SetText("Content", content);

        var buttonLabel = FindChild<TextMeshProUGUI>("ButtonText");
        var button = FindChild<Transform>("Button");
        if (buttonLabel == null && button != null) buttonLabel = button.GetComponentInChildren<TextMeshProUGUI>(true);
        if (buttonLabel != null)
        {
            string label = FirstChoiceLabel();
            buttonLabel.text = !string.IsNullOrEmpty(label) ? label : (screen.buttonText ?? "Continue");
        }

        // The button previews what continuing will do.
        if (button != null)
        {
            string preview = TooltipText.Consequences(ButtonConsequences());
            if (!string.IsNullOrEmpty(preview))
            {
                string label = FirstChoiceLabel();
                TooltipTrigger.Ensure(button.gameObject).SetCustom(!string.IsNullOrEmpty(label) ? label : "Continue", null, "What Follows", preview);
            }
            else RemoveTooltip(button.gameObject);
        }

        // The reveal owns the button: it appears once all text is shown and reports the press.
        progressiveRevealLogic = currentScreen.GetComponent<ProgressiveSentenceRevealLogic>();
        if (progressiveRevealLogic == null) progressiveRevealLogic = currentScreen.AddComponent<ProgressiveSentenceRevealLogic>();
        progressiveRevealLogic.OnContinuePressed += ContinueFromButton;
        progressiveRevealLogic.StartReveal(content);
    }

    private void SetupChorus(EventScreen screen)
    {
        var chorus = currentScreen.GetComponent<ChorusScreenManager>();
        if (chorus != null)
        {
            StartCoroutine(InitializeChorusNextFrame(chorus, screen, currentScreen));
            return;
        }

        // A chorus prefab without ChorusScreenManager: show its text and move on.
        GameLog.Warning("The chorus prefab has no ChorusScreenManager; continuing automatically.", Log);
        SetText("Content", KnotText(screen, null, "Make a choice"));
        SetText("Title", StoryTitle(screen, "Decision Point"));
        StartCoroutine(ContinueAfter(3f, currentScreen));
    }

    // ChorusScreenManager.Start must run first (it places the token and pillar display).
    private IEnumerator InitializeChorusNextFrame(ChorusScreenManager chorus, EventScreen screen, GameObject owner)
    {
        yield return null;
        if (owner == null || owner != currentScreen) yield break;
        chorus.InitializeChorusScreen(screen);
    }

    private IEnumerator ContinueAfter(float seconds, GameObject owner)
    {
        yield return new WaitForSeconds(seconds);
        if (owner != null && owner == currentScreen) ContinueFromButton();
    }

    private void SetupBridge(EventScreen screen)
    {
        SetText("Content", KnotText(screen, null, "Bridge content"));
        SetText("Title", StoryTitle(screen, "Bridge"));
        StartCoroutine(WaitForAnyInputThenAdvance(currentScreen));
    }

    private IEnumerator WaitForAnyInputThenAdvance(GameObject owner)
    {
        float readyAt = Time.unscaledTime + bridgeInputDelay;
        yield return null;
        while (owner != null && owner == currentScreen)
        {
            if (Time.unscaledTime >= readyAt && InputUtils.AnyKeyOrClickDown)
            {
                ContinueFromButton();
                yield break;
            }
            yield return null;
        }
    }

    private void SetupOutro(EventScreen screen)
    {
        SetText("Title", StoryTitle(screen, "Complete"));
        SetText("Content", KnotText(screen, null, "Story complete."));
        SetText("HasHappened", "Has Happened...");

        // The outro summarises everything the story will apply, with running totals.
        var pending = Events != null ? Events.GetCumulativeConsequences() : new List<EventConsequence>();
        pending.AddRange(ButtonConsequences());
        SetText("Consequences", EventText.DescribeConsequences(pending));

        string label = FirstChoiceLabel();
        SetText("ButtonText", !string.IsNullOrEmpty(label) ? label : (screen.buttonText ?? "Continue"));
        BindButton(OnOutroButtonPressed);
    }

    // ===== BUTTONS =====

    /// <summary>Continue from a single-button screen: queue its button's consequences and go where it leads.</summary>
    private void ContinueFromButton()
    {
        if (isScreenComplete) return;
        isScreenComplete = true;
        DetachReveal();

        QueueConsequences(ButtonConsequences());
        string target = currentKnot != null ? currentKnot.ContinueTarget : null;
        GameLog.Event($"Continue from '{currentEventScreen?.inkKnot}' → '{target ?? "(end)"}'", Log);
        if (Volumes == null) return;
        if (string.IsNullOrEmpty(target)) Volumes.CompleteStory();
        else Volumes.NavigateToKnot(target);
    }

    private void OnOutroButtonPressed()
    {
        if (isScreenComplete) return;
        isScreenComplete = true;

        var button = FindChild<Button>("Button");
        if (button != null) button.interactable = false;
        QueueConsequences(ButtonConsequences());

        if (Volumes != null) Volumes.CompleteStory();
        else if (Events != null) Events.OnStoryCompleted();
        else GameLog.Error("Neither EventVolumeManager nor EventSystemLogic is available to complete the story.", Log);
    }

    private void BindButton(UnityEngine.Events.UnityAction action)
    {
        var button = FindChild<Button>("Button");
        if (button == null)
        {
            GameLog.Warning($"{currentScreen.name} has no 'Button' child.", Log);
            return;
        }
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(action);
    }

    private void QueueConsequences(List<EventConsequence> consequences)
    {
        if (Events == null || consequences == null) return;
        foreach (var consequence in consequences) Events.AddConsequence(consequence);
    }

    private List<EventConsequence> ButtonConsequences()
    {
        var first = currentKnot != null ? currentKnot.FirstChoice : null;
        return first != null ? first.consequences : new List<EventConsequence>();
    }

    private string FirstChoiceLabel()
    {
        var first = currentKnot != null ? currentKnot.FirstChoice : null;
        return first != null ? first.label : null;
    }

    private void DetachReveal()
    {
        if (progressiveRevealLogic == null) return;
        progressiveRevealLogic.OnContinuePressed -= ContinueFromButton;
        progressiveRevealLogic = null;
    }

    // ===== CONTENT HELPERS =====

    private StoryNode CurrentStory => Volumes != null ? Volumes.GetCurrentStoryNode() : null;

    private Dictionary<string, string> CurrentMetadata
    {
        get
        {
            var story = CurrentStory;
            return story != null && story.uiMetadata != null ? story.uiMetadata : new Dictionary<string, string>();
        }
    }

    private string StoryTitle(EventScreen screen, string fallback)
    {
        var story = CurrentStory;
        if (story != null && !string.IsNullOrEmpty(story.storyTitle)) return story.storyTitle;
        return screen.title ?? fallback;
    }

    private string KnotText(EventScreen screen, string secondFallback, string fallback)
    {
        if (currentKnot != null && currentKnot.Text.Length > 0) return currentKnot.Text;
        if (!string.IsNullOrEmpty(screen.description)) return screen.description;
        return !string.IsNullOrEmpty(secondFallback) ? secondFallback : fallback;
    }

    private T FindChild<T>(string childName) where T : Component
    {
        if (currentScreen == null) return null;
        var child = currentScreen.transform.Find(childName);
        return child != null ? child.GetComponent<T>() : null;
    }

    private void SetText(string childName, string text)
    {
        var label = FindChild<TextMeshProUGUI>(childName);
        if (label != null) label.text = text ?? string.Empty;
    }

    private static void SetCustomTooltip(GameObject target, string title, string description, string breakdown = null) =>
        TooltipTrigger.Ensure(target).SetCustom(title, description, null, breakdown);

    private static void RemoveTooltip(GameObject target)
    {
        var trigger = target.GetComponent<TooltipTrigger>();
        if (trigger != null) Destroy(trigger);
    }

    private static string EventTypeName(Dictionary<string, string> metadata, EventScreen screen) =>
        metadata.TryGetValue("event_type", out string type) && !string.IsNullOrEmpty(type) ? type.Trim() : screen.eventType.ToString();

    private static EventType EventTypeOf(Dictionary<string, string> metadata, EventType fallback) =>
        metadata.TryGetValue("event_type", out string type) && System.Enum.TryParse(type.Trim(), true, out EventType parsed) ? parsed : fallback;

    private Color GetHardTextColorByEventType(EventType type)
    {
        switch (type)
        {
            case EventType.Environmental: return environmentalTextColor;
            case EventType.Mystical: return mysticalTextColor;
            case EventType.Social: return socialTextColor;
            case EventType.Crisis: return crisisTextColor;
            default: return Color.white;
        }
    }

    private static Color GetEventColor(EventType eventType)
    {
        switch (eventType)
        {
            case EventType.Environmental: return new Color(0.2f, 0.8f, 0.2f);
            case EventType.Mystical: return new Color(0.8f, 0.2f, 0.8f);
            case EventType.Social: return new Color(0.2f, 0.2f, 0.8f);
            case EventType.Crisis: return new Color(0.8f, 0.2f, 0.2f);
            default: return Color.white;
        }
    }

    // Event art lives in Resources/EventArt (GameCatalog.EventArt), looked up by sprite name.
    private static Sprite FindArt(string spriteName) =>
        !string.IsNullOrEmpty(spriteName) && GameCatalog.EventArt.TryGet(spriteName, out var sprite) ? sprite : null;

    // A story's "splash_art" tag names the sprite; a folder prefix ("EventArt/…") is tolerated.
    private static Sprite FindNamedArt(string spriteName)
    {
        if (string.IsNullOrEmpty(spriteName)) return null;
        var sprite = FindArt(spriteName.Substring(spriteName.LastIndexOf('/') + 1).Trim());
        if (sprite == null) GameLog.Warning($"splash_art '{spriteName}' is not a sprite in Resources/{GameCatalog.EventArt.ResourcesPath}.", Log);
        return sprite;
    }

    private static Color ParseColorFromMetadata(string colorString)
    {
        switch ((colorString ?? string.Empty).Trim().ToLowerInvariant())
        {
            case "red": return Color.red;
            case "green": return Color.green;
            case "blue": return Color.blue;
            case "yellow": return Color.yellow;
            case "purple": return new Color(0.8f, 0.2f, 0.8f);
            case "orange": return new Color(1f, 0.5f, 0f);
            case "cyan": return Color.cyan;
            case "magenta": return Color.magenta;
            case "black": return Color.black;
            case "gray":
            case "grey": return Color.gray;
            default:
                return ColorUtility.TryParseHtmlString(colorString, out Color color) ? color : Color.white;
        }
    }

    // ===== TRANSITIONS =====

    private void ClearScreensExceptFade()
    {
        if (screenContainer == null) return;
        foreach (Transform child in screenContainer)
        {
            if (child == null || (vignette != null && child == vignette.transform)) continue;
            FadeScreenOut(child.gameObject);
        }
        currentScreen = null;
    }

    private void FadeScreenOut(GameObject screen)
    {
        var canvasGroup = GetOrAddCanvasGroup(screen);
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;
        KillFade(screen);
        screenFadeTweens[screen] = canvasGroup.DOFade(0f, screenFadeOutDuration)
            .SetEase(screenFadeInEase)
            .SetLink(screen)
            .OnComplete(() =>
            {
                screenFadeTweens.Remove(screen);
                if (screen != null) Destroy(screen);
            });
    }

    private void FadeScreenIn(GameObject screen)
    {
        if (screen == null) return;
        var canvasGroup = GetOrAddCanvasGroup(screen);
        KillFade(screen);
        canvasGroup.alpha = 0f;
        screenFadeTweens[screen] = canvasGroup.DOFade(1f, screenFadeInDuration)
            .SetEase(screenFadeInEase)
            .SetLink(screen)
            .OnComplete(() => screenFadeTweens.Remove(screen));
    }

    private void KillFade(GameObject screen)
    {
        if (screenFadeTweens.TryGetValue(screen, out var tween) && tween != null && tween.IsActive()) tween.Kill();
        screenFadeTweens.Remove(screen);
    }

    private void KillAllScreenTweens()
    {
        foreach (var tween in screenFadeTweens.Values)
        {
            if (tween != null && tween.IsActive()) tween.Kill();
        }
        screenFadeTweens.Clear();
    }

    private static CanvasGroup GetOrAddCanvasGroup(GameObject target)
    {
        var canvasGroup = target.GetComponent<CanvasGroup>();
        return canvasGroup != null ? canvasGroup : target.AddComponent<CanvasGroup>();
    }

    private void AnimateVignetteForScreenType(ScreenType screenType)
    {
        if (vignette == null) return;
        if (currentVignetteTween != null && currentVignetteTween.IsActive()) currentVignetteTween.Kill();
        currentVignetteTween = vignette.DOFade(GetVignetteAlphaForScreenType(screenType), vignetteTransitionDuration)
            .SetEase(vignetteTransitionEase)
            .SetLink(vignette.gameObject);
    }

    private static float GetVignetteAlphaForScreenType(ScreenType screenType)
    {
        switch (screenType)
        {
            case ScreenType.Chorus: return 0.95f;
            case ScreenType.Bridge: return 0.80f;
            default: return 0.45f;
        }
    }

    // ===== PUBLIC CONTROL =====

    public bool IsScreenComplete(EventScreen screen) => isScreenComplete;

    /// <summary>The screen being shown, or null (diagnostics).</summary>
    public EventScreen CurrentEventScreen => currentScreen != null ? currentEventScreen : null;

    /// <summary>Remove the current screen at once (the story is over).</summary>
    public void HideCurrentScreen()
    {
        DetachReveal();
        if (currentScreen != null)
        {
            KillFade(currentScreen);
            DOTween.Kill(currentScreen);
            Destroy(currentScreen);
            currentScreen = null;
        }
        isScreenComplete = false;
        currentEventScreen = null;
        currentKnot = null;
    }

    /// <summary>Destroy every screen at once (the vignette stays).</summary>
    public void FadeOutAllScreensImmediate()
    {
        if (screenContainer == null) return;
        DetachReveal();
        KillAllScreenTweens();
        foreach (Transform child in screenContainer)
        {
            if (child == null || (vignette != null && child == vignette.transform)) continue;
            Destroy(child.gameObject);
        }
        currentScreen = null;
    }

    /// <summary>Destroy the current screen at once.</summary>
    public void FadeOutCurrentScreenImmediate()
    {
        if (currentScreen == null) return;
        DetachReveal();
        KillFade(currentScreen);
        Destroy(currentScreen);
        currentScreen = null;
    }
}
