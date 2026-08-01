using System;
using UnityEditor;
using UnityEngine;
using static vietlabs.fr2.FR2_Scope;

namespace vietlabs.fr2
{
    internal partial class FR2_WindowAll
    {
        private void DrawScenePanel(Rect rect)
        {
            FR2_RefDrawer drawer = isFocusingUses
                ? IsSelectingAssets ? null : SceneUsesDrawer
                : IsSelectingAssets ? RefInScene : RefSceneInScene;
            
            if (drawer == null) return;

            var api = FR2_SceneCache.Api;
            if (api == null) return;

            bool isScanning = api.Status == SceneCacheStatus.Scanning || 
                             !string.IsNullOrEmpty(api.currentSceneName);
            
            if (isScanning)
            {
                DrawSceneCacheProgress(rect);
                rect.yMin += 18f;
            }
            
            if (FR2_SceneCache.hasCache || isScanning) 
            {
                drawer.Draw(rect);
            }
        }

        private void DrawSceneCacheProgress(Rect rect)
        {
            var api = FR2_SceneCache.Api;
            if (api == null) return;

            Rect rr = rect;
            rr.height = 16f;

            if (api.Status == SceneCacheStatus.Scanning)
            {
                int scenes = api.totalScenes;
                int done = api.scenesCompleted;
                int cur = api.current, tot = api.total;
                string sceneName = api.currentSceneName;

                float progress;
                string progressText;

                if (scenes <= 1)
                {
                    progress = tot > 0 ? Mathf.Clamp01(cur * 1f / tot) : 0f;
                    string sceneInfo = !string.IsNullOrEmpty(sceneName) ? $" [{sceneName}]" : "";
                    progressText = tot > 0 ? $"Scanning: {cur} / {tot}{sceneInfo}" : $"Loading{sceneInfo}...";
                }
                else
                {
                    float sceneProgress = tot > 0 ? Mathf.Clamp01(cur * 1f / tot) : 0f;
                    progress = Mathf.Clamp01((done + sceneProgress) / scenes);
                    string sceneInfo = !string.IsNullOrEmpty(sceneName) ? $" [{sceneName}]" : "";
                    progressText = $"Scene {done + 1} / {scenes}{sceneInfo}";
                }

                EditorGUI.ProgressBar(rr, progress, progressText);
                WillRepaint = true;
                return;
            }
            
            string statusText;
            switch (api.Status)
            {
                case SceneCacheStatus.None:
                    statusText = "Scene cache is not ready!";
                    break;
                case SceneCacheStatus.Changed:
                    statusText = "Scene changed - results might be incompleted";
                    break;
                case SceneCacheStatus.Scanning:
                    statusText = "Preparing to scan scene objects...";
                    break;
                case SceneCacheStatus.Ready:
                    statusText = api.HasPartialCaches
                        ? "Scene cache ready (partial — some results may be incomplete)"
                        : "Scene cache ready";
                    break;
                default:
                    statusText = "Unknown status";
                    break;
            }
            
            EditorGUI.ProgressBar(rr, 0f, statusText);
        }



        private void DrawAssetPanel(Rect rect)
        {
            FR2_RefDrawer drawer = GetAssetDrawer();
            if (drawer == null) return;
            drawer.Draw(rect);

            if (!drawer.showDetail) return;

            settings.details = true;
            drawer.showDetail = false;
            sp1.SetSplitVisible(2, settings.details);
            Repaint();
        }

        private void DrawGitWarningPanel()
        {
            if (!FR2_SettingExt.isGitProject || FR2_SettingExt.gitIgnoreAdded || FR2_SettingExt.hideGitIgnoreWarning) return;
            
            using (FR2_Scope.HzLayout())
            {
                // Left side: Warning message
                using (FR2_Scope.VtLayout(GUILayout.ExpandWidth(true)))
                {
                    EditorGUILayout.HelpBox("You should add **/FR2_Cache.asset* to your .gitignore file to avoid committing cache files.", MessageType.Warning);
                }
                
                // Right side: Buttons stacked vertically
                using (FR2_Scope.VtLayout(FR2_Theme.Current.ApplyButtonWidth))
                {
                    if (GUILayout.Button("Apply", FR2_Theme.Current.CompactButtonHeight))
                    {
                        FR2_GitUtil.AddFR2CacheToGitIgnore();
                        FR2_SettingExt.gitIgnoreAdded = true;
                    }

                    if (GUILayout.Button("Ignore", FR2_Theme.Current.CompactButtonHeight))
                    {
                        FR2_SettingExt.hideGitIgnoreWarning = true;
                    }
                }
            }
        }

        internal bool DrawButton(Rect rect, ref bool show, GUIContent icon)
        {
            if (!FR2_ToolbarButton.Toggle(rect, ref show, icon)) return false;
            EditorUtility.SetDirty(this);
            WillRepaint = true;
            return true;
        }

        internal float DrawAssetTitleBarControls(Rect titleRect, float rightEdge)
        {
            var vs = settings.assetView;
            ValidateGroupMode(ref vs.groupMode, FR2_RefDrawer.AssetGroupModes);
            if (vs.sortModeED == null) vs.sortModeED = new FR2_EnumDrawer { tooltip = "Sort By" };
            if (vs.groupModeED == null) vs.groupModeED = new FR2_EnumDrawer
            {
                tooltip = "Group By",
                fr2_enum = new FR2_EnumDrawer.EnumInfo(FR2_RefDrawer.AssetGroupModes)
            };

            bool showExternal = FR2_SettingExt.showPackagesAndBuiltIn;
            var tb = new FR2_TitleBarBuilder(titleRect, rightEdge);
            
            tb = tb.AddDropdown(vs.sortModeED, ref vs.sortMode, v => { assetDrawers?.NotifySortChanged(); }, 65f)
                .AddDropdown(vs.groupModeED, ref vs.groupMode, v => { assetDrawers?.NotifyGroupModeChanged(); }, 100f)
                .AddSpace()
                .AddToggle(ref showExternal, FR2_Icon.Package, v =>
                {
                    FR2_SettingExt.showPackagesAndBuiltIn = v;
                    MarkDirty();
                })
                .AddToggle(ref vs.showFileSize, FR2_Icon.Filesize, v => { ApplyToAssetDrawers(d => d.showFileSize = v); assetDrawers?.NotifyDisplayChanged(); })
                .AddToggle(ref vs.showExtension, FR2_Icon.FileExtension, v => { ApplyToAssetDrawers(d => d.showExtension = v); assetDrawers?.NotifyDisplayChanged(); })
                .AddToggle(ref vs.showFullPath, FR2_Icon.FullPath, v => { ApplyToAssetConfigs(d => d.showFullPath = v); assetDrawers?.NotifyDisplayChanged(); });

            if (tb.Changed) { EditorUtility.SetDirty(this); WillRepaint = true; }
            return tb.X;
        }
        
        internal float DrawSceneTitleBarControls(Rect titleRect, float rightEdge)
        {
            var vs = isFocusingUses ? settings.sceneUsesView : settings.sceneUsedByView;
            var validModes = GetSceneGroupModesForContext();
            ValidateGroupMode(ref vs.groupMode, validModes);
            
            if (vs.groupModeED == null || !SceneGroupModesMatch(vs.groupModeED, validModes))
            {
                vs.groupModeED = new FR2_EnumDrawer
                {
                    tooltip = "Group By",
                    fr2_enum = new FR2_EnumDrawer.EnumInfo(validModes)
                };
            }

            var tb = new FR2_TitleBarBuilder(titleRect, rightEdge);
            
            tb = tb.AddDropdown(vs.groupModeED, ref vs.groupMode, v => { sceneDrawers?.NotifyGroupModeChanged(); }, 130f)
                .AddSpace()
                .AddToggle(ref vs.showFullPath, FR2_Icon.FullPath, v => { ApplyToSceneConfigs(d => d.showFullPath = v); sceneDrawers?.NotifyDisplayChanged(); });

            if (tb.Changed) { EditorUtility.SetDirty(this); WillRepaint = true; }
            return tb.X;
        }
        
        private string[] GetSceneGroupModesForContext()
        {
            if (isFocusingUses) return FR2_RefDrawer.SceneGOUsesModes;
            if (IsSelectingAssets) return FR2_RefDrawer.SceneAssetUsedByModes;
            return FR2_RefDrawer.SceneGOUsedByModes;
        }
        
        private static bool SceneGroupModesMatch(FR2_EnumDrawer ed, string[] modes)
        {
            if (ed?.fr2_enum == null) return false;
            var contents = ed.fr2_enum.contents;
            if (contents.Length != modes.Length) return false;
            for (int i = 0; i < modes.Length; i++)
            {
                if (contents[i].text != modes[i]) return false;
            }
            return true;
        }
        
        internal float DrawAddressableTitleBarControls(Rect titleRect, float rightEdge)
        {
            var vs = settings.assetView;
            if (vs.sortModeED == null) vs.sortModeED = new FR2_EnumDrawer { tooltip = "Sort By" };

            var ad = AddressableDrawer?.drawer;
            var tb = new FR2_TitleBarBuilder(titleRect, rightEdge);
            
            tb = tb.AddDropdown(vs.sortModeED, ref vs.sortMode, v => { ad?.Config?.NotifySortChanged(); })
                .AddSpace()
                .AddToggle(ref vs.showFileSize, FR2_Icon.Filesize, v => { if (ad?.AssetConfig != null) ad.AssetConfig.showFileSize = v; ad?.Config?.NotifyDisplayChanged(); })
                .AddToggle(ref vs.showExtension, FR2_Icon.FileExtension, v => { if (ad?.AssetConfig != null) ad.AssetConfig.showExtension = v; ad?.Config?.NotifyDisplayChanged(); })
                .AddToggle(ref vs.showFullPath, FR2_Icon.FullPath, v => { if (ad?.Config != null) ad.Config.showFullPath = v; ad?.Config?.NotifyDisplayChanged(); });

            if (tb.Changed) { EditorUtility.SetDirty(this); WillRepaint = true; }
            return tb.X;
        }
        
        internal void DrawToolViewBar()
        {
            bool hasTree = isFocusingDuplicate || isFocusingUnused || isFocusingUsedInBuild;
            if (!hasTree) return;
            
            var vs = settings.toolView;
            ValidateGroupMode(ref vs.groupMode, FR2_RefDrawer.ToolGroupModes);
            if (vs.sortModeED == null) vs.sortModeED = new FR2_EnumDrawer { tooltip = "Sort By" };
            if (vs.groupModeED == null)
            {
                vs.groupModeED = new FR2_EnumDrawer
                {
                    tooltip = "Group By",
                    fr2_enum = new FR2_EnumDrawer.EnumInfo(FR2_RefDrawer.ToolGroupModes)
                };
            }
            
            Rect barRect = GUILayoutUtility.GetRect(1f, Screen.width, 20f, 20f);
            GUI2.Rect(barRect, Color.black, 0.2f);

            bool showExternal = FR2_SettingExt.showPackagesAndBuiltIn;
            var tb = new FR2_TitleBarBuilder(barRect.xMax - 3f, barRect.y);
            
            tb = tb.AddDropdown(vs.sortModeED, ref vs.sortMode, v => { NotifyAllToolSortChanged(); }, 65f)
                .AddDropdown(vs.groupModeED, ref vs.groupMode, v => { NotifyAllToolGroupModeChanged(); }, 100f)
                .AddSpace()
                .AddToggle(ref showExternal, FR2_Icon.Package, v =>
                {
                    FR2_SettingExt.showPackagesAndBuiltIn = v;
                    MarkDirty();
                })
                .AddToggle(ref vs.showFileSize, FR2_Icon.Filesize, v => { ApplyToToolDrawers(d => d.showFileSize = v); NotifyAllToolDisplayChanged(); })
                .AddToggle(ref vs.showExtension, FR2_Icon.FileExtension, v => { ApplyToToolDrawers(d => d.showExtension = v); NotifyAllToolDisplayChanged(); })
                .AddToggle(ref vs.showFullPath, FR2_Icon.FullPath, v => { ApplyToToolConfigs(d => d.showFullPath = v); NotifyAllToolDisplayChanged(); });

            if (tb.Changed) { EditorUtility.SetDirty(this); WillRepaint = true; }
            
            if (isFocusingUnused)
            {
                float leftX = barRect.x + 4f;
                GUI.Label(new Rect(leftX, barRect.y, 110f, 18f), "Recursive Search");
                leftX += 112f;
                bool oldRecursive = settings.recursiveUnusedScan;
                settings.recursiveUnusedScan = GUI.Toggle(new Rect(leftX, barRect.y, 16f, 18f), settings.recursiveUnusedScan, GUIContent.none);
                if (oldRecursive != settings.recursiveUnusedScan)
                {
                    RefUnUse.ResetUnusedAsset(settings.recursiveUnusedScan);
                    EditorUtility.SetDirty(this);
                }
            }
        }
        
        private void ApplyToAssetDrawers(Action<FR2_RefDrawer.AssetDrawingConfig> action)
        {
            assetDrawers?.ApplyToAssetConfigs(action);
        }
        
        private void ApplyToAssetConfigs(Action<FR2_RefDrawer.RefDrawerConfig> action)
        {
            assetDrawers?.ApplyToConfigs(action);
        }
        
        private void ApplyToSceneConfigs(Action<FR2_RefDrawer.RefDrawerConfig> action)
        {
            sceneDrawers?.ApplyToConfigs(action);
        }
        
        private void ApplyToToolDrawers(Action<FR2_RefDrawer.AssetDrawingConfig> action)
        {
            toolDrawers?.ApplyToAssetConfigs(action);
            if (UsedInBuild?.Drawer?.AssetConfig != null) action(UsedInBuild.Drawer.AssetConfig);
        }
        
        private void ApplyToToolConfigs(Action<FR2_RefDrawer.RefDrawerConfig> action)
        {
            toolDrawers?.ApplyToConfigs(action);
            if (UsedInBuild?.Drawer?.Config != null) action(UsedInBuild.Drawer.Config);
        }
        
        private void NotifyAllToolDisplayChanged()
        {
            toolDrawers?.NotifyDisplayChanged();
            UsedInBuild?.Drawer?.Config?.NotifyDisplayChanged();
        }
        
        private void NotifyAllToolSortChanged()
        {
            toolDrawers?.NotifySortChanged();
            UsedInBuild?.RefreshSort();
            Duplicated?.RefreshSort();
        }
        
        private void NotifyAllToolGroupModeChanged()
        {
            toolDrawers?.NotifyGroupModeChanged();
            UsedInBuild?.SetDirty(); UsedInBuild?.RefreshSort();
            Duplicated?.SetDirty(); Duplicated?.RefreshSort();
        }

        private static void ValidateGroupMode(ref string groupMode, string[] validModes)
        {
            if (string.IsNullOrEmpty(groupMode) || System.Array.IndexOf(validModes, groupMode) < 0)
                groupMode = validModes[0];
        }
    }
} 