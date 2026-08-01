using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEditor;

using UnityObject = UnityEngine.Object;
using AddUsageCB = System.Action<string, long>;


namespace vietlabs.fr2
{
    internal static partial class FR2_Parser // BinaryAsset
    {
        private static readonly HashSet<string> BINARY_ASSET = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".asset", ".spriteatlas", ".unity"
        };
        
        private static bool Read_VerifyBinaryAsset(string assetPath)
        {
            try
            {
                foreach (string line in File.ReadLines(assetPath))
                {
                    return !line.StartsWith("%YAML", StringComparison.Ordinal);
                }
            } catch (Exception e)
            {
                FR2_LOG.LogWarning($"Read_VerifyBinaryAsset error: {assetPath}\n{e}");
            }

            // Should never be here!
            return false;
        }
        
        private static void ReadContent_BinaryAsset(string filePath, AddUsageCB callback)
        {
            int lastDot = filePath.LastIndexOf('.');
            string ext = lastDot >= 0 ? filePath.Substring(lastDot) : string.Empty;
            if (!BINARY_ASSET.Contains(ext)) return;
            
            var allAssets = AssetDatabase.LoadAllAssetsAtPath(filePath);
            foreach (UnityObject assetData in allAssets)
            {
                FR2_LOG.LogWarning($"Asset: {assetData} : {assetData.GetType()}");
                
                if (assetData is GameObject go)
                {
                    Component[] compList = go.GetComponentsInChildren<Component>(true);
                    for (var i = 0; i < compList.Length; i++)
                    {
                        LoadSerialized(compList[i], callback);
                    }
                }
                else if (assetData is TerrainData terrainData)
                {
                    Read_TerrainData(terrainData, callback);
                }
                else if (assetData is LightingDataAsset lightingDataAsset)
                {
                    Read_LightMap(lightingDataAsset, callback);
                }
                else
                {
                    LoadSerialized(assetData, callback);
                }
            }
        }
    }
}
