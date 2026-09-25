using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using DG.Tweening;
using TMPro;

/// <summary>
/// Drop area for one chorus choice. Dropping the <see cref="DecisionToken"/> here selects the matching
/// <see cref="ChorusChoice"/>. A choice whose requirements or costs are not met (<see cref="ChorusRules.IsAvailable"/>)
/// sits dimmed with its card's lock overlay shown, and both move to the UIBlock layer so the token cannot land on
/// them. While the token is dragged, <see cref="ChorusScreenManager"/> drives the alpha of the available areas.
/// </summary>
public class ChorusChoiceBackground : MonoBehaviour, IDropHandler
{
    [Header("Choice Configuration")]
    [SerializeField] private string choiceId; // "idealism", "realism", "pragmatism"

    [Header("Visual Settings")]
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private Image backgroundImage; // Receives the drop raycast
    [SerializeField] private float baseAlpha = 0.35f;
    [SerializeField] private float maxAlpha = 1.0f;

    [Header("References")]
    [SerializeField] private ChorusScreenManager chorusManager;
    [SerializeField] private TMP_Text hoverDescriptionText; // Optional: shows the quoted choice title

    private const LogChannel Log = LogChannel.EventScreens;
    private const float LockedAlpha = 0.15f;

    private ChorusChoiceData choiceData;
    private Tween alphaTween;
    private float originalBaseAlpha;
    private int originalBackgroundLayer;
    private int originalChoiceLayer = -1;
    private bool isAvailable = true;
    private bool initialized;

    private void Awake()
    {
        originalBaseAlpha = baseAlpha;
        originalBackgroundLayer = gameObject.layer;
        if (chorusManager == null) chorusManager = GetComponentInParent<ChorusScreenManager>();
    }

    private void Start()
    {
        if (canvasGroup == null || chorusManager == null || backgroundImage == null)
        {
            GameLog.Error($"{name}: the chorus drop area needs its CanvasGroup, ChorusScreenManager and background Image assigned.", Log);
            enabled = false;
            return;
        }
        // The drop raycast must hit this object.
        backgroundImage.raycastTarget = true;
        var localImage = GetComponent<Image>();
        if (localImage == null)
        {
            localImage = gameObject.AddComponent<Image>();
            localImage.color = new Color(1f, 1f, 1f, 0.1f);
        }
        localImage.raycastTarget = true;

        if (!initialized) canvasGroup.alpha = baseAlpha;
    }

    /// <summary>Show <paramref name="data"/> (called by ChorusScreenManager when the chorus opens).</summary>
    public void InitializeChoice(string id, ChorusChoiceData data)
    {
        choiceId = id;
        choiceData = data;
        initialized = true;
        if (hoverDescriptionText != null) hoverDescriptionText.text = data != null && !string.IsNullOrEmpty(data.title) ? $"\"{data.title}\"" : string.Empty;
        isAvailable = ChorusRules.IsAvailable(choiceData);
        ApplyAvailabilityVisuals();
    }

    /// <summary>Re-check requirements and costs; the visuals change only when availability does.</summary>
    public void RefreshAvailability()
    {
        if (choiceData == null) return;
        bool available = ChorusRules.IsAvailable(choiceData);
        if (available == isAvailable) return;
        isAvailable = available;
        ApplyAvailabilityVisuals();
    }

    private void ApplyAvailabilityVisuals()
    {
        if (!isAvailable) baseAlpha = originalBaseAlpha;
        KillTween();
        if (canvasGroup != null) canvasGroup.alpha = isAvailable ? baseAlpha : LockedAlpha;

        var choice = FindChoiceView();
        if (choice == null) return;
        choice.SetLockedOverlay(!isAvailable);

        int uiBlock = LayerMask.NameToLayer("UIBlock");
        if (uiBlock < 0) return;
        if (originalChoiceLayer < 0) originalChoiceLayer = choice.gameObject.layer != uiBlock ? choice.gameObject.layer : LayerMask.NameToLayer("UI");
        gameObject.layer = isAvailable ? originalBackgroundLayer : uiBlock;
        choice.gameObject.layer = isAvailable ? originalChoiceLayer : uiBlock;
    }

    // ===== DROP =====

    public void OnDrop(PointerEventData eventData)
    {
        var token = eventData.pointerDrag;
        if (token != null && token.GetComponent<DecisionToken>() != null) TryAccept(token);
    }

    /// <summary>Make this choice with the dropped token. True when the chorus accepted it; the token then fades out.</summary>
    public bool TryAccept(GameObject token)
    {
        if (!enabled || choiceData == null || chorusManager == null || chorusManager.IsChoiceLocked()) return false;
        RefreshAvailability();
        if (!isAvailable) return false;
        var choice = FindChoiceView();
        if (choice == null)
        {
            GameLog.Warning($"No ChorusChoice card with id '{choiceId}' under the active choices container.", Log);
            return false;
        }
        if (!choice.SelectChoice()) return false;
        FadeOutToken(token);
        return true;
    }

    private ChorusChoice FindChoiceView()
    {
        var container = chorusManager != null ? chorusManager.GetChoicesContainer() : null;
        if (container == null) return null;
        foreach (var choice in container.GetComponentsInChildren<ChorusChoice>(true))
        {
            if (choice != null && string.Equals(choice.ChoiceId, choiceId, System.StringComparison.OrdinalIgnoreCase)) return choice;
        }
        return null;
    }

    private static void FadeOutToken(GameObject token)
    {
        if (token == null) return;
        var group = token.GetComponent<CanvasGroup>();
        if (group == null)
        {
            token.SetActive(false);
            return;
        }
        group.DOFade(0f, 0.5f).SetEase(Ease.OutQuad).SetLink(token).OnComplete(() =>
        {
            if (token != null) token.SetActive(false);
        });
    }

    // ===== ALPHA (driven by ChorusScreenManager while dragging) =====

    /// <summary>Brighten towards <see cref="maxAlpha"/> as the token moves away from the centre.</summary>
    public void UpdateAlphaByTokenDistance(float distanceFromCenter, float maxDistance)
    {
        if (canvasGroup == null || !isAvailable) return;
        float t = maxDistance > 0f ? Mathf.Clamp01(distanceFromCenter / maxDistance) : 1f;
        FadeTo(Mathf.Lerp(baseAlpha, maxAlpha, t), 0.3f);
    }

    public void ResetToBaseAlpha()
    {
        if (canvasGroup == null || !isAvailable) return;
        FadeTo(baseAlpha, 0.5f);
    }

    /// <summary>Use a different resting alpha while the token is dragged.</summary>
    public void ApplyDragBaseAlpha(float dragBaseAlpha)
    {
        if (!isAvailable) return;
        baseAlpha = dragBaseAlpha;
        ResetToBaseAlpha();
    }

    public void RestoreOriginalBaseAlpha()
    {
        if (!isAvailable) return;
        baseAlpha = originalBaseAlpha;
        ResetToBaseAlpha();
    }

    private void FadeTo(float alpha, float seconds)
    {
        KillTween();
        alphaTween = canvasGroup.DOFade(alpha, seconds).SetEase(Ease.OutQuad).SetLink(gameObject);
    }

    private void KillTween()
    {
        if (alphaTween != null && alphaTween.IsActive()) alphaTween.Kill();
        alphaTween = null;
    }

    public string GetChoiceId() => choiceId;

    public bool IsAvailable() => isAvailable;

    public ChorusChoiceData GetChoiceData() => choiceData;

    private void OnDestroy() => KillTween();
}
