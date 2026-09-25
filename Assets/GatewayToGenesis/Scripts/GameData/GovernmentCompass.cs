using System;
using System.Collections.Generic;

/// <summary>
/// Pure political-compass math, separate from the scene so it can be tested and re-balanced alone.
///
/// Axes run from -3 to +3. Horizontal: Waltz (negative) vs Regalia (positive). Vertical: Chorus (positive)
/// vs Aureus (negative). |net pillar difference| below the centrist threshold is 0, then ±1 Leaning,
/// ±2 Pillar and ±3 Fanatic.
/// </summary>
public static class GovernmentCompass
{
    public static int AxisCoordinate(int netDifference, int centristThreshold, int leaningThreshold, int pillarThreshold)
    {
        int magnitude = Math.Abs(netDifference);
        int level = magnitude < centristThreshold ? 0 : magnitude < leaningThreshold ? 1 : magnitude < pillarThreshold ? 2 : 3;
        return netDifference < 0 ? -level : level;
    }

    /// <param name="waltzRegalia">Horizontal coordinate (Waltz negative, Regalia positive).</param>
    /// <param name="chorusAureus">Vertical coordinate (Chorus positive, Aureus negative).</param>
    public static GovernmentType Classify(int waltzRegalia, int chorusAureus)
    {
        int x = waltzRegalia, y = chorusAureus;
        int ax = Math.Abs(x), ay = Math.Abs(y);

        if (ax == 3 && ay == 3) return GovernmentType.FanaticRadical;
        if (x == 0 && y == 0) return GovernmentType.TrueCentrist;

        if (y == 0)
        {
            if (ax == 1) return x < 0 ? GovernmentType.WaltzCentrist : GovernmentType.RegaliaCentrist;
            if (ax == 2) return x < 0 ? GovernmentType.WaltzLeaning : GovernmentType.RegaliaLeaning;
            return x < 0 ? GovernmentType.TrueWaltz : GovernmentType.TrueRegalia;
        }
        if (x == 0)
        {
            if (ay == 1) return y > 0 ? GovernmentType.ChorusCentrist : GovernmentType.AureusCentrist;
            if (ay == 2) return y > 0 ? GovernmentType.ChorusLeaning : GovernmentType.AureusLeaning;
            return y > 0 ? GovernmentType.TrueChorus : GovernmentType.TrueAureus;
        }

        // One axis Pillar and the other Fanatic.
        if ((ax == 2 && ay == 3) || (ax == 3 && ay == 2)) return GovernmentType.Radical;

        bool waltz = x < 0, chorus = y > 0;
        if (ax == 1 && ay == 1)
        {
            return waltz
                ? (chorus ? GovernmentType.WaltzChorusCentrist : GovernmentType.WaltzAureusCentrist)
                : (chorus ? GovernmentType.RegaliaChorusCentrist : GovernmentType.RegaliaAureusCentrist);
        }
        return waltz
            ? (chorus ? GovernmentType.WaltzChorus : GovernmentType.WaltzAureus)
            : (chorus ? GovernmentType.RegaliaChorus : GovernmentType.RegaliaAureus);
    }

    private static readonly Dictionary<GovernmentType, (string name, string description)> Info = new Dictionary<GovernmentType, (string, string)>
    {
        { GovernmentType.TrueCentrist, ("True Centrist", "A perfectly balanced government with no ideological leanings") },
        { GovernmentType.WaltzChorusCentrist, ("Waltz + Chorus Centrist", "Balanced between harmony and arcane knowledge") },
        { GovernmentType.RegaliaChorusCentrist, ("Regalia + Chorus Centrist", "Balanced between authority and arcane knowledge") },
        { GovernmentType.WaltzAureusCentrist, ("Waltz + Aureus Centrist", "Balanced between harmony and innovation") },
        { GovernmentType.RegaliaAureusCentrist, ("Regalia + Aureus Centrist", "Balanced between authority and innovation") },
        { GovernmentType.WaltzLeaning, ("Waltz Leaning", "Government favors harmony and endurance") },
        { GovernmentType.RegaliaLeaning, ("Regalia Leaning", "Government favors authority and ambition") },
        { GovernmentType.ChorusLeaning, ("Chorus Leaning", "Government favors arcane knowledge and secrecy") },
        { GovernmentType.AureusLeaning, ("Aureus Leaning", "Government favors innovation and piety") },
        { GovernmentType.WaltzCentrist, ("Waltz Centrist", "Government moderately favors harmony and endurance") },
        { GovernmentType.RegaliaCentrist, ("Regalia Centrist", "Government moderately favors authority and ambition") },
        { GovernmentType.ChorusCentrist, ("Chorus Centrist", "Government moderately favors arcane knowledge and secrecy") },
        { GovernmentType.AureusCentrist, ("Aureus Centrist", "Government moderately favors innovation and piety") },
        { GovernmentType.TrueWaltz, ("True Waltz", "Government strongly emphasizes harmony and endurance") },
        { GovernmentType.TrueRegalia, ("True Regalia", "Government strongly emphasizes authority and ambition") },
        { GovernmentType.TrueChorus, ("True Chorus", "Government strongly emphasizes arcane knowledge and secrecy") },
        { GovernmentType.TrueAureus, ("True Aureus", "Government strongly emphasizes innovation and piety") },
        { GovernmentType.WaltzChorus, ("Waltz + Chorus", "Government combines harmony with arcane knowledge") },
        { GovernmentType.RegaliaChorus, ("Regalia + Chorus", "Government combines authority with arcane knowledge") },
        { GovernmentType.WaltzAureus, ("Waltz + Aureus", "Government combines harmony with innovation") },
        { GovernmentType.RegaliaAureus, ("Regalia + Aureus", "Government combines authority with innovation") },
        { GovernmentType.Radical, ("Radical", "Government with extreme ideological positions") },
        { GovernmentType.FanaticRadical, ("Fanatic Radical", "Government with fanatical ideological positions") },
    };

    public static string Name(GovernmentType type) => Info.TryGetValue(type, out var info) ? info.name : "Unknown";

    public static string Description(GovernmentType type) => Info.TryGetValue(type, out var info) ? info.description : "Unknown government type";
}
