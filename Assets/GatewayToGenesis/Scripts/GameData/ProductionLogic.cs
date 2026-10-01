using UnityEngine;

/// <summary>
/// Accrues a resource slot's net production once per second. Paused while an event is active or the player
/// paused (<see cref="TimeSystemLogic.SimulationHeld"/>), matching the rest of the simulation.
/// </summary>
public class ProductionLogic : MonoBehaviour
{
    [SerializeField] private float tickSeconds = 1f;

    private GameResourceSlot resourceSlot;

    private void Start()
    {
        resourceSlot = GetComponent<GameResourceSlot>();
        if (resourceSlot != null) InvokeRepeating(nameof(PassiveProduction), 0.1f, tickSeconds);
    }

    private void PassiveProduction()
    {
        if (TimeSystemLogic.SimulationHeld) return;
        if (resourceSlot == null || resourceSlot.productionRate == 0f) return;
        float oldAmount = resourceSlot.amount;
        resourceSlot.ChangeAmount(resourceSlot.productionRate * tickSeconds);
        if (PopGrowthLogic.Instance != null && resourceSlot.gameUnit != null && resourceSlot.gameUnit.role == ResourceRole.Food)
        {
            PopGrowthLogic.Instance.HandleExternalFoodChange(oldAmount, resourceSlot.amount);
        }
    }
}
