using UnityEngine;

/// <summary>
/// Shared enums and data structures used across multiple game data classes
/// Centralized source of truth for common game mechanics
/// </summary>

/// <summary>
/// How modifiers are applied to values
/// </summary>
public enum ModifierType
{
    Add,            // Add/subtract value directly
    Percentage,     // Multiply by percentage (e.g., 25 = +25%)
    SetValue        // Set to exact value
}

/// <summary>
/// Scope of application for bonuses and effects
/// </summary>
public enum ScopeType
{
    Individual,     // Single specific item (e.g., "food", "Decaying Hut")
    Section,        // Group of similar items (e.g., "Vital Resource" section for Food + Aetherlight)
    Global          // Everything of that type (e.g., all resources, all buildings)
}

/// <summary>
/// Unified effect types that can be used by both legends and civics
/// Consolidates all effect types into a single system
/// </summary>
public enum GameEffectType
{
    // Core stat modifications
    PillarBonus,           // Bonus to pillar stats (aureus, regalia, waltz, chorus)
    SubstatBonus,          // Bonus to substats (innovation, piety, authority, etc.)
    DerivedStatBonus,      // Bonus to derived stats (discovery efficiency, saving roll chance, etc.)
    
    // Resource and production modifications
    ResourceModifier,      // Resource production rate modifier (GlobalProductionManager)
    ProductionModifier,    // Production unit efficiency modifier (GameUnitsLogic)
    ClickPowerBonus,       // Click power modifier for resources/sections (GameUnitsLogic)
    ConstructionCostModifier, // Building cost reduction by section/type (GameUnitsLogic)
    ProductionScalingBonus, // Bonus production per production unit (e.g., +1 food per Decaying Hut)
    
    // Social and morale modifications
    MaxMoraleModifier,     // Increases max morale (more room for positive morale)
    MoraleBalanceModifier, // Decreases morale balance (easier to stay above balance)
    SatisfactionThresholdModifier, // Satisfaction threshold modifier (StatManager)
    
    // Infrastructure modifications
    HousingBonus,          // Housing capacity bonus (PopGrowthLogic)
    
    // Special effects
    SpecialAbility         // Unique effects not covered by other types
}

/// <summary>
/// Rarity classification for game objects (legends, civics, etc.)
/// </summary>
public enum GameRarity
{
    Common,         // Basic items, easily found
    Uncommon,       // Notable items, moderately rare
    Rare,           // Exceptional items, hard to find
    Epic,           // Legendary items, very rare
    Mythic,         // Mythical items, extremely rare
    Legendary       // Alternative name for Epic (used by civics)
}

/// <summary>
/// Civic tier classification for managing civic slots and progression
/// </summary>
public enum CivicTier
{
    Minor,      // Tier 3: Basic civic policies
    Major,      // Tier 2: Significant civic institutions
    Aeonic      // Tier 1: Fundamental civic principles
}

/// <summary>
/// The six main classes of legends in the game
/// Used by both legend data and civic requirements
/// </summary>
public enum LegendClass
{
    Sovereign,      // Noble rulers and political leaders
    Vanguard,       // Military commanders and protectors
    Steward,        // Economic managers and resource overseers
    Weaver,         // Craftsmen and artisans
    Seer,           // Mystics and knowledge keepers
    Justiciar       // Law enforcers and judges
}

/// <summary>
/// Types of requirements for unlocking game objects
/// Used by both civics and legends
/// </summary>
public enum RequirementType
{
    PillarStat,           // Required pillar stat value
    SubstatStat,          // Required substat value
    GovernmentType,       // Required government type
    CivicPresent,         // Required civic to be present
    CivicAbsent,          // Required civic to be absent
    EraUnlock,            // Required era (future system)
    SatisfactionLevel,    // Required satisfaction level
    MoraleLevel           // Required morale level
}

/// <summary>
/// How to compare requirement values
/// </summary>
public enum ComparisonType
{
    GreaterThan,      // >
    GreaterEqual,     // >=
    Equal,            // ==
    LessEqual,        // <=
    LessThan,         // <
    NotEqual          // !=
}

/// <summary>
/// Government types for political system
/// Used by GovernmentLogic and civic requirements
/// </summary>
public enum GovernmentType
{
    // Centrist governments (1 + 4 = 5 total)
    TrueCentrist,           // (0,0) - Perfect balance
    
    // Pillar + Pillar Centrist governments (4 total)
    WaltzChorusCentrist,   // (-1,1) - Waltz + Chorus Centrist
    RegaliaChorusCentrist, // (1,1) - Regalia + Chorus Centrist
    WaltzAureusCentrist,   // (-1,-1) - Waltz + Aureus Centrist
    RegaliaAureusCentrist, // (1,-1) - Regalia + Aureus Centrist
    
    // Pillar Leaning governments (4 total)
    WaltzLeaning,          // (-2,0) - Waltz Leaning (Pillar level)
    RegaliaLeaning,        // (2,0) - Regalia Leaning (Pillar level)
    ChorusLeaning,         // (0,2) - Chorus Leaning (Pillar level)
    AureusLeaning,         // (0,-2) - Aureus Leaning (Pillar level)
    
    // Pillar Centrist governments (4 total) - Single axis at ±1 (Leaning level)
    WaltzCentrist,         // (-1,0) - Waltz Centrist (Leaning level)
    RegaliaCentrist,       // (1,0) - Regalia Centrist (Leaning level)
    ChorusCentrist,        // (0,1) - Chorus Centrist (Leaning level)
    AureusCentrist,        // (0,-1) - Aureus Centrist (Leaning level)
    
    // True Pillar governments (4 total) - Note: ±3 coordinates (Fanatic level)
    TrueWaltz,             // (-3,0) - True Waltz (Fanatic level)
    TrueRegalia,           // (3,0) - True Regalia (Fanatic level)
    TrueChorus,            // (0,3) - True Chorus (Fanatic level)
    TrueAureus,            // (0,-3) - True Aureus (Fanatic level)
    
    // Regular government types (20 total)
    WaltzChorus,           // (-1,2), (-2,1), (-2,2), (-3,1) (-1,3)- Waltz + Chorus combinations
    RegaliaChorus,         // (1,2), (2,1), (2,2), (3,1) (1,3) - Regalia + Chorus combinations
    WaltzAureus,           // (-1,-2), (-2,-1), (-2,-2), (-3,-1) (-1,-3) - Waltz + Aureus combinations
    RegaliaAureus,         // (1,-2), (2,-1), (2,-2), (3,-1) (1,-3) - Regalia + Aureus combinations
    
    // Radical governments (8 total)
    Radical,               // One coordinate at ±2 (Pillar) and other at ±3 (Fanatic)
    
    // Fanatic Radical governments (4 total)
    FanaticRadical         // (±3,±3) - Corner combinations of Fanatic levels
}



 