using UnityEngine;
using System.Collections;
using System;

public class TooltipSystemLogic : MonoBehaviour
{
    public static TooltipSystemLogic Instance { get; private set; }

    [SerializeField] private TooltipSlot tooltipSlotPrefab;

    public TooltipSlot currentTooltipSlot;
    public TooltipData currentTooltipData;

    private Coroutine tooltipCoroutine;
    
    public bool isTooltipActive = false;

    public static event Action<TooltipData> OnTooltipShown;
    public static event Action<TooltipData> OnTooltipHidden;

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

        currentTooltipData = data; // Store the active tooltip data

        isTooltipActive = true;

        OnTooltipShown?.Invoke(data);

        tooltipCoroutine = StartCoroutine(TooltipTimer());
    }

    public void HideTooltip()
    {
        if (currentTooltipSlot != null)
        {
            Destroy(currentTooltipSlot.gameObject);
        }

        // Prevent null reference by ensuring we only invoke when necessary
        if (currentTooltipData != null)
        {
            OnTooltipHidden?.Invoke(currentTooltipData);
        }

        isTooltipActive = false;
        currentTooltipData = null;
    }


    private void LockTooltip()
    {
        if (currentTooltipSlot != null)
        {
            currentTooltipSlot.Lock();
        }
    }


    private IEnumerator TooltipTimer()
    {
        yield return new WaitForSeconds(2f);  // Wait for 2 seconds before locking the tooltip
        LockTooltip();
    }

    public void RefreshTooltip(TooltipData data)
    {
        if (currentTooltipSlot != null && isTooltipActive)
        {
            currentTooltipSlot.UpdateTooltipData(data);
        }
    }

    public void RefreshAllTooltips()
    {
        if (isTooltipActive && currentTooltipSlot != null)
        {
            // Refresh the tooltip if it's active

            currentTooltipSlot.UpdateTooltipData(currentTooltipData);
        }
    }

}
