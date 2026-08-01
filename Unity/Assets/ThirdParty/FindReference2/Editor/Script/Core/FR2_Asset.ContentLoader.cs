using System;
using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityObject = UnityEngine.Object;
using TerrainTextureData = vietlabs.fr2.FR2_Terrain.TerrainTextureData;

namespace vietlabs.fr2
{
    internal partial class FR2_Asset
    {
        // ----------------------------- CONTENT LOADING ---------------------------------------
        
        internal void LoadContentFast()
        {
            #if FR2_DEBUG
            if (fileInfoDirty) FR2_LOG.LogWarning($"Something wrong? info dirty: {assetPath}\n readTS = {m_fileInfoReadTS} | changeTS = {m_assetChangeTS}");
            #endif
            
            if (!fileContentDirty) return;
            m_cachefileWriteTS = m_fileWriteTS;
            m_forceIncludeInBuild = false;

            if (IsMissing) return;
            if (type == AssetType.SCRIPT || type == AssetType.DLL || type == AssetType.FOLDER) return;
            // if (assetPath.StartsWith("Packages/")) return;

            var startTime = DateTime.Now;
            if (shouldWriteImportLog && (fileSize >= MIN_FILE_SIZE_2LOG))
            {
                var logMessage = $"{startTime:yyyy-MM-dd HH:mm:ss} - {assetPath}, Size: {fileSize} bytes";
                File.AppendAllText(logPath, logMessage);
            }

            ClearUseGUIDs();

            if (fileSize > 5 * 1024 * 1024 || type == AssetType.NON_READABLE || IsBinaryAsset)
            {
                var dependencies = AssetDatabase.GetDependencies(assetPath, false);
                foreach (var dependency in dependencies)
                {
                    var newGUID = FR2_Cache.AssetPathToGUID(dependency);
                    if (!string.IsNullOrEmpty(newGUID) && (newGUID != guid))
                    {
                        AddUseGUID(newGUID);
                    }
                }
            } else if (IsReferencable)
            {
                LoadYAML2();
                
                // CRITICAL FIX: Validate with AssetDatabase to catch missing references
                ValidateWithAssetDatabase();
            }
            
            if (shouldWriteImportLog && (fileSize >= MIN_FILE_SIZE_2LOG))
            {
                var endTime = DateTime.Now;
                double duration = (endTime - startTime).TotalMilliseconds;
                var logMessage = $", Duration: {duration} ms";
                File.AppendAllText(logPath, logMessage + Environment.NewLine);
            }
        }

        internal void ValidateWithAssetDatabase()
        {
#if !FR2_DEV
            return;
#else
            if (!FR2_SettingExt.dbValidation) return;
            if (IsMissing || IsFolder || type == AssetType.SCRIPT) return;
            
            string[] unityDeps = AssetDatabase.GetDependencies(assetPath, false);
            
            if (unityDeps == null || unityDeps.Length == 0) return;
            
            var extraGuids = new List<string>();
            foreach (var dep in unityDeps)
            {
                if (dep == assetPath) continue;
                var guid = FR2_Cache.AssetPathToGUID(dep);
                if (string.IsNullOrEmpty(guid) || UseGUIDs.ContainsKey(guid)) continue;
                extraGuids.Add(guid);
            }
            
            if (extraGuids.Count == 0) return;
            
            var skipAdd = extension == ".tss" || extension == ".uss";
            var skipWarning = extension == ".shadergraph" || extension == ".tss" || extension == ".uss" ||
                             extension == ".vfx" || extension == ".vfxoperator" || extension == ".vfxblock"; 
            if (!skipWarning) FR2_LOG.LogWarning($"[DB Validation] {extension} | AssetDatabase has {extraGuids.Count} more deps than FR2 parser for '{assetPath}'");
            
            if (skipAdd) return;
            for (var i = 0; i < extraGuids.Count; i++)
            {
                AddUseGUID(extraGuids[i]);
                FR2_LOG.LogWarning($"[DB Validation] Missing ref [{i + 1}/{extraGuids.Count}]: {extraGuids[i]} --> {FR2_Cache.GUIDToAssetPath(extraGuids[i])}");
            }
#endif
        }

        internal void LoadYAML2()
        {
            if (!m_pathLoaded) LoadPathInfo();

            if (!File.Exists(m_assetPath))
            {
                state = AssetState.MISSING;
                return;
            }

            if (m_assetPath == "ProjectSettings/EditorBuildSettings.asset")
            {
                EditorBuildSettingsScene[] listScenes = EditorBuildSettings.scenes;
                FR2_LOG.LogWarning($"[ContentLoader] Scanning EditorBuildSettings.asset | Found {listScenes.Length} scenes");
                
                foreach (EditorBuildSettingsScene scene in listScenes)
                {
                    if (!scene.enabled) continue;
                    string path = scene.path;
                    string guid = FR2_Cache.AssetPathToGUID(path);

                    AddUseGUID(guid, 0);
                    FR2_LOG.Log($"[ContentLoader] EditorBuildSettings -> Scene: {path} | GUID={guid}");
                }
            }

            if (string.IsNullOrEmpty(extension))
            {
                FR2_LOG.LogWarning($"Something wrong? <{m_extension}>");
            }

            if (extension == ".spriteatlas" || extension == ".spriteatlasv2")
            {
                var metaPath = m_assetPath + ".meta";
                if (File.Exists(metaPath))
                {
                    foreach (var line in File.ReadLines(metaPath))
                    {
                        int idx = line.IndexOf("bindAsDefault:", StringComparison.Ordinal);
                        if (idx < 0) continue;
                        var val = line.Substring(idx + 14).Trim();
                        m_forceIncludeInBuild = val == "1";
                        break;
                    }
                }
            }
            
            FR2_Parser.ReadContent(m_assetPath, AddUseGUID);
        }

        internal void LoadFolder()
        {
            if (!Directory.Exists(m_assetPath))
            {
                state = AssetState.MISSING;
                return;
            }

            // do not analyse folders outside project
            if (!m_assetPath.StartsWith("Assets/")) return;

            try
            {
                string[] files = Directory.GetFiles(m_assetPath);
                string[] dirs = Directory.GetDirectories(m_assetPath);

                foreach (string f in files)
                {
                    if (f.EndsWith(".meta", StringComparison.Ordinal)) continue;

                    string fguid = FR2_Cache.AssetPathToGUID(f);
                    if (string.IsNullOrEmpty(fguid)) continue;

                    AddUseGUID(fguid);
                }

                foreach (string d in dirs)
                {
                    string fguid = FR2_Cache.AssetPathToGUID(d);
                    if (string.IsNullOrEmpty(fguid)) continue;

                    AddUseGUID(fguid);
                }
            }
            catch (Exception e)
            {
                FR2_LOG.LogWarning("LoadFolder() error :: " + e + "\n" + assetPath);
            }
            finally
            {

                state = AssetState.MISSING;
            }
        }

        internal void LoadBinaryAsset()
        {
            ClearUseGUIDs();

            UnityObject assetData = AssetDatabase.LoadAssetAtPath(m_assetPath, typeof(UnityObject));
            if (assetData is GameObject go)
            {
                type = AssetType.MODEL;
                LoadGameObject(go);
                binaryLoaded += 10;
            } else if (assetData is TerrainData terrainData)
            {
                type = AssetType.TERRAIN;
                LoadTerrainData(terrainData);
                binaryLoaded += 20;
            } else if (assetData is LightingDataAsset lightAsset)
            {
                type = AssetType.LIGHTING_DATA;
                LoadLightingData(lightAsset);
                binaryLoaded += 20;
            } else
            {
                LoadSerialized(assetData);
                binaryLoaded++;
            }

			FR2_LOG.Log("LoadBinaryAsset :: " + assetData + ":" + type);
            if (binaryLoaded <= 30) return;
            binaryLoaded = 0;
            FR2_Unity.UnloadUnusedAssets();
        }

        internal void LoadGameObject(GameObject go)
        {
            Component[] compList = go.GetComponentsInChildren<Component>();
            for (var i = 0; i < compList.Length; i++)
            {
                LoadSerialized(compList[i]);
            }
        }

        internal void LoadSerialized(UnityObject target)
        {
            SerializedProperty[] props = FR2_Unity.xGetSerializedProperties(target, true);

            for (var i = 0; i < props.Length; i++)
            {
                if (props[i].propertyType != SerializedPropertyType.ObjectReference) continue;

                UnityObject refObj = props[i].objectReferenceValue;
                if (refObj == null) continue;

                string refGUID = FR2_Cache.AssetPathToGUID(
                    AssetDatabase.GetAssetPath(refObj)
                );

                AddUseGUID(refGUID);
            }
        }

        private void AddTextureGUID(SerializedProperty prop)
        {
            if (prop == null || prop.objectReferenceValue == null) return;
            string path = AssetDatabase.GetAssetPath(prop.objectReferenceValue);
            if (string.IsNullOrEmpty(path)) return;
            AddUseGUID(FR2_Cache.AssetPathToGUID(path));
        }

        internal void LoadLightingData(LightingDataAsset asset)
        {
            foreach (Texture texture in FR2_Lightmap.Read(asset))
            {
                if (texture == null) continue;
                string path = AssetDatabase.GetAssetPath(texture);
                string assetGUID = FR2_Cache.AssetPathToGUID(path);
                if (!string.IsNullOrEmpty(assetGUID))
                {
                    AddUseGUID(assetGUID);
                }
            }
        }

        internal void LoadTerrainData(TerrainData terrain)
        {
            TerrainLayer[] arr0 = terrain.terrainLayers;
            for (var i = 0; i < arr0.Length; i++)
            {
                string aPath = AssetDatabase.GetAssetPath(arr0[i]);
                string refGUID = FR2_Cache.AssetPathToGUID(aPath);
                AddUseGUID(refGUID);
            }

            DetailPrototype[] arr = terrain.detailPrototypes;

            for (var i = 0; i < arr.Length; i++)
            {
                string aPath = AssetDatabase.GetAssetPath(arr[i].prototypeTexture);
                string refGUID = FR2_Cache.AssetPathToGUID(aPath);
                AddUseGUID(refGUID);
            }

            TreePrototype[] arr2 = terrain.treePrototypes;
            for (var i = 0; i < arr2.Length; i++)
            {
                string aPath = AssetDatabase.GetAssetPath(arr2[i].prefab);
                string refGUID = FR2_Cache.AssetPathToGUID(aPath);
                AddUseGUID(refGUID);
            }

            TerrainTextureData[] arr3 = FR2_Terrain.GetTerrainTextureDatas(terrain);
            for (var i = 0; i < arr3.Length; i++)
            {
                TerrainTextureData texs = arr3[i];
                for (var k = 0; k < texs.textures.Length; k++)
                {
                    Texture2D tex = texs.textures[k];
                    if (tex == null) continue;

                    string aPath = AssetDatabase.GetAssetPath(tex);
                    if (string.IsNullOrEmpty(aPath)) continue;

                    string refGUID = FR2_Cache.AssetPathToGUID(aPath);
                    if (string.IsNullOrEmpty(refGUID)) continue;

                    AddUseGUID(refGUID);
                }
            }
        }

        internal static void ClearLog()
        {
            if (shouldWriteImportLog)
            {
                File.WriteAllText(logPath, string.Empty);
            } else
            {
                if (File.Exists(logPath)) File.Delete(logPath);
            }

            scanStartTime = DateTime.Now;
        }

        internal static void WriteTotalScanTime()
        {
            if (!shouldWriteImportLog) return;
            double totalScanTime = (DateTime.Now - scanStartTime).TotalSeconds;
            File.AppendAllText(logPath, $"\nTotal scan time: {totalScanTime} seconds\n");
        }

        private void ClearUseGUIDs()
        {
		    // FR2_LOG.Log("ClearUseGUIDs: " + assetPath);
            UseGUIDs.Clear();
            UseGUIDsList.Clear();
        }
    }
} 