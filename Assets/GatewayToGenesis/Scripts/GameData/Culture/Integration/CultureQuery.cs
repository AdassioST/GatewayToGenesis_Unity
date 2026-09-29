using System.Collections.Generic;

/// <summary>
/// Read-only questions any culture feature or view may ask (<see cref="CultureSystem"/> answers them). Every answer is
/// a snapshot: changing it changes nothing in the culture, and it holds no live list. The interface is partial: each
/// feature adds its own questions in its own file (local profiles, dedications, accounts...) and answers them in its
/// own CultureSystem partial. Commands stay on CultureSystem and return <see cref="CultureCommandResult"/>; the
/// Preview* questions say what a command would do without doing it.
/// </summary>
public partial interface ICultureQuery
{
    /// <summary>Every tradition recorded, lived or dormant, oldest first.</summary>
    IReadOnlyList<TraditionView> Traditions();

    /// <summary>One tradition by its id ("trad-3"), or null.</summary>
    TraditionView Tradition(string id);

    /// <summary>The traditions kept in one settlement (-1: the nation's own).</summary>
    IReadOnlyList<TraditionView> TraditionsAt(int settlement);

    /// <summary>Traditions waiting for the player's decision (recognise, preserve, defer).</summary>
    IReadOnlyList<TraditionView> PendingTraditionChoices();

    /// <summary>The occurrences observed lately, newest last (copies), and those waiting for the next Seventh.</summary>
    IReadOnlyList<CulturalOccurrence> RecentOccurrences(int max = 20);
    IReadOnlyList<CulturalOccurrence> PendingOccurrences();

    /// <summary>What recognising, preserving or deferring would do now (nothing is changed).</summary>
    CultureCommandResult PreviewRecognizeTradition(string id);
    CultureCommandResult PreviewPreserveTradition(string id);
    CultureCommandResult PreviewDeferTradition(string id);
}

/// <summary>A tradition as a snapshot for views and other features (<see cref="ICultureQuery.Traditions"/>).</summary>
public sealed class TraditionView
{
    public string id, definition, name, description, family, canonSource;
    public bool food, local;
    public CanonStatus canon;
    /// <summary>-1: the nation (or a place unknown).</summary>
    public int settlement;
    public string place;
    public CultureEntityRef subject;
    public TraditionStage stage;
    public TraditionRecognition recognition;
    public int stageSince, lastPracticed, participations, distinctSevenths, revivals;
    public float momentum;
    public bool established;
    /// <summary>Its benefits apply now (lived and within the cap), and whether through recognition.</summary>
    public bool benefitActive, benefitCapped, recognizedNationally;
    public string origin, explanation, progress;
    public bool legacy;
    public string[] bearers;
    public CultureEntityRef[] venues, links;
    public TraditionParticipation[] history;
    public string[] benefits;
    public float recognitionCost;
    /// <summary>Why each choice is not open now (null: it is).</summary>
    public string whyNotRecognize, whyNotPreserve, whyNotDefer;

    public bool Lived => stage == TraditionStage.Practiced || stage == TraditionStage.Revived;
}
