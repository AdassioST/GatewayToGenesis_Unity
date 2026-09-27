using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The pillar challenge on a chorus choice: the pillar's icon and a luck icon for the current chance of
/// success, each with a tooltip. The chance is <see cref="ChorusRules"/>' — the same numbers the roll uses —
/// with the Piety saving-roll bonus included.
/// </summary>
public class ChallengeSlot : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Image pillarIcon;
    [SerializeField] private Image chanceImage; // Luck icon for the chance of success

    [Header("Challenge Data")]
    [SerializeField] private string pillarType; // "aureus", "regalia", "waltz", "chorus"
    [SerializeField] private int requiredStrength = 10;
    [SerializeField] private float successChance;
    [SerializeField] private float enhancedSuccessChance; // Includes the saving-roll bonus

    [Header("Chance Sprites")]
    [SerializeField] private Sprite chanceIconFated;     // Above 90%
    [SerializeField] private Sprite chanceIconBlessed;   // 60-90%
    [SerializeField] private Sprite chanceIconGamble;    // 40-60%
    [SerializeField] private Sprite chanceIconCursed;    // 10-40%
    [SerializeField] private Sprite chanceIconForsaken;  // Below 10%

    public string PillarType => pillarType;
    public int RequiredStrength => requiredStrength;
    /// <summary>Chance from the pillar alone (0-100).</summary>
    public float SuccessChance => successChance;
    /// <summary>Chance including the saving-roll bonus (0-100).</summary>
    public float EnhancedSuccessChance => enhancedSuccessChance;

    private void Start() => RefreshChallengeDisplay();

    public void InitializeChallenge(string pillar, int required, Sprite icon = null)
    {
        pillarType = pillar;
        requiredStrength = required;
        if (icon != null && pillarIcon != null) pillarIcon.sprite = icon;
        RefreshChallengeDisplay();
    }

    /// <summary>Recompute the chance from the current pillar and Piety and refresh icon and tooltips.</summary>
    public void RefreshChallengeDisplay()
    {
        if (string.IsNullOrEmpty(pillarType)) return;
        var stats = StatManager.Instance;
        int current = stats != null ? stats.GetPillarValue(pillarType) : 0;
        float bonus = stats != null ? stats.GetSavingRollChancePercentCapped() : 0f;
        int natural = ChorusRules.SuccessPercent(current, requiredStrength);
        int enhanced = ChorusRules.ChanceAbove(100 - natural, bonus);
        successChance = natural;
        enhancedSuccessChance = enhanced;

        string pillar = EventText.Humanize(pillarType);
        if (chanceImage != null)
        {
            var sprite = ChanceSprite(enhanced);
            if (sprite != null) chanceImage.sprite = sprite;
            var lines = new List<string> { TooltipText.Row($"From {pillar}", TooltipText.Value($"{natural}%")) };
            if (enhanced > natural) lines.Add(TooltipText.Row("Piety saving roll", TooltipText.Good($"+{enhanced - natural}%")));
            lines.Add(TooltipText.Row("Chance of success", TooltipText.Judge($"{enhanced}%", enhanced >= 50)));
            TooltipTrigger.Ensure(chanceImage.gameObject).SetCustom($"{LuckName(enhanced)} Luck", null, "Decision Odds", TooltipText.Lines(lines));
        }
        if (pillarIcon != null)
        {
            string odds = TooltipText.Lines(new[]
            {
                TooltipText.Row("Strength needed", TooltipText.Value(requiredStrength.ToString())),
                TooltipText.Row($"Your {pillar}", TooltipText.Judge(current.ToString(), current >= requiredStrength)),
                TooltipText.Muted($"Meeting the strength makes success certain; each point short lowers the chance."),
            });
            TooltipTrigger.Ensure(pillarIcon.gameObject).SetCustom($"{pillar} Challenge", null, "Pillar Test", odds);
        }
    }

    private Sprite ChanceSprite(float chance)
    {
        if (chance > 90f) return chanceIconFated;
        if (chance >= 60f) return chanceIconBlessed;
        if (chance >= 40f) return chanceIconGamble;
        if (chance >= 10f) return chanceIconCursed;
        return chanceIconForsaken;
    }

    private static string LuckName(float chance)
    {
        if (chance > 90f) return "Fated";
        if (chance >= 60f) return "Blessed";
        if (chance >= 40f) return "Gamble";
        if (chance >= 10f) return "Cursed";
        return "Forsaken";
    }
}
