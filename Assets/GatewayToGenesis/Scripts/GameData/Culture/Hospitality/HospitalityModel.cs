using System;
using System.Collections.Generic;

// Hospitality, redistribution and access to culture (T05): communal tables set in a settlement from finished food
// held in the stores, under an explicit serving policy, and the history of who shared what where. Saved inside
// CultureExtensionState (fields only; enums appended, never reordered).

public partial class CultureExtensionState
{
    /// <summary>Communal tables: their history, each settlement's summary and each patron's (T05).</summary>
    [SaveOptionalField] public HospitalityState hospitality = new HospitalityState();
}

/// <summary>Who a communal table is for. Append only: saves store the name.</summary>
public enum TablePolicy
{
    /// <summary>An open table in the settlement: anyone may come, neighbours by road included (All-Welcome Cozy Inn).</summary>
    PublicWelcome,
    /// <summary>A table for a community under strain: it eases its Composure most, and asks nothing back.</summary>
    RecoverySupport,
    /// <summary>A Legend hosts from the stores for their own guests: prestige for the patron, little reach for the town (Feast of Abundance).</summary>
    PatronHosted,
}

/// <summary>One food set on a communal table: what it was (a recipe by id when it has one), how much and what it was worth.</summary>
[Serializable]
public class ServedLine
{
    /// <summary>The resource taken from the stores (its name when served).</summary>
    public string resource;
    /// <summary>The recipe that makes it, by id (empty: a food with no recipe, such as Wild Honey). The id survives a rename.</summary>
    public string recipe;
    /// <summary>Portions set out (units of the resource).</summary>
    public float amount;
    /// <summary>Food value of those portions.</summary>
    public float foodValue;
    /// <summary>The luxury categories it belongs to, when served (Sweets, Fine Dishes...): empty for a plain food.</summary>
    public List<string> luxuries = new List<string>();
    /// <summary>A dish or drink the people invented.</summary>
    public bool invented;
    public bool national;

    public bool Luxury => luxuries != null && luxuries.Count > 0;

    public ServedLine Copy() => new ServedLine
    {
        resource = resource, recipe = recipe, amount = amount, foodValue = foodValue,
        luxuries = luxuries != null ? new List<string>(luxuries) : new List<string>(), invented = invented, national = national,
    };
}

/// <summary>A communal table that was set: where, under which policy, what was served and who was there when known.</summary>
[Serializable]
public class TableRecord
{
    /// <summary>"table-12": unique per table (its occurrences and contact use it).</summary>
    public string key;
    public TablePolicy policy;
    public int settlement = -1;
    /// <summary>The settlement's name then.</summary>
    public string place;
    /// <summary>The Legend who hosted (patron tables only).</summary>
    public string patron;
    public List<ServedLine> served = new List<ServedLine>();
    /// <summary>The people and groups represented, in words, only as far as they are known ("the people of Ashford", "6 recent arrivals of unknown origin").</summary>
    public List<string> groups = new List<string>();
    /// <summary>Settlements whose people came by open road (public welcome only).</summary>
    public List<int> guestsFrom = new List<int>();
    /// <summary>Legends at the table, when known (the patron, parties standing in the settlement).</summary>
    public List<string> legends = new List<string>();
    /// <summary>When: Sevenths since the founding, and the Age.</summary>
    public int seventh;
    public string ageId;
    public float foodValue;
    /// <summary>A luxury was shared at it (public welcome or recovery support only: a patron's table is private).</summary>
    public bool luxuryShared;
    public string text;
}

/// <summary>A settlement's tables all told (kept when single records are dropped): what the access report reads.</summary>
[Serializable]
public class SettlementTables
{
    public int settlement;
    public string name;
    public int tables, publicTables, recoveryTables, patronTables;
    /// <summary>Sevenths of its last table, last open table (public or recovery), last luxury shared openly and last patron's table (-1: never).</summary>
    public int lastTable = -1, lastShared = -1, lastLuxuryShared = -1, lastPatron = -1;
    /// <summary>The luxury categories last shared openly there.</summary>
    public List<string> sharedLuxuries = new List<string>();
    /// <summary>The luxury categories last served only at a patron's table there.</summary>
    public List<string> privateLuxuries = new List<string>();
    public float foodValue;
}

/// <summary>A Legend's tables as patron: how many, and when last rewarded (the patron's standing is earned once in a while, not per table).</summary>
[Serializable]
public class PatronRecord
{
    public string legend;
    public int tables;
    public int lastSeventh = -1, lastRewarded = -1;
    public float foodValue;
}

/// <summary>Everything hospitality remembers.</summary>
[Serializable]
public class HospitalityState
{
    /// <summary>The latest tables, oldest first (bounded; the summaries keep the totals).</summary>
    public List<TableRecord> tables = new List<TableRecord>();
    public List<SettlementTables> settlements = new List<SettlementTables>();
    public List<PatronRecord> patrons = new List<PatronRecord>();
    /// <summary>Tables set all told (the next table's number).</summary>
    public int serial;
}

/// <summary>
/// The numbers of hospitality. None is from the vault: every number is a proposal (Canon Gaps.md, "Hospitality and
/// shared tables").
/// </summary>
[Serializable]
public class HospitalityTuning
{
    // ----- Size of a table (food value; portions follow from each food's own value) -----
    public float publicFoodValue = 6f, recoveryFoodValue = 4f, patronFoodValue = 5f;
    /// <summary>Food value a table grows by per point of the settlement's development (the festival's own measure; not a head count).</summary>
    public float foodValuePerDevelopment = 0.05f;
    /// <summary>Most different foods on one table.</summary>
    public int maxMenu = 3;
    /// <summary>Sevenths before the same settlement sets another table.</summary>
    public int cooldownSevenths = 7;
    /// <summary>Food value per citizen the stores keep for survival: no table may draw them below it.</summary>
    public float reservePerCitizen = 0.25f;

    // ----- What a table does, by policy -----
    public float publicUnity = 3f, recoveryUnity = 1f, patronUnity = 5f;
    /// <summary>Extra Unity for an open table while the Feast of Abundance is in force (redistribution is legitimacy).</summary>
    public float abundanceUnity = 2f;
    public string abundanceCivic = "Feast of Abundance";
    /// <summary>Composure strain eased in the settlement.</summary>
    public float publicRelief = 6f, recoveryRelief = 15f, patronRelief = 2f;
    public float publicJoy = 0.06f, recoveryJoy = 0.03f, patronJoy = 0.04f;
    /// <summary>Extra joy when a luxury is shared at an open table.</summary>
    public float luxuryJoy = 0.04f;
    /// <summary>How far a table reaches the settlement's people (1: all who wish; a patron's guests are few).</summary>
    public float publicCoverage = 1f, recoveryCoverage = 1f, patronCoverage = 0.25f;
    /// <summary>A settlement needs at least this strain for recovery support.</summary>
    public float recoveryStrain = 20f;
    /// <summary>Exposure an open table gives each way between the host and each neighbour by open road, per custom kept (T02).</summary>
    public float tableExposure = 0.15f;
    /// <summary>Meaning fragments the patron earns, at most once in this many Sevenths.</summary>
    public int patronFragments = 1;
    public int patronRestSevenths = 21;

    // ----- Coverage and records -----
    /// <summary>A settlement counts as reached for this many Sevenths after its table.</summary>
    public int coverageWindowSevenths = 21;
    /// <summary>Living (0-1) added at full coverage: bounded, and apart from the luxuries' demand.</summary>
    public float coverageLiving = 0.1f;
    /// <summary>Table records kept one by one.</summary>
    public int tablesKept = 40;
}

/// <summary>What a player asks for: a table in one settlement, under a policy, from one to a few foods (and a patron).</summary>
public sealed class TableRequest
{
    public int settlement = -1;
    public TablePolicy policy;
    /// <summary>The foods to serve, by resource name (finished foods held in the stores).</summary>
    public List<string> menu = new List<string>();
    /// <summary>The hosting Legend (patron-hosted only).</summary>
    public string patron;
}
