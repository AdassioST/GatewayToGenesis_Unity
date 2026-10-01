using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// How a battle is judged and read, with no scene state (tested in <c>BattleVerdictTests</c>). Drawn from the battle
/// screens that read best at a glance:
/// - Civilization VI: one strength number per side, and under it every modifier that made it ("+3 Ideal Terrain",
///   "+5 Great General"), so the player sees why (<see cref="BattlePreview"/>'s factors).
/// - Total War: a balance-of-power bar, the advisors' predicted outcome and predicted casualties, and a ladder of
///   verdicts where the Heroic Victory is a battle won against the odds (here the Legendary Victory, by hand only).
/// - Crusader Kings III: the army sizes face to face, the advantage as one signed number, the losses as a bar.
/// - Stellaris: a word for the odds ("overwhelming", "even", "deadly").
///
/// Seven verdicts, per side (the owner, Sept 29, 2026: three victories, three defeats, and a Legendary Victory for
/// the micro layer). A side's own losses and the enemy's (shares of their Integrity, the captured counted as lost)
/// decide which, and a battle's two verdicts mirror each other. Every threshold is a proposal.
/// </summary>
public static class BattleVerdicts
{
    /// <summary>Decisive: own losses at most this share, and the enemy's at least <see cref="DecisiveMargin"/> more.</summary>
    public const float DecisiveLoss = 0.2f, DecisiveMargin = 0.3f;
    /// <summary>Pyrrhic (and, mirrored, Valiant): own losses from this share: the army is left vulnerable.</summary>
    public const float PyrrhicLoss = 0.5f;
    /// <summary>A battle won by hand is Legendary when the forecast gave its winner at most this chance.</summary>
    public const float LegendaryChance = 0.3f;

    /// <summary>
    /// The verdict for a side that <paramref name="won"/> (or held the field) with <paramref name="own"/> and
    /// <paramref name="enemy"/> losses (shares of Integrity).
    /// </summary>
    public static BattleOutcome Tier(bool won, float own, float enemy)
    {
        own = Mathf.Clamp01(own);
        enemy = Mathf.Clamp01(enemy);
        if (won)
        {
            if (own >= PyrrhicLoss) return BattleOutcome.PyrrhicVictory;
            if (own <= DecisiveLoss && enemy - own >= DecisiveMargin) return BattleOutcome.DecisiveVictory;
            return BattleOutcome.CloseVictory;
        }
        // The mirrors: the winner's Pyrrhic is the loser's Valiant, its Decisive the loser's Crushing.
        if (enemy >= PyrrhicLoss) return BattleOutcome.ValiantDefeat;
        if (enemy <= DecisiveLoss && own - enemy >= DecisiveMargin) return BattleOutcome.CrushingDefeat;
        return BattleOutcome.CloseDefeat;
    }

    /// <summary>
    /// A battle won by hand is a Legendary Victory when the forecast gave its winner at most
    /// <see cref="LegendaryChance"/> (<paramref name="predictedWinChance"/>). The auto-resolve never grants it.
    /// </summary>
    public static bool Legendary(bool won, bool byHand, float predictedWinChance) => won && byHand && predictedWinChance <= LegendaryChance;

    public static bool IsVictory(BattleOutcome o) => o == BattleOutcome.DecisiveVictory || o == BattleOutcome.CloseVictory || o == BattleOutcome.PyrrhicVictory || o == BattleOutcome.LegendaryVictory;

    /// <summary>The other side's verdict of the same battle (a Legendary Victory's loser is judged by its losses: <see cref="Tier"/>).</summary>
    public static BattleOutcome Mirror(BattleOutcome o)
    {
        switch (o)
        {
            case BattleOutcome.DecisiveVictory: return BattleOutcome.CrushingDefeat;
            case BattleOutcome.CloseVictory: return BattleOutcome.CloseDefeat;
            case BattleOutcome.PyrrhicVictory: return BattleOutcome.ValiantDefeat;
            case BattleOutcome.CrushingDefeat: return BattleOutcome.DecisiveVictory;
            case BattleOutcome.CloseDefeat: return BattleOutcome.CloseVictory;
            case BattleOutcome.ValiantDefeat: return BattleOutcome.PyrrhicVictory;
            default: return BattleOutcome.CloseDefeat;
        }
    }

    /// <summary>From the best to the worst (the Legendary first): for sorting and for a meter.</summary>
    public static int Rank(BattleOutcome o)
    {
        switch (o)
        {
            case BattleOutcome.LegendaryVictory: return 0;
            case BattleOutcome.DecisiveVictory: return 1;
            case BattleOutcome.CloseVictory: return 2;
            case BattleOutcome.PyrrhicVictory: return 3;
            case BattleOutcome.CloseDefeat: return 4;
            case BattleOutcome.ValiantDefeat: return 5;
            default: return 6;
        }
    }

    public static string Words(BattleOutcome o)
    {
        switch (o)
        {
            case BattleOutcome.LegendaryVictory: return "Legendary Victory";
            case BattleOutcome.DecisiveVictory: return "Decisive Victory";
            case BattleOutcome.CloseVictory: return "Close Victory";
            case BattleOutcome.PyrrhicVictory: return "Pyrrhic Victory";
            case BattleOutcome.CloseDefeat: return "Close Defeat";
            case BattleOutcome.ValiantDefeat: return "Valiant Defeat";
            default: return "Crushing Defeat";
        }
    }

    /// <summary>What the verdict means, in one line.</summary>
    public static string Meaning(BattleOutcome o)
    {
        switch (o)
        {
            case BattleOutcome.LegendaryVictory: return "Won by your own hand against every forecast: a battle that should have been lost. The ballads will be sung.";
            case BattleOutcome.DecisiveVictory: return "Won with minimal casualties and a massive advantage over the enemy.";
            case BattleOutcome.CloseVictory: return "Won, but with real losses: the enemy nearly beat you.";
            case BattleOutcome.PyrrhicVictory: return "Won, but your force suffered catastrophic damage that leaves it vulnerable.";
            case BattleOutcome.CloseDefeat: return "Lost, but narrowly: your force nearly carried the day.";
            case BattleOutcome.ValiantDefeat: return "Lost, but the enemy paid dearly: its victory has left it vulnerable.";
            default: return "Overwhelmed or routed: the enemy barely felt it.";
        }
    }

    /// <summary>Gold for the Legendary, greens for victories, oranges and reds for defeats.</summary>
    public static Color Color(BattleOutcome o)
    {
        switch (o)
        {
            case BattleOutcome.LegendaryVictory: return new Color(1f, 0.82f, 0.3f);
            case BattleOutcome.DecisiveVictory: return new Color(0.35f, 0.85f, 0.4f);
            case BattleOutcome.CloseVictory: return new Color(0.6f, 0.85f, 0.45f);
            case BattleOutcome.PyrrhicVictory: return new Color(0.8f, 0.8f, 0.45f);
            case BattleOutcome.CloseDefeat: return new Color(0.95f, 0.65f, 0.35f);
            case BattleOutcome.ValiantDefeat: return new Color(0.9f, 0.5f, 0.35f);
            default: return new Color(0.9f, 0.25f, 0.25f);
        }
    }

    /// <summary>Casualties as a word (Total War's predicted casualties): a share of Integrity lost.</summary>
    public static string Casualties(float share) =>
        share < 0.1f ? "Very low" : share < 0.25f ? "Low" : share < 0.45f ? "Medium" : share < 0.7f ? "High" : "Catastrophic";

    /// <summary>
    /// The verdict a forecast points to for one side: whether it wins more often than not (a held field counts for the
    /// defender), with the mean losses of both sides.
    /// </summary>
    public static BattleOutcome Predict(BattleForecast f, bool forAttacker)
    {
        if (f == null || f.runs == 0) return BattleOutcome.CloseDefeat;
        float win = forAttacker ? f.WinChance : (f.defenderWins + f.stalemates) / (float)f.runs;
        return Tier(win >= 0.5f, forAttacker ? f.attackerLoss : f.defenderLoss, forAttacker ? f.defenderLoss : f.attackerLoss);
    }

    /// <summary>A side's chance of winning in a forecast (a held field counts for the defender).</summary>
    public static float WinChance(BattleForecast f, bool forAttacker) =>
        f == null || f.runs == 0 ? 0.5f : forAttacker ? f.WinChance : (f.defenderWins + f.stalemates) / (float)f.runs;
}

/// <summary>One line of a strength breakdown ("+3 Ideal terrain (Hills)").</summary>
public sealed class StrengthFactor
{
    public string label, detail;
    /// <summary>Strength it adds (negative: takes away).</summary>
    public float amount;

    public override string ToString() => $"{(amount >= 0f ? "+" : "")}{amount:0} {label}{(string.IsNullOrEmpty(detail) ? "" : $" ({detail})")}";
}

/// <summary>One side as the pre-battle screen shows it.</summary>
public sealed class PreviewSide
{
    public BattleScoreInputs scoreInputs;
    public BattleStance stance;
    public List<BattleDeploymentPiece> deployment = new List<BattleDeploymentPiece>();
    public string name, commander, commanderTitle;
    public bool attacking;
    /// <summary>Sections on the field, and individuals in them (0 when not counted).</summary>
    public int sections, individuals;
    /// <summary>The one number (Civ VI's combat strength): its Symphony's power on this field against this enemy.</summary>
    public float strength;
    /// <summary>What makes <see cref="strength"/>: the base first, then each modifier.</summary>
    public List<StrengthFactor> factors = new List<StrengthFactor>();
    /// <summary>Its Symphony: cards in the deck, Beats a measure, and the share of its harm the cards carry.</summary>
    public int cards, beats;
    public float melodyShare;
    /// <summary>Its forecast chance to win, its mean losses, and the verdict the forecast points to.</summary>
    public float winChance, predictedLoss;
    public BattleOutcome predicted;
    public string Casualties => BattleVerdicts.Casualties(predictedLoss);
    public int Strength => Mathf.RoundToInt(strength);
}

/// <summary>
/// The pre-battle screen (Total War's, with Civ VI's strength breakdown and CK3's advantage): both sides' strengths and
/// what makes them, the balance of power, and what the forecast predicts. Built from a battle setup, which it leaves
/// untouched.
/// </summary>
public sealed class BattlePreview
{
    public List<BattleHexTerrain> terrain = new List<BattleHexTerrain>();
    public string title;
    public PreviewSide attacker = new PreviewSide { attacking = true }, defender = new PreviewSide();
    /// <summary>The attacker's odds from the two strengths, squared as Lanchester has it (the bar), and CK3's advantage: attacker minus defender.</summary>
    public float balance;
    public int Advantage => attacker.Strength - defender.Strength;
    public BattleForecast forecast;

    public PreviewSide Side(bool attacker) => attacker ? this.attacker : defender;

    /// <summary>The advisors' line for one side: "With our superior forces, our advisors predict a Decisive Victory."</summary>
    public string Advice(bool forAttacker)
    {
        var own = Side(forAttacker);
        float share = forAttacker ? balance : 1f - balance;
        string forces = share >= 0.75f ? "With our overwhelming forces" : share >= 0.58f ? "With our superior forces" : share > 0.42f ? "Evenly matched" : share > 0.25f ? "Outmatched" : "Against a far stronger enemy";
        return $"{forces}, our advisors predict a {BattleVerdicts.Words(own.predicted)}.";
    }

    /// <summary>
    /// Weighs both sides on the field (<see cref="SymphonyPower.Breakdown"/>) and plays the battle
    /// <paramref name="runs"/> times on copies (<see cref="BattleResolver.Forecast"/>; 0: no forecast, the prediction read
    /// from the strengths alone).
    /// </summary>
    public static BattlePreview Of(BattleSetup setup, CombatSettings settings = null, int runs = 20, string title = null)
    {
        settings = settings ?? new CombatSettings();
        setup = setup.Clone(setup.seed);
        var field = setup.field ?? new Battlefield();
        var spatial = new BattleSpatialState(setup.attacker, setup.defender, field, settings);
        var p = new BattlePreview { title = title ?? $"Battle of {Capital(field.place)}" };
        p.terrain = spatial.Terrain.Select(t => t.Clone()).ToList();
        Fill(p.attacker, setup.attacker, setup.defender, settings, field, true);
        Fill(p.defender, setup.defender, setup.attacker, settings, field, false);
        // Lanchester: a force's odds go as the square of its strength (59 against 44 is favoured, not even).
        float a2 = p.attacker.strength * p.attacker.strength, d2 = p.defender.strength * p.defender.strength;
        p.balance = a2 + d2 <= 0f ? 0.5f : a2 / (a2 + d2);
        if (runs > 0)
        {
            p.forecast = BattleResolver.Forecast(setup, settings, runs);
            foreach (bool side in new[] { true, false })
            {
                var s = p.Side(side);
                s.winChance = BattleVerdicts.WinChance(p.forecast, side);
                s.predictedLoss = side ? p.forecast.attackerLoss : p.forecast.defenderLoss;
                s.predicted = BattleVerdicts.Predict(p.forecast, side);
            }
        }
        else
        {
            // No forecast: the strengths alone (a Lanchester reading: the weaker loses about the square of the ratio).
            foreach (bool side in new[] { true, false })
            {
                var s = p.Side(side);
                float share = side ? p.balance : 1f - p.balance;
                s.winChance = share;
                float ratio = share <= 0f ? 9f : (1f - share) / Math.Max(0.01f, share);
                s.predictedLoss = Mathf.Clamp01(share >= 0.5f ? 0.6f * ratio * ratio : 0.9f);
            }
            foreach (bool side in new[] { true, false })
                p.Side(side).predicted = BattleVerdicts.Tier(p.Side(side).winChance >= 0.5f, p.Side(side).predictedLoss, p.Side(!side).predictedLoss);
        }
        return p;
    }

    private static void Fill(PreviewSide s, BattleSide side, BattleSide enemy, CombatSettings settings, Battlefield field, bool attacking)
    {
        s.stance = side.stance;
        s.scoreInputs = side.scoreInputs?.Clone() ?? BattleCompositionLogic.Describe(side, field, settings);
        s.deployment = side.Standing.Select(section => new BattleDeploymentPiece { name = section.name, hex = section.battleHex, count = Math.Max(1, section.count), role = section.eliteRole }).ToList();
        s.name = side?.name ?? "?";
        s.commander = side?.conductor?.name;
        s.commanderTitle = side?.conductor?.Title;
        s.sections = side?.sections.Count(x => x.Standing) ?? 0;
        s.individuals = side?.sections.Where(x => x.Standing).Sum(x => x.Alive) ?? 0;
        var rating = SymphonyPower.Rate(side, settings, field, attacking);
        s.cards = rating.cards;
        s.beats = rating.beats;
        s.melodyShare = rating.MelodyShare;
        s.factors = SymphonyPower.Breakdown(side, enemy, settings, field, attacking, out s.strength);
    }

    private static string Capital(string place) => string.IsNullOrEmpty(place) ? "the Wilds" : char.ToUpperInvariant(place[0]) + place.Substring(1);
}

public sealed class BattleDeploymentPiece
{
    public string name;
    public int hex, count;
    public BattleEliteRole role;
}
