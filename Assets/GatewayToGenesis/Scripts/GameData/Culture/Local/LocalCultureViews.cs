using System.Collections.Generic;
using System.Linq;

// Read-only snapshots of local cultures (ICultureQuery): copies, never the live saved lists.

/// <summary>One custom as a settlement knows it.</summary>
public sealed class LocalPracticeView
{
    public string practice, name, family, variant, canon, vault;
    public LocalPracticeStage stage;
    public float exposure, participation;
    public int gatherings, adoptedSeventh, lastGathered;
    public bool recognized;
    public PracticeProvenance origin;
    /// <summary>"it came along the road from Ashford", "its origin was not recorded".</summary>
    public string provenanceText;

    public bool Kept => stage == LocalPracticeStage.Practiced || stage == LocalPracticeStage.Quiet;

    public static LocalPracticeView Of(LocalPractice p)
    {
        var spec = LocalPracticeCatalog.Find(p.practice);
        string words = LocalCultureRules.ProvenanceWords(p.origin).Trim();
        return new LocalPracticeView
        {
            practice = p.practice, name = spec?.name ?? p.practice, family = spec?.family, variant = p.variant, canon = spec?.CanonText, vault = spec?.vault,
            stage = p.stage, exposure = p.exposure, participation = p.participation, gatherings = p.gatherings, adoptedSeventh = p.adoptedSeventh,
            lastGathered = p.lastGathered, recognized = p.recognized, origin = p.origin?.Copy() ?? new PracticeProvenance(),
            provenanceText = words.Length > 2 ? words.Substring(1, words.Length - 2) : words,
        };
    }
}

/// <summary>A settlement's customs: those it keeps and those it has only met.</summary>
public sealed class SettlementProfileView
{
    public int settlement;
    public string name;
    public bool gone;
    public IReadOnlyList<LocalPracticeView> practices;

    public IEnumerable<LocalPracticeView> Kept => practices.Where(p => p.Kept);
    public IEnumerable<LocalPracticeView> Met => practices.Where(p => !p.Kept);

    public static SettlementProfileView Of(SettlementProfile p) => new SettlementProfileView
    {
        settlement = p.settlement, name = p.name, gone = p.gone,
        practices = p.practices.Where(x => x != null).Select(LocalPracticeView.Of).OrderByDescending(x => x.Kept).ThenBy(x => x.name).ToList(),
    };
}

/// <summary>Two settlements in contact by road, and whether the road is open.</summary>
public sealed class ContactEdgeView
{
    public int a, b;
    public string aName, bName, blockedBy;
    public bool open;
}

/// <summary>People admitted somewhere, as recorded: origin only when known.</summary>
public sealed class ArrivalView
{
    public int settlement, people, seventh;
    public ArrivalOriginKind originKind;
    public string originLabel, channel;

    /// <summary>"12 survivors found at the Violet Grove", "8 people, origin unknown".</summary>
    public string Text
    {
        get
        {
            string who = $"{people} {(people == 1 ? "person" : "people")}";
            switch (originKind)
            {
                case ArrivalOriginKind.Settlement: return $"{who} from {originLabel ?? "one of your settlements"} ({channel})";
                case ArrivalOriginKind.Place: return $"{who} found at {originLabel ?? "a place in the wilds"}: their customs are not recorded";
                default: return $"{who} ({channel ?? "arrivals"}): origin unknown";
            }
        }
    }
}

/// <summary>A custom a cultural party carries.</summary>
public sealed class CarriedPracticeView
{
    public string practice, name, fromName;
    public int fromSettlement;
}
