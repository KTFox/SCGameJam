using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityObject = UnityEngine.Object;


namespace vietlabs.fr2
{
    internal partial class FR2_WindowAll
    {
        internal UnityObject[] _cachedSelection;
        internal int _cachedSelectionFrame = -1;
        private string[] ids;
        private bool _isRefreshingView;
        private bool _selectionTooLarge;
        
        private void OnSelectionManagerChanged()
        {
            if (FR2_SettingExt.disable) return;
            if (selection == null) return;
            if (_isLocked) return;
            
            // Check and consume ping lock state - if active, skip sync but hide warnings
            bool hadPingLock = smartLock?.ConsumePingLockState() ?? false;
            if (hadPingLock)
            {
                
                // Still need to check if selection is out of sync for UI highlighting
                var unitySelection = FR2_SelectionManager.Instance.GetUnitySelection();
                var fr2Selection = selection.GetUnityObjects();
                isSelectionOutOfSync = !AreSelectionsEqual(unitySelection, fr2Selection);
                
                WillRepaint = true;
                Repaint();
                return;
            }
            
            // When Unity selection becomes empty (e.g. objects deleted), always clear FR2 selection
            if (!FR2_SelectionManager.Instance.HasSelection)
            {
                _selectionTooLarge = false;
                if (selection.Count > 0)
                {
                    selection.SyncFromGlobalSelection();
                    isSelectionOutOfSync = false;
                    if (FR2_Cache.isReady) RefreshFR2View();
                }
                WillRepaint = true;
                Repaint();
                return;
            }
            
            int totalCount = FR2_SelectionManager.Instance.TotalCount;
            int maxCount = FR2_SettingExt.maxSelectionCount;
            if (totalCount > maxCount)
            {
                _selectionTooLarge = true;
                selection.SyncFromGlobalSelection();
                isSelectionOutOfSync = false;
                WillRepaint = true;
                Repaint();
                return;
            }
            
            _selectionTooLarge = false;
            var shouldRefresh = smartLock.ShouldRefreshWithSmartLogic(this, selection.GetUnityObjects());
            if (shouldRefresh)
            {
                selection.SyncFromGlobalSelection();
                isSelectionOutOfSync = false;
                
                if (FR2_Cache.isReady)
                {
                    RefreshFR2View();
                }
                else
                {
                }
            }
            else
            {
                isSelectionOutOfSync = true;
            }
            
            WillRepaint = true;
            Repaint();
        }
        
        public override void OnSelectionChange()
        {
            // DO NOTHING
        }

        void OnPanelSelectionChanged()
        {
            if (!FR2_Cache.isReady) return;
            if (SceneUsesDrawer == null) InitIfNeeded();
            if (UsesDrawer == null) InitIfNeeded();
            if (selection == null) return;

            navigationHistory.SetWindow(this);

            // Use unified selection manager (static access only)
            UnityObject[] currentSelection = FR2_SelectionManager.Instance.GetUnitySelection();
            
            if (currentSelection.Length > 0)
            {
                navigationHistory.RecordSelection(currentSelection);
            }
            
            if (isFocusingGUIDs)
            {
                if (guidObjs == null) guidObjs = new Dictionary<string, UnityObject>();
                else guidObjs.Clear();
                _selectedGuid = null;
                
                for (var i = 0; i < currentSelection.Length; i++)
                {
                    UnityObject item = currentSelection[i];
                var (guid, fileId) = FR2_SelectionManager.GetCachedGuidAndLocalId(item);
                    if (!string.IsNullOrEmpty(guid))
                    {
                        guidObjs.Add(guid + "/" + fileId, currentSelection[i]);
                    }
                }
            }

            if (isFocusingUnused)
            {
                RefUnUse.ResetUnusedAsset(settings.recursiveUnusedScan);
            }
        }

        internal void SetFR2Selection(UnityObject[] objects)
        {
            selection?.SetUnityObjects(objects);
            
            // Only refresh FR2 view if cache is ready - this prevents errors but keeps selection updated
            if (FR2_Cache.isReady)
            {
                RefreshFR2View();
            }
            else
            {
            }
        }
        
        internal UnityObject[] GetFR2Selection()
        {
            return selection?.GetUnityObjects() ?? Array.Empty<UnityObject>();
        }
        
        private void RefreshFR2View()
        {
            if (_isRefreshingView) return;
            _isRefreshingView = true;
            
            OnPanelSelectionChanged();
            
            if (selection != null) selection.RefreshView();
            
            ids = Array.Empty<string>();
            RefreshPanelVisible();

            if (selection.isSelectingSceneObject)
            {
                if (FR2_SceneCache.hasCache)
                {
                    var gameObjects = new List<UnityObject>();
                    foreach (string instIdStr in selection.instSet)
                    {
                        if (int.TryParse(instIdStr, out int instId))
                        {
                var obj = FR2_Unity.InstanceIdToObject(instId);
                            if (obj != null) gameObjects.Add(obj);
                        }
                    }
                    
                    foreach (var obj in gameObjects)
                    {
                        if (obj is GameObject go)
                            FR2_SceneCache.Api?.ScanGameObjectIfNeeded(go);
                    }
                    
                    RefSceneInScene.ResetSceneInScene(gameObjects.OfType<GameObject>().ToArray());
                    SceneToAssetDrawer.Reset(gameObjects.OfType<GameObject>().ToArray(), true, true);
                    SceneUsesDrawer.ResetSceneUseSceneObjects(gameObjects.OfType<GameObject>().ToArray());
                }
                
            }
            else if (selection.isSelectingAsset)
            {
                ids = selection.guidSet.ToArray();
                
                EnsureSelectedAssetsScanned(ids);
                
                UsesDrawer.Reset(ids, true);
                UsedByDrawer.Reset(ids, false);
                RefInScene.Reset(ids);
                AddressableDrawer.RefreshView();
            }
            else
            {
                SceneUsesDrawer?.ClearSelection();
                RefSceneInScene?.ClearSelection();
                SceneToAssetDrawer?.ClearSelection();
                UsesDrawer?.ClearSelection();
                UsedByDrawer?.ClearSelection();
                RefInScene?.ClearSelection();
            }
            
            RefreshContextualMessages();
            _isRefreshingView = false;
        }

        private void EnsureSelectedAssetsScanned(string[] guids)
        {
            if (!FR2_Cache.isReady) return;
            
            bool needsRefresh = false;
            foreach (var guid in guids)
            {
                if (string.IsNullOrEmpty(guid)) continue;
                
                if (!FR2_Cache._map.TryGetValue(guid, out var asset))
                {
                    asset = FR2_Cache.GetAsset(guid, true);
                    if (asset == null || !asset.IsCriticalAsset()) continue;
                    needsRefresh = true;
                    continue;
                }
                
                if (asset == null || !asset.IsCriticalAsset()) continue;
                
                if (!asset.hasBeenScanned)
                {
                    FR2_Cache.RefreshAsset(guid, true);
                    needsRefresh = true;
                }
            }
            
            if (needsRefresh) FR2_Cache.IncrementalRefresh();
        }

        private void OnSceneChanged(Scene arg0, Scene arg1)
        {
            OnSelectionChange();
        }
    }
} 