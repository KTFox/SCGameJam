using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace vietlabs.fr2
{
    internal partial class FR2_RefDrawer
    {
        public void SetRefs(Dictionary<string, FR2_Ref> dictRefs)
        {
            ValidateRefs(dictRefs);
            refs = dictRefs;
            SetupRefCallbacks();
            hasValidSelection = true;
            dirty = true;
        }
        
        private void SetupRefCallbacks() 
        {
            if (refs == null) return;
            foreach (var kvp in refs)
            {
                var rf = kvp.Value;
                rf.OnCtrlClick = HandleRefCtrlClick;
                rf.OnAltClick = HandleRefAltClick;
                rf.OnShiftClick = HandleRefShiftClick;
            }
        }

        void ValidateRefs(Dictionary<string, FR2_Ref> dictRefs)
        {
            if (dictRefs == null) return;
            bool hasScene = false, hasAsset = false;
            foreach (var kvp in dictRefs)
            {
                if (kvp.Value.isSceneRef) hasScene = true;
                else hasAsset = true;
                if (hasScene && hasAsset) { FR2_LOG.LogWarning("Mixed content???"); return; }
            }
        }

        private FR2_TimeSlice _resetUsageTimeSlice;

        public FR2_RefDrawer Reset(string[] assetGUIDs, bool isUsage)
        {
            gBookmarkCache.Clear();
            hasValidSelection = assetGUIDs != null && assetGUIDs.Length > 0;
            _resetUsageTimeSlice?.Stop();
            _resetUsageTimeSlice = null;

            if (!hasValidSelection)
            {
                refs = isUsage ? FR2_Ref.FindUsage(assetGUIDs) : FR2_Ref.FindUsedBy(assetGUIDs);
                SetupRefCallbacks();
                dirty = true;
                if (list != null) list.Clear();
                return this;
            }

            _resetUsageTimeSlice = FR2_Ref.FindRefsAsync(assetGUIDs, isUsage, true, 0, result =>
            {
                _resetUsageTimeSlice = null;
                refs = result;
                SetupRefCallbacks();
                dirty = true;
                if (list != null) list.Clear();
                window?.Repaint();
            });
            return this;
        }

        public void Reset(Dictionary<string, FR2_Ref> newRefs)
        {
            if (refs == null) refs = new Dictionary<string, FR2_Ref>();
            refs.Clear();
            ValidateRefs(newRefs);
            
            foreach (KeyValuePair<string, FR2_Ref> kvp in newRefs)
                refs.Add(kvp.Key, kvp.Value);

            SetupRefCallbacks();
            hasValidSelection = true;
            dirty = true;
            if (list != null) list.Clear();
        }

        public FR2_RefDrawer Reset(GameObject[] objs, bool findDept, bool findPrefabInAsset)
        {
            hasValidSelection = objs != null && objs.Length > 0;
            var sceneRefs = FR2_Ref.FindUsageSceneWithDetails(objs, findDept);
            refs = new Dictionary<string, FR2_Ref>(sceneRefs.Count);
            foreach (KeyValuePair<string, FR2_Ref> kvp in sceneRefs)
            {
                if (kvp.Value.isSceneRef) continue;
                refs.Add(kvp.Key, kvp.Value);
            }

            if (findPrefabInAsset)
            {
                for (var i = 0; i < objs.Length; i++)
                {
                    if (!PrefabUtility.IsAnyPrefabInstanceRoot(objs[i])) continue;
                    string guid = FR2_Unity.GetPrefabParent(objs[i]);
                    if (string.IsNullOrEmpty(guid)) continue;
                    if (refs.ContainsKey(guid)) continue;
                    FR2_Asset asset = FR2_Cache.GetAsset(guid);
                    if (asset == null) continue;
                    refs.Add(guid, new FR2_Ref(refs.Count, 1, asset, null));
                }
            }

            var refKeys = new string[refs.Count];
            refs.Keys.CopyTo(refKeys, 0);
            Dictionary<string, FR2_Ref> usageRefs = FR2_Ref.FindUsage(refKeys, 1);
            foreach (KeyValuePair<string, FR2_Ref> kvp in usageRefs)
            {
                if (refs.ContainsKey(kvp.Key)) continue;
                refs.Add(kvp.Key, kvp.Value);
            }

            SetupRefCallbacks();
            dirty = true;
            if (list != null) list.Clear();
            return this;
        }

        public FR2_RefDrawer Reset(string[] assetGUIDs)
        {
            hasValidSelection = assetGUIDs != null && assetGUIDs.Length > 0;
            refs = FR2_SceneRef.FindRefInScene(assetGUIDs, true, SetRefInScene);
            SetupRefCallbacks();
            dirty = true;
            if (list != null) list.Clear();
            return this;
        }

        private void SetRefInScene(Dictionary<string, FR2_Ref> data)
        {
            refs = data;
            SetupRefCallbacks();
            dirty = true;
            if (list != null) list.Clear();
        }

        public FR2_RefDrawer ResetSceneInScene(GameObject[] objs)
        {
            hasValidSelection = objs != null && objs.Length > 0;
            FR2_SceneCache.FindSceneInSceneAsync(objs, (results) => {
                refs = results;
                SetupRefCallbacks();
                dirty = true;
                if (list != null) list.Clear();
            });
            return this;
        }

        public FR2_RefDrawer ResetSceneUseSceneObjects(GameObject[] objs)
        {
            hasValidSelection = objs != null && objs.Length > 0;
            FR2_SceneCache.FindSceneUseSceneObjectsAsync(objs, (results) => {
                refs = results;
                SetupRefCallbacks();
                dirty = true;
                if (list != null) list.Clear();
            });
            return this;
        }

        public FR2_RefDrawer ResetUnusedAsset(bool recursive = true)
        {
            List<FR2_Asset> lst = FR2_Cache._inst.ScanUnused(recursive);
            refs = lst.ToDictionary(x => x.guid, x => new FR2_Ref(0, 1, x, null));
            SetupRefCallbacks();
            hasValidSelection = true;
            dirty = true;
            if (list != null) list.Clear();
            return this;
        }

        public void RefreshSort()
        {
            if (list == null) return;
            list.RemoveAll(item => item == null ||
                 (item.isSceneRef 
                     ? (item.component == null)
                     : (item.asset == null)
                 ));
            
            if (list.Count == 0) return;
            
            var sortMode = getSortMode();
            list.Sort((r1, r2) =>
            {
                bool isMixed = r1.isSceneRef != r2.isSceneRef;
                if (isMixed)
                {
                    var v1 = r1.isSceneRef ? 1 : 0;
                    var v2 = r2.isSceneRef ? 1 : 0;
                    return v2.CompareTo(v1);
                }

                if (r1.isSceneRef && r2.isSceneRef)
                    return SortSceneRefs((FR2_SceneRef)r1, (FR2_SceneRef)r2, sortMode);

                if (r1.asset == null) return -1;
                if (r2.asset == null) return 1;
                return SortAssetRefs(r1, r2, sortMode);
            });
            
            groupDrawer.hideGroupIfPossible = getGroupMode() == GroupMode.None;
            groupDrawer.Reset(list,
                rf =>
                {
                    if (rf == null) return null;
                    return rf.isSceneRef ? rf.GetSceneObjId() : rf.asset?.guid;
                }, GetGroup, SortGroup);
        }

        private void ApplyFilter()
        {
            dirty = false;
            ResetColumnWidths();

            if (refs == null) return;

            if (list == null) list = new List<FR2_Ref>();
            else list.Clear();

            if (skipFilter)
            {
                foreach (KeyValuePair<string, FR2_Ref> item in refs)
                    list.Add(item.Value);
                RefreshSort();
                return;
            }

            int minScore = searchTerm.Length;
            string term1 = searchTerm;
            if (!caseSensitive) term1 = term1.ToLower();
            string term2 = term1.Replace(" ", string.Empty);

            excludeCount = 0;

            foreach (KeyValuePair<string, FR2_Ref> item in refs)
            {
                FR2_Ref r = item.Value;

                if (FR2_Setting.IsTypeExcluded(r.type))
                {
                    excludeCount++;
                    continue;
                }

                if (!r.isSceneRef && r.asset != null && !FR2_SettingExt.showPackagesAndBuiltIn && (r.asset.inPackages || r.asset.isBuiltIn))
                {
                    excludeCount++;
                    continue;
                }

                if (!r.isSceneRef && r.asset != null && FR2_Setting.IsAssetPathIgnored(r.asset.assetPath))
                {
                    excludeCount++;
                    continue;
                }

                if (!showSearch || string.IsNullOrEmpty(searchTerm))
                {
                    r.matchingScore = 0;
                    list.Add(r);
                    continue;
                }

                string name1 = r.isSceneRef ? (r as FR2_SceneRef)?.sceneFullPath : r.asset.assetName;
                if (!caseSensitive) name1 = name1?.ToLower();
                string name2 = name1?.Replace(" ", string.Empty);

                int score1 = FR2_Helper.StringMatch(term1, name1);
                int score2 = FR2_Helper.StringMatch(term2, name2);

                r.matchingScore = Mathf.Max(score1, score2);
                if (r.matchingScore > minScore) list.Add(r);
            }

            RefreshSort();
        }

        private string GetGroup(FR2_Ref rf)
        {
            if (customGetGroup != null) return customGetGroup(rf);
            if (rf.depth == 0) return level0Group;
            if (getGroupMode() == GroupMode.None) return string.Empty;

            FR2_SceneRef sr = null;
            if (rf.isSceneRef)
            {
                sr = rf as FR2_SceneRef;
                if (sr == null) return null;
            }

            if (!rf.isSceneRef && rf.asset.IsExcluded) return null;

            switch (getGroupMode())
            {
                case GroupMode.Extension:
                    return rf.isSceneRef ? sr.targetType
                        : string.IsNullOrEmpty(rf.asset.extension) ? "(no extension)" : rf.asset.extension;
                case GroupMode.Type:
                    return rf.isSceneRef ? sr.targetType : FR2_AssetGroupDrawer.FILTERS[rf.type].name;
                case GroupMode.Folder:
                    return rf.isSceneRef ? sr.scenePath : rf.asset.assetFolder;
                case GroupMode.Dependency:
                    return rf.depth == 1 ? "Direct Usage" : "Indirect Usage";
                case GroupMode.Depth:
                    return "Level " + rf.depth;
                case GroupMode.Atlas:
                    return rf.isSceneRef ? "(not in atlas)" : string.IsNullOrEmpty(rf.asset.AtlasName) ? "(not in atlas)" : rf.asset.AtlasName;
                case GroupMode.AssetBundle:
                    return rf.isSceneRef ? "(not in assetbundle)" : string.IsNullOrEmpty(rf.asset.AssetBundleName) ? "(not in assetbundle)" : rf.asset.AssetBundleName;
                case GroupMode.SourceComponent:
                    if (sr == null) return "(not scene ref)";
                    return GetFirstSourceComponentLabel(sr);
                case GroupMode.PropertyPath:
                    if (sr == null) return "(not scene ref)";
                    return GetFirstPropertyPath(sr);
                case GroupMode.SourceGameObject:
                    if (sr == null) return "(not scene ref)";
                    return GetFirstSourceGameObjectName(sr);
                case GroupMode.Hierarchy:
                    if (!rf.isSceneRef) return rf.asset.assetFolder;
                    if (string.IsNullOrEmpty(sr.scenePath)) return "(root)";
                    CacheHierarchyGroup(sr);
                    return sr.scenePath;
            }

            return "(others)";
        }

        internal static readonly Dictionary<string, Component> _sourceComponentCache = new Dictionary<string, Component>();
        internal static readonly Dictionary<string, GameObject> _hierarchyGroupCache = new Dictionary<string, GameObject>();
        
        private static string GetFirstSourceComponentLabel(FR2_SceneRef sr)
        {
            var refs = sr.sourceRefs?.Count > 0 ? sr.sourceRefs : sr.backwardRefs;
            if (refs == null || refs.Count == 0) return "(no source)";
            var comp = refs[0].sourceComponent;
            if (comp == null) return "(missing)";
            string label = comp.gameObject.name + ":" + comp.GetType().Name;
            _sourceComponentCache[label] = comp;
            return label;
        }

        private static string GetFirstPropertyPath(FR2_SceneRef sr)
        {
            var refs = sr.sourceRefs?.Count > 0 ? sr.sourceRefs : sr.backwardRefs;
            if (refs == null || refs.Count == 0) return "(no property)";
            var pp = refs[0].propertyPath;
            if (string.IsNullOrEmpty(pp)) return "(no property)";
            int dot = pp.LastIndexOf('.');
            string name = dot >= 0 ? pp.Substring(dot + 1) : pp;
            return "." + name;
        }

        internal static readonly Dictionary<string, GameObject> _sourceGameObjectCache = new Dictionary<string, GameObject>();

        private static string GetFirstSourceGameObjectName(FR2_SceneRef sr)
        {
            var refs = sr.sourceRefs?.Count > 0 ? sr.sourceRefs : sr.backwardRefs;
            if (refs == null || refs.Count == 0) return "(no source)";
            var comp = refs[0].sourceComponent;
            if (comp == null) return "(missing)";
            string label = comp.gameObject.name;
            _sourceGameObjectCache[label] = comp.gameObject;
            return comp.gameObject.name;
        }

        private static void CacheHierarchyGroup(FR2_SceneRef sr)
        {
            if (_hierarchyGroupCache.ContainsKey(sr.scenePath)) return;
            
            GameObject target = sr.component is GameObject go ? go : (sr.component as Component)?.gameObject;
            if (target == null) return;
            
            Transform parent = target.transform.parent;
            if (parent != null) _hierarchyGroupCache[sr.scenePath] = parent.gameObject;
        }

        private void SortGroup(List<string> groups)
        {
            groups.Sort((item1, item2) =>
            {
                if (item1.Contains("(")) return 1;
                if (item2.Contains("(")) return -1;
                return string.Compare(item1, item2, StringComparison.Ordinal);
            });
        }

        private int SortSceneRefs(FR2_SceneRef rs1, FR2_SceneRef rs2, Sort sortMode)
        {
            switch (sortMode)
            {
                case Sort.Type:
                    int typeCompare = string.Compare(rs1.targetType, rs2.targetType, StringComparison.Ordinal);
                    return typeCompare != 0 ? typeCompare : string.Compare(rs1.sceneFullPath, rs2.sceneFullPath, StringComparison.Ordinal);
                case Sort.Path:
                    return string.Compare(rs1.sceneFullPath, rs2.sceneFullPath, StringComparison.Ordinal);
                default:
                    return string.Compare(rs1.sceneFullPath, rs2.sceneFullPath, StringComparison.Ordinal);
            }
        }

        private int SortAssetRefs(FR2_Ref r1, FR2_Ref r2, Sort sortMode)
        {
            switch (sortMode)
            {
                case Sort.Type:
                    string type1 = r1.asset.extension ?? "";
                    string type2 = r2.asset.extension ?? "";
                    int typeCompare = string.Compare(type1, type2, StringComparison.Ordinal);
                    return typeCompare != 0 ? typeCompare : string.Compare(r1.asset.assetPath, r2.asset.assetPath, StringComparison.Ordinal);
                case Sort.Path:
                    return string.Compare(r1.asset.assetPath, r2.asset.assetPath, StringComparison.Ordinal);
                case Sort.Size:
                    int sizeCompare = r2.asset.fileSize.CompareTo(r1.asset.fileSize);
                    return sizeCompare != 0 ? sizeCompare : string.Compare(r1.asset.assetPath, r2.asset.assetPath, StringComparison.Ordinal);
                default:
                    return string.Compare(r1.asset.assetPath, r2.asset.assetPath, StringComparison.Ordinal);
            }
        }

        private int SortAsset(string term11, string term12, string term21, string term22, bool swap)
        {
            int v1 = string.Compare(term11, term12, StringComparison.Ordinal);
            int v2 = string.Compare(term21, term22, StringComparison.Ordinal);
            return swap ? v1 == 0 ? v2 : v1 : v2 == 0 ? v1 : v2;
        }
    }
}
