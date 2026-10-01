using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>Visible focus and automatic scrolling for keyboard and controller UI navigation.</summary>
public sealed class UiFocus : MonoBehaviour, ISelectHandler, IDeselectHandler
{
    private UnityEngine.UI.Image[] edges;
    public static void Ensure(UnityEngine.UI.Selectable selectable)
    {
        if (selectable.GetComponent<UiFocus>() == null) selectable.gameObject.AddComponent<UiFocus>();
    }
    public void OnSelect(BaseEventData eventData)
    {
        if (edges == null)
        {
            edges = new UnityEngine.UI.Image[4];
            var rect = (RectTransform)transform;
            for (int i = 0; i < 4; i++) edges[i] = CodeUI.Solid(rect, "Focus edge", new Color32(255, 231, 159, 255));
            CodeUI.Place(edges[0].rectTransform, new Vector2(0, 1), Vector2.one, new Vector2(0, -3), Vector2.zero);
            CodeUI.Place(edges[1].rectTransform, Vector2.zero, new Vector2(1, 0), Vector2.zero, new Vector2(0, 3));
            CodeUI.Place(edges[2].rectTransform, Vector2.zero, new Vector2(0, 1), Vector2.zero, new Vector2(3, 0));
            CodeUI.Place(edges[3].rectTransform, new Vector2(1, 0), Vector2.one, new Vector2(-3, 0), Vector2.zero);
        }
        foreach (var edge in edges) edge.gameObject.SetActive(true);
        var scroll = GetComponentInParent<UnityEngine.UI.ScrollRect>();
        if (scroll == null || scroll.content == null || scroll.viewport == null) return;
        Canvas.ForceUpdateCanvases();
        var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(scroll.viewport, transform);
        var view = scroll.viewport.rect;
        float overflow = bounds.max.y > view.yMax ? view.yMax - bounds.max.y : bounds.min.y < view.yMin ? view.yMin - bounds.min.y : 0;
        if (Mathf.Abs(overflow) > .5f) { scroll.StopMovement(); scroll.content.anchoredPosition += new Vector2(0, overflow); }
    }
    public void OnDeselect(BaseEventData eventData) => Hide();
    private void OnDisable() => Hide();
    private void Hide() { if (edges != null) foreach (var edge in edges) if (edge != null) edge.gameObject.SetActive(false); }
}
