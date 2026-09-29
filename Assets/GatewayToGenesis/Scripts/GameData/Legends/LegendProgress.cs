using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// How a legend grows, with no scene state (tested in <c>LegendGrowthTests</c>). Lyrical Fragments come from deeds
/// (standing on the council through an Act of Fate, carrying the people through an Age Crisis, walking with
/// expeditions, improving the land, the roles they play in stories (ballad actors), a Motif Awakening; <see cref="LyricalFragments"/>);
/// their total sets the rank, and the rank multiplies the legend's own council bonuses. Numbers are prototype
/// proposals (roadmap D08), not canon.
/// </summary>
public static class LegendGrowthRules
{
    /// <summary>Lyrical Fragments (all kinds together) needed for ranks 1 to 5.</summary>
    public static readonly int[] RankThresholds = { 0, 10, 25, 50, 100 };

    /// <summary>Extra share of a legend's council bonuses per rank above the first.</summary>
    public const float GrowthPerRank = 0.2f;

    public static int MaxRank => RankThresholds.Length;

    /// <summary>Rank 1 to <see cref="MaxRank"/> for a number of Lyrical Fragments.</summary>
    public static int RankFor(int fragments)
    {
        int rank = 1;
        for (int i = 1; i < RankThresholds.Length; i++)
        {
            if (fragments >= RankThresholds[i]) rank = i + 1;
        }
        return rank;
    }

    /// <summary>What a legend's own council bonuses are multiplied by at <paramref name="rank"/>.</summary>
    public static float Multiplier(int rank) => 1f + GrowthPerRank * (Math.Max(1, Math.Min(MaxRank, rank)) - 1);

    /// <summary>Fragments the next rank needs, or -1 at the top.</summary>
    public static int NextThreshold(int rank) => rank >= 1 && rank < RankThresholds.Length ? RankThresholds[rank] : -1;

    /// <summary>The first legends of a new run: the commonest first, then by name.</summary>
    public static List<string> StartingRoster(IEnumerable<(string name, int rarity)> legends, int count) =>
        legends.Where(l => !string.IsNullOrEmpty(l.name)).OrderBy(l => l.rarity).ThenBy(l => l.name, StringComparer.OrdinalIgnoreCase)
            .Take(Math.Max(0, count)).Select(l => l.name).ToList();
}

/// <summary>
/// The legends the civilization has met: their Lyrical Fragments, rank and deeds, and who they are
/// (<see cref="LegendSoul"/>: Soul Leitmotif, Legend Traits, Composure). The council only offers recruited legends
/// (<see cref="LegendLeaderLogic.GetAvailableLegends"/>); others are met on the world map. A legend's rank and its
/// Composure scale its own council bonuses (<see cref="CouncilMultiplier"/>); each kind of fragment strengthens its
/// binding (<see cref="Bindings"/>). An Underdog (vault: Legend Trait.md) earns double for deeds against the odds.
///
/// Composure (vault: Composure.md) is settled every Seventh by <see cref="ComposureRules"/>: the council carries the
/// Age Crisis and the dead, rest heals. Healing from a real wound (Fractured or deeper) brings a Motif Awakening
/// (<see cref="LegendSoulRules.Awaken"/>); a legend whose Soul Leitmotif reaches Surrender is lost to Dissonance: it
/// leaves the council and the roster, and keeps its record for history. Created by <see cref="GenesisLoop"/>; saved
/// through <c>_recruited</c> (the records, souls included).
/// </summary>
public partial class LegendProgress : SingletonBehaviour<LegendProgress>
{
    private const LogChannel Log = LogChannel.Legends;

    [Tooltip("Legends known when the run begins (the commonest first).")]
    public int startingLegends = 2;

    // Field names are part of the save (GameSnapshot captures _recruited with every record): rename with care.
    private class Record
    {
        /// <summary>Lyrical Fragments by kind name (<see cref="FragmentKind"/>).</summary>
        public Dictionary<string, int> fragments = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        public readonly List<string> deeds = new List<string>();
        [SaveOptionalField]
        public LegendBalladHistory balladHistory = new LegendBalladHistory();
        [SaveOptionalField]
        public List<LegendRelationship> relationships = new List<LegendRelationship>();
        /// <summary>Who the legend is (created when it is met).</summary>
        public LegendSoul soul;
        /// <summary>Lost to Dissonance: no longer on the roster, never met again.</summary>
        public bool lost;
        /// <summary>The count of the dead this legend has already mourned (true deaths and the homeless).</summary>
        public int deathsSeen;
        /// <summary>Lasting conditions (Traumatized, Haunted...: <see cref="LegendConditions"/>).</summary>
        [SaveOptionalField]
        public List<LegendCondition> lastingConditions = new List<LegendCondition>();
        /// <summary>Sevenths until a legend missing in action turns up in a settlement (0: not missing).</summary>
        [SaveOptionalField]
        public int missingSevenths;
        /// <summary>Strain it brings home when it turns up, and where it went missing.</summary>
        [SaveOptionalField]
        public float missingStrain;
        [SaveOptionalField]
        public string missingFrom;
        /// <summary>Held captive by an Atonalis band (its key, <see cref="LegendProgress.TakeCaptive"/>; null: free), and the strain it takes each Seventh while held.</summary>
        [SaveOptionalField]
        public string heldBy;
        [SaveOptionalField]
        public float heldFeed;
    }

    // Every legend met, the lost included (so a lost legend is never met again).
    private readonly Dictionary<string, Record> _recruited = new Dictionary<string, Record>(StringComparer.OrdinalIgnoreCase);
    private bool _started, _subscribed;

    /// <summary>The roster or a legend's fragments changed.</summary>
    public event Action Changed;
    public event Action<LegendData> Recruited;
    /// <summary>A legend earned Lyrical Fragments: (legend, what it earned, the deed).</summary>
    public event Action<string, IReadOnlyList<FragmentAward>, string> FragmentsEarned;
    /// <summary>A legend reached a new rank: (legend, rank).</summary>
    public event Action<string, int> RankedUp;
    /// <summary>A legend's Composure moved to another state: (legend, from, to).</summary>
    public event Action<string, ComposureState, ComposureState> ComposureChanged;
    /// <summary>A legend had a Motif Awakening, or reached the Catalytic Abyss of Emotion.</summary>
    public event Action<string, AwakeningResult> Awakened;
    /// <summary>A legend's Soul Leitmotif reached Surrender: the legend is lost to Dissonance.</summary>
    public event Action<string> Lost;

    private static ComposureTuning Tuning => LegendLore.ComposureTuning;
    private static FragmentTuning FragmentRules => LegendLore.FragmentTuning;

    private void Start()
    {
        TimeSystemLogic.WhenReady(this, time =>
        {
            time.OnSeventhChange += OnSeventh;
            _subscribed = true;
        });
        if (_started) return;
        _started = true;
        var legends = GameCatalog.Legends.All.Where(l => l != null).Select(l => (l.legendName, (int)l.rarity));
        foreach (var name in LegendGrowthRules.StartingRoster(legends, startingLegends)) AddRecord(name);
        // Anyone already seated has obviously been met.
        var government = GovernmentLogic.Instance;
        if (government != null) foreach (var (_, legend) in government.GetAllAssignedLegends()) AddRecord(legend.legendName);
        GameLog.Event($"Legends known at the start: {string.Join(", ", RecruitedNames)}", Log);
        NotifyRosterChanged();
    }

    protected override void OnSingletonDestroy()
    {
        if (_subscribed && TimeSystemLogic.Instance != null) TimeSystemLogic.Instance.OnSeventhChange -= OnSeventh;
    }

    public bool IsRecruited(string legendName) => legendName != null && _recruited.TryGetValue(legendName, out var record) && !record.lost;

    /// <summary>Lost to Dissonance (its Soul Leitmotif reached Surrender).</summary>
    public bool IsLost(string legendName) => legendName != null && _recruited.TryGetValue(legendName, out var record) && record.lost;

    public IEnumerable<string> RecruitedNames => _recruited.Where(r => !r.Value.lost).Select(r => r.Key);

    public IEnumerable<string> LostNames => _recruited.Where(r => r.Value.lost).Select(r => r.Key);

    public int RecruitedCount => _recruited.Count(r => !r.Value.lost);

    /// <summary>A legend's Lyrical Fragments, all kinds together (what its rank is measured in).</summary>
    public int Fragments(string legendName) => LyricalFragments.Total(Purse(legendName));

    public int Fragments(string legendName, FragmentKind kind) => LyricalFragments.Count(Purse(legendName), kind);

    /// <summary>A legend's Lyrical Fragments by kind name (empty for a legend not met).</summary>
    public IReadOnlyDictionary<string, int> Purse(string legendName) =>
        legendName != null && _recruited.TryGetValue(legendName, out var record) && record.fragments != null ? record.fragments : EmptyPurse;

    private static readonly Dictionary<string, int> EmptyPurse = new Dictionary<string, int>();

    public int Rank(string legendName) => LegendGrowthRules.RankFor(Fragments(legendName));

    /// <summary>The Underdog meter: the Underdog Points of the legend's origin traits.</summary>
    public int UnderdogMeter(string legendName) => LyricalFragments.UnderdogMeter(Soul(legendName), LegendLore.Traits);

    /// <summary>An Underdog earns ×2 fragments for deeds against the odds (vault: "punch above your station").</summary>
    public bool IsUnderdog(string legendName) => LyricalFragments.IsUnderdog(UnderdogMeter(legendName), FragmentRules);

    /// <summary>What the legend's own council bonuses are multiplied by for its rank.</summary>
    public float Growth(string legendName) => IsRecruited(legendName) ? LegendGrowthRules.Multiplier(Rank(legendName)) : 1f;

    /// <summary>
    /// What the legend's own council bonuses are multiplied by: its rank's growth, its Composure (a Fractured or
    /// Spiraling Soul Leitmotif dims) and the Awakened State's surge while it lasts.
    /// </summary>
    public float CouncilMultiplier(string legendName) => Growth(legendName) * ComposureFactor(legendName);

    /// <summary>Composure's share of <see cref="CouncilMultiplier"/> (1 when calm, the surge while Awakened).</summary>
    public float ComposureFactor(string legendName)
    {
        var soul = Soul(legendName);
        if (soul == null || !IsRecruited(legendName)) return 1f;
        float factor = ComposureRules.CouncilFactor(ComposureRules.StateOf(soul.strain, Tuning), Tuning);
        return soul.surgeSevenths > 0 ? factor * Tuning.abyssSurge : factor;
    }

    public IReadOnlyList<string> Deeds(string legendName) => legendName != null && _recruited.TryGetValue(legendName, out var record) ? record.deeds : (IReadOnlyList<string>)Array.Empty<string>();

    public LegendBalladHistory BalladHistory(string legendName) => legendName != null && _recruited.TryGetValue(legendName, out var record)
        ? record.balladHistory ?? (record.balladHistory = new LegendBalladHistory()) : null;

    public void RecordBalladParticipation(string legendName, StoryNode story, string role)
    {
        if (!IsRecruited(legendName) || story == null || string.IsNullOrEmpty(story.ballad)) return;
        BalladHistory(legendName).Record(story.nodeName, story.ballad, story.verse, role, GameAge.Number);
    }

    public bool AwardLesserOpus(string legendName, LesserOpusDefinition definition, StoryNode story, bool finale)
    {
        if (!IsRecruited(legendName) || story == null || !BalladHistory(legendName).Resolve(definition,
            story.nodeName, story.ballad, story.verse, finale, GameAge.Number)) return false;
        _recruited[legendName].deeds.Add("Lesser Opus: " + definition.name + " — " + definition.description);
        Changed?.Invoke();
        NotificationFeed.Push(legendName + ": " + definition.name, "A Lesser Opus is recorded in this legend's history. Its title remains locked.",
            NotificationFeed.Topic.Council, NotificationFeed.OpenGovernment, "opus:" + legendName + ":" + definition.id);
        return true;
    }

    /// <summary>Who a met legend is (null for a legend not met).</summary>
    public LegendSoul Soul(string legendName)
    {
        if (legendName == null || !_recruited.TryGetValue(legendName, out var record)) return null;
        return EnsureSoul(legendName, record);
    }

    public ComposureState Composure(string legendName)
    {
        var soul = Soul(legendName);
        return soul != null ? ComposureRules.StateOf(soul.strain, Tuning) : ComposureState.Clouded;
    }

    /// <summary>A met legend's seven binding scores: its soul's, and what its Lyrical Fragments have grown.</summary>
    public Dictionary<string, int> Bindings(string legendName)
    {
        var scores = LegendSoulRules.Bindings(Soul(legendName), LegendLore.Traits, LegendLore.SoulTuning);
        foreach (var bonus in LyricalFragments.BindingBonuses(Purse(legendName), FragmentRules.fragmentsPerBindingPoint))
        {
            scores.TryGetValue(bonus.Key, out int score);
            scores[bonus.Key] = score + bonus.Value;
        }
        return scores;
    }

    /// <summary>Legends not met yet, in the order they are found.</summary>
    public List<LegendData> Unrecruited() => GameCatalog.Legends.All.Where(l => l != null && !_recruited.ContainsKey(l.legendName))
        .OrderBy(l => (int)l.rarity).ThenBy(l => l.legendName, StringComparer.OrdinalIgnoreCase).ToList();

    /// <summary>Meet the next legend not met yet (null when every legend is known).</summary>
    public LegendData RecruitNext(string how)
    {
        var legend = Unrecruited().FirstOrDefault();
        if (legend == null) return null;
        Recruit(legend, how);
        return legend;
    }

    public bool Recruit(LegendData legend, string how)
    {
        // A legend already met, or lost to Dissonance, is never met again.
        if (legend == null || _recruited.ContainsKey(legend.legendName)) return false;
        var record = AddRecord(legend.legendName);
        if (!string.IsNullOrEmpty(how)) record.deeds.Add(how);
        GameLog.Event($"{legend.legendName} joins the civilization ({how})", Log);
        Recruited?.Invoke(legend);
        NotifyRosterChanged();
        return true;
    }

    /// <summary>
    /// A legend earns Lyrical Fragments for a deed (an Underdog earns ×2 for one <paramref name="againstTheOdds"/>).
    /// A new rank re-applies the council so the legend's bonuses grow, and is a moment of joy. Returns what was earned.
    /// </summary>
    public List<FragmentAward> Award(string legendName, IEnumerable<FragmentAward> reward, string deed, bool againstTheOdds = false)
    {
        var earned = new List<FragmentAward>();
        if (reward == null || legendName == null || !_recruited.TryGetValue(legendName, out var record) || record.lost) return earned;
        if (record.fragments == null) record.fragments = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        float multiplier = LyricalFragments.Multiplier(IsUnderdog(legendName), againstTheOdds, FragmentRules);
        int before = LegendGrowthRules.RankFor(LyricalFragments.Total(record.fragments));
        foreach (var award in LyricalFragments.Scale(reward, multiplier))
        {
            int changed = LyricalFragments.Add(record.fragments, award.kind, award.amount);
            if (changed != 0) earned.Add(new FragmentAward(award.kind, changed));
        }
        if (earned.Count == 0) return earned;
        if (!string.IsNullOrEmpty(deed)) record.deeds.Add(deed);
        int after = LegendGrowthRules.RankFor(LyricalFragments.Total(record.fragments));
        GameLog.Event($"{legendName}: {string.Join(", ", earned.Select(a => $"{a.amount:+0;-0} {a.kind}"))}{(multiplier > 1f ? " (Underdog)" : string.Empty)} ({LyricalFragments.Total(record.fragments)} fragments, rank {after}) for \"{deed}\"", Log);
        FragmentsEarned?.Invoke(legendName, earned, deed);
        if (after != before)
        {
            var government = GovernmentLogic.Instance;
            if (government != null) government.MarkCouncilDirty();
            if (after > before)
            {
                RankedUp?.Invoke(legendName, after);
                Rejoice(legendName, record);
            }
        }
        // Binding scores grew too: seated legends' bonuses and expeditions read them.
        else if (GovernmentLogic.Instance != null && GovernmentLogic.Instance.GetSeatWithLegend(legendName) != null) GovernmentLogic.Instance.MarkCouncilDirty();
        Changed?.Invoke();
        return earned;
    }

    public List<FragmentAward> Award(string legendName, FragmentKind kind, int amount, string deed, bool againstTheOdds = false) =>
        Award(legendName, new[] { new FragmentAward(kind, amount) }, deed, againstTheOdds);

    /// <summary>Every legend seated on the council (Head of State included) earns fragments for a deed of the whole council.</summary>
    public void HonourCouncil(IEnumerable<FragmentAward> reward, string deed, bool againstTheOdds = false)
    {
        var government = GovernmentLogic.Instance;
        if (government == null || reward == null) return;
        var list = reward.ToList();
        foreach (var (_, legend) in government.GetAllAssignedLegends().ToList()) Award(legend.legendName, list, deed, againstTheOdds);
    }

    /// <summary>The seated legends, the Head of State first.</summary>
    public static List<string> Council()
    {
        var government = GovernmentLogic.Instance;
        if (government == null) return new List<string>();
        return government.GetAllAssignedLegends().Select(s => s.legend.legendName).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    /// <summary>
    /// The fragment consequence (<c>fragment:Who Kind +N</c>; <c>renown:Who +N</c> is the older form, paid in Meaning):
    /// who is a legend's name, <c>council</c>, or a ballad actor role (<see cref="BalladActors.Targets"/>).
    /// A named legend not met yet joins the civilization.
    /// </summary>
    public void ApplyFragments(IEnumerable<string> legends, FragmentKind kind, int amount, string storyTitle)
    {
        string deed = $"Remembered in \"{storyTitle}\"";
        foreach (var name in (legends ?? Enumerable.Empty<string>()).Where(n => !string.IsNullOrEmpty(n)).Distinct(StringComparer.OrdinalIgnoreCase).ToList())
        {
            if (!IsRecruited(name) && !IsLost(name) && GameCatalog.Legends.TryGet(name, out var legend)) Recruit(legend, deed);
            Award(name, kind, amount, deed);
        }
    }

    // ===== COMPOSURE =====

    // Once a Seventh: the council carries the Age Crisis and mourns the dead; everyone else rests.
    private void OnSeventh(int _)
    {
        if (SaveSession.Restoring || _recruited.Count == 0) return;
        var government = GovernmentLogic.Instance;
        var ages = AgeProgression.Instance;
        var pop = PopGrowthLogic.Instance;
        var world = WorldSystem.Instance != null && WorldSystem.Instance.Map != null ? WorldSystem.Instance : null;
        int dead = Dead(pop);
        int people = pop != null ? pop.population + pop.vagrants : 0;
        bool dirty = false;
        dirty |= TickService();

        foreach (var entry in _recruited.Where(r => !r.Value.lost).ToList())
        {
            string name = entry.Key;
            var record = entry.Value;
            var soul = EnsureSoul(name, record);
            // Legends on an expedition carry the road instead of the council (they have left their seats).
            WorldUnit expedition = null;
            bool resting = false;
            float hardship = world != null ? world.HardshipOf(name, out expedition, out resting) : 0f;
            var context = new ComposureContext
            {
                seated = government != null && government.GetSeatWithLegend(name) != null,
                onExpedition = expedition != null,
                restingAtSettlement = resting,
                hardship = hardship,
                solace = expedition != null ? Expeditions.Solace(UnitSurroundings.Of(world.Map, expedition), world.ExpeditionRules) : 0f,
                crisisBegun = ages != null && ages.CrisisBegun,
                crisisDeclared = ages != null && ages.CrisisDeclared,
                griefShare = ComposureRules.GriefShare(record.deathsSeen, dead, people),
            };
            record.deathsSeen = dead;
            dirty |= Apply(name, record, LegendSoulLife.Seventh(soul, context, name, LegendLore.Traits, WorldSeed, Tuning));
        }
        if (dirty && government != null) government.MarkCouncilDirty();
    }

    // A rank reached is an intense moment of joy (vault: "Momentarily, a Soul Leitmotif can become pristine").
    private void Rejoice(string name, Record record)
    {
        var soul = EnsureSoul(name, record);
        if (Apply(name, record, LegendSoulLife.Shift(soul, -Tuning.rankJoy, name, LegendLore.Traits, WorldSeed, Tuning)) && GovernmentLogic.Instance != null)
            GovernmentLogic.Instance.MarkCouncilDirty();
    }

    /// <summary>Act on what happened to a soul: logs, events, achievements, fragments, the lost. True when the council must be re-applied.</summary>
    private bool Apply(string name, Record record, List<SoulEvent> events)
    {
        var soul = record.soul;
        foreach (var happening in events)
        {
            switch (happening.kind)
            {
                case SoulEvent.Kind.ComposureChanged:
                    GameLog.Event($"{name}'s Composure: {happening.from} -> {happening.to} (strain {soul.strain:0})", Log);
                    ComposureChanged?.Invoke(name, happening.from, happening.to);
                    // Legends are keyed by name until U03 gives them stable instance ids.
                    if (happening.Deeper) Achievements.Report(AchievementEvent.Of(AchievementSignal.ComposureFell, (int)happening.to).From($"composure:{name}:{happening.to}", name));
                    break;
                case SoulEvent.Kind.Collapsed:
                    GameLog.Event($"{name}'s Awakened State collapses (strain {soul.strain:0})", Log);
                    break;
                case SoulEvent.Kind.Awakened:
                    var result = happening.awakening;
                    if (result.abyss)
                    {
                        GameLog.Event($"{name} reaches the Catalytic Abyss of Emotion: the Awakened State", Log);
                        Achievements.Report(AchievementEvent.Of(AchievementSignal.CatalyticAbyss).From($"abyss:{name}:{soul.awakenings.Count}", name));
                    }
                    else
                    {
                        GameLog.Event($"{name}: {soul.awakenings.LastOrDefault()}", Log);
                        Achievements.Report(AchievementEvent.Of(AchievementSignal.MotifAwakened, result.ornament, result.afterSpiraling && result.Evolved)
                            .From($"awakening:{name}:{soul.awakenings.Count}", name));
                    }
                    Awakened?.Invoke(name, result);
                    Award(name, result.abyss ? FragmentRules.catalyticAbyss : FragmentRules.motifAwakening,
                        result.abyss ? "The Catalytic Abyss of Emotion" : $"Motif Awakening to {result.element}", againstTheOdds: result.afterSpiraling);
                    Changed?.Invoke();
                    break;
                case SoulEvent.Kind.Lost:
                    Lose(name, record);
                    return true;
            }
        }
        return events.Count > 0;
    }

    // Surrender: the Soul Leitmotif becomes a Dissonance Core. The legend leaves the council, its unit and the roster.
    private void Lose(string name, Record record)
    {
        record.lost = true;
        record.deeds.Add("Surrendered to Dissonance");
        var government = GovernmentLogic.Instance;
        var seat = government != null ? government.GetSeatWithLegend(name) : null;
        if (seat != null) government.RemoveLegendFromSeat(seat.seatIndex, bypassCooldown: true);
        GameLog.Event($"{name}'s Soul Leitmotif reached Surrender: lost to Dissonance", Log);
        Lost?.Invoke(name);
        // On the road, its companions watched it fall.
        if (WorldSystem.Instance != null && WorldSystem.Instance.Map != null) WorldSystem.Instance.OnLegendLost(name);
        NotifyRosterChanged();
    }

    /// <summary>
    /// Add strain to a legend's Composure from something that happened to it (a mishap on the road, a companion's fall);
    /// it may crack, spiral or break it, as any strain does.
    /// </summary>
    public void Strain(string legendName, float amount, string cause)
    {
        if (amount <= 0f || legendName == null || !_recruited.TryGetValue(legendName, out var record) || record.lost) return;
        var soul = EnsureSoul(legendName, record);
        GameLog.Event($"{legendName}: +{amount:0.#} strain ({cause})", Log);
        if (Apply(legendName, record, LegendSoulLife.Shift(soul, amount, legendName, LegendLore.Traits, WorldSeed, Tuning)) && GovernmentLogic.Instance != null)
            GovernmentLogic.Instance.MarkCouncilDirty();
        Changed?.Invoke();
    }

    // ===== RECORDS =====

    /// <summary>Everyone who has died: citizens (the true count, not the revisable ledger) and the homeless.</summary>
    private static int Dead(PopGrowthLogic pop) => pop != null ? pop.trueDeaths + pop.vagrantDeaths : 0;

    /// <summary>The world the souls are drawn for (0 before a world exists: tests and scenes without a save).</summary>
    private static int WorldSeed => SaveSession.GenerationSeed ?? (WorldSystem.Instance != null && WorldSystem.Instance.Map != null ? WorldSystem.Instance.Map.seed : 0);

    private LegendSoul EnsureSoul(string legendName, Record record)
    {
        if (record.soul != null) return record.soul;
        var legend = GameCatalog.Legends.TryGet(legendName, out var data) ? data : null;
        var template = legend != null ? LegendSoulRules.Template.Of(legend) : new LegendSoulRules.Template { name = legendName };
        record.soul = LegendSoulRules.Create(template, LegendLore.Traits, LegendLore.SoulTuning, WorldSeed, Tuning.baseline);
        return record.soul;
    }

    private Record AddRecord(string legendName)
    {
        if (string.IsNullOrEmpty(legendName)) return null;
        if (!_recruited.TryGetValue(legendName, out var record))
        {
            // A newcomer mourns only the dead of its own time with the civilization.
            _recruited[legendName] = record = new Record { deathsSeen = Dead(PopGrowthLogic.Instance) };
            EnsureSoul(legendName, record);
        }
        return record;
    }

    private void NotifyRosterChanged()
    {
        var government = GovernmentLogic.Instance;
        if (government != null) government.OnLeaderPoolChanged?.Invoke();
        Changed?.Invoke();
    }
}
