using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityObject = UnityEngine.Object;

namespace vietlabs.fr2
{
    internal partial class FR2_WindowAll
    {
        protected void DrawScanProject()
        {
            bool writeImportLog = settings.writeImportLog;
            settings.writeImportLog = EditorGUILayout.Toggle("Write Import Log", settings.writeImportLog);
            if (writeImportLog != settings.writeImportLog)
            {
                EditorUtility.SetDirty(this);
            }

            if (GUILayout.Button("Scan project"))
            {
                FR2_Asset.shouldWriteImportLog = writeImportLog;
                FR2_Cache.DeleteCache();
                FR2_Cache.CreateCache();
            }
        }
        
        protected bool CheckDrawImport()
        {
            FR2_Unity.RefreshEditorStatus();
            
            InitIfNeeded();

            if (EditorSettings.serializationMode != SerializationMode.ForceText)
            {
                EditorGUILayout.HelpBox("FR2 requires serialization mode set to FORCE TEXT!", MessageType.Warning);
                if (GUILayout.Button("FORCE TEXT")) EditorSettings.serializationMode = SerializationMode.ForceText;

                return false;
            }

            if (FR2_Cache.cacheStatus == FR2_CacheStatus.Incompatible)
            {
                EditorGUILayout.HelpBox("Incompatible cache version found!!!\nFR2 will need a full refresh and according to your project's size this process may take several minutes to complete finish!",
                    MessageType.Warning);

                DrawScanProject();
                return false;
            }

            if (FR2_Cache.isReady) return DrawEnable();

            if (!FR2_Cache.hasCache)
            {
                EditorGUILayout.HelpBox(
                    "FR2 cache not found!\nA first scan is needed to build the cache for all asset references.\nDepending on the size of your project, this process may take a few minutes to complete but once finished, searching for asset references will be incredibly fast!",
                    MessageType.Warning);

                DrawScanProject();
                return false;
            }

            if (!DrawEnable()) return false;

            if (FR2_Cache.status == FR2_Status.ReadAsset || FR2_Cache.status == FR2_Status.BuildUsedByMap)
            {
                var (progress, text) = FR2_Cache.GetOverallProgress();
                
                // Show current asset path only during ReadAsset phase
                if (FR2_Cache.status == FR2_Status.ReadAsset)
                {
                    var progressInfo = FR2_Cache.GetReadFileProgress();
                    var currentAssetPath = progressInfo.assetPath;
                    if (!string.IsNullOrEmpty(currentAssetPath))
                    {
                        EditorGUILayout.LabelField(currentAssetPath, EditorStyles.miniLabel);
                    }
                }

                Rect rect = GUILayoutUtility.GetRect(1f, Screen.width, 18f, 18f);
                EditorGUI.ProgressBar(rect, progress, text);
                Repaint();
                return false;
            }
            
            // Show status message for any other non-ready state
            string statusMessage = GetCacheStatusMessage();
            if (!string.IsNullOrEmpty(statusMessage))
            {
                EditorGUILayout.HelpBox(statusMessage, MessageType.Info);
                Repaint();
                return false;
            }
            
            return false;
        }
        
        private string GetCacheStatusMessage()
        {
            if (FR2_Cache._inst == null) return "FR2 cache not initialized";
            
            switch (FR2_Cache.status)
            {
                case FR2_Status.None:
                    return "Initializing FR2 cache...";
                case FR2_Status.Initialized:
                    return "FR2 initialized - preparing cache...";
                case FR2_Status.Search4Cache:
                    return "Searching for existing cache...";
                case FR2_Status.Wait4CacheCreate:
                    return "Waiting for cache creation...";
                case FR2_Status.ValidateCache:
                    return "Validating cache...";
                case FR2_Status.Wait4Refresh:
                    return "Waiting for cache refresh...";
                case FR2_Status.InitCacheMap:
                    return "Initializing cache maps...";
                case FR2_Status.RefreshDB:
                    return "Refreshing asset database...";
                case FR2_Status.ReadAsset:
                    return "Reading assets...";
                case FR2_Status.BuildUsedByMap:
                    return "Building reference maps...";
                case FR2_Status.Ready:
                    return null; // Ready state - no message needed
                default:
                    return $"FR2 cache status: {FR2_Cache.status}";
            }
        }
    }
} 