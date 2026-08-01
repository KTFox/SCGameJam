using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace vietlabs.fr2
{
    [InitializeOnLoad]
    internal class FR2_CacheHelper : AssetPostprocessor
    {
        [NonSerialized] private static HashSet<string> scenes;
        [NonSerialized] private static HashSet<string> guidsIgnore;
        [NonSerialized] internal static bool inited = false; 
        
        static FR2_CacheHelper()
        {
            FR2_Cache.onReady -= InitHelper;
            FR2_Cache.onReady += InitHelper;
            EditorApplication.update -= InitHelper;
            EditorApplication.update += InitHelper;
        }

        private static void OnPostprocessAllAssets(
            string[] importedAssets, string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            if (FR2_SettingExt.disable) return;
            
            // During import, ONLY accumulate dirty marks.
            // NEVER trigger refresh/processing here — that causes SetDirty → reimport → OnPostprocess loop.
            // FR2 will pick up dirty marks when editor becomes idle (via CheckDelayRefresh).
            
            FR2_Cache.MarkAssetPathContentDirty(importedAssets);
            FR2_Cache.MarkAssetPathChanged(movedAssets);
            FR2_Cache.MarkAssetPathDeleted(deletedAssets);
            
            // Only schedule a deferred refresh — it will wait for editor idle via CheckDelayRefresh
            if (FR2_Cache.autoRefresh && !FR2_Cache.suppressRefresh)
            {
                FR2_Cache.IncrementalRefresh();
            }
        }
        
        internal static void InitHelper()
        {
            if (FR2_SettingExt.disable) return;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            if (!FR2_Cache.isReady) return;
            FR2_Cache.onReady -= InitHelper;
            EditorApplication.update -= InitHelper;
            
            inited = true;
            InitListScene();
            InitIgnore();
            _labelWidthCache.Clear();
            CheckGitStatus(false);
            
            EditorBuildSettings.sceneListChanged -= InitListScene;
            EditorBuildSettings.sceneListChanged += InitListScene;

            #if UNITY_6000_4_OR_NEWER
            EditorApplication.projectWindowItemByEntityIdOnGUI -= OnGUIProjectInstanceByEntityId;
            EditorApplication.projectWindowItemByEntityIdOnGUI += OnGUIProjectInstanceByEntityId;
            #elif UNITY_2022_1_OR_NEWER
            EditorApplication.projectWindowItemInstanceOnGUI -= OnGUIProjectInstance;
            EditorApplication.projectWindowItemInstanceOnGUI += OnGUIProjectInstance;
            #else
            EditorApplication.projectWindowItemOnGUI -= OnGUIProjectItem;
            EditorApplication.projectWindowItemOnGUI += OnGUIProjectItem;
            #endif

            InitIgnore();
            EditorApplication.RepaintProjectWindow();
        }
        
        private static void CheckGitStatus(bool force)
        {
            if (FR2_SettingExt.gitIgnoreAdded && !force) return;
            FR2_SettingExt.isGitProject = FR2_GitUtil.IsGitProject();
            if (!FR2_SettingExt.isGitProject) return;
            FR2_SettingExt.gitIgnoreAdded = FR2_GitUtil.CheckGitIgnoreContainsFR2Cache();
        }
        
        public static void InitIgnore()
        {
            guidsIgnore = new HashSet<string>();
            foreach (string item in FR2_Setting.IgnoreAsset)
            {
                string guid = FR2_Cache.AssetPathToGUID(item);
                guidsIgnore.Add(guid);
            }
        }

        private static void InitListScene()
        {
            scenes = new HashSet<string>();
            foreach (var scene in EditorBuildSettings.scenes)
            {
                var sce = FR2_Cache.AssetPathToGUID(scene.path);
                if (!string.IsNullOrEmpty(sce)) scenes.Add(sce);
            }
        }

        private static string lastGUID;
        private static readonly Dictionary<string, float> _labelWidthCache = new Dictionary<string, float>(256);
        private static GUIContent _plusContent;

        private static void OnGUIProjectInstance(int instanceID, Rect selectionRect)
        {
            if (FR2_SettingExt.disable) return;
            try
            {
                OnGUIProjectInstanceInternal(instanceID, selectionRect);
            }
            catch (Exception ex)
            {
                FR2_LOG.LogWarning($"[FR2] OnGUIProjectInstance exception (suppressed): {ex.Message}");
            }
        }

#if UNITY_6000_4_OR_NEWER
        private static void OnGUIProjectInstanceByEntityId(EntityId entityId, Rect selectionRect)
        {
            if (FR2_SettingExt.disable) return;
            try
            {
                int instanceId = FR2_Unity.EntityIdToInstanceId(entityId);
                if (instanceId == 0) return;
                OnGUIProjectInstanceInternal(instanceId, selectionRect);
            }
            catch (Exception ex)
            {
                FR2_LOG.LogWarning($"[FR2] OnGUIProjectInstance exception (suppressed): {ex.Message}");
            }
        }
#endif

        private static void OnGUIProjectInstanceInternal(int instanceID, Rect selectionRect)
        {
            string guid = FR2_Cache.GetGuidByInstanceId(instanceID);
            if (string.IsNullOrEmpty(guid)) return;

            bool isMainAsset = guid != lastGUID;
            lastGUID = guid;

            if (isMainAsset)
            {
                DrawProjectItem(guid, selectionRect);
                return;
            }

            if (!FR2_Cache._inst._setting.showSubAssetFileId) return;
            DrawSubAssetFileId(instanceID, selectionRect);
        }

        private static void DrawSubAssetFileId(int instanceID, Rect selectionRect)
        {
            var obj = FR2_Unity.InstanceIdToObject(instanceID);
            if (obj == null) return;
            if (!FR2_Cache.TryGetGUIDAndLocalFileIdentifier(obj, out _, out long localId)) return;

            var label = FR2_GUIContent.FromInt((int)localId);

            var rect2 = selectionRect;
            rect2.xMin = rect2.xMax - EditorStyles.miniLabel.CalcSize(label).x;

            using (FR2_Scope.GUIColor(new Color(.5f, .5f, .5f, 0.5f)))
            {
                GUI.Label(rect2, label, EditorStyles.miniLabel);
            }
        }

        private static void OnGUIProjectItem(string guid, Rect rect)
        {
            if (FR2_SettingExt.disable) return;
            bool isMainAsset = guid != lastGUID;
            lastGUID = guid;
            if (isMainAsset) DrawProjectItem(guid, rect);
        }

        private static void DrawProjectItem(string guid, Rect rect)
        {
            var r = new Rect(rect.x, rect.y, 1f, 16f);
            if (scenes.Contains(guid))
            {
                EditorGUI.DrawRect(r, GUI2.Theme(new Color32(72, 150, 191, 255), Color.blue));
            }
            else if (guidsIgnore.Contains(guid))
            {
                var ignoreRect = new Rect(rect.x + 3f, rect.y + 6f, 2f, 2f);
                EditorGUI.DrawRect(ignoreRect, GUI2.darkRed);
            }
            
            if (!FR2_Cache.isReady) return;
            if (!FR2_Setting.ShowReferenceCount) return;
            
            // Don't show counts while building the map
            if (FR2_Cache.status == FR2_Status.BuildUsedByMap) return;

            var api = FR2_Cache._inst;
            if (FR2_Cache._map == null) FR2_Cache.Check4Changes(false);
            if (!FR2_Cache._map.TryGetValue(guid, out FR2_Asset item)) return;

            if (item == null) return;
            if (item.UsedByMap.Count > 0)
            {
                int count = item.UsedByMap.Count;
                var content = FR2_GUIContent.FromInt(count);
                
                if (FR2_Setting.BadgeReferenceCount)
                {
                    string assetPath = FR2_Cache.GUIDToAssetPath(guid);
                    string assetName = System.IO.Path.GetFileNameWithoutExtension(assetPath);
                    if (!string.IsNullOrEmpty(assetName))
                    {
                        if (!_labelWidthCache.TryGetValue(assetName, out float labelWidth))
                        {
                            var labelContent = FR2_GUIContent.FromString(assetName);
                            labelWidth = EditorStyles.label.CalcSize(labelContent).x;
                            _labelWidthCache[assetName] = labelWidth;
                        }
                        var isRow = rect.height < 20f;
                        var x = isRow ? rect.x + labelWidth + 24f : rect.x;
                        FR2_Badge.Draw(new Vector2(x, rect.y), count, isRow);
                    }
                }
                else
                {
                    r.width = 0f;
                    r.xMin -= 100f;
                    GUI.Label(r, content, GUI2.miniLabelAlignRight);
                }
            }
            else if (item.forcedIncludedInBuild)
            {
                using (FR2_Scope.GUIColor(GUI.color.Alpha(0.2f)))
                {
                    if (_plusContent == null) _plusContent = FR2_GUIContent.FromString("+");
                    
                    r.width = 0f;
                    r.xMin -= 100f;
                    GUI.Label(r, _plusContent, GUI2.miniLabelAlignRight);
                }
            }
        }
}
}
