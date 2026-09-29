using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Public memory and civic legitimacy (T09; rules in <see cref="PublicMemoryRules"/>, saved in
/// <see cref="CultureExtensionState.publicMemory"/>). "What we commemorate can support our laws, or expose the gap
/// between our ideals and our actions."
///
/// - The council links a practised tradition (T01) to a promise in force: a stance of the Edicts or a civic
///   (<see cref="PublicMemoryTuning.promises"/>). Its first account is the council's.
/// - Each Seventh, what the culture recorded in the last 42 Sevenths is compared with it: for hospitality the tables set
///   (T05), the people admitted (T02) and the luxuries held back; for remembrance the dedications to the causes
///   remembered (T03) and how they were kept. Only records are read: what was never recorded stays unknown.
/// - A gap opens a dispute (two authored: The Hollow Table, The Loaf No One Bakes), told as a story (PublicMemory.ink)
///   and answerable in the Culture window: acknowledge, revise the promise, or let a Legend sponsor another account.
///   Each answer adds a version of the account; no table, admission, dedication, death or memory is changed.
/// - Places stand by what was recorded there, never by their customs or who lives there.
/// - The culture's part of the council's accord (<see cref="PublicAccord"/>) is capped at ±10 and explained term by term.
///   A promise no longer in force lapses its link: its effect ends, its history stays.
/// </summary>
public partial class CultureSystem
{
    private readonly PublicMemoryTuning _publicMemoryDefaults = new PublicMemoryTuning();
    private string _publicMemoryStory, _publicMemoryStoryDispute;

    public PublicMemoryTuning PublicMemoryTuning => _publicMemoryDefaults;

    private PublicMemoryState PublicMemory
    {
        get
        {
            var x = Extensions;
            if (x.publicMemory == null) x.publicMemory = new PublicMemoryState();
            return x.publicMemory;
        }
    }

    // ===== READS (snapshots) =====

    public IReadOnlyList<PublicLink> PublicLinks() => PublicMemory.links.Where(l => l != null).Select(l => l.Copy()).ToList();
    public IReadOnlyList<PublicAccount> AccountsOf(string link) => PublicMemory.AccountsOf(link).Select(a => a.Copy()).ToList();
    public PublicAccount CurrentAccount(string link) => PublicMemory.CurrentAccount(link)?.Copy();
    public IReadOnlyList<PublicDispute> PublicDisputes() => PublicMemory.disputes.Where(d => d != null).Select(d => d.Copy()).ToList();
    public PublicDispute PublicDispute(string id) => PublicMemory.Dispute(id)?.Copy();
    public IEnumerable<PublicDispute> OpenDisputes() => PublicMemory.disputes.Where(d => d != null && d.Open).Select(d => d.Copy()).ToList();

    /// <summary>The culture's part of the accord. Policy changes take effect immediately; this read changes no history.</summary>
    public (float points, IReadOnlyList<(string reason, float value)> terms) PublicAccord()
    {
        var accord = PublicMemoryRules.Accord(PublicMemory, PublicMemoryTuning, _state.sevenths, LinkInForce);
        return (accord.points, accord.terms);
    }

    private bool LinkInForce(PublicLink link) => link != null && link.Active &&
        PromiseInForce(PublicMemoryTuning.Promise(link.promise)) && Tradition(link.tradition) != null;

    /// <summary>What the records say about a link now, and how each place stands (nothing is changed).</summary>
    public (PromiseComparison comparison, List<LocalSupport> places) CompareNow(string link)
    {
        var l = PublicMemory.Link(link);
        var promise = l != null ? PublicMemoryTuning.Promise(l.promise) : null;
        if (promise == null) return (new PromiseComparison(), new List<LocalSupport>());
        var facts = Facts(l.tradition);
        return (PublicMemoryRules.Compare(promise, facts, PublicMemoryTuning), PublicMemoryRules.Local(promise, facts, PublicMemoryTuning, Contested(l)));
    }

    // A Legend's different account stands on this link and the records still contradict it.
    private bool Contested(PublicLink l)
    {
        var last = PublicMemory.disputes.Where(d => d.link == l.id && d.status == DisputeStatus.Resolved).OrderByDescending(d => d.resolvedSeventh).FirstOrDefault();
        return LinkInForce(l) && last != null && last.resolution == DisputeResolution.Sponsor &&
            last.resolvedSeventh <= _state.sevenths && _state.sevenths - last.resolvedSeventh <= PublicMemoryTuning.contestedSevenths;
    }

    // ===== WHAT WAS RECORDED =====

    // Every fact read from the other features' snapshots: tables (T05), admissions and memorials (occurrences), the
    // luxuries held back (T05's access report), the causes and dedications (T03). Nothing from the world's own truth.
    private PublicFacts Facts(string tradition)
    {
        var f = new PublicFacts { now = _state.sevenths };
        var view = Tradition(tradition);
        f.practiceLived = view != null && view.Lived;
        f.practiceName = view?.name ?? "The practice";
        foreach (var t in RecentTables(HospitalityTuning.tablesKept))
            f.tables.Add(new TableFact
            {
                key = t.key, place = t.place, patron = t.patron, policy = t.policy, settlement = t.settlement, seventh = t.seventh, luxuryShared = t.luxuryShared,
                privateLuxuries = t.policy == TablePolicy.PatronHosted ? t.served.Where(x => x.luxury).Select(x => x.name).Distinct().ToArray() : Array.Empty<string>(),
            });
        foreach (var o in Extensions.occurrences.recent)
        {
            if (o == null) continue;
            int seventh = o.stamp != null ? o.stamp.cultureSeventh : -1;
            if (o.kind == CulturalOccurrenceKind.Admission)
                f.admissions.Add(new AdmissionFact { key = o.key, settlement = o.settlement, place = o.SettlementKnown ? SettlementName(o.settlement) : null, seventh = seventh, people = (int)o.quantity, text = o.text });
            else if (o.kind == CulturalOccurrenceKind.Memorial && o.evidence != null && o.evidence.IsKnown)
                f.memorials.Add(new MemorialFact { key = o.key, evidence = o.evidence.id, settlement = o.settlement, place = o.SettlementKnown ? SettlementName(o.settlement) : null, seventh = seventh });
        }
        f.hoarded.AddRange(TableAccess().hoarded);
        foreach (var cause in MemoryCauses())
            f.causes.Add(new CauseFact { id = cause.id, title = cause.title, subject = cause.subject, cause = cause.cause, lastQuiet = MemoryPracticeOf(cause.id)?.lastQuiet ?? -1 });
        foreach (var d in MemorialDedications())
            f.dedications.Add(new DedicationFact { id = d.id, evidence = d.evidence, label = MemoryTargetLabel(d.target), status = d.status, lastPracticed = d.lastPracticed, dormantReason = d.dormantReason });
        var map = WorldSystem.Instance != null ? WorldSystem.Instance.Map : null;
        if (map != null) f.settlements.AddRange(map.Settlements.Where(s => s != null).OrderBy(s => s.id).Select(s => (s.id, s.name)));
        return f;
    }

    // ===== PROMISES =====

    /// <summary>Whether a promise's stance option or civic is the law now.</summary>
    public bool PromiseInForce(PromiseSpec p)
    {
        if (p == null) return false;
        if (p.source == PromiseSource.Civic) return CivicManager.Instance != null && CivicManager.Instance.IsCivicActive(p.civic);
        var stance = EdictCatalog.Stance(p.stance);
        return EdictSystem.IsEstablished && stance != null && string.Equals(EdictSystem.Instance.InForce(stance)?.id, p.option, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Why <paramref name="tradition"/> cannot be linked to <paramref name="promise"/> now, or null.</summary>
    public string WhyNotLinkPromise(string tradition, string promise)
    {
        if (!_state.founded) return "The culture has not been founded yet.";
        var p = PublicMemoryTuning.Promise(promise);
        var view = Tradition(tradition);
        if (view == null) return "No such tradition.";
        string why = PublicMemoryRules.WhyNotLink(PublicMemory, PublicMemoryTuning, p, tradition, view.definition, view.Lived, PromiseInForce(p), GameAge.Number, _state.sevenths);
        if (why != null) return why;
        return PublicMemoryTuning.linkUnity > 0f && !Has(UnityResource, PublicMemoryTuning.linkUnity) ? $"Needs {PublicMemoryTuning.linkUnity:0} Unity ({UnityHeld:0} held)." : null;
    }

    /// <summary>What linking would do (nothing is changed): the pledge, what it will be compared with, and what the records say now.</summary>
    public CultureCommandResult PreviewLinkPromise(string tradition, string promise)
    {
        string why = WhyNotLinkPromise(tradition, promise);
        if (why != null) return CultureCommandResult.Fail(why);
        var p = PublicMemoryTuning.Promise(promise);
        var view = Tradition(tradition);
        var now = PublicMemoryRules.Compare(p, Facts(tradition), PublicMemoryTuning);
        string against = p.kind == PromiseKind.Hospitality ? "the tables set, the people admitted and the luxuries held back" : "the dedications to the losses remembered, and whether they are kept";
        return CultureCommandResult.Ok($"The council says {view.name} shows {p.name}: \"{p.pledge}\" It costs {PublicMemoryTuning.linkUnity:0} Unity. Each Seventh it is compared with {against} over the last {PublicMemoryTuning.window} Sevenths; today the records show {now.supports} for and {now.contradicts} against. A promise the records bear out adds {PublicMemoryTuning.keptAccord:+0} to the council's accord; a gap opens a dispute ({PublicMemoryTuning.openDisputeAccord:+0;-0} while it waits).");
    }

    /// <summary>The council's decision: the tradition is said to show the promise (Unity paid now).</summary>
    public CultureCommandResult LinkPromise(string tradition, string promise)
    {
        var preview = PreviewLinkPromise(tradition, promise);
        if (!preview.succeeded) { GameLog.Event("Public memory refused: " + preview.reason, Log); return preview; }
        var p = PublicMemoryTuning.Promise(promise);
        var view = Tradition(tradition);
        var result = CultureCommandResult.Ok(preview.reason);
        if (PublicMemoryTuning.linkUnity > 0f)
        {
            var cost = new List<ResourceAmount> { new ResourceAmount { resource = UnityResource, amount = PublicMemoryTuning.linkUnity } };
            Pay(cost);
            result.paid.AddRange(cost);
        }
        var link = PublicMemoryRules.Link(PublicMemory, p, tradition, view.name, _state.sevenths, CurrentAgeId);
        link.last = PublicMemoryRules.Compare(p, Facts(tradition), PublicMemoryTuning);
        Remember("account", $"{view.name}: {p.name}", $"The council said {view.name} shows {p.name}: {p.pledge}");
        GameLog.Event($"Public memory: {link.id} links {view.name} to {p.name} ({result.PaidText}).", Log);
        RaiseChanged();
        return result;
    }

    /// <summary>Promises and whether each can be linked to <paramref name="tradition"/> now (null: it can).</summary>
    public List<(PromiseSpec promise, string why)> PromiseOptions(string tradition) =>
        PublicMemoryTuning.promises.Where(p => p != null).Select(p => (p, WhyNotLinkPromise(tradition, p.id))).ToList();

    // ===== ANSWERING A DISPUTE =====

    /// <summary>Legends who could sponsor a different account now (met, here), each with why not or null.</summary>
    public List<(string legend, string why)> SponsorOptions()
    {
        var legends = LegendProgress.Instance;
        if (legends == null) return new List<(string, string)>();
        return legends.RecruitedNames.OrderBy(n => n, StringComparer.OrdinalIgnoreCase).Select(n => (n, WhyLegendUnavailable(n))).ToList();
    }

    // The Head of State when present, else the first Legend who can speak: the sponsor a story's answer names.
    private string DefaultSponsor()
    {
        string head = GovernmentLogic.Instance != null ? GovernmentLogic.Instance.HeadOfStateLegend : null;
        if (!string.IsNullOrEmpty(head) && WhyLegendUnavailable(head) == null) return head;
        return SponsorOptions().FirstOrDefault(s => s.why == null).legend;
    }

    // Harmonic Quorum (Civic.md): a council that cannot reach Resonance cannot pass the policy.
    private string QuorumBlock()
    {
        if (CivicManager.Instance == null || !CivicManager.Instance.IsCivicActive(PublicMemoryTuning.quorumCivic) || EdictSystem.Instance == null || !EdictSystem.IsEstablished) return null;
        return EdictSystem.Instance.Mood == EdictRules.Mood.Discord ? "Under the Harmonic Quorum a council in discord cannot reach Resonance: it can only acknowledge." : null;
    }

    /// <summary>Why <paramref name="dispute"/> cannot be answered with <paramref name="resolution"/> now, or null.</summary>
    public string WhyNotResolve(string dispute, DisputeResolution resolution, string sponsor = null)
    {
        var d = PublicMemory.Dispute(dispute);
        if (d == null || !d.Open) return "No open dispute.";
        var spec = PublicMemoryTuning.Dispute(d.spec);
        if (spec == null) return "This dispute's account is no longer known.";
        if (!LinkInForce(PublicMemory.Link(d.link))) return "This promise is no longer linked to a tradition in force.";
        switch (resolution)
        {
            case DisputeResolution.Acknowledge:
                return spec != null && spec.acknowledgeUnity > 0f && !Has(UnityResource, spec.acknowledgeUnity) ? $"Needs {spec.acknowledgeUnity:0} Unity ({UnityHeld:0} held)." : null;
            case DisputeResolution.Revise:
                return QuorumBlock();
            case DisputeResolution.Sponsor:
                if (QuorumBlock() is string quorum) return quorum;
                sponsor = sponsor ?? DefaultSponsor();
                if (string.IsNullOrEmpty(sponsor)) return "No Legend is here to sponsor another account.";
                if (WhyLegendUnavailable(sponsor) is string away) return away;
                return spec != null && spec.sponsorUnity > 0f && !Has(UnityResource, spec.sponsorUnity) ? $"Needs {spec.sponsorUnity:0} Unity ({UnityHeld:0} held)." : null;
            default: return "Choose an answer.";
        }
    }

    /// <summary>What an answer would do (nothing is changed): its cost, the account's new words, and what stays as it was.</summary>
    public CultureCommandResult PreviewResolve(string dispute, DisputeResolution resolution, string sponsor = null)
    {
        string why = WhyNotResolve(dispute, resolution, sponsor);
        if (why != null) return CultureCommandResult.Fail(why);
        var d = PublicMemory.Dispute(dispute);
        var spec = PublicMemoryTuning.Dispute(d.spec);
        sponsor = resolution == DisputeResolution.Sponsor ? sponsor ?? DefaultSponsor() : null;
        var t = PublicMemoryTuning;
        string words = PublicMemoryRules.Words(resolution == DisputeResolution.Acknowledge ? spec?.acknowledgeText : resolution == DisputeResolution.Revise ? spec?.reviseText : spec?.sponsorText, d, sponsor);
        string effect = resolution == DisputeResolution.Acknowledge ? $"Costs {spec?.acknowledgeUnity ?? 0f:0} Unity. The link stands; the council's accord gains {t.acknowledgedAccord:+0} for {t.acknowledgedSevenths} Sevenths. Only newer records can dispute it again, after {t.disputeRest} Sevenths."
            : resolution == DisputeResolution.Revise ? $"Free. The link ends and its {t.keptAccord:+0} to the accord with it; the claim cannot be made again for {t.relinkRest} Sevenths."
            : $"Costs {spec?.sponsorUnity ?? 0f:0} Unity, spoken by {sponsor}. The link stands, but while the records still contradict it the account is contested ({t.contestedAccord:+0;-0} for up to {t.contestedSevenths} Sevenths) and places whose records disagree doubt it.";
        return CultureCommandResult.Ok($"\"{words}\" {effect} The records it answers stay as they are.");
    }

    /// <summary>Answer a dispute: pay, add the account's new version, and (revise) withdraw the link. The records are not touched.</summary>
    public CultureCommandResult ResolveDispute(string dispute, DisputeResolution resolution, string sponsor = null)
    {
        var preview = PreviewResolve(dispute, resolution, sponsor);
        if (!preview.succeeded) { GameLog.Event("Dispute answer refused: " + preview.reason, Log); return preview; }
        var d = PublicMemory.Dispute(dispute);
        var spec = PublicMemoryTuning.Dispute(d.spec);
        sponsor = resolution == DisputeResolution.Sponsor ? sponsor ?? DefaultSponsor() : null;
        float unity = resolution == DisputeResolution.Acknowledge ? spec?.acknowledgeUnity ?? 0f : resolution == DisputeResolution.Sponsor ? spec?.sponsorUnity ?? 0f : 0f;
        var result = CultureCommandResult.Ok(preview.reason);
        if (unity > 0f)
        {
            var cost = new List<ResourceAmount> { new ResourceAmount { resource = UnityResource, amount = unity } };
            Pay(cost);
            result.paid.AddRange(cost);
        }
        var account = PublicMemoryRules.Resolve(PublicMemory, d, spec, resolution, sponsor, result.paid, _state.sevenths, CurrentAgeId);
        if (resolution == DisputeResolution.Sponsor) d.places = CompareNow(d.link).places;
        Remember("account", $"{spec?.title ?? "A dispute"}: {PublicMemoryRules.ResolutionWord(resolution)}", account.text);
        GameLog.Event($"Public memory: {d.id} {PublicMemoryRules.ResolutionWord(resolution)} (account v{account.version}, {result.PaidText}).", Log);
        RaiseChanged();
        result.reason = account.text;
        return result;
    }

    // ===== THE SEVENTH =====

    /// <summary>
    /// Public memory's Seventh (<see cref="PublicMemoryFeature"/>, after the other features' records): links whose
    /// promise ended lapse; each active link is compared with the records; a new gap opens a dispute (once) and its story.
    /// </summary>
    internal void PublicMemorySeventh(CultureSeventh context)
    {
        var s = PublicMemory;
        PublicMemoryRules.Ensure(s);
        int now = context?.Now != null ? context.Now.cultureSeventh : _state.sevenths;
        foreach (var link in s.links.Where(l => l.Active).ToList())
        {
            var promise = PublicMemoryTuning.Promise(link.promise);
            string ended = promise == null ? "Its promise is no longer known." : !PromiseInForce(promise) ? $"{promise.name} is no longer in force." : Tradition(link.tradition) == null ? "Its tradition's record is gone." : null;
            if (ended != null)
            {
                PublicMemoryRules.Lapse(s, link, ended, now);
                PublicMemoryRules.AddAccount(s, link, AccountKind.Revised, "the council", $"{link.traditionName} no longer stands for {link.promiseName}: {ended}", null, now, CurrentAgeId);
                GameLog.Event($"Public memory: {link.id} lapsed ({ended}).", Log);
                context?.Notices.Add(($"{link.promiseName}: no longer in force", $"{link.traditionName} is no longer said to show it: {ended} What was said and recorded stays in the history.", $"culture:account:lapsed:{link.id}"));
                continue;
            }
            var facts = Facts(link.tradition);
            link.last = PublicMemoryRules.Compare(promise, facts, PublicMemoryTuning);
            if (!PublicMemoryRules.ShouldOpen(s, link, link.last, PublicMemoryTuning, now)) continue;
            var spec = PublicMemoryTuning.DisputeFor(promise.kind);
            if (spec == null) continue;
            var d = PublicMemoryRules.Open(s, spec, link, link.last, PublicMemoryRules.Local(promise, facts, PublicMemoryTuning, false), PublicMemoryTuning, now, CurrentAgeId);
            Remember("account", spec.title, PublicMemoryRules.Words(spec.summary, d));
            GameLog.Event($"Public memory: {d.id} opens ({spec.title}: {link.last.contradicts} records against, {link.last.supports} for).", Log);
            context?.Notices.Add((spec.title, PublicMemoryRules.Words(spec.summary, d) + " The council can acknowledge it, revise the promise, or let a Legend tell it another way (Culture window, Accounts).", $"culture:account:dispute:{d.id}"));
            TellDispute(spec);
        }
        PublicMemoryRules.Trim(s, PublicMemoryTuning);
    }

    // The dispute's story (PublicMemory.ink), unlocked while one is open; the panel answers it just as well.
    private void TellDispute(DisputeSpec spec, bool checkEvents = true)
    {
        var volumes = EventVolumeManager.Instance;
        if (spec == null || string.IsNullOrEmpty(spec.story) || volumes == null) return;
        if (volumes.UnlockStory(spec.story) && checkEvents) EventSystemLogic.Instance?.TriggerEventCheck();
    }

    /// <summary>After a load, the founding or a start: the records made whole, the accord worked out, open disputes' stories unlocked again. Nothing is granted.</summary>
    internal void ReconcilePublicMemory(CultureReconcileReason reason)
    {
        PublicMemoryRules.Ensure(PublicMemory);
        _publicMemoryStory = _publicMemoryStoryDispute = null;
        foreach (var d in PublicMemory.disputes.Where(x => x.Open)) TellDispute(PublicMemoryTuning.Dispute(d.spec), reason != CultureReconcileReason.Restored);
    }

    // ===== STORIES (read only) =====

    // The newest open dispute, of one kind when given.
    private PublicDispute NewestOpen(string spec = null) =>
        PublicMemory.disputes.Where(d => d != null && d.Open && (spec == null || string.Equals(d.spec, spec, StringComparison.OrdinalIgnoreCase))).OrderByDescending(d => d.openedSeventh).FirstOrDefault();

    /// <summary>
    /// A condition's value (routed from <see cref="Value"/>): accounts (active links), disputes (open),
    /// dispute:&lt;spec&gt; (1 while one is open), answer:&lt;spec&gt;:&lt;acknowledge|revise|sponsor&gt; (1 when it can be
    /// given now), promise:&lt;id&gt; (1 while in force), public_accord (the culture's part). NaN otherwise. Reads only.
    /// </summary>
    public float PublicMemoryValue(string key)
    {
        if (string.IsNullOrEmpty(key)) return float.NaN;
        var s = PublicMemory;
        if (key == "accounts") return s.links.Count(l => l != null && l.Active);
        if (key == "disputes") return s.disputes.Count(d => d != null && d.Open);
        if (key == "public_accord") return (float)Math.Round(PublicAccord().points);
        if (key.StartsWith("dispute:")) return NewestOpen(key.Substring(8).Trim()) != null ? 1f : 0f;
        if (key.StartsWith("promise:")) return PromiseInForce(PublicMemoryTuning.Promise(key.Substring(8).Trim())) ? 1f : 0f;
        if (key.StartsWith("answer:"))
        {
            var parts = key.Substring(7).Split(':');
            var d = parts.Length == 2 ? DisputeForAnswer(parts[0].Trim()) : null;
            return d != null && TryResolution(parts[1], out var r) && WhyNotResolve(d.id, r) == null ? 1f : 0f;
        }
        return float.NaN;
    }

    private static bool TryResolution(string word, out DisputeResolution r)
    {
        r = DisputeResolution.None;
        return !string.IsNullOrWhiteSpace(word) && Enum.TryParse(word.Trim(), true, out r) &&
            (r == DisputeResolution.Acknowledge || r == DisputeResolution.Revise || r == DisputeResolution.Sponsor);
    }

    /// <summary>A story's "culture:account &lt;spec&gt; &lt;acknowledge|revise|sponsor&gt;" consequence: answers the newest open dispute of that kind.</summary>
    public bool ApplyAccountConsequence(string argument)
    {
        var parts = (argument ?? string.Empty).Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2 || !TryResolution(parts[1], out var r)) return false;
        var d = DisputeForAnswer(parts[0]);
        return d != null && ResolveDispute(d.id, r).succeeded;
    }

    /// <summary>Bind once when a story opens; its text, choices and ending retain the same evidence.</summary>
    public void BeginPublicMemoryStory(string story)
    {
        _publicMemoryStory = story;
        _publicMemoryStoryDispute = PublicMemoryRules.StoryDispute(PublicMemory, PublicMemoryTuning, story)?.id;
    }

    private PublicDispute DisputeForAnswer(string spec)
    {
        var active = EventSystemLogic.Instance?.GetCurrentStoryNode()?.nodeName;
        if (active != null && active == _publicMemoryStory && PublicMemoryTuning.Dispute(spec)?.story == active)
            return PublicMemory.Dispute(_publicMemoryStoryDispute);
        return NewestOpen(spec);
    }

    /// <summary>
    /// Story words for the newest open dispute (read only): {dispute_tradition}, {dispute_promise}, {dispute_against}
    /// (its first contradicting record), {dispute_for} (its first supporting one, or none), {dispute_places},
    /// {dispute_sponsor}, {acknowledge_cost}, {sponsor_cost}.
    /// </summary>
    public string ExpandPublicMemory(string text)
    {
        if (string.IsNullOrEmpty(text) || text.IndexOf("{dispute_", StringComparison.Ordinal) < 0 && text.IndexOf("_cost}", StringComparison.Ordinal) < 0) return text;
        var active = EventSystemLogic.Instance?.GetCurrentStoryNode()?.nodeName;
        var d = active != null && active == _publicMemoryStory ? PublicMemory.Dispute(_publicMemoryStoryDispute)
            : active != null ? PublicMemoryRules.StoryDispute(PublicMemory, PublicMemoryTuning, active) : NewestOpen();
        var spec = d != null ? PublicMemoryTuning.Dispute(d.spec) : null;
        string against = d?.records.FirstOrDefault(r => r.bearing == RecordBearing.Contradicts)?.text ?? "nothing recorded against it";
        string support = d?.records.FirstOrDefault(r => r.bearing == RecordBearing.Supports)?.text ?? "nothing recorded for it";
        var places = d?.places.Where(p => p.stance != LocalStance.NoRecord).Select(p => $"{p.place} {PublicMemoryRules.StanceWord(p.stance)}").ToList() ?? new List<string>();
        return text.Replace("{dispute_tradition}", d?.traditionName ?? "the tradition").Replace("{dispute_promise}", d?.promiseName ?? "the promise")
            .Replace("{dispute_against}", against).Replace("{dispute_for}", support)
            .Replace("{dispute_places}", places.Count > 0 ? string.Join("; ", places) : "no place has a record of it either way")
            .Replace("{dispute_sponsor}", d?.sponsor ?? DefaultSponsor() ?? "no Legend")
            .Replace("{acknowledge_cost}", $"{spec?.acknowledgeUnity ?? PublicMemoryTuning.linkUnity:0}").Replace("{sponsor_cost}", $"{spec?.sponsorUnity ?? 0f:0}");
    }
}

/// <summary>Public memory's reads for the culture's queries: snapshots and previews only.</summary>
public partial interface ICultureQuery
{
    IReadOnlyList<PublicLink> PublicLinks();
    IReadOnlyList<PublicAccount> AccountsOf(string link);
    IReadOnlyList<PublicDispute> PublicDisputes();
    CultureCommandResult PreviewLinkPromise(string tradition, string promise);
    CultureCommandResult PreviewResolve(string dispute, DisputeResolution resolution, string sponsor = null);
}

/// <summary>Public memory in the Seventh's social effects: after the source actions and the traditions, so it compares this Seventh's records.</summary>
public sealed class PublicMemoryFeature : ICultureFeature
{
    public string Id => "public-memory";
    public CulturePhase Phase => CulturePhase.SocialEffects;
    public int Order => 60;

    public void Seventh(CultureSeventh context) => context?.Culture?.PublicMemorySeventh(context);

    public void Reconcile(CultureSystem culture, CultureReconcileReason reason) => culture?.ReconcilePublicMemory(reason);
}
