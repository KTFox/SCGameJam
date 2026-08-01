using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

using UnityObject = UnityEngine.Object;

namespace vietlabs.fr2
{
    internal partial class SceneCache2
    {
        public void RebuildMaps()
        {
            BuildInvertedIndex();
            BuildUsageCountMap();
        }

        private void BuildInvertedIndex()
        {
            _usedByMap.Clear();

            int totalComps = 0;
            int totalSceneRefs = 0;

            foreach (var goEntry in _goIDs)
            {
                foreach (var compEntry in goEntry.Value.compsWithRefs)
                {
                    totalComps++;
                    ulong compID = compEntry.Key;
                    CompRefs refs = compEntry.Value;

                    if (refs.sceneRefs == null) continue;

                    for (int i = 0; i < refs.sceneRefs.Count; i++)
                    {
                        totalSceneRefs++;
                        ulong targetID = refs.sceneRefs[i].targetId;
                        if (!_usedByMap.TryGetValue(targetID, out List<ulong> sources))
                        {
                            sources = new List<ulong>(4);
                            _usedByMap[targetID] = sources;
                        }
                        sources.Add(compID);
                    }
                }
            }

            FR2_LOG.Log($"BuildInvertedIndex: goIDs={_goIDs.Count}, compsWithRefs={totalComps}, sceneRefs={totalSceneRefs}, usedByMap={_usedByMap.Count}");
        }

        private void BuildUsageCountMap()
        {
            _goUsedByCount.Clear();

            foreach (var kvp in _usedByMap)
            {
                ulong targetID = kvp.Key;
                int count = kvp.Value.Count;

                UnityObject targetObj = GetObject(targetID);
                if (targetObj == null) continue;

                GameObject targetGO = targetObj.GetGameObjectFromTarget();
                if (targetGO == null) continue;

                ulong goID = GetID(targetGO);
                if (goID == 0) continue;

                if (_goUsedByCount.TryGetValue(goID, out int existing))
                    _goUsedByCount[goID] = existing + count;
                else
                    _goUsedByCount[goID] = count;
            }
        }

        public List<SceneRefInfo> GetReferencesFrom(GameObject go)
        {
            var result = new List<SceneRefInfo>();

            if (!go) return result;

            ulong goID = GetID(go);
            if (goID == 0) return result;

            if (!_goIDs.TryGetValue(goID, out GOCacheEntry entry)) return result;

            foreach (var compEntry in entry.compsWithRefs)
            {
                ulong compID = compEntry.Key;
                CompRefs refs = compEntry.Value;

                UnityObject compObj = GetObject(compID);
                Component sourceComp = compObj as Component;

                if (refs.sceneRefs != null)
                {
                    for (int i = 0; i < refs.sceneRefs.Count; i++)
                    {
                        UnityObject targetObj = GetObject(refs.sceneRefs[i].targetId);
                        if (targetObj == null) continue;

                        result.Add(new SceneRefInfo { sourceComponent = sourceComp, target = targetObj, propertyPath = refs.sceneRefs[i].propertyPath ?? "", isSceneObject = true });
                    }
                }

                if (refs.assets != null)
                {
                    for (int i = 0; i < refs.assets.Count; i++)
                    {
                        string assetPath = FR2_Cache.GUIDToAssetPath(refs.assets[i].guid);
                        if (string.IsNullOrEmpty(assetPath)) continue;

                        UnityObject asset = AssetDatabase.LoadAssetAtPath<UnityObject>(assetPath);
                        if (asset == null) continue;

                        result.Add(new SceneRefInfo { sourceComponent = sourceComp, target = asset, isSceneObject = false });
                    }
                }
            }

            return result;
        }

        public Dictionary<GameObject, List<SceneRefInfo>> GetUsedByReferences(GameObject[] targets)
        {
            var result = new Dictionary<GameObject, List<SceneRefInfo>>();

            if (targets == null || targets.Length == 0) return result;

            for (int t = 0; t < targets.Length; t++)
            {
                GameObject target = targets[t];
                if (!target) continue;

                var refs = new List<SceneRefInfo>();
                result[target] = refs;

                ulong targetGoID = GetID(target);
                if (targetGoID == 0) continue;

                var idsToCheck = new List<ulong> { targetGoID };

                var components = target.GetComponents<Component>();
                for (int c = 0; c < components.Length; c++)
                {
                    if (!components[c]) continue;
                    ulong compID = GetID(components[c]);
                    if (compID != 0) idsToCheck.Add(compID);
                }

                for (int i = 0; i < idsToCheck.Count; i++)
                {
                    ulong id = idsToCheck[i];
                    if (!_usedByMap.TryGetValue(id, out List<ulong> sourceCompIDs)) continue;

                    for (int s = 0; s < sourceCompIDs.Count; s++)
                    {
                        UnityObject sourceObj = GetObject(sourceCompIDs[s]);
                        Component sourceComp = sourceObj as Component;
                        if (sourceComp == null) continue;

                        UnityObject targetObj = GetObject(id);
                        if (targetObj == null) continue;

                        refs.Add(new SceneRefInfo { sourceComponent = sourceComp, target = targetObj, propertyPath = FindPropertyPath(sourceCompIDs[s], id), isSceneObject = true });
                    }
                }
            }

            return result;
        }

        private string FindPropertyPath(ulong sourceCompId, ulong targetId)
        {
            foreach (var goEntry in _goIDs)
            {
                if (!goEntry.Value.compsWithRefs.TryGetValue(sourceCompId, out CompRefs refs)) continue;
                if (refs.sceneRefs == null) continue;
                for (int i = 0; i < refs.sceneRefs.Count; i++)
                {
                    if (refs.sceneRefs[i].targetId == targetId) return refs.sceneRefs[i].propertyPath ?? "";
                }
            }
            return "";
        }

        public List<GameObject> FindGameObjectsReferencingAsset(string assetPath)
        {
            var detailed = FindDetailedReferencesToAsset(assetPath);
            var result = new List<GameObject>(detailed.Count);
            foreach (var kvp in detailed)
            {
                result.Add(kvp.Key);
            }
            return result;
        }

        public Dictionary<GameObject, List<SceneRefInfo>> FindDetailedReferencesToAsset(string assetPath)
        {
            var result = new Dictionary<GameObject, List<SceneRefInfo>>();
            if (string.IsNullOrEmpty(assetPath)) return result;

            string assetGUID = FR2_Cache.AssetPathToGUID(assetPath);
            if (string.IsNullOrEmpty(assetGUID)) return result;

            bool isPrefab = assetPath.EndsWith(".prefab");

            foreach (var goEntry in _goIDs)
            {
                GOCacheEntry entry = goEntry.Value;
                List<SceneRefInfo> refInfos = null;

                if (isPrefab && entry.MatchesPrefabGUID(assetGUID))
                {
                    GameObject go = GetObject(goEntry.Key) as GameObject;
                    if (go == null) continue;
                    
                    Transform parent = go.transform.parent;
                    if (parent != null)
                    {
                        ulong parentID = GetID(parent.gameObject);
                        if (parentID != 0 && _goIDs.TryGetValue(parentID, out GOCacheEntry parentEntry)
                            && parentEntry.MatchesPrefabGUID(assetGUID))
                        {
                            continue;
                        }
                    }

                    refInfos = new List<SceneRefInfo>();
                    result[go] = refInfos;
                    continue;
                }

                foreach (var compEntry in entry.compsWithRefs)
                {
                    CompRefs refs = compEntry.Value;
                    if (refs.assets == null) continue;
                    for (int i = 0; i < refs.assets.Count; i++)
                    {
                        if (refs.assets[i].guid != assetGUID) continue;
                        
                        string prop = refs.assets[i].propertyPath;
                        if (prop == "m_CorrespondingSourceObject" || prop == "m_PrefabInstance" || prop == "m_PrefabAsset") continue;

                        GameObject go = GetObject(goEntry.Key) as GameObject;
                        if (go == null) break;

                        if (refInfos == null)
                        {
                            if (!result.TryGetValue(go, out refInfos))
                            {
                                refInfos = new List<SceneRefInfo>();
                                result[go] = refInfos;
                            }
                        }

                        Component sourceComp = GetObject(compEntry.Key) as Component;
                        refInfos.Add(new SceneRefInfo
                        {
                            sourceComponent = sourceComp,
                            target = go,
                            propertyPath = refs.assets[i].propertyPath ?? "",
                            isSceneObject = false
                        });
                    }
                }
            }

            return result;
        }

        public int GetGameObjectUsageCount(GameObject go)
        {
            if (!go) return 0;

            ulong goID = GetID(go);
            if (goID == 0) return 0;

            return _goUsedByCount.TryGetValue(goID, out int count) ? count : 0;
        }
    }
}
