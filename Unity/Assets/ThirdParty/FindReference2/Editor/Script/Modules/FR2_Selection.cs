using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityObject = UnityEngine.Object;


namespace vietlabs.fr2
{
    internal class FR2_Selection : IRefDraw
    {
        internal readonly FR2_RefDrawer drawer;
        internal readonly HashSet<string> guidSet = new HashSet<string>();
        internal readonly HashSet<string> instSet = new HashSet<string>();

        private bool dirty;
        internal bool isLock;
        internal Dictionary<string, FR2_Ref> refs;
        private bool _historyDirty = true;
        private Vector2 _historyScroll;

        public FR2_Selection(IWindow window, Func<FR2_RefDrawer.Sort> getSortMode, Func<string> getGroupMode)
        {
            this.window = window;
            drawer = new FR2_RefDrawer(new FR2_RefDrawer.AssetDrawingConfig
            {
                window = window,
                getSortMode = getSortMode,
                getGroupMode = () => FR2_RefDrawer.GroupMode.None,
                showFullPath = false,
                showFileSize = false,
                showExtension = true,
                showUsageType = false,
                showAssetBundleName = false,
                showAtlasName = false,
                showToggle = false,
                showHighlight = false,
                sceneUsesLeftColumns = true,
				selectionPadLeft = FR2_WindowAll.SELECTION_PANEL_PAD_LEFT,
                shouldShowExtension = () => true,
                shouldShowDetailButton = () => false,
                onCacheInvalidated = () => { }
            })
            {
                groupDrawer = { hideGroupIfPossible = true },
                level0Group = string.Empty,
                paddingLeft = -32f,
                skipFilter = true
            };

            dirty = true;
            drawer.SetDirty();
        }

        public int Count => guidSet.Count + instSet.Count;
        public bool isSelectingAsset => guidSet.Count > 0 && instSet.Count == 0;
        public bool isSelectingSceneObject => instSet.Count > 0 && guidSet.Count == 0;
        public IWindow window { get; set; }

        public int ElementCount() => refs?.Count ?? 0;

        public bool DrawLayout()
        {
            if (dirty) RefreshView();
            return drawer.DrawLayout();
        }

        public bool Draw(Rect rect)
        {
            if (dirty) RefreshView();
            if (_historyDirty) RefreshHistoryView();

            var history = FR2_SelectionHistory.inst;
            bool hasHistory = history.Count > 0;

            if (!hasHistory)
            {
                if (refs == null || refs.Count == 0)
                {
                    return false;
                }
                rect.yMax -= 16f;
                drawer.Draw(rect);
                DrawSaveButton(rect);
                return false;
            }

            float historyHeight = CalculateHistoryHeight(history) + 2f;
            float maxHistoryHeight = Mathf.Floor(rect.height * 0.5f);
            float clampedHistoryHeight = Mathf.Min(historyHeight, maxHistoryHeight);
            float separatorH = 1f;

            Rect historyRect = rect;
            historyRect.yMin = rect.yMax - clampedHistoryHeight;

            Rect separatorRect = historyRect;
            separatorRect.yMin -= separatorH + 4f;
            separatorRect.height = separatorH;
            EditorGUI.DrawRect(separatorRect, new Color(0.5f, 0.5f, 0.5f, 0.3f));

            Rect currentRect = rect;
            currentRect.yMax = separatorRect.yMin - 2f;

            if (refs != null && refs.Count > 0)
            {
                drawer.Draw(currentRect);
            }

            DrawSaveButton(currentRect);

            if (historyHeight > clampedHistoryHeight)
            {
                Rect viewRect = new Rect(0, 0, historyRect.width - 14f, historyHeight);
                _historyScroll = GUI.BeginScrollView(historyRect, _historyScroll, viewRect);
                DrawHistorySection(new Rect(0, 0, viewRect.width, historyHeight));
                GUI.EndScrollView();
            }
            else
            {
                DrawHistorySection(historyRect);
            }

            return false;
        }

        private void DrawSaveButton(Rect panelRect)
        {
            bool hasSelection = guidSet.Count > 0 || instSet.Count > 0;
            if (!hasSelection) return;

            float btnW = 50f;
            float btnH = 16f;
            Rect btnRect = new Rect(panelRect.xMax - btnW - 14f, panelRect.yMax - btnH - 4f, btnW, btnH);
            
            if (GUI.Button(btnRect, "Save", EditorStyles.miniButton))
            {
                var history = FR2_SelectionHistory.inst;
                history.Pin(guidSet, instSet);
                _historyDirty = true;
                window?.Repaint();
            }
        }


        private float CalculateHistoryHeight(FR2_SelectionHistory history)
        {
            float h = 0f;
            for (int i = 0; i < history.Count; i++)
            {
                var g = history.Get(i);
                if (g == null) continue;
                if (!FR2_SelectionHistory.IsSceneGroupValid(g.Value)) continue;
                int count = CountValidItems(g.Value, i);
                if (count == 0) continue;
                h += 18f;
                if (count > 1 && IsGroupExpanded(i))
                {
                    h += count * 18f;
                }
            }
            return h;
        }

        private readonly HashSet<int> _expandedGroups = new HashSet<int>();

        private bool IsGroupExpanded(int groupIndex) => _expandedGroups.Contains(groupIndex);

        private void ToggleGroupExpanded(int groupIndex)
        {
            if (_expandedGroups.Contains(groupIndex)) _expandedGroups.Remove(groupIndex);
            else _expandedGroups.Add(groupIndex);
        }

        private int _renamingGroupIndex = -1;
        private string _renamingText = "";

        private void DrawHistorySection(Rect rect)
        {
            var history = FR2_SelectionHistory.inst;
            float y = rect.y;

            for (int i = 0; i < history.Count; i++)
            {
                var g = history.Get(i);
                if (g == null) continue;
                if (!FR2_SelectionHistory.IsSceneGroupValid(g.Value)) continue;

                int validCount = CountValidItems(g.Value, i);
                if (validCount == 0)
                {
                    history.Remove(i);
                    i--;
                    continue;
                }

                Rect headerRect = new Rect(rect.x, y, rect.width, 18f);
                if (headerRect.yMin > rect.yMax) break;

                DrawHistoryGroupRow(headerRect, g.Value, i, validCount);
                y += 18f;

                if (validCount > 1 && IsGroupExpanded(i))
                {
                    y = DrawHistoryGroupChildren(rect, y, g.Value, i);
                }
            }
        }

        private static string BuildTooltip(FR2_SelectionHistory.HistoryGroup group, int groupIndex = -1)
        {
            var sb = new System.Text.StringBuilder();
            int shown = 0;
            int total = 0;

            if (group.guids != null)
            {
                foreach (string guid in group.guids)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    if (string.IsNullOrEmpty(path)) continue;
                    total++;
                    if (shown < 5)
                    {
                        if (sb.Length > 0) sb.AppendLine();
                        sb.Append(System.IO.Path.GetFileName(path));
                        shown++;
                    }
                }
            }

            if (groupIndex >= 0)
            {
                var resolved = FR2_SelectionHistory.inst.GetResolvedObjects(groupIndex);
                for (int i = 0; i < resolved.Length; i++)
                {
                    if (!resolved[i]) continue;
                    total++;
                    if (shown < 5)
                    {
                        if (sb.Length > 0) sb.AppendLine();
                        sb.Append(resolved[i].name);
                        shown++;
                    }
                }
            }

            if (total > shown) sb.AppendLine().Append($"... +{total - shown} more");
            return sb.ToString();
        }

        private bool IsInCurrentSelection(string guid)
        {
            return guidSet.Contains(guid);
        }

        private void SelectSingleAsset(string path)
        {
            var obj = AssetDatabase.LoadAssetAtPath<UnityObject>(path);
            if (obj == null) return;
            Selection.activeObject = obj;
            if (window is FR2_WindowAll winAll) winAll.SetFR2Selection(new[] { obj });
        }

        private void DrawAssetItem(Rect r, string guid)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrEmpty(path)) return;

            FR2_Asset asset = FR2_Cache.GetAsset(guid);
            int usedByCount = asset?.UsedByMap.Count ?? 0;

            Rect countRect = new Rect(r.x, r.y, FR2_RefDrawer.USAGE_COUNT_COL_WIDTH, r.height);
            if (usedByCount > 0)
            {
                GUIContent countContent = FR2_GUIContent.FromInt(usedByCount);
                using (FR2_Scope.GUIColor(FR2_Theme.Current.SecondaryTextColor))
                {
                    GUI.Label(countRect, countContent, GUI2.miniLabelAlignRight);
                }
            }
            r.xMin += FR2_RefDrawer.USAGE_COUNT_COL_WIDTH + 2f;

            Texture icon = AssetDatabase.GetCachedIcon(path);
            string fileName = System.IO.Path.GetFileNameWithoutExtension(path);
            string ext = System.IO.Path.GetExtension(path);
            bool highlight = IsInCurrentSelection(guid);

            FR2_RowDrawer.DrawSimple(r, icon, fileName, ext, highlight,
                () =>
                {
                    var obj = AssetDatabase.LoadAssetAtPath<UnityObject>(path);
                    if (obj) EditorGUIUtility.PingObject(obj);
                },
                () => SelectSingleAsset(path)
            );
        }

        private void DrawSceneItem(Rect r, UnityObject obj)
        {
            if (!obj) return;

            r.xMin += FR2_RefDrawer.USAGE_COUNT_COL_WIDTH + 2f;

            bool highlight = instSet.Contains(FR2_Unity.GetInstanceId(obj).ToString());
            Texture icon = AssetPreview.GetMiniThumbnail(obj);

            FR2_RowDrawer.DrawSimple(r, icon, obj.name, null, highlight,
                () => EditorGUIUtility.PingObject(obj),
                () =>
                {
                    Selection.activeObject = obj;
                    if (window is FR2_WindowAll winAll) winAll.SetFR2Selection(new[] { obj });
                }
            );
        }

        private void DrawHistoryGroupRow(Rect r, FR2_SelectionHistory.HistoryGroup group, int index, int validCount)
        {
            Event evt = Event.current;

            if (validCount == 1)
            {
                DrawSingleItemGroup(r, group, index);
                return;
            }

            bool expanded = IsGroupExpanded(index);
            Rect foldoutRect = r;
            foldoutRect.width = 16f;
            bool newExpanded = EditorGUI.Foldout(foldoutRect, expanded, GUIContent.none);
            if (newExpanded != expanded) ToggleGroupExpanded(index);

            r.xMin += 16f;

            Rect removeRect = r;
            removeRect.xMin = r.xMax - 16f;
            removeRect.width = 16f;
            if (GUI.Button(removeRect, FR2_GUIContent.FromString("×", "Remove"), EditorStyles.miniLabel))
            {
                if (_renamingGroupIndex == index) _renamingGroupIndex = -1;
                FR2_SelectionHistory.inst.Remove(index);
                _historyDirty = true;
                window?.Repaint();
                return;
            }

            float badgeW = FR2_Badge.SelectBadgeWidth;
            Rect badgeArea = new Rect(r.x, r.y, removeRect.x - r.x, r.height);
            if (FR2_Badge.SelectBadge(badgeArea, validCount))
            {
                SelectHistoryGroup(group);
                Event.current.Use();
            }

            r.xMax = removeRect.x - badgeW - 4f;

            if (_renamingGroupIndex == index)
            {
                DrawRenameField(r, index);
                return;
            }

            string tooltip = BuildTooltip(group, index);
            bool isSceneGroup = group.globalIds != null && group.globalIds.Length > 0;
            Texture groupIcon = isSceneGroup ? FR2_Icon.Scene.image : AssetDatabase.GetCachedIcon("Assets");
            if (groupIcon != null)
            {
                Rect iconRect = r;
                iconRect.width = 16f;
                iconRect.height = 16f;
                GUI.DrawTexture(iconRect, groupIcon);
                r.xMin += 18f;
            }

            GUIContent label = new GUIContent(group.label, tooltip);
            GUI.Label(r, label, EditorStyles.label);

            if (evt.type == EventType.MouseDown && evt.button == 0 && evt.clickCount == 2 && r.Contains(evt.mousePosition))
            {
                _renamingGroupIndex = index;
                _renamingText = group.label;
                evt.Use();
            }
            else if (evt.type == EventType.MouseDown && evt.button == 1 && r.Contains(evt.mousePosition))
            {
                ShowHistoryContextMenu(index);
                evt.Use();
            }
        }

        private void DrawRenameField(Rect r, int index)
        {
            GUI.SetNextControlName("HistoryRename");
            _renamingText = EditorGUI.TextField(r, _renamingText, EditorStyles.textField);
            EditorGUI.FocusTextInControl("HistoryRename");

            Event evt = Event.current;
            if (evt.type == EventType.KeyDown && evt.keyCode == KeyCode.Return)
            {
                FR2_SelectionHistory.inst.RenameGroup(index, _renamingText);
                _renamingGroupIndex = -1;
                evt.Use();
            }
            else if (evt.type == EventType.KeyDown && evt.keyCode == KeyCode.Escape)
            {
                _renamingGroupIndex = -1;
                evt.Use();
            }
            else if (evt.type == EventType.MouseDown && !r.Contains(evt.mousePosition))
            {
                FR2_SelectionHistory.inst.RenameGroup(index, _renamingText);
                _renamingGroupIndex = -1;
            }
        }

        private void DrawSingleItemGroup(Rect r, FR2_SelectionHistory.HistoryGroup group, int index)
        {
            Rect removeRect = r;
            removeRect.xMin = r.xMax - 16f;
            removeRect.width = 16f;
            if (GUI.Button(removeRect, FR2_GUIContent.FromString("×", "Remove"), EditorStyles.miniLabel))
            {
                FR2_SelectionHistory.inst.Remove(index);
                _historyDirty = true;
                window?.Repaint();
                return;
            }

            r.xMax = removeRect.xMin - 2f;

            string singleGuid = group.guids != null && group.guids.Length > 0 ? group.guids[0] : null;

            if (singleGuid != null)
            {
                r.xMin += 16f;
                DrawAssetItem(r, singleGuid);
            }
            else if (group.globalIds != null && group.globalIds.Length > 0)
            {
                var resolved = FR2_SelectionHistory.inst.GetResolvedObjects(index);
                if (resolved.Length > 0 && resolved[0] != null)
                {
                    r.xMin += 16f;
                    DrawSceneItem(r, resolved[0]);
                }
            }
        }

        private float DrawHistoryGroupChildren(Rect rect, float y, FR2_SelectionHistory.HistoryGroup group, int groupIndex)
        {
            if (group.guids != null)
            {
                foreach (string guid in group.guids)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    if (string.IsNullOrEmpty(path)) continue;

                    Rect itemRect = new Rect(rect.x + 12f, y, rect.width - 12f, 18f);
                    if (itemRect.yMin > rect.yMax) break;

                    DrawAssetItem(itemRect, guid);
                    y += 18f;
                }
            }

            var resolved = FR2_SelectionHistory.inst.GetResolvedObjects(groupIndex);
            for (int i = 0; i < resolved.Length; i++)
            {
                if (!resolved[i]) continue;

                Rect itemRect = new Rect(rect.x + 12f, y, rect.width - 12f, 18f);
                if (itemRect.yMin > rect.yMax) break;

                DrawSceneItem(itemRect, resolved[i]);
                y += 18f;
            }

            return y;
        }

        private static int CountValidItems(FR2_SelectionHistory.HistoryGroup group, int groupIndex)
        {
            int count = 0;
            if (group.guids != null)
            {
                foreach (string guid in group.guids)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    if (!string.IsNullOrEmpty(path)) count++;
                }
            }
            count += FR2_SelectionHistory.inst.GetResolvedObjects(groupIndex).Length;
            return count;
        }

        private void RefreshHistoryView()
        {
            _historyDirty = false;
        }

        private static string GetHistoryGroupLabel(int groupIndex)
        {
            var history = FR2_SelectionHistory.inst;
            var group = history.Get(groupIndex);
            if (group == null) return $"Pin {groupIndex}";
            return group.Value.label;
        }

        private void ShowHistoryContextMenu(int groupIndex)
        {
            var history = FR2_SelectionHistory.inst;
            var group = history.Get(groupIndex);
            if (group == null) return;

            var menu = new GenericMenu();
            var g = group.Value;
            menu.AddItem(FR2_GUIContent.FromString("Select All"), false, () => SelectHistoryGroup(g));
            menu.AddItem(FR2_GUIContent.FromString("Remove"), false, () =>
            {
                history.Remove(groupIndex);
                _historyDirty = true;
                window?.Repaint();
            });
            menu.AddSeparator("");
            menu.AddItem(FR2_GUIContent.FromString("Clear All History"), false, () =>
            {
                history.Clear();
                _historyDirty = true;
                window?.Repaint();
            });
            menu.ShowAsContext();
        }

        private void SelectHistoryGroup(FR2_SelectionHistory.HistoryGroup group)
        {
            var objects = new List<UnityObject>();

            if (group.guids != null)
            {
                foreach (string guid in group.guids)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    if (string.IsNullOrEmpty(path)) continue;
                    var obj = AssetDatabase.LoadAssetAtPath<UnityObject>(path);
                    if (obj != null) objects.Add(obj);
                }
            }

            if (group.globalIds != null)
            {
                foreach (string globalIdStr in group.globalIds)
                {
                    var obj = FR2_SelectionHistory.ResolveGlobalId(globalIdStr);
                    if (obj != null) objects.Add(obj);
                }
            }

            if (objects.Count == 0) return;

            var arr = objects.ToArray();
            if (window is FR2_WindowAll winAll)
            {
                winAll.LockSelection();
                winAll.SetFR2Selection(arr);
            }
            Selection.objects = arr;
        }


        public void Add(UnityObject sceneObject)
        {
            if (sceneObject == null) return;
            var id = FR2_Unity.GetInstanceId(sceneObject).ToString();
            instSet.Add(id);
            dirty = true;
            unityObjectsCacheDirty = true;
        }

        public void Add(string guid)
        {
            if (guidSet.Contains(guid)) return;
            string assetPath = FR2_Cache.GUIDToAssetPath(guid);
            if (string.IsNullOrEmpty(assetPath))
            {
                FR2_LOG.LogWarning("Invalid GUID: " + guid);
                return;
            }

            guidSet.Add(guid);
            dirty = true;
            unityObjectsCacheDirty = true;
        }

        public void AddRange(params string[] guids)
        {
            foreach (string id in guids) Add(id);
            dirty = true;
            unityObjectsCacheDirty = true;
        }

        public void Remove(UnityObject sceneObject)
        {
            if (sceneObject == null) return;
            var id = FR2_Unity.GetInstanceId(sceneObject).ToString();
            instSet.Remove(id);
            dirty = true;
            unityObjectsCacheDirty = true;
        }

        public void Remove(string guidOrInstID)
        {
            guidSet.Remove(guidOrInstID);
            instSet.Remove(guidOrInstID);
            dirty = true;
            unityObjectsCacheDirty = true;
        }

        public void Clear()
        {
            guidSet.Clear();
            instSet.Clear();
            dirty = true;
            unityObjectsCacheDirty = true;
        }

        public void Add(FR2_Ref rf)
        {
            if (rf.isSceneRef) Add(rf.component);
            else Add(rf.asset.guid);
        }

        public void Remove(FR2_Ref rf)
        {
            if (rf.isSceneRef) Remove(rf.component);
            else Remove(rf.asset.guid);
        }

        public void SetDirty()
        {
            drawer.SetDirty();
        }

        public event Action OnSelectionChanged;

        private UnityObject[] cachedUnityObjects = null;
        private bool unityObjectsCacheDirty = true;

        public void SyncFromGlobalSelection()
        {
            var manager = FR2_SelectionManager.Instance;

            if (!manager.HasSelection)
            {
                if (Count > 0)
                {
                    Clear();
                    dirty = true;
                }
                return;
            }

            Clear();

            if (manager.IsSelectingSceneObjects)
            {
                foreach (var go in manager.SceneSelection.GameObjects)
                {
                    if (go != null) Add(go);
                }
            }
            else if (manager.IsSelectingAssets)
            {
                foreach (var entry in manager.AssetSelection.AssetEntries)
                {
                    Add(entry.guid);
                }
            }

            dirty = true;
        }

        public UnityObject[] GetUnityObjects()
        {
            if (!unityObjectsCacheDirty && cachedUnityObjects != null) return cachedUnityObjects;

            var result = new List<UnityObject>();

            foreach (string instIdStr in instSet)
            {
                if (!int.TryParse(instIdStr, out int instId)) continue;
                var obj = FR2_Unity.InstanceIdToObject(instId);
                if (obj != null) result.Add(obj);
            }

            foreach (string guid in guidSet)
            {
                string assetPath = FR2_Cache.GUIDToAssetPath(guid);
                if (string.IsNullOrEmpty(assetPath)) continue;
                var obj = AssetDatabase.LoadAssetAtPath<UnityObject>(assetPath);
                if (obj != null) result.Add(obj);
            }

            cachedUnityObjects = result.ToArray();
            unityObjectsCacheDirty = false;
            return cachedUnityObjects;
        }

        public void SetUnityObjects(UnityObject[] objects)
        {
            Clear();

            if (objects != null)
            {
                foreach (var obj in objects)
                {
                    if (obj == null) continue;

                    if (obj.IsSceneObject())
                    {
                        Add(obj);
                    }
                    else
                    {
                        string assetPath = AssetDatabase.GetAssetPath(obj);
                        if (string.IsNullOrEmpty(assetPath)) continue;
                        string guid = FR2_Cache.AssetPathToGUID(assetPath);
                        if (!string.IsNullOrEmpty(guid)) Add(guid);
                    }
                }
            }

            dirty = true;
            OnSelectionChanged?.Invoke();
        }

        public void RefreshView()
        {
            if (refs == null) refs = new Dictionary<string, FR2_Ref>();
            refs.Clear();

            if (instSet.Count > 0)
            {
                foreach (string instId in instSet)
                {
                    refs.Add(instId, new FR2_SceneRef(0, FR2_Unity.InstanceIdToObject(int.Parse(instId))));
                }
            }
            else
            {
                foreach (string guid in guidSet)
                {
                    FR2_Asset asset = FR2_Cache.GetAsset(guid);
                    if (asset == null)
                    {
                        string path = FR2_Cache.GUIDToAssetPath(guid);
                        if (string.IsNullOrEmpty(path)) continue;
                        asset = new FR2_Asset(guid);
                        asset.LoadPathInfo();
                    }
                    refs.Add(guid, new FR2_Ref(0, 0, asset, null) { isSceneRef = false });
                }
            }

            drawer.SetRefs(refs);
            dirty = false;
        }
    }
}
