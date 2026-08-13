using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ScrollbarBackground : MonoBehaviour
{
    [SerializeField] private RawImage background;
    [SerializeField] private ScrollRect scroll;

    [SerializeField] private float x = 0.01f, y;
    [SerializeField] private float soothing = 100f, soothingFactor = 3f;

    void Update()
    {
        background.uvRect = new Rect(background.uvRect.position + new Vector2(x, y) * scroll.velocity / Mathf.Pow(soothing,soothingFactor) * -1, background.uvRect.size);
    }
}
