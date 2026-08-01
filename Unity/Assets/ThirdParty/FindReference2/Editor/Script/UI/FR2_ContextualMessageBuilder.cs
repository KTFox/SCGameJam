using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityObject = UnityEngine.Object;

namespace vietlabs.fr2
{
    internal static class FR2_ContextualMessageBuilder
    {
        internal static string Generate(UnityObject[] objects, string action, string suffix = " any other assets")
        {
            if (objects == null || objects.Length == 0)
                return $"Nothing selected is {action}{suffix}!";

            if (suffix.Contains("current scene"))
                suffix = suffix.Replace("current scene", GetSceneContextInfo());

            var ignoredAssets = new List<string>();
            var nonIgnoredAssets = new List<UnityObject>();

            foreach (var obj in objects)
            {
                if (AssetDatabase.Contains(obj))
                {
                    string assetPath = AssetDatabase.GetAssetPath(obj);
                    bool isIgnored = FR2_Setting.IgnoreAsset.Any(ignore =>
                        assetPath.Equals(ignore, StringComparison.OrdinalIgnoreCase) ||
                        assetPath.StartsWith(ignore + "/", StringComparison.OrdinalIgnoreCase));

                    if (isIgnored) ignoredAssets.Add(obj.name);
                    else nonIgnoredAssets.Add(obj);
                }
                else
                {
                    nonIgnoredAssets.Add(obj);
                }
            }

            if (ignoredAssets.Count > 0 && nonIgnoredAssets.Count == 0)
            {
                return objects.Length == 1
                    ? $"{objects[0].name} is in the ignore list and won't show references!"
                    : $"All {objects.Length} selected assets are in the ignore list and won't show references!";
            }

            if (ignoredAssets.Count > 0)
            {
                string baseMessage = GenerateBasicMessage(nonIgnoredAssets.ToArray(), action, suffix);
                return $"{baseMessage} ({ignoredAssets.Count} ignored asset{(ignoredAssets.Count > 1 ? "s" : "")} not shown)";
            }

            return GenerateBasicMessage(objects, action, suffix);
        }

        private static string GenerateBasicMessage(UnityObject[] objects, string action, string suffix)
        {
            if (objects == null || objects.Length == 0)
                return $"Nothing selected is {action}{suffix}!";

            bool isSceneRelated = suffix.Contains("GameObjects") || suffix.Contains("other objects");

            if (objects.Length == 1) return GenerateSingleMessage(objects[0], action, suffix, isSceneRelated);

            string selectionSummary = GenerateSelectionSummary(objects);

            if (action == "USED BY")
                return $"{selectionSummary} are not {action}{suffix}!";

            if (!isSceneRelated)
            {
                var scanStatus = GetMultipleAssetsScanStatus(objects);

                if (scanStatus.allUnscanned)
                    return ScanMessage(selectionSummary, "not scanned yet");
                if (scanStatus.allDirty)
                    return ScanMessage(selectionSummary, "content changed");
                if (scanStatus.hasMixed)
                    return ScanMessage(selectionSummary, "need scanning");
            }

            return $"{selectionSummary} are not {action}{suffix}!";
        }

        private static string GenerateSingleMessage(UnityObject obj, string action, string suffix, bool isSceneRelated)
        {
            if (obj == null) return "Object destroyed!";

            string name = obj.name;
            bool isAsset = AssetDatabase.Contains(obj);
            if (!isAsset) return $"{GetFriendlyTypeName(obj)} '{name}' is not {action}{suffix}!";

            string assetPath = AssetDatabase.GetAssetPath(obj);
            if (Directory.Exists(assetPath)) return $"{name} does not use any other assets!";

            string guid = FR2_Cache.AssetPathToGUID(assetPath);
            var asset = FR2_Cache.GetAsset(guid);

            bool isIgnoredAsset = FR2_Setting.IgnoreAsset.Any(ignore =>
                assetPath.Equals(ignore, StringComparison.OrdinalIgnoreCase) ||
                assetPath.StartsWith(ignore + "/", StringComparison.OrdinalIgnoreCase));
            bool isBuiltInAsset = asset != null && FR2_Asset.BUILT_IN_ASSETS.Contains(asset.guid);
            bool isNonCritical = asset != null && !asset.IsCriticalAsset();

            if (action == "USING" && (isIgnoredAsset || isBuiltInAsset || isNonCritical))
            {
                if (isIgnoredAsset) return $"{name} usage is skipped (Ignored asset)";
                if (isBuiltInAsset) return $"{name} usage is skipped (Built-in asset)";
                return $"{name} usage is skipped (Non-critical asset)";
            }

            if (action == "USED BY") return $"{name} is not {action}{suffix}!";

            if (!isSceneRelated)
            {
                var assetStatus = GetAssetScanStatus(asset);
                if (assetStatus.isNonScannable) return $"{name} does not use any other assets!";
                if (assetStatus.needsScanning) return ScanMessage(name, "not scanned yet");
                if (assetStatus.isDirty) return ScanMessage(name, "content changed");
            }

            return $"{name} is not {action}{suffix}!";
        }

        private static string ScanMessage(string subject, string issue)
        {
            return FR2_SettingExt.isAutoRefreshEnabled
                ? $"{subject} {issue} - click on FR2 panel to trigger auto refresh!"
                : $"{subject} {issue} - hit Refresh for complete results!";
        }

        internal static string GenerateSelectionSummary(UnityObject[] objects)
        {
            var typeCounts = new Dictionary<string, int>();

            foreach (var obj in objects)
            {
                string typeName = GetFriendlyTypeName(obj);
                if (typeCounts.ContainsKey(typeName)) typeCounts[typeName]++;
                else typeCounts[typeName] = 1;
            }

            var sortedTypes = typeCounts.OrderByDescending(kvp => kvp.Value)
                                       .ThenBy(kvp => kvp.Key)
                                       .ToList();

            if (sortedTypes.Count == 1)
            {
                var kvp = sortedTypes[0];
                return $"{kvp.Value} selected {GetPluralTypeName(kvp.Key, kvp.Value)}";
            }

            if (sortedTypes.Count <= 3)
            {
                var parts = sortedTypes.Select(kvp => $"{kvp.Value} {GetPluralTypeName(kvp.Key, kvp.Value)}");
                return string.Join(", ", parts);
            }

            return $"{objects.Length} selected objects";
        }

        internal static string GetFriendlyTypeName(UnityObject obj)
        {
            if (obj == null) return "Unknown";

            string typeName = obj.GetType().Name;

            switch (typeName)
            {
                case "Texture2D": return "Texture2D";
                case "Material": return "Material";
                case "AudioClip": return "Audio Clip";
                case "GameObject": return "GameObject";
                case "MonoScript": return "Script";
                case "DefaultAsset":
                    if (AssetDatabase.IsValidFolder(AssetDatabase.GetAssetPath(obj))) return "Folder";
                    return "Asset";
                case "Sprite": return "Sprite";
                case "Mesh": return "Mesh";
                case "Shader": return "Shader";
                case "AnimationClip": return "Animation";
                case "Cubemap": return "Cubemap";
                case "Font": return "Font";
                case "TextAsset": return "Text Asset";
                case "ScriptableObject": return "Scriptable Object";
                case "Prefab": return "Prefab";
                default:
                    if (typeName.Contains('.'))
                        typeName = typeName.Substring(typeName.LastIndexOf('.') + 1);
                    return typeName;
            }
        }

        private static string GetPluralTypeName(string typeName, int count)
        {
            if (count <= 1) return typeName;

            switch (typeName.ToLower())
            {
                case "gameobject": return "GameObjects";
                case "material": return "Materials";
                case "texture2d": return "Texture2Ds";
                case "audio clip": return "Audio Clips";
                case "script": return "Scripts";
                case "folder": return "Folders";
                case "sprite": return "Sprites";
                case "mesh": return "Meshes";
                case "shader": return "Shaders";
                case "animation": return "Animations";
                case "cubemap": return "Cubemaps";
                case "font": return "Fonts";
                case "text asset": return "Text Assets";
                case "scriptable object": return "Scriptable Objects";
                case "prefab": return "Prefabs";
                default: return typeName + "s";
            }
        }

        internal static (bool isNonScannable, bool needsScanning, bool isDirty) GetAssetScanStatus(FR2_Asset asset)
        {
            if (asset == null) return (false, false, false);
            var isNonScannable = asset.type == FR2_Asset.AssetType.DLL ||
                                 asset.type == FR2_Asset.AssetType.SCRIPT ||
                                 asset.type == FR2_Asset.AssetType.NON_READABLE ||
                                 !asset.IsCriticalAsset();

#if FR2_DEBUG
            if (asset.isDirty) FR2_LOG.Log($"GetAssetScanStatus: {asset.assetPath} -->\n isNonScannable: {isNonScannable} | needScanning: {!asset.hasBeenScanned} | isDirty = {asset.isDirty}");
#endif

            return (isNonScannable, !asset.hasBeenScanned && !isNonScannable, asset.isDirty && !isNonScannable);
        }

        private static (bool allUnscanned, bool allDirty, bool hasMixed) GetMultipleAssetsScanStatus(UnityObject[] objects)
        {
            if (!FR2_Cache.isReady || FR2_Cache._inst == null)
                return (false, false, false);

            bool hasUnscannedAssets = false;
            bool hasDirtyAssets = false;
            bool hasRegularAssets = false;

            foreach (var obj in objects)
            {
                if (!AssetDatabase.Contains(obj)) continue;

                string assetPath = AssetDatabase.GetAssetPath(obj);
                string guid = FR2_Cache.AssetPathToGUID(assetPath);
                var asset = FR2_Cache.GetAsset(guid, true);
                if (asset == null) continue;

                bool isNonScannable = asset.type == FR2_Asset.AssetType.DLL ||
                                      asset.type == FR2_Asset.AssetType.SCRIPT ||
                                      asset.type == FR2_Asset.AssetType.NON_READABLE ||
                                      !asset.IsCriticalAsset();

                if (isNonScannable) { hasRegularAssets = true; continue; }

                if (!asset.hasBeenScanned) hasUnscannedAssets = true;
                else if (asset.isDirty) hasDirtyAssets = true;
                else hasRegularAssets = true;
            }

            return (
                hasUnscannedAssets && !hasDirtyAssets && !hasRegularAssets,
                hasDirtyAssets && !hasUnscannedAssets && !hasRegularAssets,
                (hasUnscannedAssets || hasDirtyAssets) && hasRegularAssets
            );
        }

        private static string GetSceneContextInfo()
        {
            var prefabStage = UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage();
            if (prefabStage != null)
            {
                string prefabName = Path.GetFileNameWithoutExtension(prefabStage.assetPath);
                return $"current Prefab ({prefabName})";
            }

            int sceneCount = FR2_Unity.LoadedSceneCount;
            if (sceneCount == 0) return "current scene";

            if (sceneCount == 1)
            {
                var scene = FR2_Unity.GetActiveScene();
                if (scene.IsValid() && !string.IsNullOrEmpty(scene.name))
                    return $"current scene ({scene.name})";
                return "current scene";
            }

            return "current scenes";
        }
    }
}
