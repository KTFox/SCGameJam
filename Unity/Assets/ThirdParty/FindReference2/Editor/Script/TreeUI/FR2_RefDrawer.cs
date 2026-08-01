using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityObject = UnityEngine.Object;

namespace vietlabs.fr2
{
    internal struct ConfigChangedEvent
    {
        internal enum ChangeType { Display, Sort, GroupMode }
        public ChangeType type;
        public ConfigChangedEvent(ChangeType type) { this.type = type; }
    }
    
    internal partial class FR2_RefDrawer : IRefDraw
    {
        internal const float USAGE_COUNT_COL_WIDTH = 20f;
        
        internal class RefDrawerConfig : IEventSource
        {
            public IWindow window;
            public Func<Sort> getSortMode;
            public Func<string> getGroupMode;
            public bool showFullPath;
            public bool showToggle = true;
            public bool showHighlight = true;
            public bool sceneUsesLeftColumns;
            public float selectionPadLeft;
            public Func<bool> shouldShowExtension;
            public Func<bool> shouldShowDetailButton;
            public Action onCacheInvalidated;
            
            public void NotifyDisplayChanged()
            {
                this.Dispatch(new ConfigChangedEvent(ConfigChangedEvent.ChangeType.Display));
            }
            
            public void NotifySortChanged()
            {
                this.Dispatch(new ConfigChangedEvent(ConfigChangedEvent.ChangeType.Sort));
            }
            
            public void NotifyGroupModeChanged()
            {
                this.Dispatch(new ConfigChangedEvent(ConfigChangedEvent.ChangeType.GroupMode));
            }
        }

        internal class AssetDrawingConfig : RefDrawerConfig
        {
            public bool showFileSize;
            public bool showExtension;
            public bool showUsageType;
            public bool showAssetBundleName;
            public bool showAtlasName;
        }

        internal class SceneDrawingConfig : RefDrawerConfig
        {
            public bool showDetails;
        }

        public static GUIStyle toolbarSearchField;
        public static GUIStyle toolbarSearchFieldCancelButton;
        public static GUIStyle toolbarSearchFieldCancelButtonEmpty;

        private readonly Func<string> getGroupMode;
        private readonly Func<Sort> getSortMode;
        internal readonly FR2_TreeUI2.GroupDrawer groupDrawer;
        public readonly List<FR2_Asset> highlight = new List<FR2_Asset>();

        private readonly string searchTerm = string.Empty;
        private readonly bool showSearch = true;
        public Action<Rect, FR2_Ref> afterItemDraw;
        public Action<Rect, FR2_Ref> beforeItemDraw;
        public bool caseSensitive = false;
        public Action<Rect, string, int> customDrawGroupLabel;
        public Func<FR2_Ref, string> customGetGroup;

        private bool dirty;
        public RefDrawerConfig Config { get; private set; }
        private int excludeCount;
        public AssetDrawingConfig AssetConfig { get; private set; }
        public SceneDrawingConfig SceneConfig { get; private set; }

        public string level0Group;
        internal List<FR2_Ref> list;
        public string messageEmpty = "It's empty!";
        public string messageNoRefs = "Do select something!";
        internal Dictionary<string, FR2_Ref> refs;
        private bool selectFilter;
        public bool showDetail;
        private bool showIgnore;
        public float paddingLeft = -4f;
        public float paddingRight = 4f;
        public bool skipFilter;
        private bool hasValidSelection = false;
        public Func<string> GetContextualEmptyMessage;
        
        private FR2_Ref _showDetailRef;
        private Action _cachedShowDetailAction;
        private readonly Comparison<FR2_Asset> _highlightSortComparison;

        public FR2_RefDrawer(RefDrawerConfig config)
        {
            this.window = config.window;
            this.getSortMode = config.getSortMode;
            this.getGroupMode = config.getGroupMode;
            this.Config = config;
            this.AssetConfig = config as AssetDrawingConfig ?? new AssetDrawingConfig();
            this.SceneConfig = config as SceneDrawingConfig ?? new SceneDrawingConfig();
            groupDrawer = new FR2_TreeUI2.GroupDrawer(DrawGroup, DrawAsset);
            
            _cachedShowDetailAction = OnShowDetail;
            _highlightSortComparison = CompareHighlightDepth;
            
            config.AddListener<ConfigChangedEvent>(OnConfigChanged);
        }
        
        private void OnShowDetail()
        {
            if (_showDetailRef == null) return;
            showDetail = true;
            highlight.Clear();
            highlight.Add(_showDetailRef.asset);

            FR2_Asset p = _showDetailRef.addBy;
            var cnt = 0;
            while (p != null && refs.ContainsKey(p.guid))
            {
                highlight.Add(p);
                FR2_Ref fr2Ref = refs[p.guid];
                if (fr2Ref != null) p = fr2Ref.addBy;
                if (++cnt > 100) break;
            }

            highlight.Sort(_highlightSortComparison);
            Event.current.Use();
        }
        
        private int CompareHighlightDepth(FR2_Asset item1, FR2_Asset item2)
        {
            int d1 = refs[item1.guid].depth;
            int d2 = refs[item2.guid].depth;
            return d1.CompareTo(d2);
        }
        
        private void OnConfigChanged(ConfigChangedEvent evt)
        {
            switch (evt.type)
            {
                case ConfigChangedEvent.ChangeType.Display:
                    window?.Repaint();
                    break;
                case ConfigChangedEvent.ChangeType.Sort:
                    RefreshSort();
                    window?.Repaint();
                    break;
                case ConfigChangedEvent.ChangeType.GroupMode:
                    SetDirty();
                    RefreshSort();
                    window?.Repaint();
                    break;
            }
        }

        internal FR2_Ref[] source => FR2_Ref.FromList(list);
        internal bool HasContent => list != null && list.Count > 0;
        public IWindow window { get; set; }
        public bool IsDirty => dirty;
        public bool IsEmpty => refs == null || refs.Count == 0 || !groupDrawer.hasChildren;

        public bool Draw(Rect rect)
        {
            if (dirty) ApplyFilter();

            if (!hasValidSelection)
            {
                DrawEmpty(rect, messageNoRefs);
                return false;
            }

            if (refs == null || refs.Count == 0)
            {
                string contextualMessage = GetContextualEmptyMessage?.Invoke();
                DrawEmpty(rect, contextualMessage ?? messageEmpty);
                return false;
            }

            if (list == null || list.Count == 0)
            {
                DrawEmpty(rect, messageEmpty);
                return false;
            }

            if (!groupDrawer.hasChildren)
            {
                string contextualMessage = GetContextualEmptyMessage?.Invoke();
                DrawEmpty(rect, contextualMessage ?? messageEmpty);
                return false;
            }

            if (groupDrawer.hasValidTree) groupDrawer.tree.itemPaddingLeft = paddingLeft;
            groupDrawer.Draw(rect);
            return false;
        }

        public bool DrawLayout()
        {
            if (dirty) ApplyFilter();

            if (!hasValidSelection)
            {
                EditorGUILayout.HelpBox(messageNoRefs, MessageType.Info);
                return false;
            }

            if (refs == null || refs.Count == 0)
            {
                string contextualMessage = GetContextualEmptyMessage?.Invoke();
                EditorGUILayout.HelpBox(contextualMessage ?? messageEmpty, MessageType.Info);
                return false;
            }

            if (!groupDrawer.hasChildren)
            {
                string contextualMessage = GetContextualEmptyMessage?.Invoke();
                EditorGUILayout.HelpBox(contextualMessage ?? messageEmpty, MessageType.Info);
                return false;
            }

            if (groupDrawer.hasValidTree) groupDrawer.tree.itemPaddingLeft = paddingLeft;
            groupDrawer.DrawLayout();
            return false;
        }

        public int ElementCount()
        {
            if (refs == null) return 0;
            return refs.Count;
        }

        private void DrawEmpty(Rect rect, string text)
        {
            rect = GUI2.Padding(rect, 2f, 2f);
            rect.height = 45f;

            MessageType messageType = MessageType.Info;
            if (text.Contains("not scanned") || text.Contains("content changed") || text.Contains("refresh cache"))
                messageType = MessageType.Warning;

            EditorGUI.HelpBox(rect, text, messageType);
        }

        public void DrawDetails(Rect rect)
        {
            Rect r = rect;
            r.xMin -= 8f;
            r.height = 18f;

            for (var i = 0; i < highlight.Count; i++)
            {
                var asset = highlight[i];
                asset.PopulateRowData(_rowData, false, window, showExtension: AssetConfig.showExtension);
                if (!Config.showHighlight) _rowData.selection = RowSelection.None;
                _rowData.showCheckbox = false;
                _rowData.selectionPadLeft = 0f;
                _rowData.selection = RowSelection.None;
                _rowData.ClearColumns();
                asset.SetColumnUsedByCount(_rowData, 0, _assetLeftColumns[0]);
                FR2_RowDrawer.Draw(r, _rowData, _assetLeftColumns);
                r.y += 18f;
                r.xMin += 12f;
            }
        }

        private readonly RowDrawData _rowData = new RowDrawData();
        private readonly MetadataColumn[] _assetLeftColumns = new MetadataColumn[]
        {
            new MetadataColumn("usedByCount", USAGE_COUNT_COL_WIDTH, ColumnAlign.Right),
        };
        private readonly MetadataColumn[] _assetColumns = new MetadataColumn[]
        {
            new MetadataColumn("fileSize", 0f),
            new MetadataColumn("addressable", 0f),
            new MetadataColumn("atlas", 0f),
            new MetadataColumn("assetBundle", 0f),
        };
        private readonly MetadataColumn[] _sceneColumns = new MetadataColumn[]
        {
            new MetadataColumn("refInfo", 0f),
        };

        private void DrawAsset(Rect r, string guid)
        {
            if (!refs.TryGetValue(guid, out FR2_Ref rf)) return;

            if (rf.isSceneRef)
            {
                if (rf.component == null) return;
                if (!(rf is FR2_SceneRef re)) return;
                beforeItemDraw?.Invoke(r, rf);

                SetCheckboxData(rf);
                re.PopulateRowData(_rowData, Config.showFullPath);
                if (!Config.showHighlight) _rowData.selection = RowSelection.None;
                re.SetColumnReferenceInfo(_rowData, 0, _sceneColumns[0]);
                FR2_RowDrawer.Draw(r, _rowData, Config.sceneUsesLeftColumns ? _assetLeftColumns : null, _sceneColumns);
                CheckColumnWidthChanges(_sceneColumns);
            }
            else
            {
                beforeItemDraw?.Invoke(r, rf);

                bool isHighlight = Config.showHighlight && highlight.Contains(rf.asset);
                bool shouldShowDetailBtn = Config.shouldShowDetailButton?.Invoke() ?? true;

                _showDetailRef = rf;
                SetCheckboxData(rf);
                rf.asset.PopulateRowData(_rowData, Config.showFullPath, window,
                    shouldShowDetailBtn ? _cachedShowDetailAction : null, rf, showExtension: AssetConfig.showExtension);
                if (!Config.showHighlight) _rowData.selection = RowSelection.None;
                if (isHighlight) _rowData.state = RowState.Active;

                _rowData.ClearColumns();
                rf.asset.SetColumnUsedByCount(_rowData, 0, _assetLeftColumns[0]);

                _assetColumns[0].visible = AssetConfig.showFileSize;
                if (AssetConfig.showFileSize) rf.asset.SetColumnFileSize(_rowData, 0, _assetColumns[0]);
                rf.asset.SetColumnAddressable(_rowData, 1, _assetColumns[1]);
                _assetColumns[2].visible = AssetConfig.showAtlasName && FR2_Setting.s.displayAtlasName;
                if (_assetColumns[2].visible) rf.asset.SetColumnAtlas(_rowData, 2, _assetColumns[2]);
                _assetColumns[3].visible = AssetConfig.showAssetBundleName && FR2_Setting.s.displayAssetBundleName;
                if (_assetColumns[3].visible) rf.asset.SetColumnAssetBundle(_rowData, 3, _assetColumns[3]);

                FR2_RowDrawer.Draw(r, _rowData, _assetLeftColumns, _assetColumns);
                CheckColumnWidthChanges(_assetColumns);
            }

            afterItemDraw?.Invoke(r, rf);
        }

        private void CheckColumnWidthChanges(MetadataColumn[] columns)
        {
            for (int i = 0; i < columns.Length; i++)
            {
                if (!columns[i].widthChanged) continue;
                columns[i].widthChanged = false;
                window?.Repaint();
                return;
            }
        }

        private void ResetColumnWidths()
        {
            foreach (var col in _assetColumns) col.width = 0f;
            foreach (var col in _sceneColumns) col.width = 0f;
        }

        private void DrawToggleGroup(Rect r, string groupLabel)
        {
            BookmarkInfo info = GetBMInfo(groupLabel);
            if (info.total == 0) return;
            
            bool selectAll = info.count == info.total;
            r.width = 16f;
            
            Event evt = Event.current;
            bool isMouseOver = r.Contains(evt.mousePosition);
            bool isMouseDown = evt.type == EventType.MouseDown && evt.button == 0 && isMouseOver;
            
            if (isMouseDown)
            {
                bool ctrl = Application.platform == RuntimePlatform.OSXEditor ? evt.command : evt.control;
                bool alt = evt.alt;
                
                if (ctrl)
                {
                    bool newState = !selectAll;
                    ApplySameActionToAllSiblingGroups(newState);
                    evt.Use();
                    return;
                }
                if (alt)
                {
                    InvertAllSiblingGroupsState();
                    evt.Use();
                    return;
                }
            }
            
            if (GUI2.Toggle(r, ref selectAll)) SetBookmarkGroup(groupLabel, selectAll);
        }

        private void DrawGroup(Rect r, string label, int childCount)
        {
            if (string.IsNullOrEmpty(label)) label = "(none)";
            
            BookmarkInfo info = GetBMInfo(label);
            bool hasBookmarkableItems = info.total > 0;
            
            if (hasBookmarkableItems)
            {
                DrawToggleGroup(r, label);
                r.xMin += 18f;
            }

            if (hasBookmarkableItems && info.count > 0)
            {
                if (FR2_Badge.SelectBadge(r, info.count))
                {
                    CommitGroupBookmarked(label);
                    Event.current.Use();
                }
                r.xMax -= FR2_Badge.SelectBadgeWidth + 4f;
            }

            Rect clickRect = r;
            string groupMode = getGroupMode();
            if (groupMode == GroupMode.Folder)
            {
                Texture tex = AssetDatabase.GetCachedIcon("Assets");
                var iconRect = new Rect(r.x, r.y, 16f, 16f);
                GUI.DrawTexture(iconRect, tex);
                r.xMin += 16f;
            }
            else if (groupMode == GroupMode.Hierarchy)
            {
                Texture tex = null;
                if (_hierarchyGroupCache.TryGetValue(label, out var go) && go != null)
                    tex = AssetPreview.GetMiniThumbnail(go);

                if (tex != null)
                {
                    var iconRect = new Rect(r.x, r.y, 16f, 16f);
                    GUI.DrawTexture(iconRect, tex);
                }
                r.xMin += 18f;
            }

            if (customDrawGroupLabel != null)
            {
                customDrawGroupLabel.Invoke(r, label, childCount);
            }
            else
            {
                r.xMax -= 32f;
                
                if (groupMode == GroupMode.SourceComponent)
                {
                    DrawSourceComponentGroupLabel(r, label);
                }
                else if (groupMode == GroupMode.SourceGameObject)
                {
                    DrawSourceGameObjectGroupLabel(r, label);
                }
                else
                {
                    GUI.Label(r, FR2_GUIContent.FromString(label), EditorStyles.label);
                }
            }

            bool hasMouse = (Event.current.type == EventType.MouseUp) && clickRect.Contains(Event.current.mousePosition);
            if (hasMouse && (Event.current.button == 0))
            {
                if (groupMode == GroupMode.Folder)
                {
                    string folderPath = label.EndsWith("/") ? label.TrimEnd('/') : label;
                    var folder = AssetDatabase.LoadAssetAtPath<UnityObject>("Assets/" + folderPath);
                    if (folder != null) EditorGUIUtility.PingObject(folder);
                    Event.current.Use();
                }
                else if (groupMode == GroupMode.Hierarchy && _hierarchyGroupCache.TryGetValue(label, out var pingGo) && pingGo != null)
                {
                    EditorGUIUtility.PingObject(pingGo);
                    Event.current.Use();
                }
            }
            
            if (hasMouse && (Event.current.button == 1))
            {
                var menu = new GenericMenu();
                
                if (hasBookmarkableItems)
                {
                    menu.AddItem(FR2_GUIContent.FromString("Add Bookmark"), false, () => { SetBookmarkGroup(label, true); });
                    menu.AddItem(FR2_GUIContent.FromString("Remove Bookmark"), false, () => { SetBookmarkGroup(label, false); });
                }
                else
                {
                    menu.AddDisabledItem(FR2_GUIContent.FromString("Add Bookmark (No assets to bookmark)"));
                    menu.AddDisabledItem(FR2_GUIContent.FromString("Remove Bookmark"));
                }

                menu.ShowAsContext();
                Event.current.Use();
            }
        }

        public void SetDirty()
        {
            dirty = true;
        }

        public void InvalidateGroupCache()
        {
            gBookmarkCache.Clear();
            Config.onCacheInvalidated?.Invoke();
        }
        
        public string GetGroupForRef(FR2_Ref rf)
        {
            return GetGroup(rf);
        }

        public void ClearSelection()
        {
            hasValidSelection = false;
            refs = null;
            if (list != null) list.Clear();
            dirty = true;
        }

        public bool isExclueAnyItem()
        {
            return excludeCount > 0;
        }

        public Dictionary<string, FR2_Ref> getRefs()
        {
            return refs;
        }
        
        private static readonly RowDrawData _groupRowData = new RowDrawData();
        
        private static void DrawSourceComponentGroupLabel(Rect r, string label)
        {
            if (!_sourceComponentCache.TryGetValue(label, out var comp) || comp == null)
            {
                GUI.Label(r, FR2_GUIContent.FromString(label), EditorStyles.label);
                return;
            }
            
            FR2_SceneRef.PopulateRowDataAsComponent(_groupRowData, comp);
            FR2_RowDrawer.Draw(r, _groupRowData);
        }

        private static void DrawSourceGameObjectGroupLabel(Rect r, string label)
        {
            if (!_sourceGameObjectCache.TryGetValue(label, out var go) || go == null)
            {
                GUI.Label(r, FR2_GUIContent.FromString(label), EditorStyles.label);
                return;
            }
            
            FR2_SceneRef.PopulateRowDataAsGameObject(_groupRowData, go);
            FR2_RowDrawer.Draw(r, _groupRowData);
        }
    }
}
