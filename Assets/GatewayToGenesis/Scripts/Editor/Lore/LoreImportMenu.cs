using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Editor menu for <see cref="LoreImport"/>: brings the vault's lore into the White-Haven Library's Glossary
/// (Assets/Resources/Library/Library.json). Run it whenever the vault changes; the report
/// (Assets/Resources/Library/Vault~/ImportReport.md) lists what was imported and what was left out. The vault
/// folder is remembered per machine.
/// </summary>
public static class LoreImportMenu
{
    private const string Title = "Import Lore From Vault";
    private const string VaultPrefKey = "GatewayToGenesis.VaultPath";
    private const string DefaultVault = @"C:\Arcanoria Master\Arcanoria";

    [MenuItem("Tools/Gateway to Genesis/Import Lore From Vault")]
    public static void Import()
    {
        string vault = EditorPrefs.GetString(VaultPrefKey, DefaultVault);
        if (!Directory.Exists(Path.Combine(vault, "Worldbuilding")) && !ChooseVault(out vault)) return;

        string library = Path.GetFullPath(Path.Combine(Application.dataPath, "Resources", "Library"));
        var result = LoreImport.Run(new LoreImport.Settings { vaultRoot = vault, libraryRoot = library, gameTerms = GameTerms() });
        AssetDatabase.Refresh();
        GameCatalog.InvalidateAll();

        ImportAchievements(vault);
        ImportLegendNotes(vault);
        foreach (var problem in result.problems) Debug.LogWarning($"[{Title}] {problem}");
        Debug.Log($"[{Title}] {result.keywords.Count} Glossary entries from {vault} ({(result.written ? "Library.json written" : "Library.json unchanged")}), " +
                  $"{result.problems.Count} problem(s). Report: Assets/Resources/Library/Vault~/ImportReport.md");
    }

    public const string AchievementNote = "Worldbuilding/Events/Achievement.md";

    [MenuItem("Tools/Gateway to Genesis/Import Achievements From Vault")]
    public static void ImportAchievementsOnly()
    {
        string vault = EditorPrefs.GetString(VaultPrefKey, DefaultVault);
        if (!Directory.Exists(Path.Combine(vault, "Worldbuilding")) && !ChooseVault(out vault)) return;
        ImportAchievements(vault);
        AssetDatabase.Refresh();
    }

    /// <summary>
    /// Copies the vault's achievement note verbatim to Resources/Achievements/Achievements.md (the canon is copied, never
    /// rewritten). Shape problems are reported, not repaired: fix them in the vault.
    /// </summary>
    private static void ImportAchievements(string vault)
    {
        string source = Path.Combine(vault, AchievementNote);
        if (!File.Exists(source))
        {
            Debug.LogWarning($"[Import Achievements] {source} not found; the achievement list was not updated.");
            return;
        }
        string text = File.ReadAllText(source);
        string folder = Path.Combine(Application.dataPath, "Resources", "Achievements");
        Directory.CreateDirectory(folder);
        string target = Path.Combine(folder, "Achievements.md");
        bool changed = !File.Exists(target) || File.ReadAllText(target) != text;
        if (changed) File.WriteAllText(target, text);
        var parsed = global::AchievementNote.Parse(text);
        foreach (var problem in parsed.problems) Debug.LogWarning($"[Import Achievements] {AchievementNote}: {problem}");
        Achievements.Invalidate();
        Debug.Log($"[Import Achievements] {parsed.achievements.Count} achievements ({(changed ? "Achievements.md written" : "unchanged")}), {parsed.problems.Count} problem(s) in the vault note.");
    }

    /// <summary>The vault notes legends are read from at run time (<see cref="LegendLore"/>), and where their copies go under Assets.</summary>
    public static readonly (string vaultPath, string resource)[] LegendNotes =
    {
        ("Worldbuilding/Society/Stellar Legacy/Legend Trait.md", "Resources/Legends/Legend Trait.md"),
        ("Worldbuilding/Origin of Magic/Spellweaving/Composure.md", "Resources/Legends/Composure.md"),
    };

    [MenuItem("Tools/Gateway to Genesis/Import Legend Notes From Vault")]
    public static void ImportLegendNotesOnly()
    {
        string vault = EditorPrefs.GetString(VaultPrefKey, DefaultVault);
        if (!Directory.Exists(Path.Combine(vault, "Worldbuilding")) && !ChooseVault(out vault)) return;
        ImportLegendNotes(vault);
        AssetDatabase.Refresh();
    }

    /// <summary>
    /// Copies the Legend Trait and Composure notes verbatim to Resources/Legends (the canon is copied, never rewritten)
    /// and lists what they are still missing for the game: gaps are fixed in the vault, then imported again.
    /// </summary>
    private static void ImportLegendNotes(string vault)
    {
        foreach (var (vaultPath, resource) in LegendNotes)
        {
            string source = Path.Combine(vault, vaultPath);
            if (!File.Exists(source))
            {
                Debug.LogWarning($"[Import Legend Notes] {source} not found; Assets/{resource} was not updated.");
                continue;
            }
            string text = File.ReadAllText(source);
            string target = Path.Combine(Application.dataPath, resource);
            Directory.CreateDirectory(Path.GetDirectoryName(target));
            bool changed = !File.Exists(target) || File.ReadAllText(target) != text;
            if (changed) File.WriteAllText(target, text);
            var problems = resource.EndsWith("Composure.md") ? ComposureNote.Parse(text).problems : LegendTraitNote.Parse(text).problems;
            foreach (var problem in problems) Debug.LogWarning($"[Import Legend Notes] {vaultPath}: {problem}");
            Debug.Log($"[Import Legend Notes] {vaultPath} ({(changed ? "written" : "unchanged")}), {problems.Count} gap(s) in the vault note.");
        }
        LegendLore.Invalidate();
    }

    [MenuItem("Tools/Gateway to Genesis/Choose Vault Folder...")]
    public static void ChooseVaultFolder() => ChooseVault(out _);

    private static bool ChooseVault(out string vault)
    {
        vault = EditorUtility.OpenFolderPanel("The Arcanoria vault (the folder holding Worldbuilding)", EditorPrefs.GetString(VaultPrefKey, DefaultVault), "");
        if (string.IsNullOrEmpty(vault)) return false;
        if (!Directory.Exists(Path.Combine(vault, "Worldbuilding")))
        {
            EditorUtility.DisplayDialog(Title, $"{vault} has no Worldbuilding folder, so it is not the vault.", "OK");
            return false;
        }
        EditorPrefs.SetString(VaultPrefKey, vault);
        return true;
    }

    // Words the game makes keywords by itself, so the report does not list them as missing.
    private static IEnumerable<string> GameTerms() =>
        GameCatalog.Resources.Keys.Concat(GameCatalog.Technologies.Keys).Concat(GameCatalog.Sections.Keys).Concat(StatDefinitions.Pillars).ToList();
}
