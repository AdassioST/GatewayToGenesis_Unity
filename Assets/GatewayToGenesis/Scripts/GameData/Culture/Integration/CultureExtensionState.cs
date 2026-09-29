using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// The culture's extension envelope, saved inside <see cref="CultureState.extensions"/> (optional, so saves made before
/// it load with a fresh one). It is partial: each culture feature declares its own saved record in its own file,
/// always [SaveOptionalField] and never moved afterwards, e.g.
/// <code>public partial class CultureExtensionState { [SaveOptionalField] public MemoryState memory = new MemoryState(); }</code>
/// The envelope itself owns the version, the shared occurrence ledger and the traditions (T01).
/// </summary>
public partial class CultureExtensionState
{
    /// <summary>The envelope's shape; <see cref="CultureMigration"/> brings older envelopes up to it.</summary>
    public const int CurrentVersion = 1;

    /// <summary>0: made before the envelope existed (an older save), or not yet migrated.</summary>
    [SaveOptionalField] public int version;
    [SaveOptionalField] public CultureOccurrenceLedger occurrences = new CultureOccurrenceLedger();
    [SaveOptionalField] public TraditionState traditions = new TraditionState();
}

/// <summary>A dedupe key and the Seventh (since the founding) it was accepted.</summary>
[Serializable]
public class ProcessedOccurrence
{
    public string key;
    public int seventh;
}

/// <summary>
/// Occurrences the culture observed: those committed since the last Seventh (waiting for it, saved so a save between
/// Sevenths loses none), a bounded list of the recent ones for the player, and the keys already accepted.
/// </summary>
[Serializable]
public class CultureOccurrenceLedger
{
    public List<CulturalOccurrence> pending = new List<CulturalOccurrence>();
    public List<CulturalOccurrence> recent = new List<CulturalOccurrence>();
    public List<ProcessedOccurrence> accepted = new List<ProcessedOccurrence>();
    /// <summary>Next number for keys that have nothing else unique in them (kitchen batches).</summary>
    public int serial;
    /// <summary>Occurrences accepted all told.</summary>
    public int total;
}

/// <summary>
/// The ledger's rules (pure): accept an occurrence once per key, hand the waiting ones to the Seventh, keep the recent
/// ones bounded and forget keys older than the dedupe window.
/// </summary>
public static class CultureOccurrences
{
    /// <summary>Sevenths a dedupe key is remembered (two Echoes: longer than any task or cooldown that could replay it).</summary>
    public const int DedupeWindow = 126;
    /// <summary>Most recent occurrences kept for the player.</summary>
    public const int RecentKept = 60;

    public static bool Seen(CultureOccurrenceLedger ledger, string key) =>
        ledger != null && !string.IsNullOrEmpty(key) && (ledger.accepted.Any(a => a != null && a.key == key) || ledger.pending.Any(p => p != null && p.key == key));

    /// <summary>
    /// Accept <paramref name="o"/> into the waiting list. False (nothing changed) with no key or a key already seen: a
    /// second report of one committed action never counts twice.
    /// </summary>
    public static bool Accept(CultureOccurrenceLedger ledger, CulturalOccurrence o, int seventh)
    {
        if (ledger == null || o == null || string.IsNullOrEmpty(o.key) || Seen(ledger, o.key)) return false;
        ledger.pending.Add(o);
        ledger.accepted.Add(new ProcessedOccurrence { key = o.key, seventh = seventh });
        ledger.total++;
        return true;
    }

    /// <summary>The waiting occurrences, in the order they were committed; the waiting list is emptied and they join the recent ones.</summary>
    public static List<CulturalOccurrence> Drain(CultureOccurrenceLedger ledger, int seventh)
    {
        var list = ledger != null ? ledger.pending.Where(o => o != null).ToList() : new List<CulturalOccurrence>();
        if (ledger == null) return list;
        ledger.pending.Clear();
        ledger.recent.AddRange(list);
        Prune(ledger, seventh);
        return list;
    }

    /// <summary>Keep the recent list bounded and forget keys older than the window.</summary>
    public static void Prune(CultureOccurrenceLedger ledger, int seventh)
    {
        if (ledger == null) return;
        if (ledger.recent.Count > RecentKept) ledger.recent.RemoveRange(0, ledger.recent.Count - RecentKept);
        ledger.accepted.RemoveAll(a => a == null || seventh - a.seventh > DedupeWindow);
    }

    public static int NextSerial(CultureOccurrenceLedger ledger) => ledger == null ? 0 : ++ledger.serial;
}

/// <summary>
/// Brings a culture's extension envelope up to <see cref="CultureExtensionState.CurrentVersion"/>. Idempotent: run
/// after every load and at the founding. It never invents history: an older save has no participants, occasions or
/// bearers to recover, so nothing is made up for them. Only what the old state recorded as a decision is linked (a
/// national food the people embraced gets its tradition record, marked as recorded before the ledger).
/// </summary>
public static class CultureMigration
{
    /// <summary>Make sure every part of the envelope exists (an older save, or a field missing from one).</summary>
    public static CultureExtensionState Ensure(CultureState state)
    {
        if (state == null) return null;
        if (state.extensions == null) state.extensions = new CultureExtensionState();
        var x = state.extensions;
        if (x.occurrences == null) x.occurrences = new CultureOccurrenceLedger();
        if (x.occurrences.pending == null) x.occurrences.pending = new List<CulturalOccurrence>();
        if (x.occurrences.recent == null) x.occurrences.recent = new List<CulturalOccurrence>();
        if (x.occurrences.accepted == null) x.occurrences.accepted = new List<ProcessedOccurrence>();
        if (x.traditions == null) x.traditions = new TraditionState();
        TraditionRules.Ensure(x.traditions);
        return x;
    }

    /// <summary>Upgrade the envelope; true when anything changed.</summary>
    public static bool Upgrade(CultureState state, TraditionTuning tuning)
    {
        var x = Ensure(state);
        if (x == null) return false;
        bool changed = false;
        if (state.founded) changed |= TraditionRules.LinkNationalFoods(state, x.traditions, tuning) > 0;
        if (x.version < CultureExtensionState.CurrentVersion)
        {
            x.version = CultureExtensionState.CurrentVersion;
            changed = true;
        }
        return changed;
    }
}
