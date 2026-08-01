using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;

namespace vietlabs.fr2
{
    internal partial class FR2_Cache
    {
        internal List<List<string>> ScanSimilar(Action IgnoreWhenScan, Action IgnoreFolderWhenScan)
        {
            if (!isReady)
            {
                FR2_LOG.LogWarning("ScanSimilar() call when FR2_Cache is not ready!");
                return new List<List<string>>();
            }
            
            var dict = new Dictionary<string, List<FR2_Asset>>(_map.Count);
            foreach (KeyValuePair<string, FR2_Asset> item in _map)
            {
                if (item.Value == null) continue;
                if (item.Value.IsMissing || item.Value.IsFolder) continue;
                if (item.Value.IsBinaryAsset) continue;
                if (item.Value.inPlugins) continue;
                if (item.Value.inEditor) continue;
                if (item.Value.IsExcluded) continue;
                if (!item.Value.assetPath.StartsWith("Assets/")) continue;
                if (IsModelDerivedMaterial(item.Value)) continue;
                if (FR2_Setting.IsTypeExcluded(FR2_AssetGroupDrawer.GetIndex(item.Value.extension)))
                {
                    if (IgnoreWhenScan != null) IgnoreWhenScan();
                    continue;
                }

                string hash = item.Value.fileInfoHash;
                if (string.IsNullOrEmpty(hash)) continue;

                if (!dict.TryGetValue(hash, out List<FR2_Asset> list))
                {
                    list = new List<FR2_Asset>();
                    dict.Add(hash, list);
                }

                list.Add(item.Value);
            }

            return dict.Values
                .Where(item => item.Count > 1)
                .OrderByDescending(item => item[0].fileSize)
                .Select(item => item.Select(asset => asset.assetPath).ToList())
                .ToList();
        }

        private static bool IsModelDerivedMaterial(FR2_Asset asset)
        {
            if (asset.extension != ".mat") return false;
            var usedBy = asset.UsedByMap;
            if (usedBy == null || usedBy.Count == 0) return false;
            foreach (var kvp in usedBy)
            {
                if (kvp.Value == null) continue;
                if (kvp.Value.type != FR2_Asset.AssetType.MODEL) return false;
            }
            return true;
        }

        private static bool ShouldExcludeFromUnused(FR2_Asset v, HashSet<string> addressable)
        {
            if (v.IsMissing || v.inEditor || v.IsScript || v.inResources || v.inPlugins || v.inStreamingAsset || v.IsFolder) return true;
            if (!v.assetPath.StartsWith("Assets/")) return true;
            if (v.forcedIncludedInBuild) return true;
            if (v.assetName == "LICENSE") return true;

            var ignoreList = FR2_Setting.IgnoreAsset;
            foreach (var ignore in ignoreList)
            {
                if (v.assetPath.Equals(ignore, StringComparison.OrdinalIgnoreCase)) return true;
                if (v.assetPath.StartsWith(ignore, StringComparison.OrdinalIgnoreCase) &&
                    v.assetPath.Length > ignore.Length && v.assetPath[ignore.Length] == '/') return true;
            }

            string ext = FR2_StringCache.GetCachedExtension(v.assetPath);
            Type assetType = AssetDatabase.GetMainAssetTypeAtPath(v.assetPath);
            if (string.IsNullOrEmpty(ext) || assetType == typeof(DefaultAsset)) return true;

            if (SPECIAL_USE_ASSETS.Contains(v.assetPath)) return true;
            if (SPECIAL_EXTENSIONS.Contains(v.extension)) return true;
            if (v.type == FR2_Asset.AssetType.DLL) return true;
            if (v.type == FR2_Asset.AssetType.SCRIPT) return true;
            if (v.type == FR2_Asset.AssetType.UNKNOWN) return true;
            if (addressable.Contains(v.guid)) return true;
            if (v.IsExcluded) return true;
            if (!string.IsNullOrEmpty(v.AtlasName)) return true;
            if (!string.IsNullOrEmpty(v.AssetBundleName)) return true;
            if (!string.IsNullOrEmpty(v.AddressableName)) return true;
            return false;
        }

        internal List<FR2_Asset> ScanUnused(bool recursive = true)
        {
            if (!isReady)
            {
                FR2_LOG.LogWarning("ScanUnused() call when FR2_Cache is not ready!");
                return new List<FR2_Asset>();
            }

            // Get Addressable assets - optimized to avoid LINQ
            var addressable = new HashSet<string>();
            if (FR2_Addressable.isOk)
            {
                var addresses = FR2_Addressable.GetAddresses();
                foreach (var kvp in addresses)
                {
                    foreach (var guid in kvp.Value.assetGUIDs)
                    {
                        addressable.Add(guid);
                    }
                    foreach (var guid in kvp.Value.childGUIDs)
                    {
                        addressable.Add(guid);
                    }
                }
            }

            var result = new List<FR2_Asset>();
            var unusedAssets = new HashSet<string>();
            
            foreach (KeyValuePair<string, FR2_Asset> item in _map)
            {
                FR2_Asset v = item.Value;
                
                if (ShouldExcludeFromUnused(v, addressable)) continue;

                if ((v.extension == ".spriteatlas" || v.extension == ".spriteatlasv2") && v.UsedByMap.Count > 0) continue;

                if (v.UsedByMap.Count == 0)
                {
                    result.Add(v);
                    unusedAssets.Add(v.guid);
                }
            }
            
            // If not recursive, return the level 1 results
            if (!recursive)
            {
                result.Sort((item1, item2) => item1.extension == item2.extension
                    ? string.Compare(item1.assetPath, item2.assetPath, StringComparison.Ordinal)
                    : string.Compare(item1.extension, item2.extension, StringComparison.Ordinal));
                    
                return result;
            }
            
            // Recursive scan for higher level unused assets
            bool foundNewUnused = true;
            while (foundNewUnused)
            {
                foundNewUnused = false;
                var newUnusedAssets = new HashSet<string>();
                
                foreach (KeyValuePair<string, FR2_Asset> item in _map)
                {
                    FR2_Asset v = item.Value;
                    
                    if (unusedAssets.Contains(v.guid)) continue;
                    if (ShouldExcludeFromUnused(v, addressable)) continue;

                    if (v.UsedByMap.Count > 0)
                    {
                        bool onlyUsedByUnusedAssets = true;
                        foreach (var usedBy in v.UsedByMap)
                        {
                            if (!unusedAssets.Contains(usedBy.Key))
                            {
                                onlyUsedByUnusedAssets = false;
                                break;
                            }
                        }
                        
                        if (onlyUsedByUnusedAssets)
                        {
                            result.Add(v);
                            newUnusedAssets.Add(v.guid);
                            foundNewUnused = true;
                        }
                    }
                }
                
                unusedAssets.UnionWith(newUnusedAssets);
            }

            result.Sort((item1, item2) => item1.extension == item2.extension
                ? string.Compare(item1.assetPath, item2.assetPath, StringComparison.Ordinal)
                : string.Compare(item1.extension, item2.extension, StringComparison.Ordinal));

            return result;
        }
    }
} 