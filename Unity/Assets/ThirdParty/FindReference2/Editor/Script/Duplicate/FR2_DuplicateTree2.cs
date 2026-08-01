using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;
namespace vietlabs.fr2
{
    internal class FR2_DuplicateTree2 : IRefDraw
    {
        private const float TimeDelayDelete = .5f;

        private static readonly FR2_FileCompare fc = new FR2_FileCompare();
        private readonly Func<string> getGroupMode;

        private readonly Func<FR2_RefDrawer.Sort> getSortMode;
        private readonly FR2_TreeUI2.GroupDrawer groupDrawer;
        private readonly string searchTerm = "";
        private List<List<string>> cacheAssetList;
        public bool caseSensitive = false;
        private Dictionary<string, List<FR2_Ref>> dicIndex; //index, list

        private bool dirty;
        private int excludeCount;
        private string guidPressDelete;

        internal List<FR2_Ref> list;
        internal Dictionary<string, FR2_Ref> refs;
        private readonly RowDrawData _dupRowData = new RowDrawData();
        private readonly MetadataColumn[] _dupLeftColumns = new MetadataColumn[]
        {
            new MetadataColumn("usedByCount", FR2_RefDrawer.USAGE_COUNT_COL_WIDTH, ColumnAlign.Right),
        };
        private readonly MetadataColumn[] _dupColumns = new MetadataColumn[]
        {
            new MetadataColumn("fileSize", 0f),
            new MetadataColumn("addressable", 0f),
            new MetadataColumn("atlas", 0f),
            new MetadataColumn("assetBundle", 0f),
        };
        public int scanExcludeByIgnoreCount;
        public int scanExcludeByTypeCount;
        private float TimePressDelete;
        
        // New fields for verification UI
        private Dictionary<string, string> groupVerificationStatus = new Dictionary<string, string>();
        private Dictionary<string, float> groupVerificationProgress = new Dictionary<string, float>();
        private Dictionary<string, int> groupVerificationOrder = new Dictionary<string, int>();
        private bool isSignatureScanComplete = false;

        private Dictionary<string, string> preferredGuids = new Dictionary<string, string>();
        private HashSet<string> dismissedGroups = new HashSet<string>();

        // Add enum for progress state
        private enum ProgressState
        {
            Idle,
            Scanning,
            Verifying,
            Complete
        }
        private ProgressState progressState = ProgressState.Idle;

        private bool _waitingForRescan;

        public FR2_DuplicateTree2(IWindow window, Func<FR2_RefDrawer.Sort> getSortMode, Func<string> getGroupMode)
        {
            this.window = window;
            this.getSortMode = getSortMode;
            this.getGroupMode = getGroupMode;
            groupDrawer = new FR2_TreeUI2.GroupDrawer(DrawGroup, DrawAsset);
            FR2_Cache.onReady += OnCacheReady;
        }

        public IWindow window { get; set; }

        public bool Draw(Rect rect)
        {
            return false;
        }

        public bool DrawLayout()
        {
            if (dirty) RefreshView(cacheAssetList);

            // Show progress bar on top based on progressState
            if (progressState == ProgressState.Scanning || progressState == ProgressState.Verifying)
            {
                float p = fc.nScaned / (float)Mathf.Max(1, fc.nChunks2);
                string label = progressState == ProgressState.Scanning ? "Scanning" : "Verifying";
                Rect progressRect = GUILayoutUtility.GetRect(1, Screen.width, 18f, 18f);
                EditorGUI.ProgressBar(progressRect, p, string.Format($"{label} {{0}} / {{1}}", fc.nScaned, fc.nChunks2));
                GUILayout.Space(2);
            }

            if (_mergeTimeSlice != null && _mergeReplaceList != null)
            {
                int total = _mergeReplaceList.Count;
                int current = _mergeTimeSlice.currentIndex;
                float p = current / (float)Mathf.Max(1, total);
                Rect mergeRect = GUILayoutUtility.GetRect(1, Screen.width, 18f, 18f);
                EditorGUI.ProgressBar(mergeRect, p, $"Merging {current} / {total}");
                GUILayout.Space(2);
            }

            // Update progress state based on fc
            if (fc.nChunks2 > 0 && fc.nScaned < fc.nChunks2)
            {
                if (progressState != ProgressState.Scanning && progressState != ProgressState.Verifying)
                    progressState = ProgressState.Scanning;
            }
            else if (fc.nChunks2 > 0 && fc.nScaned >= fc.nChunks2)
            {
                if (progressState != ProgressState.Complete)
                    progressState = ProgressState.Complete;
            }
            else
            {
                progressState = ProgressState.Idle;
            }

            if (progressState == ProgressState.Complete || progressState == ProgressState.Idle)
            {
                if (groupDrawer.hasValidTree) groupDrawer.tree.itemPaddingRight = 4f;
                groupDrawer.DrawLayout();
            }

            DrawHeader();
            return false;
        }

        public int ElementCount()
        {
            return list?.Count ?? 0;
        }

        private void DrawAsset(Rect r, string guid)
        {
            if (!refs.TryGetValue(guid, out FR2_Ref rf)) return;

            string groupLabel = rf.group;
            bool isPreferred = preferredGuids.TryGetValue(groupLabel, out string prefGuid) && prefGuid == guid;

            Rect radioRect = new Rect(r.x, r.y + (r.height - 14f) * 0.5f, 14f, 14f);
            bool newPreferred = GUI.Toggle(radioRect, isPreferred, GUIContent.none, EditorStyles.radioButton);
            if (newPreferred && !isPreferred)
            {
                preferredGuids[groupLabel] = guid;
                window.WillRepaint = true;
                Event.current.Use();
            }

            r.xMin += 20f;

            bool showMerge = isPreferred && !FR2_Export.IsMergeProcessing && HasOthersWithUsage(groupLabel, guid);
            
            const float mergeW = 23f;
            const float mergeMargin = 2f;
            
            if (showMerge)
            {
                var mergeRect = new Rect(r.xMax - mergeW - mergeMargin, r.y, mergeW, r.height);
                if (GUI.Button(mergeRect, new GUIContent("M", "Merge all duplicate references to this asset"), EditorStyles.miniButton))
                {
                    if (dicIndex.TryGetValue(groupLabel, out List<FR2_Ref> groupRefs))
                    {
                        Selection.objects = groupRefs
                            .Select(x => FR2_Unity.LoadAssetAtPath<Object>(x.asset.assetPath))
                            .Where(o => o != null).ToArray();
                        FR2_Export.MergeDuplicate(rf.asset.guid);
                    }
                }
            }
            
            r.width -= mergeW + mergeMargin;

            var assetRect = r;
            Color? nameClr = isPreferred ? new Color(0.4f, 0.7f, 1f, 1f) : (Color?)null;
            bool showDupPath = getGroupMode() != FR2_RefDrawer.GroupMode.Folder;
            rf.asset.PopulateRowData(_dupRowData, showDupPath, window, null, rf, nameClr);
            if (isPreferred) _dupRowData.state = RowState.Active;

            _dupRowData.ClearColumns();
            rf.asset.SetColumnUsedByCount(_dupRowData, 0, _dupLeftColumns[0]);
            if (FR2_Setting.ShowFileSize) rf.asset.SetColumnFileSize(_dupRowData, 0, _dupColumns[0]);
            rf.asset.SetColumnAddressable(_dupRowData, 1, _dupColumns[1]);
            if (FR2_Setting.s.displayAtlasName) rf.asset.SetColumnAtlas(_dupRowData, 2, _dupColumns[2]);
            if (FR2_Setting.s.displayAssetBundleName) rf.asset.SetColumnAssetBundle(_dupRowData, 3, _dupColumns[3]);

            FR2_RowDrawer.Draw(assetRect, _dupRowData, _dupLeftColumns, _dupColumns);
        }

        private bool HasOthersWithUsage(string groupLabel, string preferredGuid)
        {
            if (!dicIndex.TryGetValue(groupLabel, out List<FR2_Ref> groupRefs)) return false;
            foreach (FR2_Ref other in groupRefs)
            {
                if (other.asset.guid == preferredGuid) continue;
                if (other.asset.UsageCount() > 0) return true;
            }
            return false;
        }

        private bool wasPreDelete(string guid)
        {
            if (guidPressDelete == null || guid != guidPressDelete) return false;

            if (Time.realtimeSinceStartup - TimePressDelete < TimeDelayDelete) return true;

            guidPressDelete = null;
            return false;
        }

        private void DrawGroup(Rect r, string label, int childCount)
        {
            if (!dicIndex.TryGetValue(label, out List<FR2_Ref> groupRefs)) return;
            FR2_Asset asset = groupRefs[0].asset;

            Texture tex = AssetDatabase.GetCachedIcon(asset.assetPath);
            Rect rect = r;

            if (tex != null)
            {
                rect.width = 16f;
                GUI.DrawTexture(rect, tex);
            }

            rect = r;
            rect.xMin += 16f;
            float nameW = EditorStyles.boldLabel.CalcSize(new GUIContent(asset.assetName)).x + 4f;
            rect.width = nameW;
            GUI.Label(rect, asset.assetName, EditorStyles.boldLabel);

            float infoX = rect.xMax + 2f;
            var countRect = new Rect(infoX, r.y, 30f, r.height);
            GUI.Label(countRect, "(" + childCount + ")", EditorStyles.miniLabel);

            var sizeRect = new Rect(countRect.xMax + 2f, r.y, 60f, r.height);
            Color prev = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, 0.4f);
            GUI.Label(sizeRect, FR2_Helper.GetfileSizeString(asset.fileSize), EditorStyles.miniLabel);
            GUI.color = prev;

            var removeRect = new Rect(r.xMax - 16f, r.y + 2f, 14f, r.height - 4f);
            if (GUI.Button(removeRect, "×", EditorStyles.miniLabel))
            {
                dismissedGroups.Add(label);
                RemoveGroupFromView(label);
            }

            if (!FR2_Export.IsMergeProcessing && preferredGuids.TryGetValue(label, out string pGuid) && !HasOthersWithUsage(label, pGuid))
            {
                float cleanW = EditorStyles.miniButton.CalcSize(new GUIContent("Clean Up")).x;
                var cleanRect = new Rect(removeRect.x - cleanW - 2f, r.y + 3f, cleanW, r.height - 6f);
                Color prevC = GUI.contentColor;
                GUI.contentColor = new Color(0.2f, .8f, 0.3f, 1f);
                if (GUI.Button(cleanRect, new GUIContent("Clean Up", "Will remove all duplicated (unused) copies"), EditorStyles.miniButton))
                {
                    var paths = new List<string>();
                    foreach (FR2_Ref other in groupRefs)
                    {
                        if (other.asset.guid == pGuid) continue;
                        paths.Add(other.asset.assetPath);
                    }

                    FR2_LOG.Log($"[Duplicate] Clean Up group '{label}': deleting {paths.Count} assets, keeping {pGuid}");

                    FR2_Cache.suppressRefresh = true;
                    try
                    {
                        AssetDatabase.StartAssetEditing();
                        foreach (string path in paths)
                            AssetDatabase.DeleteAsset(path);
                        AssetDatabase.StopAssetEditing();
                    }
                    finally
                    {
                        FR2_Cache.suppressRefresh = false;
                    }

                    FR2_Cache.MarkAssetPathDeleted(paths);
                    RemoveGroupFromView(label);
                }
                GUI.contentColor = prevC;
            }
        }

        public void Reset(List<List<string>> assetList)
        {
            progressState = ProgressState.Scanning;
            groupVerificationStatus.Clear();
            groupVerificationProgress.Clear();
            groupVerificationOrder.Clear();
            preferredGuids.Clear();
            dismissedGroups.Clear();
            
            fc.Reset(assetList, OnUpdateView, RefreshView);
        }

        private void OnUpdateView(List<List<string>> assetList)
        {
            if (assetList != null)
            {
                cacheAssetList = assetList;
                dirty = true;
                window.WillRepaint = true;
            }
        }

        public bool isExclueAnyItem()
        {
            return excludeCount > 0 || scanExcludeByTypeCount > 0;
        }

        public bool isExclueAnyItemByIgnoreFolder()
        {
            return scanExcludeByIgnoreCount > 0;
        }

        private void RefreshView(List<List<string>> assetList)
        {
            cacheAssetList = assetList;
            dirty = false;
            list = new List<FR2_Ref>();
            refs = new Dictionary<string, FR2_Ref>();
            dicIndex = new Dictionary<string, List<FR2_Ref>>();
            preferredGuids.Clear();
            if (assetList == null) return;

            int minScore = searchTerm.Length;
            string term1 = searchTerm;
            if (!caseSensitive) term1 = term1.ToLower();

            string term2 = term1.Replace(" ", string.Empty);
            excludeCount = 0;

            for (var i = 0; i < assetList.Count; i++)
            {
                var lst = new List<FR2_Ref>();
                for (var j = 0; j < assetList[i].Count; j++)
                {
                    string path = assetList[i][j];
                    if (!path.StartsWith("Assets/"))
                    {
                        FR2_LOG.LogWarning("Ignore asset: " + path);
                        continue;
                    }

                    string guid = FR2_Cache.AssetPathToGUID(path);
                    if (string.IsNullOrEmpty(guid)) continue;

                    if (refs.ContainsKey(guid)) continue;

                    FR2_Asset asset = FR2_Cache.GetAsset(guid);
                    if (asset == null) continue;
                    if (asset.IsMissing) continue;
                    if (!asset.assetPath.StartsWith("Assets/")) continue;

                    if (!FR2_SettingExt.showPackagesAndBuiltIn && (asset.inPackages || asset.isBuiltIn)) continue;

                    var fr2 = new FR2_Ref(i, 0, asset, null);

                    if (FR2_Setting.IsTypeExcluded(fr2.type))
                    {
                        excludeCount++;
                        continue; //skip this one
                    }

                    if (string.IsNullOrEmpty(searchTerm))
                    {
                        fr2.matchingScore = 0;
                        list.Add(fr2);
                        lst.Add(fr2);
                        refs.Add(guid, fr2);
                        continue;
                    }

                    //calculate matching score
                    string name1 = fr2.asset.assetName;
                    if (!caseSensitive) name1 = name1.ToLower();

                    string name2 = name1.Replace(" ", string.Empty);

                    int score1 = FR2_Helper.StringMatch(term1, name1);
                    int score2 = FR2_Helper.StringMatch(term2, name2);

                    fr2.matchingScore = Mathf.Max(score1, score2);
                    if (fr2.matchingScore > minScore)
                    {
                        list.Add(fr2);
                        lst.Add(fr2);
                        refs.Add(guid, fr2);
                    }
                }

                if (lst.Count > 1)
                {
                    string groupLabel = i.ToString();
                    foreach (var rf in lst) rf.group = groupLabel;
                    if (dismissedGroups.Contains(groupLabel)) continue;

                    dicIndex.Add(groupLabel, lst);
                    if (isSignatureScanComplete)
                    {
                        groupVerificationStatus[groupLabel] = "Pending";
                    }

                    if (!preferredGuids.ContainsKey(groupLabel))
                    {
                        preferredGuids[groupLabel] = AutoSelectPreferred(lst);
                    }
                }
            }

            ResetGroup();
        }

        private void ResetGroup()
        {
            groupDrawer.Reset(list,
                rf => rf.asset.guid
                , GetGroup, SortGroup);
            if (window != null) window.Repaint();
        }

        private string GetGroup(FR2_Ref rf)
        {
            return rf.group;
        }

        private void SortGroup(List<string> groups)
        {
            // Sort by verification status, then by size
            if (isSignatureScanComplete)
            {
                groups.Sort((a, b) => {
                    // First check if either is currently verifying
                    if (groupVerificationStatus.ContainsKey(a) && groupVerificationStatus[a] == "Verifying")
                        return -1;
                    if (groupVerificationStatus.ContainsKey(b) && groupVerificationStatus[b] == "Verifying")
                        return 1;
                    
                    // Then check if verified
                    bool aVerified = groupVerificationStatus.ContainsKey(a) && groupVerificationStatus[a] == "Verified";
                    bool bVerified = groupVerificationStatus.ContainsKey(b) && groupVerificationStatus[b] == "Verified";
                    if (aVerified && !bVerified) return -1;
                    if (!aVerified && bVerified) return 1;
                    
                    // Then check queue position
                    if (groupVerificationOrder.ContainsKey(a) && groupVerificationOrder.ContainsKey(b))
                        return groupVerificationOrder[a].CompareTo(groupVerificationOrder[b]);
                    
                    // Default to standard order
                    return a.CompareTo(b);
                });
            }
        }

        public void SetDirty()
        {
            dirty = true;
        }

        public void RefreshSort()
        {
            if (groupDrawer.hasValidTree)
            {
                SortGroup(groupDrawer.tree.rootItem.children.Select(item => item.id).ToList());
                groupDrawer.Reset(list,
                    rf => rf.asset.guid,
                    GetGroup, SortGroup);
                if (window != null) window.Repaint();
            }
        }

        private void DrawHeader()
        {
            string text = groupDrawer.hasValidTree ? "Rescan" : "Scan";
            bool hasGroups = dicIndex != null && dicIndex.Count > 0;

            int mergeCount = 0;
            int removeCount = 0;
            if (hasGroups)
            {
                foreach (var kvp in dicIndex)
                {
                    if (kvp.Value.Count < 2) continue;
                    if (!preferredGuids.ContainsKey(kvp.Key)) continue;
                    string prefGuid = preferredGuids[kvp.Key];

                    if (HasOthersWithUsage(kvp.Key, prefGuid))
                    {
                        mergeCount++;
                    }
                    else
                    {
                        foreach (FR2_Ref rf in kvp.Value)
                        {
                            if (rf.asset.guid == prefGuid) continue;
                            if (rf.asset.UsageCount() == 0) removeCount++;
                        }
                    }
                }
            }

            using (FR2_Scope.HzLayout())
            {
                if (GUILayout.Button(text))
                {
                    _waitingForRescan = true;
                    if (FR2_Cache.isReady)
                    {
                        FR2_LOG.Log("[Duplicate] Rescan clicked, cache ready, calling OnCacheReady directly");
                        OnCacheReady();
                    }
                    else
                    {
                        FR2_LOG.Log("[Duplicate] Rescan clicked, cache not ready, waiting");
                        FR2_Cache.IncrementalRefresh();
                    }
                }

                bool isBusy = FR2_Export.IsMergeProcessing || _mergeTimeSlice != null || !FR2_Cache.isReady;
                
                GUI.enabled = mergeCount > 0 && !isBusy;
                if (GUILayout.Button(new GUIContent(
                    $"Merge Usage ({mergeCount})",
                    "Replace all duplicate references to point to the preferred asset in each group")))
                {
                    MergeAllGroups();
                }

                GUI.enabled = removeCount > 0 && !isBusy;
                if (GUILayout.Button(new GUIContent(
                    $"Remove Duplicated ({removeCount})",
                    "Delete non-preferred duplicate assets that have zero references (safe to remove)")))
                {
                    RemoveUnusedDuplicates();
                }

                GUI.enabled = true;
            }
        }

        private void OnCacheReady()
        {
            FR2_LOG.Log($"[Duplicate] OnCacheReady: _waitingForRescan={_waitingForRescan}");
            if (!_waitingForRescan) return;
            _waitingForRescan = false;
            
            scanExcludeByTypeCount = 0;
            var result = FR2_Cache._inst.ScanSimilar(IgnoreTypeWhenScan, IgnoreFolderWhenScan);
            FR2_LOG.Log($"[Duplicate] ScanSimilar: {result?.Count ?? 0} groups");
            Reset(result);
        }

        private void IgnoreTypeWhenScan()
        {
            scanExcludeByTypeCount++;
        }

        private void IgnoreFolderWhenScan()
        {
            scanExcludeByIgnoreCount++;
        }

        private string AutoSelectPreferred(List<FR2_Ref> groupRefs)
        {
            if (groupRefs == null || groupRefs.Count == 0) return null;

            string bestGuid = groupRefs[0].asset.guid;
            int bestUsage = groupRefs[0].asset.UsageCount();
            int bestPathLen = groupRefs[0].asset.assetPath.Length;
            long bestModTime = GetFileWriteTime(groupRefs[0].asset.assetPath);

            for (int i = 1; i < groupRefs.Count; i++)
            {
                FR2_Asset a = groupRefs[i].asset;
                int usage = a.UsageCount();
                int pathLen = a.assetPath.Length;
                long modTime = GetFileWriteTime(a.assetPath);

                bool isBetter = false;
                if (usage > bestUsage) isBetter = true;
                else if (usage == bestUsage && pathLen < bestPathLen) isBetter = true;
                else if (usage == bestUsage && pathLen == bestPathLen && modTime < bestModTime) isBetter = true;

                if (!isBetter) continue;
                bestGuid = a.guid;
                bestUsage = usage;
                bestPathLen = pathLen;
                bestModTime = modTime;
            }

            return bestGuid;
        }

        private static long GetFileWriteTime(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath) || !File.Exists(assetPath)) return long.MaxValue;
            return File.GetLastWriteTime(assetPath).Ticks;
        }

        private void RemoveGroupFromView(string groupLabel)
        {
            if (!dicIndex.TryGetValue(groupLabel, out List<FR2_Ref> groupRefs))
            {
                FR2_LOG.LogWarning($"[Duplicate] RemoveGroupFromView: group '{groupLabel}' not found in dicIndex");
                return;
            }

            FR2_LOG.Log($"[Duplicate] RemoveGroupFromView: removing group '{groupLabel}' with {groupRefs.Count} refs");

            foreach (FR2_Ref rf in groupRefs)
            {
                list.Remove(rf);
                refs.Remove(rf.asset.guid);
            }

            dicIndex.Remove(groupLabel);
            preferredGuids.Remove(groupLabel);
            groupDrawer.RemoveGroup(groupLabel);

            if (window != null) window.Repaint();
        }

        private FR2_TimeSlice _mergeTimeSlice;
        private List<(string from, string to, FR2_Asset consumer)> _mergeReplaceList;

        private void MergeAllGroups()
        {
            if (FR2_Export.IsMergeProcessing) return;
            if (_mergeTimeSlice != null) return;
            if (dicIndex == null || dicIndex.Count == 0) return;

            _mergeReplaceList = new List<(string, string, FR2_Asset)>();

            int skippedNoUsage = 0;
            int skippedBinary = 0;
            int totalGroups = 0;
            int mergedGroups = 0;

            foreach (var kvp in dicIndex)
            {
                if (kvp.Value.Count < 2) continue;
                if (!preferredGuids.TryGetValue(kvp.Key, out string prefGuid)) continue;
                if (!HasOthersWithUsage(kvp.Key, prefGuid)) continue;
                totalGroups++;

                bool addedAny = false;
                foreach (FR2_Ref rf in kvp.Value)
                {
                    if (rf.asset.guid == prefGuid) continue;
                    if (rf.asset.UsageCount() == 0) { skippedNoUsage++; continue; }

                    foreach (var usedByKvp in rf.asset.UsedByMap)
                    {
                        FR2_Asset consumer = usedByKvp.Value;
                        if (consumer == null || consumer.IsMissing) continue;
                        if (consumer.IsBinaryAsset) { skippedBinary++; continue; }
                        _mergeReplaceList.Add((rf.asset.guid, prefGuid, consumer));
                        addedAny = true;
                    }
                }
                if (addedAny) mergedGroups++;
            }

            FR2_LOG.Log($"[Duplicate] MergeAll: {totalGroups} groups, {mergedGroups} with replacements, {_mergeReplaceList.Count} total replacements, skippedNoUsage={skippedNoUsage}, skippedBinary={skippedBinary}");

            if (_mergeReplaceList.Count == 0) return;

            AssetDatabase.StartAssetEditing();

            _mergeTimeSlice = new FR2_TimeSlice(
                () => _mergeReplaceList.Count,
                idx =>
                {
                    var (from, to, consumer) = _mergeReplaceList[idx];
                    bool ok = consumer.ReplaceReference(from, to, 0);
                    if (!ok) FR2_LOG.LogWarning($"[Duplicate] ReplaceReference FAILED: {consumer.assetPath} ({consumer.type})");
                },
                () =>
                {
                    AssetDatabase.StopAssetEditing();
                    
                    var modifiedPaths = new HashSet<string>();
                    foreach (var (from, to, consumer) in _mergeReplaceList)
                        modifiedPaths.Add(consumer.assetPath);
                    
                    _mergeTimeSlice = null;
                    _mergeReplaceList = null;
                    
                    AssetDatabase.StartAssetEditing();
                    foreach (string path in modifiedPaths)
                        AssetDatabase.ImportAsset(path, ImportAssetOptions.Default);
                    AssetDatabase.StopAssetEditing();
                    
                    FR2_LOG.Log($"[Duplicate] MergeAll complete, reimported {modifiedPaths.Count} assets");
                    _waitingForRescan = true;
                },
                (current, total) =>
                {
                    window.Repaint();
                }
            );
            _mergeTimeSlice.jobName = "MergeDuplicates";
            _mergeTimeSlice.Start();
        }

        private void RemoveUnusedDuplicates()
        {
            if (dicIndex == null || dicIndex.Count == 0) return;

            var allPaths = new List<string>();
            var groupsToRemove = new List<string>();

            foreach (var kvp in dicIndex)
            {
                if (kvp.Value.Count < 2) continue;
                if (!preferredGuids.TryGetValue(kvp.Key, out string prefGuid)) continue;
                if (HasOthersWithUsage(kvp.Key, prefGuid)) continue;

                foreach (FR2_Ref rf in kvp.Value)
                {
                    if (rf.asset.guid == prefGuid) continue;
                    if (rf.asset.UsageCount() > 0) continue;
                    allPaths.Add(rf.asset.assetPath);
                }

                groupsToRemove.Add(kvp.Key);
            }

            if (allPaths.Count == 0) return;

            foreach (string label in groupsToRemove)
            {
                dismissedGroups.Add(label);
                RemoveGroupFromView(label);
            }

            FR2_Cache.suppressRefresh = true;
            try
            {
                AssetDatabase.StartAssetEditing();
                foreach (string path in allPaths)
                    AssetDatabase.DeleteAsset(path);
                AssetDatabase.StopAssetEditing();
            }
            finally
            {
                FR2_Cache.suppressRefresh = false;
            }

            FR2_Cache.MarkAssetPathDeleted(allPaths);
        }
    }
}
