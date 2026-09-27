using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Civic acquisition and removal: tier slots, requirements, conflicts, effects and removal penalties.
///
/// Civics are loaded from <see cref="GameCatalog.Civics"/>. A civic's effects are applied through
/// <see cref="EffectRouter"/> under the source "Civic: {name}", so removing it restores every system exactly.
/// Civics that grant a council position register their seat with <see cref="GovernmentLogic"/>.
/// </summary>
public class CivicManager : SingletonBehaviour<CivicManager>
{
    private const LogChannel Log = LogChannel.Civics;

    [Header("Civic Slot Configuration")]
    [SerializeField] private int startingAeonicSlots = 1;
    [SerializeField] private int startingMajorSlots = 1;
    [SerializeField] private int startingMinorSlots = 2;
    [SerializeField] private int maxAeonicSlots = 2;
    [SerializeField] private int maxMajorSlots = 4;
    [SerializeField] private int maxMinorSlots = 6;

    [Header("Debug")]
    [Tooltip("Ctrl+U/R/G/F unlock or remove test civics (editor and development builds only).")]
    [SerializeField] private bool enableDebugKeys = true;

    private readonly Dictionary<CivicTier, List<CivicData>> _activeByTier = new Dictionary<CivicTier, List<CivicData>>
    {
        { CivicTier.Aeonic, new List<CivicData>() },
        { CivicTier.Major, new List<CivicData>() },
        { CivicTier.Minor, new List<CivicData>() },
    };

    private readonly Dictionary<string, CivicRemovalPenalty> removalPenalties = new Dictionary<string, CivicRemovalPenalty>(StringComparer.OrdinalIgnoreCase);

    public event Action<CivicData, bool> OnCivicChanged;   // (civic, wasAdded)
    public event Action OnCivicSlotsChanged;
    /// <summary>Civic availability changed (unlock/removal); civic pool UI should rebuild.</summary>
    public event Action OnCivicPoolChanged;
    public event Action OnActiveCivicsChanged;

    /// <summary>Ongoing morale penalty after a civic is removed.</summary>
    [Serializable]
    public class CivicRemovalPenalty
    {
        public string civicName;
        public int moralePenaltyPerSeventh;
        public int remainingSevenths;
        public string source;

        public CivicRemovalPenalty(string civic, int morale, int duration, string source)
        {
            civicName = civic;
            moralePenaltyPerSeventh = morale;
            remainingSevenths = duration;
            this.source = source;
        }
    }

    public static string SourceName(CivicData civic) => $"Civic: {civic.civicName}";

    public void RefreshAfterLoad() { OnActiveCivicsChanged?.Invoke(); OnCivicPoolChanged?.Invoke(); OnCivicSlotsChanged?.Invoke(); }

    private void Start()
    {
        // Requirements are validated with the rest of the content by ContentValidator.
        TimeSystemLogic.WhenReady(this, time => time.OnSeventhChange += OnSeventhTick);
        GameLog.Event($"{GameCatalog.Civics.Count} civics available", Log);
    }

    protected override void OnSingletonDestroy()
    {
        if (TimeSystemLogic.Instance != null) TimeSystemLogic.Instance.OnSeventhChange -= OnSeventhTick;
    }

    private void Update()
    {
        if (!enableDebugKeys) return;
        if (InputUtils.DebugKeyDown(Key.U)) UnlockCivic("Innovation Council", "Debug Input");
        if (InputUtils.DebugKeyDown(Key.R) && IsCivicActive("Innovation Council")) RemoveCivic("Innovation Council", "Debug Input");
        if (InputUtils.DebugKeyDown(Key.G)) UnlockCivic("Guild Masters", "Debug Input");
        if (InputUtils.DebugKeyDown(Key.F)) UnlockCivic("Military Academy", "Debug Input");
    }

    // ===== UNLOCK / REMOVE =====

    /// <summary>Unlock and activate a civic if its requirements, conflicts and slots allow it.</summary>
    public bool UnlockCivic(string civicName, string source = "Manual")
    {
        var (canUnlock, reasons) = CanUnlockCivic(civicName);
        if (!canUnlock)
        {
            GameLog.Warning($"Cannot unlock '{civicName}': {string.Join("; ", reasons)}", Log);
            return false;
        }

        var civic = GameCatalog.Civics.Get(civicName, nameof(CivicManager));
        _activeByTier[civic.tier].Add(civic);
        EffectRouter.ApplySet(SourceName(civic), civic.effects.Where(e => e != null).Select(e => e.ToEffect()));
        if (civic.grantsCouncilPosition) GovernmentLogic.Instance?.RegisterCivicCouncilPosition(civic);

        GameLog.Event($"Unlocked civic '{civic.civicName}' ({civic.tier}) from '{source}'", Log);
        NotifyChanged(civic, true);
        return true;
    }

    /// <summary>
    /// Remove an active civic, its effects and its council seat (a position holding it reverts to a default
    /// seat and its legend is unseated), then apply its removal penalties.
    /// </summary>
    public bool RemoveCivic(string civicName, string source = "Manual")
    {
        CivicData civic = GetActiveCivic(civicName);
        if (civic == null)
        {
            GameLog.Warning($"Cannot remove '{civicName}': it is not active.", Log);
            return false;
        }

        if (civic.grantsCouncilPosition) GovernmentLogic.Instance?.UnregisterCivicCouncilPosition(civic);
        _activeByTier[civic.tier].Remove(civic);
        EffectRouter.RemoveSource(SourceName(civic));
        ApplyRemovalPenalties(civic, source);

        GameLog.Event($"Removed civic '{civic.civicName}' from '{source}'", Log);
        NotifyChanged(civic, false);
        return true;
    }

    private void NotifyChanged(CivicData civic, bool added)
    {
        OnCivicChanged?.Invoke(civic, added);
        OnCivicSlotsChanged?.Invoke();
        OnActiveCivicsChanged?.Invoke();
        OnCivicPoolChanged?.Invoke();
    }

    /// <summary>Why a civic can or cannot be unlocked right now (empty list means it can).</summary>
    public (bool canUnlock, List<string> reasons) CanUnlockCivic(string civicName)
    {
        var reasons = new List<string>();
        if (!GameCatalog.Civics.TryGet(civicName, out var civic))
        {
            reasons.Add("Civic not found");
            return (false, reasons);
        }
        if (IsCivicActive(civic.civicName))
        {
            reasons.Add("Civic already active");
            return (false, reasons);
        }
        foreach (var requirement in civic.requirements ?? new List<CivicRequirement>())
        {
            if (!IsRequirementMet(requirement)) reasons.Add($"Requirement not met: {requirement.GetAutoDescription()}");
        }
        foreach (string conflict in civic.conflictingCivics ?? new List<string>())
        {
            if (IsCivicActive(conflict)) reasons.Add($"Conflicts with active civic '{conflict}'");
        }
        foreach (string required in civic.requiredCivics ?? new List<string>())
        {
            if (!IsCivicActive(required)) reasons.Add($"Requires civic '{required}'");
        }
        if (GetActiveCivicCount(civic.tier) >= GetMaxSlots(civic.tier)) reasons.Add($"No available {civic.tier} tier slots");
        return (reasons.Count == 0, reasons);
    }

    /// <summary>Evaluated as the equivalent event condition (<see cref="CivicRequirement.ToCondition"/>).</summary>
    public static bool IsRequirementMet(CivicRequirement requirement) => requirement == null || requirement.IsMet();

    // Penalties are losses by definition, whatever sign the author used.
    private void ApplyRemovalPenalties(CivicData civic, string source)
    {
        if (civic.satisfactionPenalty != 0 && StatManager.Instance != null)
        {
            StatManager.Instance.ModifySatisfactionPoints(-Mathf.Abs(civic.satisfactionPenalty), $"Civic Removal: {civic.civicName}");
        }
        if (civic.moralePenaltyPerSeventh != 0 && civic.removalPenaltyDuration > 0)
        {
            removalPenalties[civic.civicName] = new CivicRemovalPenalty(civic.civicName, -Mathf.Abs(civic.moralePenaltyPerSeventh), civic.removalPenaltyDuration, source);
        }
    }

    private void OnSeventhTick(int newSeventh)
    {
        if (removalPenalties.Count == 0) return;
        foreach (var penalty in removalPenalties.Values.ToList())
        {
            StatManager.Instance?.ApplyMoraleShift(penalty.moralePenaltyPerSeventh);
            if (--penalty.remainingSevenths <= 0)
            {
                removalPenalties.Remove(penalty.civicName);
                GameLog.Event($"Removal penalty for '{penalty.civicName}' expired", Log);
            }
        }
    }

    // ===== QUERIES =====

    public bool IsCivicActive(string civicName) => GetActiveCivic(civicName) != null;

    public CivicData GetActiveCivic(string civicName)
    {
        if (string.IsNullOrEmpty(civicName)) return null;
        foreach (var list in _activeByTier.Values)
        {
            foreach (var civic in list)
            {
                if (string.Equals(civic.civicName, civicName, StringComparison.OrdinalIgnoreCase)) return civic;
            }
        }
        return null;
    }

    public List<CivicData> GetActiveCivics(CivicTier tier) => new List<CivicData>(_activeByTier[tier]);

    public List<CivicData> GetAllActiveCivics() => _activeByTier.Values.SelectMany(list => list).ToList();

    public int GetActiveCivicCount(CivicTier tier) => _activeByTier[tier].Count;

    private int GetMaxSlots(CivicTier tier)
    {
        switch (tier)
        {
            case CivicTier.Aeonic: return maxAeonicSlots;
            case CivicTier.Major: return maxMajorSlots;
            default: return maxMinorSlots;
        }
    }

    public int GetMaxCivicSlots(CivicTier tier) => GetMaxSlots(tier);

    public int GetAvailableSlots(CivicTier tier) => GetMaxSlots(tier) - GetActiveCivicCount(tier);

    public int GetStartingSlots(CivicTier tier)
    {
        switch (tier)
        {
            case CivicTier.Aeonic: return startingAeonicSlots;
            case CivicTier.Major: return startingMajorSlots;
            default: return startingMinorSlots;
        }
    }

    public int GetRemainingStartingSlots(CivicTier tier) => Mathf.Max(0, GetStartingSlots(tier) - GetActiveCivicCount(tier));

    public List<CivicData> GetUnlockableCivics() => GameCatalog.Civics.All.Where(c => CanUnlockCivic(c.civicName).canUnlock).ToList();

    public List<string> GetAllAvailableCivicNames() => GameCatalog.Civics.Keys.ToList();

    public List<CivicData> GetAllAvailableCivics() => GameCatalog.Civics.All.ToList();
}
