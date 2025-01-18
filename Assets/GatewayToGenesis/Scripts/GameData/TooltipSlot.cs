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

    private void Update()
    {
        ResizeTooltip();

        Vector2 position = Input.mousePosition;

        transform.position = position + offset;
    }

    public void ResizeTooltip()
    {
        if (title.text != null && description.text != null)
        {
            int headerLength = title.text.Length;
            int contentLength = description.text.Length;

            layoutElement.enabled = Mathf.Max(title.preferredWidth, description.preferredWidth) >= layoutElement.preferredWidth;
        }

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
        // Update only the dynamic sections
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