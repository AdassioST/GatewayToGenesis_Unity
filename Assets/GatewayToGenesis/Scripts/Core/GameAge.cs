using System;

/// <summary>
/// The Age of Magic the world is in. The game starts in the Age of Desolation (Age 0: the vault's "Ages 0").
/// Keyword cards read it to choose what they say (<see cref="KeywordMasterfile.Reading"/>), as the website's cards
/// read the chapter. Ages are named by the slug of their title ("age-of-embers"), as in the White-Haven Library's
/// ages table; its number is its "Ages N" folder in the vault (the vault's own "Tiers" group several of them). Nothing advances the Age yet: when the passage
/// between Ages is built, it calls <see cref="Set"/>.
/// </summary>
public static class GameAge
{
    public const string FirstAge = "age-of-desolation";

    /// <summary>The current Age's id ("age-of-desolation").</summary>
    public static string Id { get; private set; } = FirstAge;

    /// <summary>The current Age's number (0 for the Age of Desolation).</summary>
    public static int Number { get; private set; }

    /// <summary>The Age changed.</summary>
    public static event Action Changed;

    public static void Set(string ageId, int number)
    {
        if (string.IsNullOrWhiteSpace(ageId)) return;
        ageId = ageId.Trim().ToLowerInvariant();
        if (ageId == Id && number == Number) return;
        Id = ageId;
        Number = Math.Max(0, number);
        GameLog.Event($"The world enters {ageId} (Age {Number})", LogChannel.Time);
        Changed?.Invoke();
    }

    // Statics survive between play sessions when domain reload is disabled: every session starts in the first Age.
    [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset()
    {
        Id = FirstAge;
        Number = 0;
        Changed = null;
    }
}
