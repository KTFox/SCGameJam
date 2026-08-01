using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityObject = UnityEngine.Object;

namespace vietlabs.fr2
{
    internal partial class FR2_RefDrawer
    {
        private readonly Dictionary<string, BookmarkInfo> gBookmarkCache = new Dictionary<string, BookmarkInfo>();

        public int GetBookmarkedCount()
        {
            if (refs == null) return 0;
            int count = 0;
            foreach (var kvp in refs)
            {
                if (kvp.Value.asset != null && kvp.Value.asset.isBuiltIn) continue;
                if (FR2_Bookmark.Contains(kvp.Value)) count++;
            }
            return count;
        }

        public void CommitBookmarked()
        {
            if (refs == null) return;
            var objects = new List<UnityObject>();
            foreach (var kvp in refs)
            {
                if (!FR2_Bookmark.Contains(kvp.Value)) continue;
                
                if (kvp.Value.isSceneRef)
                {
                    if (kvp.Value.component == null) continue;
                    var go = kvp.Value.component is Component c ? c.gameObject : kvp.Value.component;
                    if (go != null) objects.Add(go);
                }
                else
                {
                    string path = FR2_Cache.GUIDToAssetPath(kvp.Value.asset.guid);
                    var obj = AssetDatabase.LoadAssetAtPath<UnityObject>(path);
                    if (obj != null) objects.Add(obj);
                }
            }
            if (objects.Count > 0)
            {
                (Config.window as FR2_WindowAll)?.LockSelection();
                Selection.objects = objects.ToArray();
            }
        }

        private void CommitGroupBookmarked(string groupLabel)
        {
            var ids = groupDrawer.GetChildren(groupLabel);
            if (ids == null) return;
            
            var objects = new List<UnityObject>();
            for (int i = 0; i < ids.Count; i++)
            {
                if (!refs.TryGetValue(ids[i], out FR2_Ref rf)) continue;
                if (!FR2_Bookmark.Contains(rf)) continue;
                
                if (rf.isSceneRef)
                {
                    if (rf.component == null) continue;
                    var go = rf.component is Component c ? c.gameObject : rf.component;
                    if (go != null) objects.Add(go);
                }
                else
                {
                    string path = FR2_Cache.GUIDToAssetPath(rf.asset.guid);
                    var obj = AssetDatabase.LoadAssetAtPath<UnityObject>(path);
                    if (obj != null) objects.Add(obj);
                }
            }
            if (objects.Count > 0)
            {
                (Config.window as FR2_WindowAll)?.LockSelection();
                Selection.objects = objects.ToArray();
            }
        }

        private void SetBookmarkGroup(string groupLabel, bool willbookmark)
        {
            var ids = groupDrawer.GetChildren(groupLabel);
            if (ids == null) return;
            
            for (var i = 0; i < ids.Count; i++)
            {
                if (!refs.TryGetValue(ids[i], out FR2_Ref rf)) continue;
                if (willbookmark) FR2_Bookmark.Add(rf);
                else FR2_Bookmark.Remove(rf);
            }

            InvalidateGroupCache();
        }

        private BookmarkInfo GetBMInfo(string groupLabel)
        {
            if (gBookmarkCache.TryGetValue(groupLabel, out BookmarkInfo info)) return info;

            var ids = groupDrawer.GetChildren(groupLabel);
            info = new BookmarkInfo();
            if (ids != null)
            {
                for (var i = 0; i < ids.Count; i++)
                {
                    if (!refs.TryGetValue(ids[i], out FR2_Ref rf)) continue;
                    if (rf.asset != null && rf.asset.isBuiltIn) continue;
                    info.total++;
                    if (FR2_Bookmark.Contains(rf)) info.count++;
                }
            }

            gBookmarkCache.Add(groupLabel, info);
            return info;
        }

        private void SetCheckboxData(FR2_Ref rf)
        {
            bool canSelect = rf.asset == null || !rf.asset.isBuiltIn;
            _rowData.showCheckbox = Config.showToggle;
            _rowData.selectionPadLeft = Config.selectionPadLeft;
            if (!Config.showToggle) return;
            if (!canSelect)
            {
                _rowData.checkboxValue = false;
                _rowData.checkboxDisabled = true;
                _rowData.onCheckboxChanged = null;
                _rowData.onCheckboxShiftClick = null;
                _rowData.onCheckboxAltClick = null;
                _rowData.onCheckboxCtrlClick = null;
                return;
            }
            _rowData.checkboxDisabled = false;
            _rowData.checkboxValue = rf.isSelected();
            _rowData.onCheckboxChanged = (newVal) =>
            {
                if (newVal) FR2_Bookmark.Add(rf);
                else FR2_Bookmark.Remove(rf);
            };
            _rowData.onCheckboxShiftClick = rf.OnShiftClick != null ? () => rf.OnShiftClick(rf) : null;
            _rowData.onCheckboxAltClick = rf.OnAltClick != null ? () => rf.OnAltClick(rf) : null;
            _rowData.onCheckboxCtrlClick = rf.OnCtrlClick != null ? () => rf.OnCtrlClick(rf) : null;
        }

        public void ToggleAllItems()
        {
            if (refs == null) return;
            
            int bookmarkedCount = 0;
            int totalBookmarkableCount = 0;
            
            foreach (var kvp in refs)
            {
                if (kvp.Value.asset != null && kvp.Value.asset.isBuiltIn) continue;
                totalBookmarkableCount++;
                if (FR2_Bookmark.Contains(kvp.Value)) bookmarkedCount++;
            }

            if (totalBookmarkableCount == 0) return;
            bool newState = bookmarkedCount < totalBookmarkableCount / 2;

            foreach (var kvp in refs)
            {
                if (kvp.Value.asset != null && kvp.Value.asset.isBuiltIn) continue;
                if (newState) FR2_Bookmark.Add(kvp.Value);
                else FR2_Bookmark.Remove(kvp.Value);
            }
            
            InvalidateGroupCache();
        }
        
        public void ToggleGroupItems(string groupLabel)
        {
            var ids = groupDrawer.GetChildren(groupLabel);
            if (ids == null) return;
            
            for (var i = 0; i < ids.Count; i++)
            {
                if (!refs.TryGetValue(ids[i], out FR2_Ref rf)) continue;
                bool currentState = FR2_Bookmark.Contains(rf);
                if (currentState) FR2_Bookmark.Remove(rf);
                else FR2_Bookmark.Add(rf);
            }
            
            InvalidateGroupCache();
        }
        
        public void SetGroupItemsState(string groupLabel, bool willBookmark)
        {
            SetBookmarkGroup(groupLabel, willBookmark);
        }

        private void HandleRefCtrlClick(FR2_Ref rf)
        {
            bool newState = !rf.isSelected();
            if (newState) FR2_Bookmark.Add(rf);
            else FR2_Bookmark.Remove(rf);
            
            string groupName = GetGroupForRef(rf);
            if (!string.IsNullOrEmpty(groupName) && groupDrawer.GetChildren(groupName) != null)
                SetGroupItemsState(groupName, newState);
            
            InvalidateGroupCache();
        }
        
        private void HandleRefAltClick(FR2_Ref rf)
        {
            string groupName = GetGroupForRef(rf);
            if (!string.IsNullOrEmpty(groupName) && groupDrawer.GetChildren(groupName) != null)
                ToggleGroupItems(groupName);
        }
        
        private void HandleRefShiftClick(FR2_Ref rf)
        {
            ToggleAllItems();
        }
        
        private void ApplySameActionToAllSiblingGroups(bool newState)
        {
            if (groupDrawer?.tree?.rootItem?.children == null) return;
            
            foreach (var groupItem in groupDrawer.tree.rootItem.children)
            {
                BookmarkInfo info = GetBMInfo(groupItem.id);
                if (info.total > 0) SetBookmarkGroup(groupItem.id, newState);
            }
        }
        
        private void InvertAllSiblingGroupsState()
        {
            if (groupDrawer?.tree?.rootItem?.children == null) return;
            
            foreach (var groupItem in groupDrawer.tree.rootItem.children)
            {
                BookmarkInfo info = GetBMInfo(groupItem.id);
                if (info.total > 0)
                {
                    bool currentState = info.count == info.total;
                    SetBookmarkGroup(groupItem.id, !currentState);
                }
            }
        }

        internal class BookmarkInfo
        {
            public int count;
            public int total;
        }
    }
}
