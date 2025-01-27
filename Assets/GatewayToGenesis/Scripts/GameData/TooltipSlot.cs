using JetBrains.Annotations;
using System.Collections.Generic;
using TMPro;
using Unity.Loading;
using UnityEngine;
using UnityEngine.UI;

public class TooltipSlot : MonoBehaviour
{
    public float timer { get; private set; }

    public Vector2 offset;

    [SerializeField] private LayoutElement layoutElement;

    [SerializeField] private GameObject titleSection, descriptionSection, typeSection, productionModifiersSection, storageBreakdownSection, resourceRequirementsSection, effectsSection, techRequirementsSection;

    public TextMeshProUGUI title, description, productionModifiers, type, storageBreakdown, resourceRequirements, effects, techRequirements;

    private List<TextMeshProUGUI> textElements;

    public RectTransform rectTransform;

    private void Start()
    {

        rectTransform = GetComponent<RectTransform>();
        textElements = new List<TextMeshProUGUI> { title, description, productionModifiers, type, storageBreakdown, resourceRequirements, effects, techRequirements };
    }
    private void Update()
    {
        ResizeTooltip();
        AdjustTooltipPosition();
    }
    private void AdjustTooltipPosition()
    {
        Vector2 mousePosition = Input.mousePosition;
        Vector2 tooltipSize = rectTransform.sizeDelta * rectTransform.lossyScale;

        Vector2 screenSize = new Vector2(Screen.width, Screen.height);

        Vector2 pivot = new Vector2(0f, 1f);
        Vector2 offset = new Vector2(20f, -5f);

        if (mousePosition.x + tooltipSize.x > screenSize.x) // Too far right
        {
            pivot.x = 1f; // Move to the left
            offset.x = -10f;
        }

        if (mousePosition.y - tooltipSize.y < 0) // Too far down
        {
            pivot.y = 0f; // Move up
            offset.y = 5f;
        }

        if (mousePosition.x - tooltipSize.x < 0) // Too far left
        {
            pivot.x = 0f; // Back to right
            offset.x = 20f;
        }

        if (mousePosition.y + tooltipSize.y > screenSize.y) // Too far up
        {
            pivot.y = 1f; // Back to down
            offset.y = -5f;
        }

        rectTransform.pivot = pivot;
        transform.position = mousePosition + offset;
    }
    public void ResizeTooltip()
    {
        bool shouldEnableLayout = false;

        foreach (TextMeshProUGUI textElement in textElements)
        {
            if (textElement != null && textElement.text != null)
            {
                if (textElement.preferredWidth >= layoutElement.preferredWidth)
                {
                    shouldEnableLayout = true;

                    break;
                }
            }
        }

        layoutElement.enabled = shouldEnableLayout;
    }

    public void InitializeTooltipData(TooltipData data)
    {
        title.text = data.tooltipTitle;
        description.text = data.tooltipDescription;
        type.text = data.type;

        resourceRequirements.text = data.resourceRequirements;
        productionModifiers.text = data.productionModifiers;

        storageBreakdown.text = data.storageBreakdown;

        effects.text = data.productionEffects;
        techRequirements.text = data.techRequirements;

        //ADAPT LOGIC TO CHANGE ON SECTIONS
        if(data.type != null)
        {
            if (SectionData.sectionDataDictionary != null && SectionData.sectionDataDictionary.ContainsKey(data.type))
            {

                title.alignment = TextAlignmentOptions.Center;
                layoutElement.preferredWidth = 650f;

                type.text = "";
            }
        }

        titleSection.SetActive(!string.IsNullOrEmpty(data.tooltipTitle));
        descriptionSection.SetActive(!string.IsNullOrEmpty(data.tooltipDescription));
        typeSection.SetActive(!string.IsNullOrEmpty(data.type));

        resourceRequirementsSection.SetActive(!string.IsNullOrEmpty(data.resourceRequirements));
        productionModifiersSection.SetActive(!string.IsNullOrEmpty(data.productionModifiers));

        storageBreakdownSection.SetActive(!string.IsNullOrEmpty(data.storageBreakdown));

        effectsSection.SetActive(!string.IsNullOrEmpty(data.productionEffects));
        techRequirementsSection.SetActive(!string.IsNullOrEmpty(data.techRequirements));

    }

    public void Lock()
    {
        // Lock the tooltip (e.g., allow hover over keywords inside the tooltip)
        // This can be customized as per your requirement

        Debug.Log("Tooltip locked");
    }

    public void UpdateTooltipData(TooltipData data)
    {
        if (!string.IsNullOrEmpty(data.productionModifiers))
        {
            productionModifiers.text = data.productionModifiers;
            productionModifiersSection.SetActive(true);
        }

        if (!string.IsNullOrEmpty(data.resourceRequirements))
        {
            resourceRequirements.text = data.resourceRequirements;
            resourceRequirementsSection.SetActive(true);
        }

        if (!string.IsNullOrEmpty(data.productionEffects))
        {
            effects.text = data.productionEffects;
            effectsSection.SetActive(true);
        }

        if (!string.IsNullOrEmpty(data.techRequirements))
        {
            techRequirements.text = data.techRequirements;
            techRequirementsSection.SetActive(true);
        }

    }


}