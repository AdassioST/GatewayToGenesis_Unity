using System;
using System.Collections.Generic;

/// <summary>
/// Pure derived-stat math: how a substat level turns into a derived stat. No scene or Unity state,
/// so it can be unit tested and re-balanced in one place.
///
/// Every derived stat is a percentage (4.5 means 4.5%). A few are exposed as multipliers built from
/// that percentage: expedition cost/time become 1 - p/100 and morale recovery becomes 1 + p/100.
/// </summary>
public static class StatGrowth
{
    public enum FormulaType
    {
        Linear,       // ax + b
        Quadratic,    // (ax² + bx + c) / 100
        Logarithmic,  // a·log10(bx + c) + d
        Static        // a per level
    }

    public struct Tier
    {
        public FormulaType type;
        public float a, b, c, d;
        public float balance;

        public Tier(FormulaType type, float a, float b, float c, float d, float balance)
        {
            this.type = type;
            this.a = a;
            this.b = b;
            this.c = c;
            this.d = d;
            this.balance = balance;
        }
    }

    public enum OutputKind
    {
        Percent,
        OneMinusPercent,
        OnePlusPercent
    }

    public struct Curve
    {
        public Tier[] tiers;
        public OutputKind output;
    }

    /// <summary>Upper level of tiers 1-4; the fifth tier is open-ended.</summary>
    public struct Boundaries
    {
        public int tier1, tier2, tier3, tier4;

        public Boundaries(int tier1, int tier2, int tier3, int tier4)
        {
            this.tier1 = tier1;
            this.tier2 = tier2;
            this.tier3 = tier3;
            this.tier4 = tier4;
        }

        public static readonly Boundaries Default = new Boundaries(42, 86, 150, 220);
    }

    private static Tier Q(float a, float b, float balance) => new Tier(FormulaType.Quadratic, a, b, 0f, 0f, balance);
    private static Tier L(float a, float b, float c, float d, float balance) => new Tier(FormulaType.Logarithmic, a, b, c, d, balance);
    private static Tier S(float a, float balance) => new Tier(FormulaType.Static, a, 0f, 0f, 0f, balance);

    private static Curve C(OutputKind output, params Tier[] tiers) => new Curve { tiers = tiers, output = output };

    /// <summary>Growth curves by derived stat (keys match <see cref="StatDefinitions.DerivedSource"/>).</summary>
    public static readonly IReadOnlyDictionary<string, Curve> Curves = new Dictionary<string, Curve>(StringComparer.OrdinalIgnoreCase)
    {
        { "discoveryEfficiency", C(OutputKind.Percent, Q(-0.1f, 8f, 1f), L(1f, -3f, 280f, -1f, 0.8f), S(0.5f, 0.6f), S(0.25f, 0.4f)) },
        { "savingRollChance", C(OutputKind.Percent, Q(-0.05f, 4f, 1f), L(0.5f, -2f, 200f, -0.5f, 0.8f), S(0.25f, 0.6f), S(0.125f, 0.4f)) },
        { "legendEffectiveness", C(OutputKind.Percent, Q(-0.1f, 8f, 1f), L(1f, -3f, 280f, -1f, 0.8f), S(0.5f, 0.6f), S(0.25f, 0.4f)) },
        { "expeditionCostMod", C(OutputKind.OneMinusPercent, Q(-0.02f, 1.6f, 1f), L(0.2f, -1f, 100f, -0.2f, 0.8f), S(0.1f, 0.6f), S(0.05f, 0.4f)) },
        { "expeditionTimeMod", C(OutputKind.OneMinusPercent, Q(-0.03f, 2.4f, 1f), L(0.3f, -1.5f, 150f, -0.3f, 0.8f), S(0.15f, 0.6f), S(0.075f, 0.4f)) },
        { "satisfactionEffectiveness", C(OutputKind.Percent, Q(-0.12f, 9.6f, 1f), L(1.2f, -3.6f, 336f, -1.2f, 0.8f), S(0.6f, 0.6f), S(0.3f, 0.4f)) },
        { "moraleLossMod", C(OutputKind.Percent, Q(-0.04f, 3.2f, 1f), L(0.4f, -1.2f, 112f, -0.4f, 0.8f), S(0.2f, 0.6f), S(0.1f, 0.4f)) },
        { "moraleRecoveryMod", C(OutputKind.OnePlusPercent, Q(-0.06f, 4.8f, 1f), L(0.6f, -1.8f, 168f, -0.6f, 0.8f), S(0.3f, 0.6f), S(0.15f, 0.4f)) },
        { "clickPowerBonus", C(OutputKind.Percent, Q(-0.08f, 6.4f, 1f), L(0.8f, -2.4f, 224f, -0.8f, 0.8f), S(0.4f, 0.6f), S(0.2f, 0.4f)) },
        { "magicEffectiveness", C(OutputKind.Percent, Q(-0.2f, 16f, 1f), L(2f, -6f, 560f, -2f, 0.8f), S(1f, 0.6f), S(0.5f, 0.4f)) },
    };

    /// <summary>Sum of per-level bonuses for levels 1..level, walking the tier boundaries.</summary>
    public static float TieredGrowth(int level, Tier[] tiers, Boundaries bounds, float globalBalance = 1f)
    {
        if (level <= 0 || tiers == null) return 0f;
        int[] caps = { bounds.tier1, bounds.tier2, bounds.tier3, bounds.tier4, int.MaxValue };

        float total = 0f;
        int previousCap = 0;
        for (int i = 0; i < tiers.Length && i < caps.Length; i++)
        {
            int cap = caps[i];
            int levelsInTier = Math.Max(0, Math.Min(level - previousCap, cap - previousCap));
            if (levelsInTier > 0)
            {
                var tier = tiers[i];
                float scale = tier.balance * globalBalance;
                switch (tier.type)
                {
                    case FormulaType.Linear:
                        for (int l = 1; l <= levelsInTier; l++)
                        {
                            float x = previousCap + l;
                            total += (tier.a * x + tier.b) * scale;
                        }
                        break;
                    case FormulaType.Quadratic:
                        for (int l = 1; l <= levelsInTier; l++)
                        {
                            float x = previousCap + l;
                            total += (tier.a * x * x + tier.b * x + tier.c) / 100f * scale;
                        }
                        break;
                    case FormulaType.Logarithmic:
                        for (int l = 1; l <= levelsInTier; l++)
                        {
                            float x = previousCap + l;
                            float argument = tier.b * x + tier.c;
                            if (argument > 0f) total += (tier.a * (float)Math.Log10(argument) + tier.d) * scale;
                        }
                        break;
                    case FormulaType.Static:
                        total += levelsInTier * tier.a * scale;
                        break;
                }
            }
            previousCap = cap;
            if (level <= cap) break;
        }
        return total;
    }

    /// <summary>Base value of a derived stat for the given level of its source substat.</summary>
    public static float Evaluate(string derivedStat, int substatLevel, Boundaries bounds, float globalBalance = 1f)
    {
        if (!Curves.TryGetValue(derivedStat, out var curve)) return 0f;
        float percent = TieredGrowth(substatLevel, curve.tiers, bounds, globalBalance);
        switch (curve.output)
        {
            case OutputKind.OneMinusPercent: return 1f - percent / 100f;
            case OutputKind.OnePlusPercent: return 1f + percent / 100f;
            default: return percent;
        }
    }
}
