using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using static vietlabs.fr2.FR2_Scope;
using UnityObject = UnityEngine.Object;

namespace vietlabs.fr2
{
    internal partial class FR2_WindowAll
    {
        private void InitializeComponents()
        {
            
            // Initialize UI components first to ensure selection exists before selection manager events
            InitializeUIComponents();
            InitializeNavigationHistory();
            InitializeDrawers();
            InitializeTools();
            InitializeDrawerProperties();
            
            InitTabs();
            InitPanes();
            
            // Initialize selection manager AFTER everything else is ready
            InitializeSelectionManager();
            
            if (FR2_Cache.isReady)
            {
                RefreshActiveTab();
                RefreshFR2View();
            }
            else
            {
                // Debug.LogWarning("FR2 is not Ready just yet!");    
                FR2_Cache.onReady -= RefreshActiveTab;
                FR2_Cache.onReady += RefreshActiveTab;
                FR2_Cache.onReady -= RefreshFR2View;
                FR2_Cache.onReady += RefreshFR2View;
            }
            
            Repaint();
        }

        void RefreshActiveTab()
        {
            if (tabs == null || toolTabs == null)
            {
                return;
            }
            
            if (settings.toolMode)
            {
                toolTabs.onTabChange?.Invoke();
            }
            else
            {
                tabs.onTabChange?.Invoke();
            }
            
            // If selection was out of sync due to cache not being ready, sync now
            if (isSelectionOutOfSync && selection != null)
            {
                selection.SyncFromGlobalSelection();
                RefreshFR2View();
                isSelectionOutOfSync = false;
            }
        }
        

        private void InitializeNavigationHistory()
        {
            if (navigationHistory == null) navigationHistory = new FR2_NavigationHistory();
            navigationHistory.SetWindow(this);
        }

        private void InitializeSelectionManager()
        {
            FR2_Event.AddGlobalListener<SelectionChangedEvent>(_ => OnSelectionManagerChanged());
            FR2_Event.AddGlobalListener<SettingChangedEvent>(_ => MarkDirty());
            FR2_Event.AddGlobalListener<IgnoreChangedEvent>(_ => MarkDirty());
        }

        private void InitializeDrawers()
        {
            UsesDrawer = FR2_DrawerFactory.CreateAssetDrawer(this,
                "[Selected Assets] are not [USING] (depends on / contains reference to) any other assets!",
                () => cachedUsesMessage);

            UsedByDrawer = FR2_DrawerFactory.CreateAssetDrawer(this,
                "[Selected Assets] are not [USED BY] any other assets!",
                () => cachedUsedByMessage,
                () => !isFocusingUsedBy);

            SceneToAssetDrawer = FR2_DrawerFactory.CreateAssetDrawer(this,
                "[Selected GameObjects] are not [USING] any assets!",
                () => cachedSceneToAssetMessage);

            AddressableDrawer = new FR2_AddressableDrawer(this, () => settings.assetView.sortMode, () => settings.assetView.groupMode);
            Duplicated = new FR2_DuplicateTree2(this, () => settings.toolView.sortMode, () => settings.toolView.groupMode);

            RefInScene = FR2_DrawerFactory.CreateSceneDrawer(this, settings.sceneUsedByView,
                "[Selected Assets] are not [USED BY] any GameObjects in current scene!",
                () => cachedRefInSceneMessage);

            RefSceneInScene = FR2_DrawerFactory.CreateSceneDrawer(this, settings.sceneUsedByView,
                "[Selected GameObjects] are not [USED BY] any GameObjects in current scene!",
                () => cachedSceneInSceneMessage);

            SceneUsesDrawer = FR2_DrawerFactory.CreateSceneDrawer(this, settings.sceneUsesView,
                "[Selected GameObjects] are not [USING] any GameObjects in current scene!",
                () => cachedSceneUsesMessage);

            RefUnUse = FR2_DrawerFactory.CreateToolDrawer(this,
                "Wow! No unused assets found!",
                () => !isFocusingUnused);

            assetDrawers = new FR2_DrawerGroup(UsesDrawer, UsedByDrawer, SceneToAssetDrawer);
            sceneDrawers = new FR2_DrawerGroup(RefInScene, SceneUsesDrawer, RefSceneInScene);
            toolDrawers = new FR2_DrawerGroup(RefUnUse);
        }

        private void InitializeTools()
        {
            UsedInBuild = new FR2_UsedInBuild(this, () => settings.toolView.sortMode, () => settings.toolView.groupMode);
            MissingReference = new FR2_MissingReference(this, () => settings.toolView.sortMode, () => settings.toolView.groupMode);
            AssetOrganizer = new FR2_AssetOrganizer(this, () => settings.toolView.sortMode, () => settings.toolView.groupMode);
            DeleteEmptyFolder = new FR2_DeleteEmptyFolder(this, () => settings.toolView.sortMode, () => settings.toolView.groupMode);
        }

        private void InitializeUIComponents()
        {
            selection = new FR2_Selection(this, () => settings.assetView.sortMode, () => settings.assetView.groupMode);
            selection.OnSelectionChanged -= OnLocalSelectionChanged;
            selection.OnSelectionChanged += OnLocalSelectionChanged;
            bookmark = new FR2_Bookmark(this, () => settings.assetView.sortMode, () => FR2_RefDrawer.GroupMode.None);
            
            // Setup bookmark cache invalidation callback - each drawer will invalidate its own cache
            FR2_Event.AddGlobalListener<BookmarkChangedEvent>(_ => {
                assetDrawers?.InvalidateGroupCache();
                sceneDrawers?.InvalidateGroupCache();
                toolDrawers?.InvalidateGroupCache();
            });
            
            // Initial sync with Unity selection - delay until after full initialization
            EditorApplication.delayCall += () =>
            {
                if (selection == null) return;
                if (!FR2_Cache.isReady) return;
                
                selection.SyncFromGlobalSelection();
                isSelectionOutOfSync = false; // Reset flag after initial sync
                RefreshFR2View();
            };
        }

        private void OnLocalSelectionChanged()
        {
            // When local selection changes (user interacts with selection panel), 
            // refresh the Uses/Used By tabs to reflect the current selection
            if (selection != null)
            {
                // Debug.Log($"OnLocalSelectionChanged - Count: {selection.Count}, IsSelectingAsset: {selection.isSelectingAsset}, GuidCount: {selection.guidSet.Count}");
                RefreshFR2View();
            }
        }

        private void InitializeDrawerProperties()
        {
            this.CacheAllDrawers();
        }

        private void RefreshSelectedAssets()
        {
            if (!FR2_Cache.isReady) return;
            
            var guids = selection?.guidSet;
            if (guids == null || guids.Count == 0)
            {
                FR2_Cache.IncrementalRefresh();
                return;
            }
            
            foreach (var guid in guids)
            {
                if (string.IsNullOrEmpty(guid)) continue;
                
                if (!FR2_Cache._map.ContainsKey(guid))
                {
                    FR2_Cache.GetAsset(guid, true);
                }
                else
                {
                    var asset = FR2_Cache._map[guid];
                    if (asset != null) asset.LoadFileInfo();
                    FR2_Cache.RefreshAsset(guid, true);
                }
            }
            
            FR2_Cache.IncrementalRefresh();
        }

        private void InitPanes()
        {
            sp2 = new FR2_SplitView(this)
            {
                isHorz = false,
                splits = new List<FR2_SplitView.Info>
                {
                    new FR2_SplitView.Info
                    {
                        title = new GUIContent("Scene", FR2_Icon.Scene.image),
                        draw = DrawScene,
                        visible = settings.scene,
                        sizePolicy = FR2_SplitView.Info.SizePolicy.Flexible,
                        minPixel = 100f,
                        GetDynamicTitle = GetScenePanelTitle,
                        GetDrawerDirtyState = IsScenePanelDirty,
                        OnRefresh = () => FR2_SceneCache.Api?.ForceRefresh(),
                        GetBookmarkCount = () => GetSceneDrawer()?.GetBookmarkedCount() ?? 0,
                        OnBookmarkClick = () => GetSceneDrawer()?.CommitBookmarked(),
                        DrawTitleBarControls = DrawSceneTitleBarControls
                    },
                    new FR2_SplitView.Info
                    {
                        title = new GUIContent("Assets", FR2_Icon.Asset.image),
                        draw = DrawAsset,
                        visible = settings.asset,
                        sizePolicy = FR2_SplitView.Info.SizePolicy.Flexible,
                        minPixel = 100f,
                        GetDynamicTitle = GetAssetPanelTitle,
                        GetDrawerDirtyState = IsAssetPanelDirty,
                        OnRefresh = RefreshSelectedAssets,
                        GetBookmarkCount = () => GetAssetDrawer()?.GetBookmarkedCount() ?? 0,
                        OnBookmarkClick = () => GetAssetDrawer()?.CommitBookmarked(),
                        DrawTitleBarControls = DrawAssetTitleBarControls
                    },
                    new FR2_SplitView.Info
                        { title = null, draw = rect => AddressableDrawer.Draw(rect), visible = false }
                }
            };

            sp2.SetupSplitParents();
            sp2.dirty = true;

            sp1 = new FR2_SplitView(this)
            {
                isHorz = true,
                splits = new List<FR2_SplitView.Info>
                {
                    new FR2_SplitView.Info
                    {
                        title = null, //new GUIContent("Selection"),
                        draw = DrawSelectionPanel,
                        weight = 0f,
                        visible = settings.selection,
                        sizePolicy = FR2_SplitView.Info.SizePolicy.KeepPixel,
                        preferredPixel = settings.selectionPanelPixel
                    },
                    new FR2_SplitView.Info
                    {
                        title = null,
                        draw = _ => sp2.Draw(_),
                        weight = 1f,
                        visible = true,
                        sizePolicy = FR2_SplitView.Info.SizePolicy.Flexible
                    },
                    new FR2_SplitView.Info
                    {
                        title = new GUIContent("Details", FR2_Icon.Hierarchy.image),
                        draw = DrawDetailsPanel,
                        weight = 0f,
                        visible = settings.details,
                        sizePolicy = FR2_SplitView.Info.SizePolicy.KeepPixel,
                        preferredPixel = settings.detailsPanelPixel
                    },
                    new FR2_SplitView.Info
                    {
                        title = new GUIContent("Bookmark", FR2_Icon.Favorite.image),
                        draw = _ => bookmark.Draw(_),
                        weight = 0f,
                        visible = settings.bookmark,
                        sizePolicy = FR2_SplitView.Info.SizePolicy.KeepPixel,
                        preferredPixel = settings.bookmarkPanelPixel
                    }
                }
            };

            sp1.SetupSplitParents();
            sp1.dirty = true;
            sp1.OnSplitterChanged = SavePanelSizes;
            
            Repaint();
            WillRepaint = true;
        }
        
        private void SavePanelSizes()
        {
            // Save panel sizes back to settings for persistence
            // Debug.Log($"[RESIZE_DEBUG] SavePanelSizes called - RESIZE DETECTED");
            for (int i = 0; i < sp1.splits.Count; i++)
            {
                var split = sp1.splits[i];
                if (split.sizePolicy != FR2_SplitView.Info.SizePolicy.KeepPixel) continue;
                
                float oldValue = 0f;
                
                // Map split index to corresponding setting
                switch (i)
                {
                    case 0: // Selection panel
                        oldValue = settings.selectionPanelPixel;
                        settings.selectionPanelPixel = split.preferredPixel;
                        break;
                    case 2: // Details panel  
                        oldValue = settings.detailsPanelPixel;
                        settings.detailsPanelPixel = split.preferredPixel;
                        break;
                    case 3: // Bookmark panel
                        oldValue = settings.bookmarkPanelPixel;
                        settings.bookmarkPanelPixel = split.preferredPixel;
                        break;
                }
                
            }
            
            // Mark the window dirty so Unity saves the serialized settings
            EditorUtility.SetDirty(this);
            // Debug.Log($"[RESIZE_DEBUG] Settings saved - Bookmark: {settings.bookmarkPanelPixel:F1}, Details: {settings.detailsPanelPixel:F1}");
        }

        private void InitTabs()
        {
            bottomTabs = FR2_TabView.Create(this, true,
                new GUIContent(FR2_Icon.Setting.image, "Settings"),
                new GUIContent(FR2_Icon.Ignore.image, "Ignore"),
                new GUIContent(FR2_Icon.Filter.image, "Filter by Type")
            );
            bottomTabs.current = -1;
            bottomTabs.flexibleWidth = false;
            bottomTabs.onTabChange = () => { 
                // Bottom tab changes work directly on FR2 selection - no locks needed
            };

            toolTabs = FR2_TabView.Create(this, false, "Duplicate", "GUID", "Unused", "In Build", "Others");
            toolTabs.current = settings.toolTabIndex;
            toolTabs.onTabChange = () =>
            {
                settings.toolTabIndex = toolTabs.current;

                if (toolTabs.current == 0) // Duplicate
                {
                    if (Duplicated != null)
                    {
                        Duplicated.SetDirty();
                        Duplicated.RefreshSort();
                    }
                }

                if (toolTabs.current == 1) // GUID
                {
                    // GUIDs tool doesn't use drawer system, no action needed
                }

                if (toolTabs.current == 2) // Unused
                {
                    if (RefUnUse != null)
                    {
                        RefUnUse.ResetUnusedAsset(settings.recursiveUnusedScan);
                        RefUnUse.SetDirty();
                        RefUnUse.RefreshSort();
                    }
                }

                if (toolTabs.current == 3) // UsedInBuild
                {
                    if (UsedInBuild != null)
                    {
                        UsedInBuild.SetDirty();
                        UsedInBuild.RefreshSort();
                    }
                }

                if (toolTabs.current == 4) // Others
                {
                    // Others tab has its own internal tab system, no action needed
                }
                
                // Ensure proper group mode restrictions for tools that need them
                if (toolTabs.IsFocusingAny(2, 3)) // Unused or UsedInBuild
                {
                    if (!allowedToolModes.Contains(settings.toolView.groupMode))
                    {
                        settings.toolView.groupMode = FR2_RefDrawer.GroupMode.Type;
                    }
                }
                
                Repaint();
            };
            
            if (FR2_Addressable.asmStatus == FR2_Addressable.ASMStatus.AsmNotFound)
            { // No Addressable
                tabs = FR2_TabView.Create(this, false, // , "Tools"
                    "Uses", "Used By"
                );
            } else
            {
                tabs = FR2_TabView.Create(this, false, // , "Tools"
                    "Uses", "Used By", "Addressables"
                );
            }
            
            tabs.onTabChange = () =>
            {
                settings.mainTabIndex = tabs.current;
                OnTabChange();
            };
            tabs.current = settings.mainTabIndex;
            
            const float IconW = 24f;
            const float LockButtonW = 150f; // Fixed width for lock button with text
            const float BookmarkW = 44f;
            tabs.offsetFirst = IconW * 2 + LockButtonW; // prev, next, lock(with text)
            tabs.offsetLast = IconW * 3 + BookmarkW;

            tabs.callback = new DrawCallback
            {
                BeforeDraw = rect =>
                {
                    if (navigationHistory == null) navigationHistory = new FR2_NavigationHistory();
                    
                    rect.width = IconW;
                    
                    // Previous button
                    bool canGoBack = navigationHistory.CanGoBack;
                    using (GUIEnable(canGoBack))
                    {
                        if (FR2_ToolbarButton.Button(rect, FR2_GUIContent.FromString("<", "Go Back")))
                        {
                            navigationHistory.GoBack();
                            GUIUtility.ExitGUI();
                        }
                    }
                    rect.x += IconW;
                    
                    bool canGoForward = navigationHistory.CanGoForward;
                    using (GUIEnable(canGoForward))
                    {
                        if (FR2_ToolbarButton.Button(rect, FR2_GUIContent.FromString(">", "Go Forward")))
                        {
                            navigationHistory.GoForward();
                            GUIUtility.ExitGUI();
                        }
                    }
                    rect.x += IconW;
                    
                    // Lock/SmartLock button area - fixed width with text content
                    rect.width = LockButtonW;
                    
                    {
                        // Normal lock button with selection count
                        UnityObject[] fr2CurrentSelection = GetFR2Selection();
                        int selectionCount = fr2CurrentSelection?.Length ?? 0;
                        
                        // Split the button area - left side for selection info, right side for lock icon
                        Rect selectionRect = rect;
                        selectionRect.width = LockButtonW - 30f; // Leave space for lock icon
                        
                        Rect lockIconRect = rect;
                        lockIconRect.x = selectionRect.xMax;
                        lockIconRect.width = 30f;
                        
                        // Selection info button (clicking toggles selection visibility)
                        string selectionText = selectionCount > 0 ? $"Selection ({selectionCount})" : "Selection";
                        GUIContent selectionContent = new GUIContent(selectionText, "Click to toggle selection panel");
                        
                        var selState = isSelectionOutOfSync ? ToolbarButtonState.Warning
                            : settings.selection ? ToolbarButtonState.Active
                            : ToolbarButtonState.Normal;
                        if (FR2_ToolbarButton.Button(selectionRect, selectionContent, selState))
                        {
                            settings.selection = !settings.selection;
                            sp1.SetSplitVisible(0, settings.selection);
                            WillRepaint = true;
                        }
                        
                        bool hasSelection = selection.Count > 0;
                        GUIContent pinContent = new GUIContent(
                            _isLocked ? FR2_Icon.Lock.image : FR2_Icon.Unlock.image,
                            _isLocked ? "Unlock selection (allow Unity selection to update FR2)"
                                      : hasSelection ? "Lock current selection" : "No selection to lock"
                        );
                        
                        using (GUIEnable(hasSelection || _isLocked ? (bool?)null : false))
                        {
                            var lockState = _isLocked ? ToolbarButtonState.Active : ToolbarButtonState.Normal;
                            if (FR2_ToolbarButton.Button(lockIconRect, pinContent, lockState))
                            {
                                _isLocked = !_isLocked;
                                if (!_isLocked)
                                {
                                    selection.SyncFromGlobalSelection();
                                    if (FR2_Cache.isReady) RefreshFR2View();
                                }
                                WillRepaint = true;
                            }
                        }
                    }
                },

                AfterDraw = rect =>
                {
                    rect.xMin = rect.xMax - (IconW * 3 + BookmarkW);
                    rect.width = IconW;

                    var sceneOff = ScenePanelHasContent() ? ToolbarButtonState.Warning : ToolbarButtonState.Normal;
                    if (FR2_ToolbarButton.Toggle(rect, ref settings.scene, FR2_GUIContent.FromTexture(FR2_Icon.Scene.image, "Show / Hide Scene References"), sceneOff))
                    {
                        if ((settings.asset == false) && (settings.scene == false))
                        {
                            settings.asset = true;
                            sp2.SetSplitVisible(1, settings.asset);
                        }

                        RefreshPanelVisible();
                        Repaint();
                    }

                    rect.x += IconW;
                    var assetOff = AssetPanelHasContent() ? ToolbarButtonState.Warning : ToolbarButtonState.Normal;
                    if (FR2_ToolbarButton.Toggle(rect, ref settings.asset, FR2_GUIContent.FromTexture(FR2_Icon.Asset.image, "Show / Hide Asset References"), assetOff))
                    {
                        if ((settings.asset == false) && (settings.scene == false))
                        {
                            settings.scene = true;
                            sp2.SetSplitVisible(0, settings.scene);
                        }

                        RefreshPanelVisible();
                        Repaint();
                    }

                    rect.x += IconW;
                    if (FR2_ToolbarButton.Toggle(rect, ref settings.details, FR2_GUIContent.FromTexture(FR2_Icon.Details.image, "Show / Hide Details")))
                    {
                        sp1.SetSplitVisible(2, settings.details);
                        Repaint();
                    }

                    rect.x += IconW;
                    {
                        rect.width = BookmarkW;
                        int bookmarkCount = FR2_Bookmark.Count;
                        bool hasBookmarks = bookmarkCount > 0;

                        var bookmarkTitle = FR2_GUIContent.FromTexture(FR2_Icon.Favorite.image, "Show / Hide Bookmarks");
                        bookmarkTitle.text = hasBookmarks
                            ? (bookmarkCount > 99 ? "99+" : $"{bookmarkCount}")
                            : string.Empty;

                        var bmOff = hasBookmarks ? ToolbarButtonState.Active : ToolbarButtonState.Normal;
                        var bmState = settings.bookmark ? ToolbarButtonState.Active : bmOff;
                        if (FR2_ToolbarButton.Toggle(rect, ref settings.bookmark, bookmarkTitle, bmOff))
                        {
                            sp1.SetSplitVisible(3, settings.bookmark);
                            Repaint();
                        }
                    }
                }
            };
        }



    }
}