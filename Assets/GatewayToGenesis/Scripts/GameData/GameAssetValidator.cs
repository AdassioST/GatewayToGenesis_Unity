using System.Collections.Generic;
using UnityEngine;
using System.Linq;

/// <summary>
/// Centralized asset validation system for all game resources, weather profiles, and other ScriptableObjects.
/// Provides fast O(1) lookups and validation warnings for missing assets.
/// Pattern: Static utility class with lazy-loaded caches, similar to GovernmentLogic validation.
/// </summary>
public static class GameAssetValidator
{
    #region Static Caches
    // Resource validation
    private static HashSet<string> validResourceNames = null;
    private static HashSet<string> validSectionNames = null;
    private static HashSet<string> validProductionUnitNames = null;
    
    // Weather validation
    private static HashSet<string> validWeatherProfileNames = null;
    private static Dictionary<string, WeatherProfileSO> weatherProfileLookup = null;
    
    // Stat validation
    private static HashSet<string> validPillarNames = null;
    private static HashSet<string> validSubstatNames = null;
    private static HashSet<string> validDerivedStatNames = null;
    
    // Technology validation
    private static HashSet<string> validTechnologyNames = null;
    
    // Civic validation
    private static HashSet<string> validCivicNames = null;
    #endregion
    
    #region Weather Profile Validation
    /// <summary>
    /// Load and cache all weather profiles from Resources/WeatherProfiles ONLY
    /// </summary>
    public static void LoadWeatherProfiles()
    {
        if (validWeatherProfileNames != null) return; // Already loaded
        
        validWeatherProfileNames = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
        weatherProfileLookup = new Dictionary<string, WeatherProfileSO>(System.StringComparer.OrdinalIgnoreCase);
        
        try
        {
            // Load ONLY from Resources/WeatherProfiles folder
            WeatherProfileSO[] profiles = Resources.LoadAll<WeatherProfileSO>("WeatherProfiles");
            
            foreach (var profile in profiles)
            {
                if (profile != null && !string.IsNullOrEmpty(profile.name))
                {
                    validWeatherProfileNames.Add(profile.name);
                    weatherProfileLookup[profile.name] = profile;
                }
            }
            
            if (GameLoggingSystem.Instance != null)
            {
                GameLoggingSystem.Instance.LogEvent(
                    $"Loaded {validWeatherProfileNames.Count} weather profiles from Resources/WeatherProfiles: {string.Join(", ", validWeatherProfileNames)}",
                    "GameAssetValidator"
                );
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[GameAssetValidator] Error loading weather profiles: {e.Message}");
            validWeatherProfileNames = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
            weatherProfileLookup = new Dictionary<string, WeatherProfileSO>(System.StringComparer.OrdinalIgnoreCase);
        }
    }
    
    /// <summary>
    /// Validate that a weather profile exists in Resources/WeatherProfiles
    /// </summary>
    public static bool ValidateWeatherProfile(string profileName, string requestingSystem = "Unknown")
    {
        if (string.IsNullOrEmpty(profileName)) return false;
        
        LoadWeatherProfiles(); // Ensure cache is loaded
        
        if (!validWeatherProfileNames.Contains(profileName))
        {
            Debug.LogWarning(
                $"[GameAssetValidator] Weather profile '{profileName}' requested by {requestingSystem} does not exist in Resources/WeatherProfiles.\n" +
                $"Valid weather profiles: {string.Join(", ", validWeatherProfileNames)}"
            );
            return false;
        }
        
        return true;
    }
    
    /// <summary>
    /// Get a weather profile by name (centralized lookup with validation)
    /// </summary>
    public static WeatherProfileSO GetWeatherProfile(string profileName, string requestingSystem = "Unknown")
    {
        if (string.IsNullOrEmpty(profileName)) return null;
        
        LoadWeatherProfiles(); // Ensure cache is loaded
        
        if (weatherProfileLookup.TryGetValue(profileName, out WeatherProfileSO profile))
        {
            return profile;
        }
        
        // Validation warning
        Debug.LogWarning(
            $"[GameAssetValidator] Weather profile '{profileName}' requested by {requestingSystem} not found in Resources/WeatherProfiles.\n" +
            $"Valid profiles: {string.Join(", ", validWeatherProfileNames)}"
        );
        
        return null;
    }
    
    /// <summary>
    /// Get all valid weather profile names
    /// </summary>
    public static List<string> GetAllWeatherProfileNames()
    {
        LoadWeatherProfiles();
        return new List<string>(validWeatherProfileNames);
    }
    
    /// <summary>
    /// Check if a weather profile exists without logging warnings
    /// </summary>
    public static bool WeatherProfileExists(string profileName)
    {
        if (string.IsNullOrEmpty(profileName)) return false;
        LoadWeatherProfiles();
        return validWeatherProfileNames.Contains(profileName);
    }
    #endregion
    
    #region Resource Validation
    /// <summary>
    /// Load all valid resource names from Resources
    /// </summary>
    public static void LoadResourceNames()
    {
        if (validResourceNames != null) return; // Already loaded
        
        validResourceNames = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
        
        try
        {
            var allResources = Resources.LoadAll<ResourceSO>("");
            foreach (var resource in allResources)
            {
                if (resource != null && !string.IsNullOrEmpty(resource.name))
                {
                    validResourceNames.Add(resource.name);
                }
            }
            
            if (GameLoggingSystem.Instance != null)
            {
                GameLoggingSystem.Instance.LogEvent(
                    $"Loaded {validResourceNames.Count} valid resource names",
                    "GameAssetValidator"
                );
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[GameAssetValidator] Error loading resource names: {e.Message}");
            validResourceNames = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
        }
    }
    
    /// <summary>
    /// Validate a resource name
    /// </summary>
    public static bool ValidateResource(string resourceName, string requestingSystem = "Unknown")
    {
        if (string.IsNullOrEmpty(resourceName)) return false;
        
        LoadResourceNames();
        
        if (!validResourceNames.Contains(resourceName))
        {
            Debug.LogWarning(
                $"[GameAssetValidator] Resource '{resourceName}' requested by {requestingSystem} does not exist.\n" +
                $"Valid resources: {string.Join(", ", validResourceNames)}"
            );
            return false;
        }
        
        return true;
    }
    
    /// <summary>
    /// Check if resource exists without logging warnings
    /// </summary>
    public static bool ResourceExists(string resourceName)
    {
        if (string.IsNullOrEmpty(resourceName)) return false;
        LoadResourceNames();
        return validResourceNames.Contains(resourceName);
    }
    #endregion
    
    #region Section Validation
    private static Dictionary<string, SectionData> sectionLookup = null;
    
    /// <summary>
    /// Load all valid section names and data from Resources/Sections
    /// </summary>
    public static void LoadSectionNames()
    {
        if (validSectionNames != null) return; // Already loaded
        
        validSectionNames = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
        sectionLookup = new Dictionary<string, SectionData>(System.StringComparer.OrdinalIgnoreCase);
        
        try
        {
            var sections = Resources.LoadAll<SectionData>("Sections");
            foreach (var section in sections)
            {
                if (section != null && !string.IsNullOrEmpty(section.name))
                {
                    validSectionNames.Add(section.name);
                    sectionLookup[section.name] = section;
                }
            }
            
            if (GameLoggingSystem.Instance != null)
            {
                GameLoggingSystem.Instance.LogEvent(
                    $"Loaded {validSectionNames.Count} sections from Resources/Sections",
                    "GameAssetValidator"
                );
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[GameAssetValidator] Error loading section names: {e.Message}");
            validSectionNames = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
            sectionLookup = new Dictionary<string, SectionData>(System.StringComparer.OrdinalIgnoreCase);
        }
    }
    
    /// <summary>
    /// Validate a section name
    /// </summary>
    public static bool ValidateSection(string sectionName, string requestingSystem = "Unknown")
    {
        if (string.IsNullOrEmpty(sectionName)) return false;
        
        LoadSectionNames();
        
        if (!validSectionNames.Contains(sectionName))
        {
            Debug.LogWarning(
                $"[GameAssetValidator] Section '{sectionName}' requested by {requestingSystem} does not exist in Resources/Sections.\n" +
                $"Valid sections: {string.Join(", ", validSectionNames)}"
            );
            return false;
        }
        
        return true;
    }
    
    /// <summary>
    /// Check if section exists without logging warnings
    /// </summary>
    public static bool SectionExists(string sectionName)
    {
        if (string.IsNullOrEmpty(sectionName)) return false;
        LoadSectionNames();
        return validSectionNames.Contains(sectionName);
    }
    
    /// <summary>
    /// Get section data by name (centralized lookup)
    /// </summary>
    public static SectionData GetSection(string sectionName, string requestingSystem = "Unknown")
    {
        if (string.IsNullOrEmpty(sectionName)) return null;
        
        LoadSectionNames();
        
        if (sectionLookup.TryGetValue(sectionName, out SectionData section))
        {
            return section;
        }
        
        Debug.LogWarning($"[GameAssetValidator] Section '{sectionName}' requested by {requestingSystem} not found.");
        return null;
    }
    
    /// <summary>
    /// Get all sections (for dictionary initialization)
    /// </summary>
    public static Dictionary<string, SectionData> GetAllSections()
    {
        LoadSectionNames();
        return new Dictionary<string, SectionData>(sectionLookup);
    }
    #endregion
    
    #region Production Unit Validation
    private static Dictionary<string, ProductionUnitData> productionUnitLookup = null;
    
    /// <summary>
    /// Load all valid production unit names and data from Resources/Production
    /// </summary>
    public static void LoadProductionUnitNames()
    {
        if (validProductionUnitNames != null) return; // Already loaded
        
        validProductionUnitNames = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
        productionUnitLookup = new Dictionary<string, ProductionUnitData>(System.StringComparer.OrdinalIgnoreCase);
        
        try
        {
            var productionUnits = Resources.LoadAll<ProductionUnitData>("Production");
            foreach (var unit in productionUnits)
            {
                if (unit != null && unit.gameUnit != null && !string.IsNullOrEmpty(unit.gameUnit.name))
                {
                    validProductionUnitNames.Add(unit.gameUnit.name);
                    productionUnitLookup[unit.gameUnit.name] = unit;
                }
            }
            
            if (GameLoggingSystem.Instance != null)
            {
                GameLoggingSystem.Instance.LogEvent(
                    $"Loaded {validProductionUnitNames.Count} production units from Resources/Production",
                    "GameAssetValidator"
                );
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[GameAssetValidator] Error loading production unit names: {e.Message}");
            validProductionUnitNames = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
            productionUnitLookup = new Dictionary<string, ProductionUnitData>(System.StringComparer.OrdinalIgnoreCase);
        }
    }
    
    /// <summary>
    /// Validate a production unit name
    /// </summary>
    public static bool ValidateProductionUnit(string unitName, string requestingSystem = "Unknown")
    {
        if (string.IsNullOrEmpty(unitName)) return false;
        
        LoadProductionUnitNames();
        
        if (!validProductionUnitNames.Contains(unitName))
        {
            Debug.LogWarning(
                $"[GameAssetValidator] Production unit '{unitName}' requested by {requestingSystem} does not exist in Resources/Production.\n" +
                $"Valid production units: {string.Join(", ", validProductionUnitNames.Take(10))}{(validProductionUnitNames.Count > 10 ? $"... ({validProductionUnitNames.Count} total)" : "")}"
            );
            return false;
        }
        
        return true;
    }
    
    /// <summary>
    /// Get production unit data by name (centralized lookup)
    /// </summary>
    public static ProductionUnitData GetProductionUnit(string unitName, string requestingSystem = "Unknown")
    {
        if (string.IsNullOrEmpty(unitName)) return null;
        
        LoadProductionUnitNames();
        
        if (productionUnitLookup.TryGetValue(unitName, out ProductionUnitData unit))
        {
            return unit;
        }
        
        Debug.LogWarning($"[GameAssetValidator] Production unit '{unitName}' requested by {requestingSystem} not found.");
        return null;
    }
    
    /// <summary>
    /// Get all production units (for dictionary initialization)
    /// </summary>
    public static Dictionary<string, ProductionUnitData> GetAllProductionUnits()
    {
        LoadProductionUnitNames();
        return new Dictionary<string, ProductionUnitData>(productionUnitLookup);
    }
    #endregion
    
    #region Stat Validation
    /// <summary>
    /// Load all valid stat names (pillars, substats, derived stats)
    /// </summary>
    public static void LoadStatNames()
    {
        if (validPillarNames != null) return; // Already loaded
        
        validPillarNames = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase)
        {
            "aureus", "regalia", "waltz", "chorus"
        };
        
        validSubstatNames = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase)
        {
            "innovation", "piety", "authority", "ambition", "symphony", "euphony", "arcane", "secrecy"
        };
        
        validDerivedStatNames = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase)
        {
            "discoveryEfficiency", "savingRollChance", "legendEffectiveness", "expeditionCostMod",
            "expeditionTimeMod", "satisfactionEffectiveness", "moraleLossMod", "moraleRecoveryMod",
            "clickPowerBonus", "magicEffectiveness", "communionStage"
        };
    }
    
    /// <summary>
    /// Validate a stat name (pillar, substat, derived, or special global stat)
    /// </summary>
    public static bool ValidateStat(string statName, string requestingSystem = "Unknown")
    {
        if (string.IsNullOrEmpty(statName)) return false;
        
        LoadStatNames();
        
        // Check standard stats
        if (validPillarNames.Contains(statName) || 
            validSubstatNames.Contains(statName) || 
            validDerivedStatNames.Contains(statName))
        {
            return true;
        }
        
        // Check special global stats
        string[] specialGlobalStats = { "housing", "morale", "moraleBalance", "maxmorale", "satisfaction", "satisfactionPoints", "satisfactionLevel", "satisfactionupgradethreshold" };
        if (specialGlobalStats.Contains(statName.ToLower()))
        {
            return true;
        }
        
        Debug.LogWarning(
            $"[GameAssetValidator] Stat '{statName}' requested by {requestingSystem} is not a valid stat.\n" +
            $"Valid pillars: {string.Join(", ", validPillarNames)}\n" +
            $"Valid substats: {string.Join(", ", validSubstatNames)}\n" +
            $"Valid derived: {string.Join(", ", validDerivedStatNames)}\n" +
            $"Valid global: housing, morale, moraleBalance, maxmorale, satisfaction, satisfactionPoints, satisfactionLevel, satisfactionupgradethreshold"
        );
        
        return false;
    }
    #endregion
    
    #region Technology Validation
    private static Dictionary<string, TechnologyData> technologyLookup = null;
    
    /// <summary>
    /// Load all valid technology names and data from Resources/Technology
    /// </summary>
    public static void LoadTechnologyNames()
    {
        if (validTechnologyNames != null) return; // Already loaded
        
        validTechnologyNames = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
        technologyLookup = new Dictionary<string, TechnologyData>(System.StringComparer.OrdinalIgnoreCase);
        
        try
        {
            var technologies = Resources.LoadAll<TechnologyData>("Technology");
            foreach (var tech in technologies)
            {
                if (tech != null && !string.IsNullOrEmpty(tech.name))
                {
                    validTechnologyNames.Add(tech.name);
                    technologyLookup[tech.name] = tech;
                }
            }
            
            if (GameLoggingSystem.Instance != null)
            {
                GameLoggingSystem.Instance.LogEvent(
                    $"Loaded {validTechnologyNames.Count} technologies from Resources/Technology",
                    "GameAssetValidator"
                );
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[GameAssetValidator] Error loading technology names: {e.Message}");
            validTechnologyNames = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
            technologyLookup = new Dictionary<string, TechnologyData>(System.StringComparer.OrdinalIgnoreCase);
        }
    }
    
    /// <summary>
    /// Validate a technology name
    /// </summary>
    public static bool ValidateTechnology(string technologyName, string requestingSystem = "Unknown")
    {
        if (string.IsNullOrEmpty(technologyName)) return false;
        
        LoadTechnologyNames();
        
        if (!validTechnologyNames.Contains(technologyName))
        {
            Debug.LogWarning(
                $"[GameAssetValidator] Technology '{technologyName}' requested by {requestingSystem} does not exist in Resources/Technology.\n" +
                $"Valid technologies: {string.Join(", ", validTechnologyNames.Take(10))}{(validTechnologyNames.Count > 10 ? $"... ({validTechnologyNames.Count} total)" : "")}"
            );
            return false;
        }
        
        return true;
    }
    
    /// <summary>
    /// Get technology data by name (centralized lookup)
    /// </summary>
    public static TechnologyData GetTechnology(string technologyName, string requestingSystem = "Unknown")
    {
        if (string.IsNullOrEmpty(technologyName)) return null;
        
        LoadTechnologyNames();
        
        if (technologyLookup.TryGetValue(technologyName, out TechnologyData tech))
        {
            return tech;
        }
        
        Debug.LogWarning($"[GameAssetValidator] Technology '{technologyName}' requested by {requestingSystem} not found.");
        return null;
    }
    
    /// <summary>
    /// Get all technologies (for dictionary initialization)
    /// </summary>
    public static Dictionary<string, TechnologyData> GetAllTechnologies()
    {
        LoadTechnologyNames();
        return new Dictionary<string, TechnologyData>(technologyLookup);
    }
    #endregion
    
    #region Legend Validation
    // NOTE: Legends are EXCLUDED from validation - they are dynamically generated during gameplay
    // Do NOT use validator for legends - they should be loaded directly by LegendLeaderLogic
    #endregion
    
    #region Civic Validation
    private static Dictionary<string, CivicData> civicLookup = null;
    
    /// <summary>
    /// Load all valid civic names and data from Resources/Civics
    /// </summary>
    public static void LoadCivicNames()
    {
        if (validCivicNames != null) return; // Already loaded
        
        validCivicNames = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
        civicLookup = new Dictionary<string, CivicData>(System.StringComparer.OrdinalIgnoreCase);
        
        try
        {
            var civics = Resources.LoadAll<CivicData>("Civics");
            foreach (var civic in civics)
            {
                if (civic != null && !string.IsNullOrEmpty(civic.civicName))
                {
                    validCivicNames.Add(civic.civicName);
                    civicLookup[civic.civicName] = civic;
                }
            }
            
            if (GameLoggingSystem.Instance != null)
            {
                GameLoggingSystem.Instance.LogEvent(
                    $"Loaded {validCivicNames.Count} civics from Resources/Civics",
                    "GameAssetValidator"
                );
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[GameAssetValidator] Error loading civic names: {e.Message}");
            validCivicNames = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
            civicLookup = new Dictionary<string, CivicData>(System.StringComparer.OrdinalIgnoreCase);
        }
    }
    
    /// <summary>
    /// Validate a civic name
    /// </summary>
    public static bool ValidateCivic(string civicName, string requestingSystem = "Unknown")
    {
        if (string.IsNullOrEmpty(civicName)) return false;
        
        LoadCivicNames();
        
        if (!validCivicNames.Contains(civicName))
        {
            Debug.LogWarning(
                $"[GameAssetValidator] Civic '{civicName}' requested by {requestingSystem} does not exist in Resources/Civics.\n" +
                $"Valid civics: {string.Join(", ", validCivicNames.Take(10))}{(validCivicNames.Count > 10 ? $"... ({validCivicNames.Count} total)" : "")}"
            );
            return false;
        }
        
        return true;
    }
    
    /// <summary>
    /// Get civic data by name (centralized lookup)
    /// </summary>
    public static CivicData GetCivic(string civicName, string requestingSystem = "Unknown")
    {
        if (string.IsNullOrEmpty(civicName)) return null;
        
        LoadCivicNames();
        
        if (civicLookup.TryGetValue(civicName, out CivicData civic))
        {
            return civic;
        }
        
        Debug.LogWarning($"[GameAssetValidator] Civic '{civicName}' requested by {requestingSystem} not found.");
        return null;
    }
    
    /// <summary>
    /// Get all civics (for initialization)
    /// </summary>
    public static Dictionary<string, CivicData> GetAllCivics()
    {
        LoadCivicNames();
        return new Dictionary<string, CivicData>(civicLookup);
    }
    #endregion
    
    #region Utility Methods
    /// <summary>
    /// Initialize all validation caches at game startup (optional optimization)
    /// </summary>
    public static void InitializeAllCaches()
    {
        LoadWeatherProfiles();
        LoadResourceNames();
        LoadSectionNames();
        LoadProductionUnitNames();
        LoadStatNames();
        LoadTechnologyNames();
        LoadCivicNames();
        // NOTE: Legends excluded - dynamically generated during gameplay
        
        if (GameLoggingSystem.Instance != null)
        {
            GameLoggingSystem.Instance.LogEvent(
                "All asset validation caches initialized",
                "GameAssetValidator"
            );
        }
    }
    
    /// <summary>
    /// Clear all caches (for hot-reload scenarios)
    /// </summary>
    public static void ClearAllCaches()
    {
        validWeatherProfileNames = null;
        weatherProfileLookup = null;
        validResourceNames = null;
        validSectionNames = null;
        validProductionUnitNames = null;
        productionUnitLookup = null;
        validPillarNames = null;
        validSubstatNames = null;
        validDerivedStatNames = null;
        validTechnologyNames = null;
        technologyLookup = null;
        validCivicNames = null;
        civicLookup = null;
        sectionLookup = null;
        // NOTE: Legends excluded - dynamically generated
        
        if (GameLoggingSystem.Instance != null)
        {
            GameLoggingSystem.Instance.LogEvent(
                "All asset validation caches cleared",
                "GameAssetValidator"
            );
        }
    }
    
    /// <summary>
    /// Get comprehensive validation report
    /// </summary>
    public static string GetValidationReport()
    {
        InitializeAllCaches();
        
        return $"=== GAME ASSET VALIDATOR REPORT ===\n" +
               $"Weather Profiles: {validWeatherProfileNames?.Count ?? 0} (from Resources/WeatherProfiles)\n" +
               $"Civics: {validCivicNames?.Count ?? 0} (from Resources/Civics)\n" +
               $"Technologies: {validTechnologyNames?.Count ?? 0} (from Resources/Technology)\n" +
               $"Production Units: {validProductionUnitNames?.Count ?? 0} (from Resources/Production)\n" +
               $"Sections: {validSectionNames?.Count ?? 0} (from Resources/Sections)\n" +
               $"Resources: {validResourceNames?.Count ?? 0}\n" +
               $"Pillars: {validPillarNames?.Count ?? 0}\n" +
               $"Substats: {validSubstatNames?.Count ?? 0}\n" +
               $"Derived Stats: {validDerivedStatNames?.Count ?? 0}\n" +
               $"NOTE: Legends excluded (dynamically generated)";
    }
    #endregion
}
