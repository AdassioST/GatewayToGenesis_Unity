using UnityEngine;
using UnityEngine.UI;

/// <summary>Buttons with this ignore clicks on (nearly) transparent pixels of their image. The sprite needs Read/Write enabled.</summary>
public class ButtonHitBox : MonoBehaviour
{
    [SerializeField, Range(0f, 1f)] private float alphaThreshold = 0.1f;

    private void Awake()
    {
        if (TryGetComponent(out Image image)) image.alphaHitTestMinimumThreshold = alphaThreshold;
    }
}
