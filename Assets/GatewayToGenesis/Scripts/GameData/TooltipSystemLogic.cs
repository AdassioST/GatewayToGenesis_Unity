using UnityEngine;
using System.Collections;

public class TooltipSystemLogic : MonoBehaviour
{
    public static TooltipSystemLogic Instance { get; private set; }

    [SerializeField] private TooltipSlot tooltipSlotPrefab;
    private TooltipSlot currentTooltipSlot;
    private Coroutine tooltipCoroutine;
    private bool isTooltipActive = false;

    private void Awake()
    {
        Instance = this;
    }

    private void Update()
    {
        // Listen for hover over UI and trigger tooltips dynamically
        if (isTooltipActive && currentTooltipSlot != null)
        {
            // Check if tooltip timer should lock
            if (tooltipCoroutine == null && Time.time - tooltipSlotPrefab.timer > 2f)
            {
                LockTooltip();
            }
        }
    }

    public void ShowTooltip(TooltipData data)
    {
        if (currentTooltipSlot != null)
        {
            Destroy(currentTooltipSlot.gameObject);
        }

        currentTooltipSlot = Instantiate(tooltipSlotPrefab);
        currentTooltipSlot.InitializeTooltipData(data);
        currentTooltipSlot.transform.SetParent(transform);

        isTooltipActive = true;
        tooltipCoroutine = StartCoroutine(TooltipTimer());
    }

    private void LockTooltip()
    {
        if (currentTooltipSlot != null)
        {
            currentTooltipSlot.Lock();
        }
    }

    public void HideTooltip()
    {
        if (currentTooltipSlot != null)
        {
            Destroy(currentTooltipSlot.gameObject);
        }

        isTooltipActive = false;
    }

    private IEnumerator TooltipTimer()
    {
        yield return new WaitForSeconds(2f);  // Wait for 2 seconds before locking the tooltip
        LockTooltip();
    }
}
