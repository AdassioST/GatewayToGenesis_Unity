using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// The government's Edicts: its standing laws (stances) and its decrees (edicts). Rules in <see cref="EdictRules"/>,
/// content in <see cref="EdictCatalog"/>, numbers in <see cref="EdictTuning"/>; created by <see cref="GenesisLoop"/>,
/// saved whole (<see cref="_state"/> in GameSnapshot.Schema), shown in the Government tab (<see cref="EdictsSection"/>).
///
/// - Established once the council holds <see cref="EdictTuning.minCouncilSeats"/> seats counting the Head of State (the
///   third opens with Call and Response). Until then the realm keeps its customs: nothing here applies.
/// - Stances: one option always in force per stance. Each leans a pillar (moving the political compass, so the laws
///   shape the government type), gives its effects, and sets levers other systems read (<see cref="Levers"/>:
///   arrivals, the roofless, births, caravans, and the Realm's border policy). A changed stance stands a Phase.
/// - Edicts: decrees sealed by the Head of State for a cost, filling one slot each (seats minus two), in force for a
///   while, then resting. The legend answering for an edict's council area makes it stronger.
/// - Accord: seated legends approve of the laws their seats' areas favor and object to those they oppose, and the
///   government's compass weighs in; harmony strengthens the legends, discord costs morale and slows stance changes.
/// Stories read it through the "edicts", "stance" and "edict" condition domains.
/// </summary>
public class EdictSystem : SingletonBehaviour<EdictSystem>
{
    private const LogChannel Log = LogChannel.Council;
    public const string StanceSource = "Edicts: Stances", DecreeSource = "Edicts: Decrees", AccordSource = "Edicts: Council Accord";

    public EdictSettings Settings { get; private set; }
    public EdictTuning Tuning => Settings != null && Settings.tuning != null ? Settings.tuning : _defaults;

    // Saved (GameSnapshot.Schema).
    private EdictState _state = new EdictState();

    private readonly EdictTuning _defaults = new EdictTuning();
    private GovernmentLogic _government;
    private string _appliedKey;
    private bool _dirty = true;
    private float _checkAt;

    /// <summary>Anything about the edicts changed (established, a stance, an edict sealed or ended, the accord).</summary>
    public event Action Changed;

    // ===== STATIC READS (safe with no edicts in the scene) =====

    public static bool IsEstablished => Instance != null && Instance._state.established;

    /// <summary>The numbers the laws in force set for other systems; neutral before the edicts are established.</summary>
    public static EdictLevers Levers => Instance != null ? EdictRules.Levers(Instance._state) : EdictLevers.Neutral;

    // ===== READS =====

    public EdictState State => _state;
    public int Seats => _government != null ? EdictRules.Seats(_government.GetUnlockedSeatCount()) : 1;
    public int Capacity => EdictRules.Capacity(Seats, Tuning);
    public int SeatsNeeded => Mathf.Max(0, Tuning.minCouncilSeats - Seats);
    public StanceOption InForce(StanceDefinition stance) => EdictRules.InForce(_state, stance);
    public int StanceCooldown(string stanceId) => EdictRules.Cooldown(_state, stanceId, true);
    public int EdictCooldown(string edictId) => EdictRules.Cooldown(_state, edictId, false);
    public ActiveEdict Active(string edictId) => _state.active.FirstOrDefault(a => string.Equals(a.id, edictId, StringComparison.OrdinalIgnoreCase));
    public IReadOnlyList<DecreeMoment> Record => _state.record;
    public string HeadOfState => _government != null ? _government.HeadOfStateLegend : null;

    /// <summary>What each seated legend (Head of State aside: the one who decrees) thinks of the laws in force.</summary>
    public List<LegendOpinion> Opinions() => EdictRules.Opinions(_state, Judges());

    public float Accord()
    {
        var (x, y) = _government != null ? _government.GetCurrentCoordinates() : (0, 0);
        float accord = EdictRules.Accord(_state, Opinions(), x, y);
        // The culture's public memory (T09): a capped, explained part (CultureSystem.PublicAccord), only once edicts exist.
        return _state.established ? Mathf.Clamp(accord + CultureAccord, -100f, 100f) : accord;
    }

    /// <summary>The culture's part of the accord: what its public accounts and the records say of the promises in force (0 with no culture).</summary>
    public static float CultureAccord => CultureSystem.Instance != null ? CultureSystem.Instance.PublicAccord().points : 0f;

    public EdictRules.Mood Mood => EdictRules.MoodOf(Accord(), Tuning);

    /// <summary>How an option sits with the current government (+1 resonant, -1 against a pillar held firmly).</summary>
    public int Resonance(StanceOption option)
    {
        var (x, y) = _government != null ? _government.GetCurrentCoordinates() : (0, 0);
        return EdictRules.Resonance(option, x, y);
    }

    /// <summary>The legend who would answer for an edict's area, and the strength it would be sealed with.</summary>
    public (string legend, string seat, float strength) Answering(EdictDefinition edict)
    {
        if (edict == null || _government == null || string.IsNullOrEmpty(edict.area)) return (null, null, 1f);
        var answer = _government.AnswerFor(edict.area);
        return answer.Found ? (answer.legend, answer.seat, EdictRules.Strength(true, answer.distance, Tuning)) : (null, null, 1f);
    }

    public string WhyNotChange(string stanceId, string optionId) => EdictRules.WhyNotChange(_state, stanceId, optionId, Researched);

    public string WhyNotSeal(string edictId) => EdictRules.WhyNotSeal(_state, EdictCatalog.Edict(edictId), Capacity, HeadOfState, Researched, Affordable);

    // ===== LIFETIME =====

    protected override void OnSingletonAwake()
    {
        Settings = Resources.Load<EdictSettings>("Government/Edicts");
    }

    private void Start()
    {
        TimeSystemLogic.WhenReady(this, time => time.OnSeventhChange += OnSeventh);
    }

    protected override void OnSingletonDestroy()
    {
        if (TimeSystemLogic.Instance != null) TimeSystemLogic.Instance.OnSeventhChange -= OnSeventh;
        Unhook();
    }

    private void Hook()
    {
        if (_government != null || GovernmentLogic.Instance == null) return;
        _government = GovernmentLogic.Instance;
        _government.OnCouncilCompositionChanged += MarkDirty;
        _government.OnCouncilSeatChanged += OnSeatChanged;
        _government.OnCoordinatesChanged += OnCompassChanged;
    }

    private void Unhook()
    {
        if (_government == null) return;
        _government.OnCouncilCompositionChanged -= MarkDirty;
        _government.OnCouncilSeatChanged -= OnSeatChanged;
        _government.OnCoordinatesChanged -= OnCompassChanged;
        _government = null;
    }

    private void OnSeatChanged(CouncilSeat seat) => MarkDirty();
    private void OnCompassChanged(int x, int y) => MarkDirty();
    private void MarkDirty() => _dirty = true;

    private void Update()
    {
        Hook();
        if (Time.unscaledTime < _checkAt || SaveMenu.BlocksGameplay || SaveSession.Restoring) return;
        _checkAt = Time.unscaledTime + 0.5f;
        // A council that has grown enough (or a save loaded with one) establishes the edicts.
        if (!_state.established && _government != null && EdictRules.CanEstablish(Seats, Tuning)) Establish();
        if (_dirty) Apply();
    }

    // ===== ESTABLISHMENT =====

    /// <summary>The government begins to issue edicts: the realm's customs become its first stances. False when it already does.</summary>
    public bool Establish(string source = null)
    {
        if (_state.established) return false;
        _state.established = true;
        _state.sevenths = 0;
        _state.stances = EdictRules.Founding(WorldSystem.Instance != null ? WorldSystem.Instance.BorderPolicy : BorderPolicy.Measured);
        Remember("The Edicts are established", $"With {Seats} seats on the council, the government can now set its stances and seal edicts ({Capacity} slot{(Capacity == 1 ? "" : "s")}).");
        GameLog.Event($"Edicts established{(source != null ? $" ({source})" : string.Empty)}: {Seats} seats, {Capacity} edict slot(s).", Log);
        NotificationFeed.Push("The Edicts are established", "Your council can now set the realm's stances (the roofless, strangers, growth, the borders, the Pillars) and seal edicts. See the Government tab.",
            NotificationFeed.Topic.Council, EdictsSection.Open, "edicts:established");
        _dirty = true;
        Apply();
        return true;
    }

    // ===== STANCES =====

    /// <summary>Change a stance. False with <paramref name="why"/> when it cannot change now.</summary>
    public bool SetStance(string stanceId, string optionId, out string why)
    {
        why = WhyNotChange(stanceId, optionId);
        if (why != null) { GameLog.Event($"Stance change refused: {why}", Log); return false; }
        var stance = EdictCatalog.Stance(stanceId);
        var was = InForce(stance);
        var option = stance.Option(optionId);
        _state.stances[stance.id] = option.id;
        int cooldown = EdictRules.StanceCooldown(Tuning, Mood);
        if (cooldown > 0) _state.stanceCooldowns[stance.id] = cooldown;
        _state.decrees++;
        Remember($"{stance.title}: {option.title}", $"{(was != null ? $"No longer {was.title}. " : string.Empty)}{option.summary}");
        GameLog.Event($"Stance {stance.title}: {was?.title} → {option.title} (stands {cooldown} Sevenths).", Log);
        NotificationFeed.Push($"{stance.title}: {option.title}", option.summary, NotificationFeed.Topic.Council, EdictsSection.Open, "edicts:stance:" + stance.id);
        _dirty = true;
        Apply();
        return true;
    }

    /// <summary>
    /// The Realm panel's border policy. Before the edicts are established it is set directly; after, it is The Borders
    /// stance (its cooldown applies). False with <paramref name="why"/> when it cannot change now.
    /// </summary>
    public static bool RequestBorderPolicy(BorderPolicy policy, out string why)
    {
        why = null;
        if (!IsEstablished)
        {
            WorldSystem.Instance?.SetBorderPolicy(policy);
            return true;
        }
        var option = EdictCatalog.BorderOption(policy);
        if (option == null) { why = "No stance sets that policy."; return false; }
        return Instance.SetStance(EdictCatalog.Borders.id, option.id, out why);
    }

    // ===== EDICTS =====

    /// <summary>Seal an edict: pay its cost and put it in force. False with <paramref name="why"/> when it cannot be sealed now.</summary>
    public bool Seal(string edictId, out string why)
    {
        why = WhyNotSeal(edictId);
        if (why != null) { GameLog.Event($"Edict refused: {why}", Log); return false; }
        var edict = EdictCatalog.Edict(edictId);
        if (edict.cost > 0f && !string.IsNullOrEmpty(edict.costResource) && !Spend(edict.costResource, edict.cost))
        {
            why = $"Needs {edict.cost:0} {edict.costResource}.";
            return false;
        }
        var (legend, seat, strength) = Answering(edict);
        _state.active.Add(new ActiveEdict { id = edict.id, remaining = Mathf.Max(1, edict.duration), strength = strength, sealedBy = HeadOfState, sealedSeventh = _state.sevenths });
        _state.decrees++;
        string answered = legend != null ? $" {legend} ({seat}) carries it out: {strength:0.##}x strength." : string.Empty;
        Remember($"Edict: {edict.title}", $"Sealed by {HeadOfState} for {edict.duration} Sevenths.{answered}");
        GameLog.Event($"Edict {edict.title} sealed by {HeadOfState} for {edict.duration} Sevenths at {strength:0.##}x.", Log);
        NotificationFeed.Push($"Edict sealed: {edict.title}", $"{edict.summary}{answered}", NotificationFeed.Topic.Council, EdictsSection.Open, "edicts:seal:" + edict.id);
        _dirty = true;
        Apply();
        return true;
    }

    /// <summary>End an edict before its time; it rests as if it had run its course. False when it is not in force.</summary>
    public bool Revoke(string edictId)
    {
        var active = Active(edictId);
        if (active == null) return false;
        _state.active.Remove(active);
        var edict = EdictCatalog.Edict(active.id);
        if (edict != null && edict.cooldown > 0) _state.edictCooldowns[edict.id] = edict.cooldown;
        Remember($"Edict revoked: {edict?.title ?? active.id}", $"Ended with {active.remaining} Seventh{(active.remaining == 1 ? "" : "s")} left.");
        GameLog.Event($"Edict {edict?.title ?? active.id} revoked.", Log);
        _dirty = true;
        Apply();
        return true;
    }

    // ===== THE SEVENTH =====

    private void OnSeventh(int seventh)
    {
        if (!_state.established || SaveSession.Restoring) return;
        foreach (var id in EdictRules.Tick(_state))
        {
            var edict = EdictCatalog.Edict(id);
            Remember($"Edict ended: {edict?.title ?? id}", "It ran its course.");
            NotificationFeed.Push($"Edict ended: {edict?.title ?? id}", $"It can be sealed again in {edict?.cooldown ?? 0} Sevenths.", NotificationFeed.Topic.Council, EdictsSection.Open, "edicts:end:" + id);
        }
        _dirty = true;
        Apply();
    }

    // ===== EFFECTS =====

    // Re-applied from scratch only when something that feeds them changed (a restored save keeps them in the
    // ledgers, so the first check may re-apply them once). The borders follow The Borders stance.
    private void Apply()
    {
        _dirty = false;
        if (!_state.established)
        {
            if (_appliedKey == null) return;
            _appliedKey = null;
            EffectRouter.RemoveSource(StanceSource);
            EffectRouter.RemoveSource(DecreeSource);
            EffectRouter.RemoveSource(AccordSource);
            RaiseChanged();
            return;
        }
        var mood = Mood;
        string key = $"{string.Join(",", EdictRules.OptionsInForce(_state).Select(o => o.id))}|{string.Join(",", _state.active.Select(a => $"{a.id}@{a.strength:0.###}"))}|{mood}";
        SyncBorders();
        if (key == _appliedKey) { RaiseChanged(); return; }
        _appliedKey = key;
        EffectRouter.ApplySet(StanceSource, EdictRules.StanceEffects(_state));
        EffectRouter.ApplySet(DecreeSource, _state.active.SelectMany(EdictRules.EdictEffects).ToList());
        EffectRouter.ApplySet(AccordSource, EdictRules.MoodEffects(mood, Tuning));
        RaiseChanged();
    }

    private void SyncBorders()
    {
        var world = WorldSystem.Instance;
        var policy = EdictRules.Levers(_state).borders;
        if (world != null && policy.HasValue && world.BorderPolicy != policy.Value) world.SetBorderPolicy(policy.Value);
    }

    private void RaiseChanged() => Changed?.Invoke();

    // ===== STORIES =====

    /// <summary>A condition's value (GameValues "edicts", "stance", "edict"): see <see cref="EdictRules.Value"/>.</summary>
    public float Value(string domain, string target) => EdictRules.Value(_state, domain, target, Capacity, Accord());

    // ===== HELPERS =====

    private IEnumerable<EdictRules.Judge> Judges()
    {
        if (_government == null) yield break;
        foreach (var seat in _government.GetActiveRegularSeats())
        {
            if (seat == null || seat.assignedLegend == null) continue;
            yield return new EdictRules.Judge { legend = seat.assignedLegend.legendName, seat = seat.GetEffectiveTitle(), areas = seat.areas };
        }
    }

    private void Remember(string title, string text)
    {
        _state.record.Add(new DecreeMoment
        {
            title = title, text = text, seventh = _state.sevenths,
            ageId = AgeProgression.Instance != null && AgeProgression.Instance.Current != null ? AgeProgression.Instance.Current.id : null,
        });
        if (_state.record.Count > 60) _state.record.RemoveAt(0);
    }

    private static bool Researched(string technology) =>
        !string.IsNullOrEmpty(technology) && GameUnitsLogic.Instance != null && GameUnitsLogic.Instance.IsTechnologyUnlocked(technology);

    private static bool Affordable(string resource, float amount) =>
        GameUnitsLogic.Instance != null && GameUnitsLogic.Instance.GetResourceAmountExact(resource) >= amount;

    private static bool Spend(string resource, float amount)
    {
        var slot = GameUnitsLogic.Instance != null ? GameUnitsLogic.Instance.GetResourceSlotFromName(resource) : null;
        if (slot == null || slot.amount < amount) return false;
        slot.ChangeAmount(-amount);
        return true;
    }
}
