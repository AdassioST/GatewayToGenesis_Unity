using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using DG.Tweening;

/// <summary>
/// The chorus decision token. Dragging it lights up the drop area under the pointer (through
/// <see cref="ChorusScreenManager"/>); releasing it over an available <see cref="ChorusChoiceBackground"/> makes
/// that choice, anywhere else (or over a UIBlock-layer element) sends it back. Once a choice is made it
/// cannot be dragged again.
/// </summary>
[RequireComponent(typeof(CanvasGroup))]
public class DecisionToken : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [Header("Token Settings")]
    [SerializeField] private float returnDuration = 0.5f;
    [SerializeField] private Ease returnEase = Ease.OutBack;

    [Header("Visual Feedback")]
    [SerializeField] private Image tokenImage;

    [Header("Alpha System")]
    [SerializeField] private float maxDistanceForAlpha = 200f; // Distance from centre where the drop area reaches full alpha

    [Header("Raycast Filtering")]
    [SerializeField] private LayerMask uiBlockMask; // Layers that block hover and drop (locked choices)

    private readonly List<RaycastResult> raycastResults = new List<RaycastResult>();
    private Vector3 startPosition;
    private Vector3 screenCenter;
    private RectTransform rectTransform;
    private bool isDragging;
    private ChorusScreenManager chorusManager;

    private void Start()
    {
        startPosition = transform.position;
        rectTransform = GetComponent<RectTransform>();
        chorusManager = GetComponentInParent<ChorusScreenManager>();
        var canvas = GetComponentInParent<Canvas>();
        if (canvas != null) screenCenter = canvas.transform.position;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (chorusManager != null && chorusManager.IsChoiceLocked()) return;
        isDragging = true;
        rectTransform.DOKill();
        transform.SetAsLastSibling();
        if (chorusManager != null) chorusManager.OnTokenDragStarted();
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!isDragging) return;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform.parent as RectTransform, eventData.position, eventData.pressEventCamera, out Vector2 local))
        {
            rectTransform.localPosition = local;
            UpdateHover(eventData);
        }
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (!isDragging) return;
        isDragging = false;
        var target = FindDropTarget(eventData);
        bool accepted = target != null && target.TryAccept(gameObject);
        // Unity may already have delivered the drop to the area itself; a made choice keeps the token where it is.
        if (!accepted && (chorusManager == null || !chorusManager.IsChoiceLocked())) ReturnToStartPosition();
        if (chorusManager != null) chorusManager.OnTokenDragEnded();
    }

    private void UpdateHover(PointerEventData eventData)
    {
        if (chorusManager == null) return;
        var target = FindDropTarget(eventData);
        if (target != null) chorusManager.UpdateBackgroundHover(target, Vector3.Distance(transform.position, screenCenter), maxDistanceForAlpha);
        else chorusManager.ResetAllBackgroundsToBase();
    }

    /// <summary>The drop area under the pointer, unless a UIBlock-layer element covers it first.</summary>
    private ChorusChoiceBackground FindDropTarget(PointerEventData eventData)
    {
        if (EventSystem.current == null) return null;
        raycastResults.Clear();
        EventSystem.current.RaycastAll(eventData, raycastResults);
        foreach (var result in raycastResults)
        {
            var hit = result.gameObject;
            if (hit == null || hit.transform.IsChildOf(transform)) continue;
            if (((1 << hit.layer) & uiBlockMask) != 0) return null;
            if (hit.TryGetComponent(out ChorusChoiceBackground background)) return background;
        }
        return null;
    }

    private void ReturnToStartPosition()
    {
        rectTransform.DOKill();
        rectTransform.DOMove(startPosition, returnDuration).SetEase(returnEase).SetLink(gameObject);
    }

    /// <summary>Put the token back at once.</summary>
    public void ResetToStartPosition()
    {
        if (rectTransform != null) rectTransform.DOKill();
        transform.position = startPosition;
    }

    public void SetTokenImage(Sprite sprite)
    {
        if (tokenImage != null) tokenImage.sprite = sprite;
    }

    public bool IsDragging() => isDragging;
}
