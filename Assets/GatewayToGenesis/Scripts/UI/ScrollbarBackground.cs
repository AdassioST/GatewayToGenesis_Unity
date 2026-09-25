using UnityEngine;
using UnityEngine.UI;

/// <summary>Drifts a tiled background against the scroll direction while a scroll view moves (a parallax hint).</summary>
public class ScrollbarBackground : MonoBehaviour
{
    [SerializeField] private RawImage background;
    [SerializeField] private ScrollRect scroll;

    [SerializeField] private float x = 0.01f, y;
    [SerializeField] private float soothing = 100f, soothingFactor = 3f;

    // The drift was tuned per frame at 60 fps; scaling by frame time keeps that speed at any frame rate.
    private const float TunedFrameRate = 60f;

    private void Update()
    {
        if (background == null || scroll == null || scroll.velocity.sqrMagnitude < 0.0001f) return;
        Vector2 drift = new Vector2(x, y) * scroll.velocity / Mathf.Pow(soothing, soothingFactor) * (Time.unscaledDeltaTime * TunedFrameRate);
        background.uvRect = new Rect(background.uvRect.position - drift, background.uvRect.size);
    }
}
