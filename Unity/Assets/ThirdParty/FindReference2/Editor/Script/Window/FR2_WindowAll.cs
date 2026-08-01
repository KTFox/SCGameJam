using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using static vietlabs.fr2.FR2_Scope;
using UnityObject = UnityEngine.Object;

namespace vietlabs.fr2
{
    internal partial class FR2_WindowAll : FR2_WindowBase, IHasCustomMenu
    {
        internal const float ASSET_SELECTION_PAD_LEFT = 40f;
        internal const float SCENE_SELECTION_PAD_LEFT = 40f;
        internal const float BOOKMARK_SELECTION_PAD_LEFT = 24f;
        internal const float TOOL_SELECTION_PAD_LEFT = 44f;
        internal const float SELECTION_PANEL_PAD_LEFT = 6f;
        
        [SerializeField] internal PanelSettings settings = new PanelSettings();

        [MenuItem("Window/Find Reference 2")]
        private static void ShowWindow()
        {
            var _window = CreateInstance<FR2_WindowAll>();
            _window.InitIfNeeded();
            FR2_Unity.SetWindowTitle(_window, "FR2");
            _window.Show();
        }

        [NonSerialized] internal FR2_Bookmark bookmark;
        [NonSerialized] internal FR2_Selection selection;
        [NonSerialized] internal FR2_UsedInBuild UsedInBuild;
        [NonSerialized] internal FR2_DuplicateTree2 Duplicated;
        [NonSerialized] internal FR2_RefDrawer RefUnUse;
        [NonSerialized] internal FR2_MissingReference MissingReference;
        [NonSerialized] internal FR2_AssetOrganizer AssetOrganizer;
        [NonSerialized] internal FR2_DeleteEmptyFolder DeleteEmptyFolder;

        [NonSerialized] internal FR2_RefDrawer UsesDrawer; // [Selected Assets] are [USING] (depends on / contains reference to) ---> those assets
        [NonSerialized] internal FR2_RefDrawer UsedByDrawer; // [Selected Assets] are [USED BY] <---- those assets 
        [NonSerialized] internal FR2_RefDrawer SceneToAssetDrawer; // [Selected GameObjects in current Scene] are [USING] ---> those assets
        [NonSerialized] internal FR2_AddressableDrawer AddressableDrawer;


        [NonSerialized] internal FR2_RefDrawer RefInScene; // [Selected Assets] are [USED BY] <---- those components in current Scene 
        [NonSerialized] internal FR2_RefDrawer SceneUsesDrawer; // [Selected GameObjects] are [USING] ---> those components / GameObjects in current scene
        [NonSerialized] internal FR2_RefDrawer RefSceneInScene; // [Selected GameObjects] are [USED BY] <---- those components / GameObjects in current scene
        
        [NonSerialized] internal FR2_SmartLock smartLock = new FR2_SmartLock();
        [NonSerialized] private FR2_NavigationHistory navigationHistory = new FR2_NavigationHistory();
        
        [NonSerialized] internal FR2_DrawerGroup assetDrawers;
        [NonSerialized] internal FR2_DrawerGroup sceneDrawers;
        [NonSerialized] internal FR2_DrawerGroup toolDrawers;

        // FR2_Theme singleton provides centralized UI constants
        [NonSerialized] internal FR2_Theme theme;

        // Simple flag to track selection sync status for UI highlighting
        [NonSerialized] internal bool isSelectionOutOfSync;
        
        
        // Cached contextual messages for drawers
        [NonSerialized] private string cachedUsesMessage;
        [NonSerialized] private string cachedUsedByMessage;
        [NonSerialized] private string cachedRefInSceneMessage;
        [NonSerialized] private string cachedSceneUsesMessage;
        [NonSerialized] private string cachedSceneToAssetMessage;
        [NonSerialized] private string cachedSceneInSceneMessage;
        [NonSerialized] private UnityObject[] lastCachedSelection;

        public void Reload()
        {
            InitializeComponents();
        }

        private GUIContent GetScenePanelTitle()
        {
            string titleText = "Scene";
            string tooltip = "Scene references";

            // Check scene status for title text modifications
            if (FR2_SceneCache.Api != null)
            {
                switch (FR2_SceneCache.Api.Status)
                {
                    case SceneCacheStatus.Scanning:
                        int cur = FR2_SceneCache.Api.current;
                        int total = FR2_SceneCache.Api.total;
                        if (total > 0)
                        {
                            titleText += $" (scanning {cur}/{total})";
                            tooltip = $"Currently scanning scene objects: {cur} of {total}";
                        }
                        else
                        {
                            titleText += " (scanning...)";
                            tooltip = "Currently scanning scene objects";
                        }
                        break;
                    case SceneCacheStatus.None:
                        titleText += " (not ready)";
                        tooltip = "Scene cache is not initialized";
                        break;
                    case SceneCacheStatus.Changed:
                        tooltip = "Scene changed - results might be incomplete";
                        break;
                    case SceneCacheStatus.Ready:
                        tooltip = "Scene cache ready";
                        break;
                }
            }

            return FR2_GUIContent.From(titleText, FR2_Icon.Scene.image, tooltip);
        }

        private GUIContent GetAssetPanelTitle()
        {
            string titleText = "Assets";
            string tooltip = "Asset references";

            // Check asset status for title text modifications
            
            if (!FR2_Cache.isReady)
            {
                titleText += " (processing...)";
                tooltip = $"Processing assets: {(FR2_Cache.refreshProgress * 100):F0}%";
            }
            else if (FR2_Cache.hasDirtyAsset)
            {
                tooltip = "Assets changed - cache needs refresh";
            }
            else
            {
                tooltip = "Asset cache ready";
            }
            
            return FR2_GUIContent.From(titleText, FR2_Icon.Asset.image, tooltip);
        }

        
        private bool ScenePanelHasContent()
        {
            if (!FR2_SceneCache.hasCache) return false;
            
            FR2_RefDrawer drawer = isFocusingUses
                ? IsSelectingAssets ? null : SceneUsesDrawer
                : IsSelectingAssets ? RefInScene : RefSceneInScene;
                
            return drawer != null && drawer.HasContent;
        }
        
        private bool AssetPanelHasContent()
        {
            if (!FR2_Cache.isReady) return false;
            
            FR2_RefDrawer drawer = GetAssetDrawer();
            return drawer != null && drawer.HasContent;
        }
        
        private bool BookmarkPanelHasContent()
        {
            return bookmark != null && FR2_Bookmark.Count > 0;
        }

        private bool IsScenePanelDirty()
        {
            if (FR2_SceneCache.Api == null) return false;
            
            // Show yellow title for various scene issues
            return FR2_SceneCache.Api.Status == SceneCacheStatus.Changed ||
                   FR2_SceneCache.Api.Status == SceneCacheStatus.None ||
                   FR2_SceneCache.Api.Status == SceneCacheStatus.Scanning;
        }

        private bool IsAssetPanelDirty()
        {
            return !FR2_Cache.isReady || FR2_Cache.hasDirtyAsset;
        }

        private string GetSceneStatusMessage()
        {
            if (FR2_SceneCache.Api == null) return null;

            switch (FR2_SceneCache.Api.Status)
            {
                case SceneCacheStatus.Changed:
                    return "Scene changed - results might be incomplete";
                case SceneCacheStatus.Scanning:
                    return "Scanning scene objects...";
                case SceneCacheStatus.None:
                    return "Scene cache not ready";
                default:
                    return null;
            }
        }

        private string GetAssetStatusMessage()
        {
            if (FR2_Cache._inst == null) return null;

            if (FR2_Cache.hasDirtyAsset)
            {
                return "Assets changed - cache needs refresh";
            }
            else if (!FR2_Cache.isReady)
            {
                return "Asset cache not ready";
            }

            return null;
        }
        
        [NonSerialized] private bool _isLocked;
        protected bool lockSelection => _isLocked;
        
        internal void LockSelection()
        {
            _isLocked = true;
            WillRepaint = true;
        }

        // Helper properties to access unified selection manager
        private bool IsSelectingAssets => selection?.isSelectingAsset ?? false;
        private bool IsSelectingSceneObjects => selection?.isSelectingSceneObject ?? false;

        private bool IsSelectionOutOfSync
        {
            get
            {
                if (selection == null) return false;
                
                var unitySelection = FR2_SelectionManager.Instance.GetUnitySelection();
                var fr2Selection = selection.GetUnityObjects();
                
                return !AreSelectionsEqual(unitySelection, fr2Selection);
            }
        }
        
        private void RefreshContextualMessages()
        {
            var currentSelection = GetFR2Selection();
            
            if (lastCachedSelection != null && AreSelectionsEqual(lastCachedSelection, currentSelection))
            {
                return;
            }
            
            lastCachedSelection = currentSelection;
            
            if (UsesDrawer.IsEmpty) cachedUsesMessage = FR2_ContextualMessageBuilder.Generate(currentSelection, "USING");
            if (UsedByDrawer.IsEmpty) cachedUsedByMessage = FR2_ContextualMessageBuilder.Generate(currentSelection, "USED BY");
            if (RefInScene.IsEmpty) cachedRefInSceneMessage = FR2_ContextualMessageBuilder.Generate(currentSelection, "USED BY", " any GameObjects in current scene");
            
            if (SceneUsesDrawer.IsEmpty) cachedSceneUsesMessage = FR2_ContextualMessageBuilder.Generate(currentSelection, "USING", " any other objects");
            if (SceneToAssetDrawer.IsEmpty) cachedSceneToAssetMessage = FR2_ContextualMessageBuilder.Generate(currentSelection, "USING", " any assets");
            if (RefSceneInScene.IsEmpty) cachedSceneInSceneMessage = FR2_ContextualMessageBuilder.Generate(currentSelection, "USED BY", " any other GameObjects");
        }

        private void ClearAllCachedUIElements()
        {
            // Clear cached contextual messages
            cachedUsesMessage = null;
            cachedUsedByMessage = null;
            cachedRefInSceneMessage = null;
            cachedSceneUsesMessage = null;
            cachedSceneToAssetMessage = null;
            cachedSceneInSceneMessage = null;
            lastCachedSelection = null;
        }
        
        private static readonly HashSet<UnityObject> _selCompareSet = new HashSet<UnityObject>();
        
        private static bool AreSelectionsEqual(UnityObject[] selection1, UnityObject[] selection2)
        {
            if (selection1 == null && selection2 == null) return true;
            if (selection1 == null || selection2 == null) return false;
            if (selection1.Length != selection2.Length) return false;

            _selCompareSet.Clear();
            for (int i = 0; i < selection1.Length; i++)
                _selCompareSet.Add(selection1[i]);

            for (int i = 0; i < selection2.Length; i++)
            {
                if (!_selCompareSet.Contains(selection2[i])) return false;
            }
            return true;
        }

        private void OnEnable()
        {
            FR2_Unity.RefreshEditorStatus();
            wantsMouseMove = true;

            // Initialize theme based on current Unity skin (needed because it's NonSerialized)
            theme = EditorGUIUtility.isProSkin ? FR2_Theme.Dark : FR2_Theme.Light;

            // Initialize selection manager early
            InitializeSelectionManager();

            RegisterSceneCacheCallbacks();
            FR2_Cache.onReady -= OnAssetCacheReady;
            FR2_Cache.onReady += OnAssetCacheReady;
            UpdateSceneCacheAutoRefresh();
            Repaint();
        }
        
        private void OnFocus()
        {
            FR2_Unity.RefreshEditorStatus();
        }

        private void RegisterSceneCacheCallbacks()
        {
            FR2_SceneCache.onReady -= OnSceneCacheReady;
            FR2_SceneCache.onReady += OnSceneCacheReady;
        }

        private void OnSceneCacheReady()
        {
            WillRepaint = true;
            Repaint();
        }

        private void OnAssetCacheReady()
        {
            WillRepaint = true;
            ClearAllCachedUIElements();

            if (selection != null)
            {
                selection.SyncFromGlobalSelection();
                isSelectionOutOfSync = false;
            }

            RefreshFR2View();
            Repaint();
        }


        private void UpdateSceneCacheAutoRefresh()
        {
            if (FR2_SceneCache.Api != null) FR2_SceneCache.Api.AutoRefresh = FR2_SettingExt.isAutoRefreshEnabled;
        }

        protected void InitIfNeeded()
        {
            if (UsesDrawer != null) return;
            InitializeComponents();
        }

        private bool ValidateLockedSelection()
        {
            if (!lockSelection) return true;
            
            var currentFR2Selection = GetFR2Selection();
            if (currentFR2Selection == null || currentFR2Selection.Length == 0)
            {
                UnlockAndSyncSelection();
                return false;
            }

            var validObjects = currentFR2Selection.Where(obj => obj != null).ToArray();

            if (validObjects.Length == 0)
            {
                UnlockAndSyncSelection();
                RefreshFR2View();
                return false;
            }

            if (validObjects.Length != currentFR2Selection.Length)
            {
                SetFR2Selection(validObjects);
                return true;
            }
            
            return true;
        }

        private void UnlockAndSyncSelection()
        {
            selection.SyncFromGlobalSelection();
            isSelectionOutOfSync = false;
        }
        
        private bool isScenePanelVisible
        {
            get
            {
                if (isFocusingAddressable) return false;

                if (IsSelectingAssets && isFocusingUses) return false;
                if (!IsSelectingAssets && isFocusingUsedBy) return true;

                return settings.scene;
            }
        }
        
        private bool isAssetPanelVisible
        {
            get
            {
                if (isFocusingAddressable) return false;

                if (IsSelectingAssets && isFocusingUses) return true;
                if (!IsSelectingAssets && isFocusingUsedBy) return false;

                return settings.asset;
            }
        }


        [NonSerialized] public FR2_SplitView sp1; // container : Selection / sp2 / Bookmark 
        [NonSerialized] public FR2_SplitView sp2; // Scene / Assets
        
        [NonSerialized] private FR2_TabView tabs;
        [NonSerialized] private FR2_TabView toolTabs;
        [NonSerialized] private FR2_TabView bottomTabs;
        private void DrawScene(Rect rect)
        {
            DrawScenePanel(rect);
        }
    
        private void DrawAsset(Rect rect)
        {
            DrawAssetPanel(rect);
        }

        private void DrawSelectionPanel(Rect rect)
        {
            if (selection == null) return;
            
            if (_selectionTooLarge)
            {
                int totalCount = FR2_SelectionManager.Instance?.TotalCount ?? 0;
                int maxCount = FR2_SettingExt.maxSelectionCount;
                string message = $"Selection too large ({totalCount} items). Inspection skipped.\nMax allowed: {maxCount} (configurable in Settings)";
                
                Rect msgRect = rect;
                msgRect.height = 40f;
                msgRect.x += 4f;
                msgRect.width -= 8f;
                EditorGUI.HelpBox(msgRect, message, MessageType.Info);
                
                rect.yMin += 44f;
            }
            
            selection.Draw(rect);
        }

        private void DrawDetailsPanel(Rect rect)
        {
            var drawer = GetDetailDrawerSource();
            if (drawer != null)
            {
                drawer.DrawDetails(rect);
            }
            else
            {
                EditorGUI.HelpBox(rect, "No details available - select an item in the main panel to see details", MessageType.Info);
            }
        }

        private FR2_RefDrawer GetDetailDrawerSource()
        {
            if (isFocusingUses)
            {
                return IsSelectingAssets ? UsesDrawer : SceneToAssetDrawer;
            }
            else if (isFocusingUsedBy)
            {
                return IsSelectingAssets ? UsedByDrawer : RefSceneInScene;
            }
            return null;
        }

        protected override void OnGUI()
        {
            OnGUI2();
        }


        internal void ToggleDetailsPanel()
        {
            settings.details = !settings.details;
            if (sp1 != null && sp1.splits != null && sp1.splits.Count > 2)
            {
                sp1.SetSplitVisible(2, settings.details);
            }
            Repaint();
        }



        public bool isFocusingUses => tabs?.IsFocusing(0) ?? false;
        public bool isFocusingUsedBy => tabs?.IsFocusing(1) ?? false;
        public bool isFocusingAddressable => tabs?.IsFocusing(2) ?? false;

        // 
        public bool isFocusingDuplicate => toolTabs?.IsFocusing(0) ?? false;
        public bool isFocusingGUIDs => toolTabs?.IsFocusing(1) ?? false;
        public bool isFocusingUnused => toolTabs?.IsFocusing(2) ?? false;
        public bool isFocusingUsedInBuild => toolTabs?.IsFocusing(3) ?? false;
        public bool isFocusingOthers => toolTabs?.IsFocusing(4) ?? false;

        private static readonly HashSet<string> allowedToolModes = new HashSet<string>(FR2_RefDrawer.ToolGroupModes);

        private void OnTabChange()
        {
            if (deleteUnused != null) deleteUnused.hasConfirm = false;
            if (UsedInBuild != null) UsedInBuild.SetDirty();

            // Fix: Refresh panel visibility when switching between Uses/Used By tabs
            RefreshPanelVisible();
        }


        protected bool DrawFooter()
        {
            bottomTabs.DrawLayout();
            var bottomBar = GUILayoutUtility.GetLastRect();
            bottomBar = bottomBar.LPad(theme.FooterButtonsOffset);

            var (buttonRect, _) = bottomBar.ExtractRight(theme.IconButtonSize);
            DrawButton(buttonRect, ref settings.toolMode, FR2_Icon.CustomTool);

            return false;
        }




        // Save status to temp variable so the result will be consistent between Layout & Repaint
        internal static int delayRepaint;
        private static bool checkDrawImportResult;


        private void OnGUI2()
        {
            if (Event.current.type == EventType.Layout) FR2_Unity.RefreshEditorStatus();
            if (EditorApplication.isCompiling)
            {
                EditorGUILayout.HelpBox("Compiling scripts, please wait!", MessageType.Warning);
                Repaint();
                return;
            }
            
            if (FR2_SettingExt.disable)
            {
                DrawEnable();
                return;
            }
            
#if FR2_DEBUG
            GUILayout.BeginHorizontal();
            {
                EditorGUILayout.ObjectField(FR2_Cache._inst, typeof(FR2_Cache), false);
                EditorGUILayout.EnumPopup(FR2_Cache.status);
            }
            GUILayout.EndHorizontal();
#endif
            if (EditorApplication.isUpdating)
            {
                EditorGUILayout.HelpBox("Importing assets, please wait!", MessageType.Warning);
                Repaint();
                return;
            }

            if (FR2_Cache.status == FR2_Status.None)
            {
                EditorGUILayout.HelpBox("Initializing FR2 cache, please wait...", MessageType.Info);
                Repaint();
                return;
            }
            if (FR2_Cache._inst == null && FR2_Cache.cacheStatus >= FR2_CacheStatus.Found)
            {
                FR2_LOG.LogWarning("Did you just deleted the FR2_Cache.asset?");
                if (Event.current.type == EventType.Layout)
                    EditorApplication.delayCall += FR2_Cache.Initialize;
                EditorGUILayout.HelpBox("FR2 Cache missing, reinitializing...", MessageType.Warning);
                return;
            }
            
            if (sp1 == null) InitPanes();
            
            if (Event.current.type == EventType.MouseMove || Event.current.type == EventType.ScrollWheel || Event.current.type == EventType.MouseLeaveWindow || Event.current.type == EventType.MouseEnterWindow)
            {
                WillRepaint = true;
            }
            
            UpdateSceneCacheAutoRefresh();

            if (!FR2_CacheHelper.inited) FR2_CacheHelper.InitHelper();
            
            var result = CheckDrawImport();
            if (Event.current.type == EventType.Layout) checkDrawImportResult = result;

            if (!checkDrawImportResult) return;

            if (settings.toolMode)
            {
                if (!FR2_SettingExt.hideToolsWarning)
                {
                    using (FR2_Scope.HzLayout())
                    {
                        EditorGUILayout.HelpBox(FR2_GUIContent.From(
                            "Tools are POWERFUL & DANGEROUS! Only use if you know what you are doing!!!",
                            FR2_Icon.Warning.image));
                        if (GUILayout.Button("  x", EditorStyles.label, theme.CloseButtonWidth, theme.WarningCloseButtonHeight))
                            FR2_SettingExt.hideToolsWarning = true;
                    }
                }

                DrawGitWarningPanel();

                toolTabs.DrawLayout();
                DrawToolViewBar();
                DrawTools();
            }
            else
            {
                DrawGitWarningPanel();

                tabs.DrawLayout();
                sp1.DrawLayout();
            }

            DrawSettings();
            DrawFooter();

            if (!WillRepaint) return;
            WillRepaint = false;
            Repaint();
        }
    }
}