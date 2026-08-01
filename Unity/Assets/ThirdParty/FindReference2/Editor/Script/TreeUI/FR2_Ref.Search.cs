using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;


namespace vietlabs.fr2
{
    internal partial class FR2_Ref
    {
        private static readonly Dictionary<Object, string> objectToGuidCache = new Dictionary<Object, string>();
        
        public static void ClearObjectToGuidCache()
        {
            objectToGuidCache.Clear();
        }

        internal static Dictionary<string, FR2_Ref> FindRefs(string[] guids, bool usageOrUsedBy, bool addFolder, int initDepth = 0)
        {
            if (guids == null || guids.Length == 0) 
                return new Dictionary<string, FR2_Ref>();
            
            var dict = new Dictionary<string, FR2_Ref>(guids.Length * 10);
            var frontier = new List<FR2_Ref>(guids.Length * 4);
            var selectedGuids = new HashSet<string>(guids);

            for (var i = 0; i < guids.Length; i++)
            {
                string guid = guids[i];
                if (dict.ContainsKey(guid)) continue;

                FR2_Asset asset = FR2_Cache.GetAsset(guid);
                if (asset == null) continue;

                var r = new FR2_Ref(i, initDepth, asset, null);
                if (!asset.IsFolder || addFolder) dict.Add(guid, r);
                frontier.Add(r);
            }

            int frontierCap = FR2_SettingExt.bfsFrontierCap;
            for (int i = 0; i < frontier.Count && i < frontierCap; i++)
            {
                var current = frontier[i];
                if (usageOrUsedBy) current.AppendUsage(dict, frontier);
                else current.AppendUsedBy(dict, frontier);
            }

            if (frontier.Count >= frontierCap)
                Debug.LogWarning($"[FR2] BFS frontier cap reached ({frontierCap}). Results may be incomplete. Increase in Project Settings > Find Reference 2.");

            var keysToRemove = new List<string>(dict.Count / 4);
            foreach (KeyValuePair<string, FR2_Ref> kvp in dict)
            {
                if (kvp.Value.depth == initDepth || selectedGuids.Contains(kvp.Key))
                    keysToRemove.Add(kvp.Key);
            }
            
            for (var i = 0; i < keysToRemove.Count; i++)
                dict.Remove(keysToRemove[i]);

            return dict;
        }

        internal static FR2_TimeSlice FindRefsAsync(string[] guids, bool usageOrUsedBy, bool addFolder, int initDepth, Action<Dictionary<string, FR2_Ref>> onComplete)
        {
            if (guids == null || guids.Length == 0)
            {
                onComplete?.Invoke(new Dictionary<string, FR2_Ref>());
                return null;
            }

            var dict = new Dictionary<string, FR2_Ref>(guids.Length * 10);
            var frontier = new List<FR2_Ref>(guids.Length * 4);
            var selectedGuids = new HashSet<string>(guids);

            for (var i = 0; i < guids.Length; i++)
            {
                string guid = guids[i];
                if (dict.ContainsKey(guid)) continue;

                FR2_Asset asset = FR2_Cache.GetAsset(guid);
                if (asset == null) continue;

                var r = new FR2_Ref(i, initDepth, asset, null);
                if (!asset.IsFolder || addFolder) dict.Add(guid, r);
                frontier.Add(r);
            }

            var ts = new FR2_TimeSlice(
                () => frontier.Count,
                (idx) =>
                {
                    if (idx >= FR2_SettingExt.bfsFrontierCap) return;
                    var current = frontier[idx];
                    if (usageOrUsedBy) current.AppendUsage(dict, frontier);
                    else current.AppendUsedBy(dict, frontier);
                },
                () =>
                {
                    if (frontier.Count >= FR2_SettingExt.bfsFrontierCap)
                        Debug.LogWarning($"[FR2] BFS frontier cap reached ({FR2_SettingExt.bfsFrontierCap}). Results may be incomplete. Increase in Project Settings > Find Reference 2.");

                    var keysToRemove = new List<string>(dict.Count / 4);
                    foreach (KeyValuePair<string, FR2_Ref> kvp in dict)
                    {
                        if (kvp.Value.depth == initDepth || selectedGuids.Contains(kvp.Key))
                            keysToRemove.Add(kvp.Key);
                    }
                    for (var i = 0; i < keysToRemove.Count; i++) dict.Remove(keysToRemove[i]);
                    onComplete?.Invoke(dict);
                });
            ts.jobName = usageOrUsedBy ? "FindUsage" : "FindUsedBy";
            ts.Start();
            return ts;
        }

        public static FR2_TimeSlice FindUsageAsync(string[] guids, int initDepth, Action<Dictionary<string, FR2_Ref>> onComplete)
        {
            return FindRefsAsync(guids, true, true, initDepth, onComplete);
        }

        public static FR2_TimeSlice FindUsedByAsync(string[] guids, int initDepth, Action<Dictionary<string, FR2_Ref>> onComplete)
        {
            return FindRefsAsync(guids, false, true, initDepth, onComplete);
        }

        public static Dictionary<string, FR2_Ref> FindUsage(string[] guids, int initDepth = 0)
        {
            return FindRefs(guids, true, true, initDepth);
        }

        public static Dictionary<string, FR2_Ref> FindUsedBy(string[] guids, int initDepth = 0)
        {
            return FindRefs(guids, false, true, initDepth);
        }

        public static Dictionary<string, FR2_Ref> FindUsageScene(GameObject[] objs, bool depth)
        {
            var dict = new Dictionary<string, FR2_Ref>();

            for (var i = 0; i < objs.Length; i++)
            {
                if (objs[i].IsAssetObject()) continue;
                var goId = FR2_Unity.GetInstanceId(objs[i]).ToString();
                if (!dict.ContainsKey(goId))
                    dict.Add(goId, new FR2_SceneRef(0, objs[i]));

                foreach (Object item in FR2_Unity.GetAllRefObjects(objs[i]))
                    AppendUsageScene(dict, item);

                if (!depth) continue;
                foreach (GameObject child in objs[i].GetAllChildren(false))
                    foreach (Object item2 in FR2_Unity.GetAllRefObjects(child))
                        AppendUsageScene(dict, item2);
            }

            return dict;
        }

        public static Dictionary<string, FR2_Ref> FindUsageSceneWithDetails(GameObject[] objs, bool depth)
        {
            if (FR2_SceneCache.Api == null)
                return new Dictionary<string, FR2_Ref>();
            
            var dict = new Dictionary<string, FR2_Ref>();

            for (var i = 0; i < objs.Length; i++)
            {
                if (!objs[i]) continue;
                if (objs[i].IsAssetObject()) continue;

                var instanceId = FR2_SceneCache.GetCachedInstanceIdString(FR2_Unity.GetInstanceId(objs[i]));
                if (!dict.ContainsKey(instanceId)) 
                    dict.Add(instanceId, new FR2_SceneRef(0, objs[i]));

                CollectFromSceneCache2(dict, objs[i]);

                if (!depth) continue;
                foreach (GameObject child in objs[i].GetAllChildren(false))
                    CollectFromSceneCache2(dict, child);
            }

            return dict;
        }

        private static void CollectFromSceneCache2(Dictionary<string, FR2_Ref> dict, GameObject gameObject)
        {
            if (!gameObject) return;
            var scene = gameObject.scene;
            if (!scene.IsValid()) return;
            
            var sceneCache = FR2_SceneCache.Api?.GetSceneCacheForScene(scene);
            if (sceneCache == null) return;
            if (sceneCache.CurrentStatus != SceneCache2.Status.Ready && sceneCache.CurrentStatus != SceneCache2.Status.Partial) return;
            
            var refInfos = sceneCache.GetReferencesFrom(gameObject);
            
            foreach (var refInfo in refInfos)
            {
                if (refInfo.isSceneObject) continue;
                var target = refInfo.target;
                if (ReferenceEquals(target, null)) continue;
                AppendUsageSceneWithRef(dict, target, refInfo.sourceComponent, refInfo.propertyPath);
            }
        }

        private static void AppendUsageScene(Dictionary<string, FR2_Ref> dict, Object obj)
        {
            if (!obj) return;
            
            if (!objectToGuidCache.TryGetValue(obj, out string guid))
            {
                if (!FR2_Cache.TryGetGUIDAndLocalFileIdentifier(obj, out guid, out _)) return;
                if (string.IsNullOrEmpty(guid)) return;
                objectToGuidCache[obj] = guid;
            }
            
            if (dict.ContainsKey(guid)) return;

            FR2_Asset asset = FR2_Cache.GetAsset(guid);
            if (asset == null) return;

            dict.Add(guid, new FR2_Ref(0, 1, asset, null));
        }

        private static void AppendUsageSceneWithRef(Dictionary<string, FR2_Ref> dict, Object obj, Component sourceComponent, string propertyPath)
        {
            if (!objectToGuidCache.TryGetValue(obj, out string guid))
            {
                string path = AssetDatabase.GetAssetPath(obj);
                if (string.IsNullOrEmpty(path)) return;

                guid = FR2_Cache.AssetPathToGUID(path);
                if (string.IsNullOrEmpty(guid)) return;
                objectToGuidCache[obj] = guid;
            }

            var asset = FR2_Cache.GetAsset(guid, true);
            if (asset == null) return;

            if (dict.TryGetValue(guid, out FR2_Ref existingRef))
            {
                if (existingRef.sceneReferenceInfo == null)
                {
                    existingRef.sceneReferenceInfo = new List<SceneRefInfo>();
                    existingRef.sceneReferenceInfoSet = new HashSet<SceneRefInfo>();
                }
                
                var refInfo = new SceneRefInfo
                {
                    sourceComponent = sourceComponent,
                    target = obj,
                    propertyPath = propertyPath,
                    isSceneObject = false
                };
                
                if (existingRef.sceneReferenceInfoSet.Add(refInfo))
                    existingRef.sceneReferenceInfo.Add(refInfo);
                return;
            }

            var newRef = new FR2_Ref(0, 1, asset, null);
            var newRefInfo = new SceneRefInfo
            {
                sourceComponent = sourceComponent,
                target = obj,
                propertyPath = propertyPath,
                isSceneObject = false
            };
            
            newRef.sceneReferenceInfo = new List<SceneRefInfo> { newRefInfo };
            newRef.sceneReferenceInfoSet = new HashSet<SceneRefInfo> { newRefInfo };
            dict.Add(guid, newRef);
        }
    }
}
