using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// The life of the culture (numbers in <see cref="CultureLifeTuning"/>, arithmetic in <see cref="CultureLifeRules"/>
/// and <see cref="CultureNaming"/>; saved in <see cref="CultureState"/>):
/// - Unity: a resource the shared life makes, from song (the Weaver share), civics of music and rite, districts of song,
///   faith and pleasure and their landmarks, and in bursts from rites, festivals and holidays. Happy people make more.
///   It is spent to raise landmarks and to set holidays apart.
/// - Rites and gatherings: an evening of song, a rite of remembrance... held from the Culture window.
/// - Festivals: a cultural party (an expedition chartered for it, <see cref="ExpeditionCharter.CulturalParty"/>) holds
///   one in a settlement: Unity, morale, joy, eased Composure and the culture taking root around it.
/// - Holidays: with high morale and Unity to spend, the people set the day apart; it is kept on that Seventh of that
///   Phase of every Echo.
/// - Names: the culture names its hamlets, districts and landmarks in its own voice (its character and myth).
/// - The kitchen: recipes turn what the stores hold into dishes that feed more; standing orders cook every Seventh.
///   The cellar brews and distils beverages the same way (ales, meads, wines, spirits; never from an Eleos Bloom).
/// - Luxuries and happiness: once the people are many, living weighs as much as surviving.
/// </summary>
public partial class CultureSystem
{
    public const string UnitySource = "Culture: Song and rite", LandmarkSource = "Culture: Landmarks",
        HappinessSource = "Culture: Happiness", HolidaySource = "Culture: Holidays";

    private readonly CultureLifeTuning _lifeDefaults = new CultureLifeTuning();
    private readonly Dictionary<EnclaveFamily, float> _lived = new Dictionary<EnclaveFamily, float>();
    private string _lifeKey;
    private float _unityRate, _landmarkRate, _faithRate;

    /// <summary>The life's numbers as authored (Resources/Culture/Culture, else the code's defaults).</summary>
    public CultureLifeTuning AuthoredLife => Settings != null && Settings.life != null ? Settings.life : _lifeDefaults;

    /// <summary>The life's numbers with the people's invented dishes and drinks among its recipes and luxuries (CultureSystem.Invention.cs).</summary>
    public CultureLifeTuning Life => LifeWithInventions(AuthoredLife);
    public string UnityResource => string.IsNullOrEmpty(Life.unityResource) ? "Unity" : Life.unityResource;

    /// <summary>A holiday was set apart, or kept on its day.</summary>
    public event Action<Holiday> HolidayEstablished, HolidayKept;
    /// <summary>A festival was held in a settlement.</summary>
    public event Action<Settlement> FestivalHeld;

    // ===== READS =====

    /// <summary>How well the people live, 0-100 (surviving, and living once they are many).</summary>
    public float Happiness => _state.happiness;
    public float Joy => _state.joy;
    public float LuxuryMet => _state.luxury;
    public IReadOnlyList<LuxuryMet> LuxuriesMet => _state.luxuriesMet;
    public float UnityHeld => GameUnitsLogic.Instance != null ? GameUnitsLogic.Instance.GetResourceAmountExact(UnityResource) : 0f;
    public IReadOnlyList<Holiday> Holidays => _state.holidays;
    public IReadOnlyList<Landmark> Landmarks => _state.landmarks;
    public IReadOnlyList<CulturalName> Names => _state.names;
    public int FestivalsHeld => _state.festivalsHeld;
    public float DishesCooked => _state.dishesCooked;
    public float DrinksMade => _state.drinksMade;

    /// <summary>The culture as it names things now: its character, myth and what it calls itself.</summary>
    public NamingVoice Voice => new NamingVoice(Dominant ?? EnclaveFamily.Weaver, _state.myth, IsNamed ? Identity.adjective : null, IsNamed ? Identity.name : null);

    /// <summary>"the Iridians", or "your people" before the people are named.</summary>
    public static string PeopleWord => IsNamed ? "the " + CultureRules.Plural(Demonym) : "your people";

    /// <summary>The calendar now: the Seventh (1-21), the Phase of the Echo (1-3), the Echo (1-4), the Cycle and the Phase's and Echo's names.</summary>
    public (int seventh, int phase, int echo, int cycle, string phaseName, string echoName) Now
    {
        get
        {
            var time = _time != null ? _time : TimeSystemLogic.Instance;
            return time == null ? (1, 1, 1, 1, null, null)
                : (time.CurrentSeventh, time.CurrentPhase, time.CurrentEcho, time.CurrentCycle, time.CurrentPhaseUnit?.unitName, time.CurrentEchoUnit?.unitName);
        }
    }

    // ===== UNITY =====

    /// <summary>Unity per Seventh now, by where it comes from.</summary>
    public UnityBreakdown UnityPerSeventh()
    {
        if (!_state.founded) return new UnityBreakdown { multiplier = 1f };
        var life = Life;
        int civics = CivicManager.Instance == null ? 0 : life.unityCivics.Count(c => !string.IsNullOrEmpty(c) && CivicManager.Instance.IsCivicActive(c));
        var devs = new List<float>();
        var map = WorldSystem.Instance != null ? WorldSystem.Instance.Map : null;
        if (map != null)
            foreach (var s in map.Settlements)
            {
                var family = FamilyOfSettlement(s);
                if (family.HasValue && life.unityDistricts.Contains(family.Value)) devs.Add(s.development);
            }
        float landmarks = LandmarkSpecs().Sum(p => p.spec.unityPerSeventh);
        return CultureLifeRules.Unity(life, Leaning(EnclaveFamily.Weaver), string.Equals(_state.myth, "song", StringComparison.OrdinalIgnoreCase), civics, devs, landmarks, _state.happiness);
    }

    private void GainUnity(float amount)
    {
        if (amount <= 0f || GameUnitsLogic.Instance == null) return;
        GameUnitsLogic.Instance.ChangeResourceFromName(UnityResource, amount, false);
    }

    private bool Has(string resource, float amount) => GameUnitsLogic.Instance != null && GameUnitsLogic.Instance.GetResourceAmountExact(resource) + 1e-4f >= amount;

    private string WhyNotPay(IEnumerable<ResourceAmount> cost)
    {
        foreach (var c in cost ?? Enumerable.Empty<ResourceAmount>())
            if (c != null && c.amount > 0f && !Has(c.resource, c.amount))
                return $"Needs {c.amount:0.#} {c.resource} ({(GameUnitsLogic.Instance != null ? GameUnitsLogic.Instance.GetResourceAmountExact(c.resource) : 0f):0.#} held).";
        return null;
    }

    private void Pay(IEnumerable<ResourceAmount> cost)
    {
        foreach (var c in cost ?? Enumerable.Empty<ResourceAmount>())
            if (c != null && c.amount > 0f) GameUnitsLogic.Instance?.ChangeResourceFromName(c.resource, -c.amount, false);
    }

    public static string CostText(IEnumerable<ResourceAmount> cost, float foodValue = 0f)
    {
        var parts = (cost ?? Enumerable.Empty<ResourceAmount>()).Where(c => c != null && c.amount > 0f).Select(c => $"{c.amount:0.#} {c.resource}").ToList();
        if (foodValue > 0f) parts.Add($"{foodValue:0.#} food value");
        return parts.Count == 0 ? "free" : string.Join(", ", parts);
    }

    // What the culture lived this Seventh (rites, festivals, landmarks): its leanings drift toward it with what is eaten and worked.
    private void Lived(string family, float share = 1f)
    {
        if (!CultureRules.TryFamily(family, out var f)) return;
        _lived.TryGetValue(f, out float was);
        _lived[f] = was + Mathf.Max(0f, Life.livedPerRite) * share;
    }

    private void AddJoy(float amount) => _state.joy = Mathf.Clamp01(_state.joy + Mathf.Max(0f, amount));

    private static void Cheer(int morale, int sevenths, string source)
    {
        if (morale != 0) StatManager.Instance?.ApplyMoraleShift(morale, source, true, Mathf.Max(1, sevenths));
    }

    // ===== RITES AND GATHERINGS =====

    public ActivityRecord ActivityRecordOf(string id) => _state.activities.FirstOrDefault(a => string.Equals(a.id, id, StringComparison.OrdinalIgnoreCase));

    /// <summary>Sevenths until the rite can be held again (0: now).</summary>
    public int ActivityWait(CultureActivitySpec a)
    {
        var record = a != null ? ActivityRecordOf(a.id) : null;
        return record == null || record.count == 0 ? 0 : Mathf.Max(0, record.lastSeventh + a.cooldownSevenths - _state.sevenths);
    }

    /// <summary>Why the rite or gathering cannot be held now, or null.</summary>
    public string WhyNotActivity(CultureActivitySpec a)
    {
        if (a == null) return "No such rite.";
        if (!_state.founded) return "The culture has not been founded yet.";
        if (!string.IsNullOrEmpty(a.technology) && !Researched(a.technology)) return $"Research {a.technology} first.";
        if (!string.IsNullOrEmpty(a.civic) && (CivicManager.Instance == null || !CivicManager.Instance.IsCivicActive(a.civic)))
        {
            bool leans = a.orLeaning > 0f && CultureRules.TryFamily(a.family, out var family) && Leaning(family) + 1e-4f >= a.orLeaning;
            if (!leans) return a.orLeaning > 0f ? $"Only a people living by {a.civic}, or leaning {CultureRules.Percent(a.orLeaning)} {a.family}, holds it." : $"Only a people living by {a.civic} holds it.";
        }
        int wait = ActivityWait(a);
        if (wait > 0) return $"Held not long ago: {wait} more Seventh{(wait == 1 ? "" : "s")}.";
        return WhyNotPay(a.cost) ?? Pantry.WhyNotAfford(a.foodValue);
    }

    /// <summary>Hold a rite or gathering: pay it, then Unity, morale and joy. False (and nothing paid) when it cannot be held.</summary>
    public bool HoldActivity(string id)
    {
        var a = Life.Activity(id);
        string why = WhyNotActivity(a);
        if (why != null) { GameLog.Event($"{a?.name ?? id}: {why}", Log); return false; }
        if (a.foodValue > 0f && !Pantry.TrySpend(a.foodValue)) return false;
        Pay(a.cost);
        foreach (var c in a.cost.Where(c => c != null)) RecordUse(c.resource, c.amount);
        var record = ActivityRecordOf(a.id);
        if (record == null) _state.activities.Add(record = new ActivityRecord { id = a.id });
        bool first = record.count == 0;
        record.count++;
        record.lastSeventh = _state.sevenths;
        GainUnity(a.unity);
        Cheer(a.morale, a.moraleSevenths, a.name);
        AddJoy(a.joy);
        Lived(a.family);
        if (first) Remember("rite", $"The first {a.name}", a.description);
        RecordRite(a, record);
        GameLog.Event($"{a.name} held: +{a.unity:0.#} Unity.", Log);
        NotificationFeed.Push(a.name, $"{Capital(PeopleWord)} gathered: +{a.unity:0.#} Unity{(a.morale != 0 ? $", {a.morale:+0;-0} morale" : string.Empty)}.", NotificationFeed.Topic.Culture, CultureWindow.Open, $"culture:rite:{a.id}:{_state.sevenths}");
        RaiseChanged();
        return true;
    }

    // ===== FESTIVALS =====

    public FestivalRecord FestivalRecordOf(int settlement) => _state.festivals.FirstOrDefault(f => f.settlement == settlement);

    /// <summary>Food value a festival in <paramref name="s"/> sets on the table (more for a grown settlement).</summary>
    public float FestivalFoodValue(Settlement s) => Mathf.Max(0f, Life.festivalFoodValue + (s != null ? s.development : 0f) * Life.festivalFoodPerDevelopment);

    /// <summary>Whether cultural parties can set out (their technology researched and the culture founded).</summary>
    public bool FestivalsUnlocked => _state.founded && (string.IsNullOrEmpty(Life.festivalTechnology) || Researched(Life.festivalTechnology));

    /// <summary>Why no festival can be held in <paramref name="s"/> now, or null (its feast included).</summary>
    public string WhyNotFestival(Settlement s)
    {
        if (!_state.founded) return "The culture has not been founded yet: there is nothing of its own to celebrate.";
        if (s == null) return "A festival is held in one of your settlements.";
        var record = FestivalRecordOf(s.id);
        int wait = record == null || record.count == 0 ? 0 : record.lastSeventh + Life.festivalCooldownSevenths - _state.sevenths;
        if (wait > 0) return $"{s.name} celebrated not long ago: {wait} more Seventh{(wait == 1 ? "" : "s")}.";
        return Pantry.WhyNotAfford(FestivalFoodValue(s));
    }

    /// <summary>Set the festival's table (its food value from the stores) as the party begins. False when the stores cannot.</summary>
    public bool PrepareFestival(Settlement s) => WhyNotFestival(s) == null && Pantry.TrySpend(FestivalFoodValue(s));

    /// <summary>
    /// The festival a cultural party of <paramref name="legends"/> held in <paramref name="s"/> (its table already set):
    /// Unity, morale, joy, eased Composure and the culture taking root around it. Returns what the notice says.
    /// </summary>
    public string CelebrateFestival(Settlement s, IList<string> legends)
    {
        if (s == null || !_state.founded) return null;
        var life = Life;
        int members = legends?.Count ?? 0;
        float unity = Mathf.Max(0f, life.festivalUnity + life.festivalUnityPerLegend * members);
        GainUnity(unity);
        string festival = CultureNaming.Holiday(Voice, "festival", s.name, CultureNaming.Seed("festival", s.id, Identity.name));
        Cheer(life.festivalMorale, life.festivalMoraleSevenths, festival);
        AddJoy(life.festivalJoy);
        s.strain = Mathf.Max(0f, s.strain - Mathf.Max(0f, life.festivalRelief));
        var map = WorldSystem.Instance != null ? WorldSystem.Instance.Map : null;
        if (map != null)
            foreach (var t in HeldCells().Where(t => HexCoord.Distance(t.coord, s.coord) <= Mathf.Max(0, life.festivalRadius)))
                _state.presence[t.index] = Mathf.Clamp01(PresenceAt(t.index) + Mathf.Max(0f, life.festivalPresence));
        var record = FestivalRecordOf(s.id);
        if (record == null) _state.festivals.Add(record = new FestivalRecord { settlement = s.id });
        record.count++;
        record.lastSeventh = _state.sevenths;
        _state.festivalsHeld++;
        RecordFestival(s, record, legends, festival);
        Lived("Weaver", 0.6f);
        Lived("Indulgent", 0.4f);
        if (record.count == 1)
            Remember("festival", festival, $"{Capital(PeopleWord)} filled the streets of {s.name}{(members > 0 ? $", led by {string.Join(", ", legends)}" : string.Empty)}.");
        GameLog.Event($"{festival} in {s.name}: +{unity:0.#} Unity.", Log);
        FestivalHeld?.Invoke(s);
        string text = $"{festival}: {s.name} celebrates. +{unity:0.#} Unity, {life.festivalMorale:+0;-0} morale for {life.festivalMoraleSevenths} Sevenths, its Composure eased.";
        NotificationFeed.Push(festival, text, NotificationFeed.Topic.Culture, CultureWindow.Open, $"culture:festival:{s.id}:{record.count}");
        if (_state.festivalsHeld == 1 && _state.holidays.Count == 0)
            NotificationFeed.Push("A day to remember", $"Having learned to celebrate, {PeopleWord} could set a day apart as a holiday (Culture window: Holidays) once morale runs high.", NotificationFeed.Topic.Culture, CultureWindow.Open, "culture:holiday-hint");
        RaiseChanged();
        return text;
    }

    // ===== HOLIDAYS =====

    private int MoraleSurplus => StatManager.Instance != null ? StatManager.Instance.GetMorale() - StatManager.Instance.GetMoraleBalance() : 0;

    public float NextHolidayCost => CultureLifeRules.HolidayCost(_state.holidays.Count, Life);

    /// <summary>Why the people cannot set today apart as a holiday, or null.</summary>
    public string WhyNotHoliday()
    {
        var now = Now;
        return CultureLifeRules.WhyNotHoliday(Life, _state.founded, _state.festivalsHeld, MoraleSurplus, UnityHeld, _state.holidays,
            _state.sevenths - _state.lastHolidaySeventh, now.seventh, now.phase);
    }

    /// <summary>What today's holiday would be called and what it would remember (the latest moment of the heritage, or the people's joy).</summary>
    public (string name, string occasion, string kind) HolidayPreview()
    {
        var moment = _state.moments.LastOrDefault(m => m != null && m.kind != "holiday" && m.kind != "declined-food");
        string kind = moment?.kind ?? "joy";
        string subject = null;
        if (kind == "national-food") subject = _state.foodways.Where(f => f.national).OrderByDescending(f => f.nationalSeventh).FirstOrDefault()?.resource;
        else if (kind == "festival") subject = _state.festivals.OrderByDescending(f => f.lastSeventh).Select(f => SettlementName(f.settlement)).FirstOrDefault(n => n != null);
        else if (kind == "landmark") subject = _state.landmarks.LastOrDefault()?.name;
        var now = Now;
        string name = CultureNaming.Holiday(Voice, kind, subject, CultureNaming.Seed("holiday", Identity.name, now.seventh, now.phase, _state.holidays.Count), TakenNames());
        return (name, moment?.title ?? "the people's joy", kind);
    }

    /// <summary>
    /// Set today apart: it is recorded (the Seventh of the Phase, the Echo, the Cycle) and kept on that day of every Echo.
    /// Its Unity is spent and it is kept at once for the first time. Null when it cannot be (the reason is logged).
    /// </summary>
    public Holiday EstablishHoliday()
    {
        string why = WhyNotHoliday();
        if (why != null) { GameLog.Event("Holiday refused: " + why, Log); return null; }
        float cost = NextHolidayCost;
        GameUnitsLogic.Instance?.ChangeResourceFromName(UnityResource, -cost, false);
        var (name, occasion, _) = HolidayPreview();
        var now = Now;
        var h = new Holiday
        {
            name = name, occasion = occasion, seventh = now.seventh, phase = now.phase, echo = now.echo, cycle = now.cycle,
            phaseName = now.phaseName, echoName = now.echoName, established = _state.sevenths,
        };
        _state.holidays.Add(h);
        _state.lastHolidaySeventh = _state.sevenths;
        Remember("holiday", h.name, $"Set apart on {CultureCalendar.Established(h)} to remember {occasion}: kept on {CultureCalendar.Day(h)}.");
        GameLog.Event($"Holiday established: {h.name} ({CultureCalendar.Day(h)}), -{cost:0} Unity.", Log);
        HolidayEstablished?.Invoke(h);
        NotificationFeed.Push($"A holiday: {h.name}", $"{Capital(PeopleWord)} set this day apart to remember {occasion}. It returns on {CultureCalendar.Day(h)}.", NotificationFeed.Topic.Culture, CultureWindow.Open, "culture:holiday:" + h.name);
        KeepHoliday(h, now.cycle, now.echo);
        // Its observance (the calendar's one owner of its days) from today on: today's occasion was just kept.
        ObservanceRules.LinkHolidays(ObservanceData, _state.holidays, Stamp());
        ApplyLife();
        RaiseChanged();
        return h;
    }

    /// <summary>The holiday kept next, and in how many Sevenths (0: today), or null with none.</summary>
    public (Holiday holiday, int sevenths) NextHoliday()
    {
        var now = Now;
        var next = _state.holidays.Where(h => h != null).Select(h => (h, CultureCalendar.SeventhsUntil(h, now.seventh, now.phase))).OrderBy(p => p.Item2).FirstOrDefault();
        return next.h == null ? (null, 0) : next;
    }

    // A holiday on its day: the table (full joy when the stores can set it), Unity, morale, joy.
    private void KeepHoliday(Holiday h, int cycle, int echo)
    {
        var life = Life;
        int people = PopGrowthLogic.Instance != null ? PopGrowthLogic.Instance.population : 0;
        float feast = Mathf.Max(life.holidayFeastMinimum, life.holidayFeastPerHundred * people / 100f);
        bool table = Pantry.TrySpend(feast);
        float share = table ? 1f : 0.5f;
        GainUnity(life.holidayUnity * share);
        Cheer(Mathf.RoundToInt(life.holidayMorale * share), life.holidayMoraleSevenths, h.name);
        AddJoy(life.holidayJoy * share);
        Lived(Voice.voice.ToString(), 0.5f);
        h.kept++;
        h.lastKept = CultureCalendar.EchoKey(cycle, echo);
        RecordHoliday(h, cycle, echo, table);
        GameLog.Event($"{h.name} kept ({(table ? "a full table" : "the stores were bare")}).", Log);
        HolidayKept?.Invoke(h);
        if (h.kept > 1)
            NotificationFeed.Push(h.name, table ? $"{Capital(PeopleWord)} keep {h.name}: +{life.holidayUnity:0.#} Unity, {life.holidayMorale:+0} morale." : $"{Capital(PeopleWord)} keep {h.name} quietly: the stores could not set the table.",
                NotificationFeed.Topic.Culture, CultureWindow.Open, $"culture:kept:{h.name}:{cycle}:{echo}");
    }

    // ===== NAMES =====

    private string SettlementName(int id)
    {
        var map = WorldSystem.Instance != null ? WorldSystem.Instance.Map : null;
        return map?.Settlements.FirstOrDefault(s => s.id == id)?.name;
    }

    private HashSet<string> TakenNames()
    {
        var taken = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var map = WorldSystem.Instance != null ? WorldSystem.Instance.Map : null;
        if (map != null) foreach (var s in map.Settlements) if (!string.IsNullOrEmpty(s.name)) taken.Add(s.name);
        foreach (var l in _state.landmarks) if (!string.IsNullOrEmpty(l.name)) taken.Add(l.name);
        foreach (var h in _state.holidays) if (!string.IsNullOrEmpty(h.name)) taken.Add(h.name);
        return taken;
    }

    /// <summary>The name the culture gave <paramref name="settlement"/> last, or null.</summary>
    public CulturalName NameOf(int settlement) => _state.names.LastOrDefault(n => n.settlement == settlement);

    /// <summary>
    /// The culture names one of its own places: a new hamlet (<paramref name="district"/> empty) or a district it was
    /// just made into. The settlement takes the name (the old one is remembered). Null before the founding.
    /// </summary>
    public static string NameSettlement(Settlement s, string district, EnclaveFamily? family) => Instance != null ? Instance.GiveName(s, district, family) : null;

    public string GiveName(Settlement s, string district, EnclaveFamily? family)
    {
        if (!_state.founded || s == null || s.kind == SettlementKind.Capital) return null;
        var taken = TakenNames();
        taken.Remove(s.name ?? string.Empty);
        int times = _state.names.Count(n => n.settlement == s.id);
        string name = CultureNaming.District(Voice, family, CultureNaming.Seed("place", s.id, district ?? "hamlet", Identity.name, times), taken);
        _state.names.Add(new CulturalName { settlement = s.id, name = name, former = s.name, district = district, voice = Voice.voice, seventh = _state.sevenths });
        string former = s.name;
        s.name = name;
        if (family.HasValue) Remember("named-place", name, $"{Capital(PeopleWord)} named their new {family.Value} district {name} (once {former}).");
        GameLog.Event($"The culture names {former}: {name}.", Log);
        RaiseChanged();
        return name;
    }

    // ===== LANDMARKS =====

    /// <summary>The way of living a settlement's district belongs to (tributaries only), or null.</summary>
    public static EnclaveFamily? FamilyOfSettlement(Settlement s)
    {
        if (s == null || !WorldTributaries.IsTributary(s) || string.IsNullOrEmpty(s.district)) return null;
        var rules = WorldSystem.Instance != null ? WorldSystem.Instance.Rules?.tributaries : null;
        string enclave = rules?.District(s.district)?.enclave;
        return CultureRules.TryFamily(enclave, out var family) || CultureRules.TryFamily(s.district, out family) ? family : (EnclaveFamily?)null;
    }

    public IEnumerable<Landmark> LandmarksAt(int settlement) => _state.landmarks.Where(l => l.settlement == settlement);

    // Every landmark standing with its spec.
    private IEnumerable<(Landmark landmark, LandmarkSpec spec)> LandmarkSpecs() =>
        _state.landmarks.Select(l => (l, Life.Landmark(l.spec))).Where(p => p.Item2 != null);

    /// <summary>The landmark lines a settlement can raise: its district's (or a Religious Haven's faith line).</summary>
    public IEnumerable<LandmarkSpec> LandmarkLines(Settlement s)
    {
        if (s == null) return Enumerable.Empty<LandmarkSpec>();
        var family = FamilyOfSettlement(s);
        bool haven = s.kind == SettlementKind.Haven;
        return Life.landmarks.Where(l => l != null && ((family.HasValue && CultureRules.TryFamily(l.family, out var f) && f == family.Value) || (haven && l.haven)))
            .OrderBy(l => l.family).ThenBy(l => l.tier);
    }

    /// <summary>The next landmark each of the settlement's lines can raise (the first tier not standing).</summary>
    public IEnumerable<LandmarkSpec> NextLandmarks(Settlement s)
    {
        var standing = s == null ? new HashSet<string>() : new HashSet<string>(LandmarksAt(s.id).Select(l => l.spec), StringComparer.OrdinalIgnoreCase);
        return LandmarkLines(s).GroupBy(l => l.family).Select(g => g.OrderBy(l => l.tier).FirstOrDefault(l => !standing.Contains(l.id))).Where(l => l != null);
    }

    /// <summary>Why <paramref name="spec"/> cannot be raised in <paramref name="s"/> now, or null.</summary>
    public string WhyNotLandmark(Settlement s, LandmarkSpec spec)
    {
        if (!_state.founded) return "The culture has not been founded yet.";
        if (s == null || spec == null) return "Nothing to raise.";
        if (!LandmarkLines(s).Contains(spec)) return $"A {spec.name} belongs in {WorldTributaries.Article(spec.family + " District")}{(spec.haven ? " or a Religious Haven" : string.Empty)}.";
        var standing = LandmarksAt(s.id).Select(l => Life.Landmark(l.spec)).Where(l => l != null).ToList();
        if (standing.Any(l => l.id == spec.id)) return $"{s.name} already has its {spec.name}.";
        var before = Life.landmarks.Where(l => l != null && l.family == spec.family && l.tier < spec.tier).OrderByDescending(l => l.tier).FirstOrDefault();
        if (before != null && !standing.Contains(before)) return $"Raise its {before.name} first.";
        if (s.development + 1e-3f < spec.minDevelopment) return $"{s.name} needs {spec.minDevelopment:0} development (now {s.development:0}).";
        return WhyNotPay(spec.cost);
    }

    /// <summary>Raise a landmark: paid, named in the culture's voice, and remembered. Null when it cannot be.</summary>
    public Landmark RaiseLandmark(Settlement s, string specId)
    {
        var spec = Life.Landmark(specId);
        string why = WhyNotLandmark(s, spec);
        if (why != null) { GameLog.Event($"Landmark refused: {why}", Log); return null; }
        Pay(spec.cost);
        string name = CultureNaming.Landmark(Voice, spec.name, CultureNaming.Seed("landmark", s.id, spec.id, Identity.name), TakenNames());
        var landmark = new Landmark
        {
            settlement = s.id, spec = spec.id, name = name, seventh = _state.sevenths,
            ageId = AgeProgression.Instance != null && AgeProgression.Instance.Current != null ? AgeProgression.Instance.Current.id : null,
        };
        _state.landmarks.Add(landmark);
        RecordLandmark(s, landmark, spec);
        Lived(spec.family);
        AddJoy(0.1f);
        Remember("landmark", name, $"{Capital(PeopleWord)} raised a {spec.name.ToLowerInvariant()} in {s.name}. {spec.description}");
        GameLog.Event($"{name} raised in {s.name}.", Log);
        NotificationFeed.Push(name, $"A {spec.name.ToLowerInvariant()} rises in {s.name}: {(spec.unityPerSeventh > 0f ? $"+{spec.unityPerSeventh:0.#} Unity" : string.Empty)}{(spec.faithPerSeventh > 0f ? $", +{spec.faithPerSeventh:0.#} Faith" : string.Empty)} a Seventh.",
            NotificationFeed.Topic.Culture, CultureWindow.Open, "culture:landmark:" + name);
        ApplyLife();
        RaiseChanged();
        return landmark;
    }

    // ===== THE KITCHEN =====

    public bool KitchenCivic => CivicManager.Instance != null && Life.kitchenCivics.Any(c => !string.IsNullOrEmpty(c) && CivicManager.Instance.IsCivicActive(c));

    private static float Held(string resource) => GameUnitsLogic.Instance != null ? GameUnitsLogic.Instance.GetResourceAmountExact(resource) : 0f;

    /// <summary>A resource's food value in the stores (0 for spices and anything not stored food).</summary>
    public static float FoodValueOf(string resource)
    {
        var kind = Pantry.Instance != null && Pantry.Instance.Settings != null ? Pantry.Instance.Settings.Kind(resource) : null;
        return kind == null || !CultureRules.Feeds(kind.cuisine) ? 0f : Mathf.Max(0f, kind.foodValue);
    }

    public float YieldOf(RecipeSpec recipe) => CultureLifeRules.Yield(recipe, KitchenCivic, Life.kitchenCivicBonus);

    public int BatchesPossible(RecipeSpec recipe) => CultureLifeRules.Batches(recipe, Held);

    /// <summary>Why <paramref name="recipe"/> cannot be cooked now, or null.</summary>
    public string WhyNotCook(RecipeSpec recipe)
    {
        if (recipe == null) return "No such recipe.";
        if (!_state.founded) return "The culture has not been founded yet: no one keeps a Flavor Log.";
        if (!string.IsNullOrEmpty(recipe.technology) && !Researched(recipe.technology)) return $"Research {recipe.technology} first.";
        if (BatchesPossible(recipe) <= 0)
        {
            var missing = recipe.inputs.Where(i => i != null && Held(i.resource) + 1e-4f < i.amount).Select(i => $"{i.amount:0.#} {i.resource} ({Held(i.resource):0.#} held)");
            return "Needs " + string.Join(", ", missing) + ".";
        }
        return null;
    }

    /// <summary>
    /// Cook (or brew, or distil) up to <paramref name="batches"/> batches of a recipe: its inputs are used, its dishes or
    /// drinks stored. Returns the batches made.
    /// </summary>
    public int Cook(string recipeId, int batches = 1, bool announce = true)
    {
        var recipe = Life.Recipe(recipeId);
        if (WhyNotCook(recipe) != null || batches <= 0) return 0;
        int n = Mathf.Min(batches, BatchesPossible(recipe));
        if (n <= 0) return 0;
        var units = GameUnitsLogic.Instance;
        foreach (var input in recipe.inputs.Where(i => i != null && i.amount > 0f))
        {
            units.ChangeResourceFromName(input.resource, -input.amount * n, false);
            RecordUse(input.resource, input.amount * n);
        }
        float made = YieldOf(recipe) * n;
        string kind = recipe.InCellar ? "drink" : "dish", title = $"The first {recipe.dish}";
        bool first = !_state.moments.Any(m => m.kind == kind && m.title == title);
        units.ChangeResourceFromName(recipe.dish, made, false);
        if (recipe.InCellar) _state.drinksMade += made;
        else _state.dishesCooked += made;
        GainUnity(recipe.unity * n);
        RecordCooking(recipe, n);
        // The kitchen is the field's work; the cellar is the pleasure of the table.
        Lived(recipe.InCellar ? "Indulgent" : "Agromagical", 0.1f * n);
        if (first) Remember(kind, title, recipe.description);
        GameLog.Event($"{recipe.Done} {n} batch{(n == 1 ? "" : "es")} of {recipe.dish}: +{made:0.#}.", Log);
        if (announce) RaiseChanged();
        return n;
    }

    public int StandingOrder(string recipeId) => _state.orders.FirstOrDefault(o => string.Equals(o.recipe, recipeId, StringComparison.OrdinalIgnoreCase))?.batches ?? 0;

    /// <summary>Cook <paramref name="batches"/> of a recipe every Seventh, as far as the stores allow (0 cancels).</summary>
    public void SetStandingOrder(string recipeId, int batches)
    {
        batches = Mathf.Clamp(batches, 0, Mathf.Max(0, Life.maxStandingBatches));
        _state.orders.RemoveAll(o => string.Equals(o.recipe, recipeId, StringComparison.OrdinalIgnoreCase));
        if (batches > 0 && Life.Recipe(recipeId) != null) _state.orders.Add(new KitchenOrder { recipe = recipeId, batches = batches });
        RaiseChanged();
    }

    private void CookOrders()
    {
        foreach (var order in _state.orders.ToList()) Cook(order.recipe, order.batches, announce: false);
    }

    // ===== LUXURIES AND HAPPINESS =====

    // The people take what they want of each luxury this Seventh (once they are many enough to want any).
    private void ConsumeLuxuries()
    {
        int people = PopGrowthLogic.Instance != null ? PopGrowthLogic.Instance.population : 0;
        var world = WorldSystem.Instance;
        bool hungry = PopGrowthLogic.Instance != null && PopGrowthLogic.Instance.isFoodScarce;
        var plan = CultureLifeRules.Luxuries(Life, Held, people,
            CultureLifeRules.Amenities(world?.Map, world?.Settings?.generation, Life),
            resource => !hungry || FoodValueOf(resource) <= 0f);
        _state.luxuriesMet = plan.draws.Select(d => new LuxuryMet { category = d.category, met = d.Met }).ToList();
        _state.luxury = plan.score;
        _state.culturalFaith = plan.Faith;
        foreach (var draw in plan.draws)
            foreach (var (resource, amount) in draw.takes)
            {
                GameUnitsLogic.Instance?.ChangeResourceFromName(resource, -amount, false);
                RecordUse(resource, amount);
            }
        if (plan.Faith > 0f)
        {
            GameUnitsLogic.Instance?.ChangeResourceFromName("Faith", plan.Faith, false);
            Lived("Esoteric", Math.Min(0.2f, plan.Faith * 0.02f));
        }
    }

    /// <summary>What the people's happiness is read from now (luxuries as met last Seventh).</summary>
    public WellbeingInputs WellbeingNow()
    {
        var pop = PopGrowthLogic.Instance;
        var pantry = Pantry.Instance;
        float stored = pantry != null ? pantry.Value : 0f;
        float dishes = pantry != null ? pantry.Stock().Where(s => Life.IsDish(s.kind.resource)).Sum(s => s.Value) : 0f;
        return new WellbeingInputs
        {
            population = pop != null ? pop.population : 0,
            vagrants = pop != null ? pop.vagrants : 0,
            hungry = pop != null && pop.isFoodScarce,
            storedValue = stored,
            variety = pantry != null ? pantry.Variety : 0,
            luxury = _state.luxury,
            dishShare = stored > 0f ? dishes / stored : 0f,
            joy = _state.joy,
            landmarks = Mathf.Min(1f, LandmarkSpecs().Sum(p => p.spec.living)),
            sharedTables = HospitalityRules.LivingFrom(SharedTableCoverage(), HospitalityTuning),
        };
    }

    public Wellbeing CurrentWellbeing() => CultureLifeRules.Happiness(WellbeingNow(), Life);

    private void RefreshWellbeing()
    {
        var w = CurrentWellbeing();
        _state.happiness = w.happiness;
        _state.survival = w.survival;
        _state.living = w.living;
        if (!_state.livingAnnounced && w.weight > 0f && _state.founded)
        {
            _state.livingAnnounced = true;
            Remember("living", "More than bread", $"{Capital(PeopleWord)} are many now: surviving is no longer enough, they want to live (luxuries, fine dishes, festivals).");
            NotificationFeed.Push("More than bread", $"{Capital(PeopleWord)} are many now: luxuries, fine dishes and celebrations weigh in their happiness beside food and shelter.", NotificationFeed.Topic.Culture, CultureWindow.Open, "culture:living");
        }
    }

    // A Seventh of life: the kitchen's orders, luxuries, joy fading, holidays on their day, happiness, effects.
    private void LifeSeventh()
    {
        if (!_state.founded) return;
        CookOrders();
        ConsumeLuxuries();
        _state.joy = CultureLifeRules.Joy(_state.joy, 0f, Life.joyFade);
        KeepObservances();
        RefreshWellbeing();
        ApplyLife();
    }

    // Morale from happiness and max morale from the holidays kept, re-applied only when either changed; the rates of
    // Unity and Faith follow the length of a Seventh (checked every half second).
    private void ApplyLife()
    {
        if (!_state.founded) return;
        var life = Life;
        int morale = CultureLifeRules.Morale(_state.happiness, life);
        float maxMorale = life.holidayMaxMorale * _state.holidays.Count;
        string key = $"{morale}|{maxMorale}";
        if (key != _lifeKey)
        {
            _lifeKey = key;
            EffectRouter.ApplySet(HappinessSource, morale != 0 ? new List<GameEffect> { new GameEffect(GameEffectType.MoraleModifier, morale, ModifierType.Add) } : new List<GameEffect>());
            EffectRouter.ApplySet(HolidaySource, maxMorale > 0f ? new List<GameEffect> { new GameEffect(GameEffectType.MaxMoraleModifier, maxMorale, ModifierType.Add) } : new List<GameEffect>());
        }
        UpdateLifeRates();
    }

    private void UpdateLifeRates()
    {
        var production = GlobalProductionManager.Instance;
        var units = GameUnitsLogic.Instance;
        if (production == null || units == null || units.storageTab == null) return;
        float perSeventh = _time != null ? (_time.canTrackTime ? _time.GetEffectiveSecondsPerSeventh() : _time.BaseSecondsPerSeventh) : 180f;
        var u = UnityPerSeventh();
        float unity = (u.song + u.civics + u.districts) * u.multiplier / Mathf.Max(1f, perSeventh);
        float landmarks = u.landmarks * u.multiplier / Mathf.Max(1f, perSeventh);
        float faith = LandmarkSpecs().Sum(p => p.spec.faithPerSeventh) / Mathf.Max(1f, perSeventh);
        var slot = units.GetResourceSlotFromName(UnityResource);
        if (slot == null && (unity > 0f || landmarks > 0f)) { units.ChangeResourceFromName(UnityResource, 0f, false); slot = units.GetResourceSlotFromName(UnityResource); }
        if (slot != null && Life.unityCapacity > 0f && !Mathf.Approximately(slot.maxAmount, Life.unityCapacity)) slot.maxAmount = Life.unityCapacity;
        if (Mathf.Abs(unity - _unityRate) > 1e-6f) { _unityRate = unity; production.SetFlatRate(UnityResource, UnitySource, unity); }
        if (Mathf.Abs(landmarks - _landmarkRate) > 1e-6f) { _landmarkRate = landmarks; production.SetFlatRate(UnityResource, LandmarkSource, landmarks); }
        if (Mathf.Abs(faith - _faithRate) > 1e-6f)
        {
            _faithRate = faith;
            if (faith > 0f && units.GetResourceSlotFromName("Faith") == null) units.ChangeResourceFromName("Faith", 0f, false);
            production.SetFlatRate("Faith", LandmarkSource, faith);
        }
    }

    // After a load (or the founding) happiness is read once, before the first Seventh.
    private void CheckLife()
    {
        if (!_state.founded) return;
        if (_state.happiness <= 0f && _state.survival <= 0f) RefreshWellbeing();
        ApplyLife();
    }

    private static string Capital(string s) => string.IsNullOrEmpty(s) ? s : char.ToUpperInvariant(s[0]) + s.Substring(1);

    // ===== STORIES =====

    private float LifeValue(string t)
    {
        switch (t)
        {
            case "unity": return Mathf.Round(UnityHeld);
            case "happiness": return Mathf.Round(_state.happiness);
            case "joy": return Mathf.Round(_state.joy * 100f);
            case "luxury": return Mathf.Round(_state.luxury * 100f);
            case "holidays": return _state.holidays.Count;
            case "festivals": return _state.festivalsHeld;
            case "landmarks": return _state.landmarks.Count;
            case "dishes": return Mathf.Round(_state.dishesCooked);
            case "drinks": return Mathf.Round(_state.drinksMade);
            default: return float.NaN;
        }
    }
}
