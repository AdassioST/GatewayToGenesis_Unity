using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// One-off migration for ARCHITECTURE.md §10.4: retires the first-prototype <c>GatewayToGenesis/ScriptableObjects</c>
/// folder (ResourceSO, AgeSO, ...). Run it twice: the first run removes button OnClick entries that still call
/// into those assets in the open scenes (save the scene afterwards); once nothing outside the folder depends on
/// it, the next run moves it to <c>_Archive~</c> with its .meta files, so moving it back restores the same GUIDs.
/// Delete this script once the folder is archived.
/// </summary>
public static class LegacyScriptableObjectsCleanup
{
    private const string LegacyFolder = "Assets/GatewayToGenesis/ScriptableObjects";
    private const string ArchiveFolder = "Assets/GatewayToGenesis/_Archive~/ScriptableObjects";
    private const string Title = "Retire legacy ScriptableObjects";

    [MenuItem("Tools/Gateway to Genesis/Retire Legacy ScriptableObjects")]
    private static void Run()
    {
        if (!AssetDatabase.IsValidFolder(LegacyFolder))
        {
            EditorUtility.DisplayDialog(Title, "The legacy folder is already gone. This script can be deleted.", "OK");
            return;
        }

        int removed = RemoveLegacyClickCalls();
        if (removed > 0)
        {
            EditorUtility.DisplayDialog(Title,
                $"Removed {removed} OnClick entr{(removed == 1 ? "y" : "ies")} calling legacy assets (see the Console). " +
                "Save the scene, then run this command again to archive the folder.", "OK");
            return;
        }

        if (Enumerable.Range(0, SceneManager.sceneCount).Any(i => SceneManager.GetSceneAt(i).isDirty))
        {
            EditorUtility.DisplayDialog(Title, "Save the open scenes first: references are checked on disk.", "OK");
            return;
        }

        var users = FindUsersOutsideFolder();
        if (users.Count > 0)
        {
            Debug.LogWarning($"[{Title}] Still referenced by:\n" + string.Join("\n", users));
            EditorUtility.DisplayDialog(Title, $"{users.Count} asset(s) still use the legacy folder (listed in the Console). Nothing was moved.", "OK");
            return;
        }

        if (Directory.Exists(ArchiveFolder))
        {
            EditorUtility.DisplayDialog(Title, $"{ArchiveFolder} already exists. Nothing was moved.", "OK");
            return;
        }
        if (!EditorUtility.DisplayDialog(Title, $"Nothing uses {LegacyFolder}. Move it to {ArchiveFolder}?", "Move", "Cancel")) return;

        Directory.Move(LegacyFolder, ArchiveFolder);
        File.Delete(LegacyFolder + ".meta");
        AssetDatabase.Refresh();
        Debug.Log($"[{Title}] Moved {LegacyFolder} to {ArchiveFolder}. This script can now be deleted.");
    }

    private static int RemoveLegacyClickCalls()
    {
        int removed = 0;
        var buttons = Object.FindObjectsByType<Button>(FindObjectsInactive.Include);
        foreach (var button in buttons)
        {
            for (int i = button.onClick.GetPersistentEventCount() - 1; i >= 0; i--)
            {
                var target = button.onClick.GetPersistentTarget(i);
                if (!IsLegacy(AssetDatabase.GetAssetPath(target))) continue;

                Undo.RecordObject(button, Title);
                Debug.Log($"[{Title}] {HierarchyPath(button.transform)}: removed OnClick {target.name}.{button.onClick.GetPersistentMethodName(i)}", button);
                UnityEventTools.RemovePersistentListener(button.onClick, i);
                EditorSceneManager.MarkSceneDirty(button.gameObject.scene);
                removed++;
            }
        }
        return removed;
    }

    private static List<string> FindUsersOutsideFolder()
    {
        var users = new List<string>();
        foreach (var path in AssetDatabase.GetAllAssetPaths())
        {
            if (!path.StartsWith("Assets/") || IsLegacy(path) || AssetDatabase.IsValidFolder(path)) continue;
            if (AssetDatabase.GetDependencies(path, false).Any(IsLegacy)) users.Add(path);
        }
        return users;
    }

    private static bool IsLegacy(string assetPath) => !string.IsNullOrEmpty(assetPath) && assetPath.StartsWith(LegacyFolder + "/");

    private static string HierarchyPath(Transform t) => t.parent == null ? t.name : HierarchyPath(t.parent) + "/" + t.name;
}
