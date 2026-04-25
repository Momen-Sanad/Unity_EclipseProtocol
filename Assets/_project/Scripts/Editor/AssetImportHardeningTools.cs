#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace EclipseProtocol.EditorTools
{
    internal static class AssetImportHardeningTools
    {
        private const string MenuRoot = "Tools/Eclipse Protocol/Diagnostics/";
        private const string HunterDronePrefabPath = "Assets/_project/Prefabs/Enemies/HunterDrone.prefab";
        private static readonly Regex DependencyRegex = new Regex("\"([^\"]+)\"\\s*:\\s*\"([^\"]+)\"", RegexOptions.Compiled);

        [MenuItem(MenuRoot + "Recover HunterDrone Prefab Import")]
        private static void RecoverHunterDronePrefabImport()
        {
            RecoverAssets(new[] { HunterDronePrefabPath }, "HunterDrone prefab");
        }

        [MenuItem(MenuRoot + "Recover Selected Asset Imports")]
        private static void RecoverSelectedAssetImports()
        {
            string[] selectedGuids = Selection.assetGUIDs ?? Array.Empty<string>();
            string[] selectedPaths = selectedGuids
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            if (selectedPaths.Length == 0)
            {
                Debug.LogWarning("No assets selected. Select one or more assets in the Project window and try again.");
                return;
            }

            RecoverAssets(selectedPaths, "selected assets");
        }

        [MenuItem(MenuRoot + "Recover Selected Asset Imports", true)]
        private static bool ValidateRecoverSelectedAssetImports()
        {
            string[] selectedGuids = Selection.assetGUIDs;
            return selectedGuids != null && selectedGuids.Length > 0;
        }

        [MenuItem(MenuRoot + "Check Package Risk Flags")]
        private static void CheckPackageRiskFlags()
        {
            string manifestPath = Path.Combine(GetProjectRoot(), "Packages", "manifest.json");
            if (!File.Exists(manifestPath))
            {
                Debug.LogError("Could not find Packages/manifest.json.");
                return;
            }

            string manifestText = File.ReadAllText(manifestPath);
            MatchCollection matches = DependencyRegex.Matches(manifestText);
            List<string> riskyPackages = new List<string>();
            string aiAssistantVersion = string.Empty;

            foreach (Match match in matches)
            {
                if (match.Groups.Count < 3)
                {
                    continue;
                }

                string packageName = match.Groups[1].Value;
                string packageVersion = match.Groups[2].Value;
                if (!packageName.StartsWith("com.", StringComparison.Ordinal))
                {
                    continue;
                }

                if (packageName.Equals("com.unity.ai.assistant", StringComparison.Ordinal))
                {
                    aiAssistantVersion = packageVersion;
                }

                if (IsPreReleaseVersion(packageVersion))
                {
                    riskyPackages.Add(packageName + " " + packageVersion);
                }
            }

            if (riskyPackages.Count == 0)
            {
                Debug.Log("Package risk check: no pre-release package versions found in manifest.json.");
                return;
            }

            riskyPackages.Sort(StringComparer.OrdinalIgnoreCase);
            string body = "Pre-release packages detected:\n- " + string.Join("\n- ", riskyPackages);
            Debug.LogWarning(body);

            if (!string.IsNullOrEmpty(aiAssistantVersion) && IsPreReleaseVersion(aiAssistantVersion))
            {
                Debug.LogWarning(
                    "com.unity.ai.assistant is on a pre-release version (" + aiAssistantVersion + "). " +
                    "If prefab import/save crashes recur, prefer manual prefab edits and pin a stable package release.");
            }
        }

        [MenuItem(MenuRoot + "Clean Temp Artifact Cache")]
        private static void CleanTempArtifactCache()
        {
            string tempPrimaryPath = Path.Combine(GetProjectRoot(), "Library", "TempArtifacts", "Primary");
            Directory.CreateDirectory(tempPrimaryPath);

            int deletedFiles = 0;
            int deletedDirs = 0;

            foreach (string filePath in Directory.GetFiles(tempPrimaryPath))
            {
                File.Delete(filePath);
                deletedFiles++;
            }

            foreach (string dirPath in Directory.GetDirectories(tempPrimaryPath))
            {
                Directory.Delete(dirPath, true);
                deletedDirs++;
            }

            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            Debug.Log(
                "Temp artifact cache cleaned at " + tempPrimaryPath +
                " (files deleted: " + deletedFiles + ", directories deleted: " + deletedDirs + ").");
        }

        private static void RecoverAssets(IReadOnlyList<string> assetPaths, string label)
        {
            EnsureArtifactFoldersExist();

            int successCount = 0;
            int failCount = 0;
            List<string> failedAssets = new List<string>();
            bool reloadLocked = false;
            bool autoRefreshDisallowed = false;
            try
            {
                EditorApplication.LockReloadAssemblies();
                reloadLocked = true;
                AssetDatabase.DisallowAutoRefresh();
                autoRefreshDisallowed = true;

                for (int i = 0; i < assetPaths.Count; i++)
                {
                    string path = assetPaths[i];

                    try
                    {
                        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);

                        if (path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase) &&
                            !ValidatePrefabAsset(path, out string prefabError))
                        {
                            failCount++;
                            failedAssets.Add(path + " (" + prefabError + ")");
                            continue;
                        }

                        UnityEngine.Object mainAsset = AssetDatabase.LoadMainAssetAtPath(path);
                        if (mainAsset == null)
                        {
                            failCount++;
                            failedAssets.Add(path + " (main asset is null after import)");
                            continue;
                        }

                        successCount++;
                    }
                    catch (Exception ex)
                    {
                        failCount++;
                        failedAssets.Add(path + " (" + ex.GetType().Name + ": " + ex.Message + ")");
                    }
                }
            }
            finally
            {
                if (autoRefreshDisallowed)
                {
                    AssetDatabase.AllowAutoRefresh();
                }

                if (reloadLocked)
                {
                    EditorApplication.UnlockReloadAssemblies();
                }
            }

            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            string summary = "Recovered " + label + ": " + successCount + " succeeded, " + failCount + " failed.";
            if (failCount == 0)
            {
                Debug.Log(summary);
                return;
            }

            string failureDetails = string.Join("\n- ", failedAssets);
            Debug.LogError(summary + "\n- " + failureDetails);
        }

        private static bool ValidatePrefabAsset(string path, out string error)
        {
            Type mainType = AssetDatabase.GetMainAssetTypeAtPath(path);
            if (mainType != typeof(GameObject))
            {
                error = "main asset type is " + (mainType == null ? "<null>" : mainType.FullName);
                return false;
            }

            GameObject prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefabAsset == null)
            {
                error = "prefab did not load as GameObject";
                return false;
            }

            error = string.Empty;
            return true;
        }

        private static void EnsureArtifactFoldersExist()
        {
            string projectRoot = GetProjectRoot();
            string artifactsPath = Path.Combine(projectRoot, "Library", "Artifacts");
            string tempArtifactsPath = Path.Combine(projectRoot, "Library", "TempArtifacts");
            string tempPrimaryPath = Path.Combine(tempArtifactsPath, "Primary");

            Directory.CreateDirectory(artifactsPath);
            Directory.CreateDirectory(tempArtifactsPath);
            Directory.CreateDirectory(tempPrimaryPath);
        }

        private static string GetProjectRoot()
        {
            return Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        }

        private static bool IsPreReleaseVersion(string version)
        {
            return version.IndexOf("-pre", StringComparison.OrdinalIgnoreCase) >= 0
                || version.IndexOf("-preview", StringComparison.OrdinalIgnoreCase) >= 0
                || version.IndexOf("-exp", StringComparison.OrdinalIgnoreCase) >= 0
                || version.IndexOf("-alpha", StringComparison.OrdinalIgnoreCase) >= 0
                || version.IndexOf("-beta", StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
#endif
