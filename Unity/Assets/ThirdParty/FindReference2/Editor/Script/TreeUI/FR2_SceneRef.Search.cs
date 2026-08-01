using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityObject = UnityEngine.Object;


namespace vietlabs.fr2
{
    internal partial class FR2_SceneRef
    {
        private static Dictionary<string, FR2_Ref> refs = new Dictionary<string, FR2_Ref>();
        private static string[] cacheAssetGuids;
        private static Action<Dictionary<string, FR2_Ref>> onFindRefInSceneComplete;

        public static void FindSceneUseSceneObjectsAsync(GameObject[] targets, Action<Dictionary<string, FR2_Ref>> onComplete)
        {
            FR2_SceneCache.FindSceneUseSceneObjectsAsync(targets, onComplete);
        }

        public static void FindSceneBackwardReferencesAsync(GameObject[] targets, Action<Dictionary<string, FR2_Ref>> onComplete)
        {
            FR2_SceneCache.FindSceneBackwardReferencesAsync(targets, onComplete);
        }

        public static void FindSceneInSceneAsync(GameObject[] targets, Action<Dictionary<string, FR2_Ref>> onComplete)
        {
            FR2_SceneCache.FindSceneInSceneAsync(targets, onComplete);
        }

        public static Dictionary<string, FR2_Ref> FindRefInScene(
            string[] assetGUIDs, bool depth,
            Action<Dictionary<string, FR2_Ref>> onComplete)
        {
            cacheAssetGuids = assetGUIDs;
            onFindRefInSceneComplete = onComplete;
            if (FR2_SceneCache.hasCache)
            {
                FindRefInScene();
            }
            else
            {
                FR2_SceneCache.onReady -= FindRefInScene;
                FR2_SceneCache.onReady += FindRefInScene;
            }

            return refs;
        }

        private static void FindRefInScene()
        {
            refs.InitializeOrClear();

            for (var i = 0; i < cacheAssetGuids.Length; i++)
            {
                var asset = FR2_Cache.GetAsset(cacheAssetGuids[i]);
                if (asset == null) continue;

                Add(refs, asset, 0);
                ApplyFilter(refs, asset);
            }

            if (onFindRefInSceneComplete != null) onFindRefInSceneComplete(refs);
            FR2_SceneCache.onReady -= FindRefInScene;
        }

        private static void ApplyFilter(Dictionary<string, FR2_Ref> refs, FR2_Asset asset)
        {
            string targetPath = FR2_Cache.GUIDToAssetPath(asset.guid);
            if (string.IsNullOrEmpty(targetPath)) return;

            if (targetPath != asset.assetPath) asset.MarkAsMoved();

            var target = AssetDatabase.LoadAssetAtPath(targetPath, typeof(UnityObject));
            if (target == null) return;

            int directHits = SearchSceneCachesForAsset(refs, targetPath);

            if (directHits == 0 && IsLeafAsset(asset))
                ResolveIndirectSceneRefs(refs, asset, 0, null);
        }

        private static void ResolveIndirectSceneRefs(Dictionary<string, FR2_Ref> refs, FR2_Asset asset, int depth, HashSet<string> visited)
        {
            if (depth > 3) return;
            
            // Prevent cycles — UsedByMap can have circular references (A uses B, B uses A)
            if (visited == null) visited = new HashSet<string>();
            if (!visited.Add(asset.guid)) return;
            
            var usedByMap = asset.UsedByMap;
            if (usedByMap == null || usedByMap.Count == 0) return;

            foreach (var kvp in usedByMap)
            {
                FR2_Asset intermediate = kvp.Value;
                if (intermediate == null || intermediate.IsMissing) continue;

                string intermediatePath = FR2_Cache.GUIDToAssetPath(intermediate.guid);
                if (string.IsNullOrEmpty(intermediatePath)) continue;

                int hits = SearchSceneCachesForAsset(refs, intermediatePath);
                if (hits == 0) ResolveIndirectSceneRefs(refs, intermediate, depth + 1, visited);
            }
        }

        private static bool IsLeafAsset(FR2_Asset asset)
        {
            string ext = asset.extension;
            if (string.IsNullOrEmpty(ext)) return false;
            switch (ext)
            {
                case ".png": case ".jpg": case ".jpeg": case ".tga": case ".psd":
                case ".tif": case ".tiff": case ".gif": case ".bmp": case ".exr":
                case ".hdr": case ".svg":
                case ".shader": case ".shadergraph": case ".shadersubgraph":
                case ".cginc": case ".hlsl": case ".glslinc":
                case ".wav": case ".mp3": case ".ogg": case ".aif": case ".aiff":
                case ".flac": case ".it": case ".mod": case ".s3m": case ".xm":
                case ".ttf": case ".otf": case ".fnt":
                case ".cubemap": case ".renderTexture":
                    return true;
                default:
                    return false;
            }
        }

        private static int SearchSceneCachesForAsset(Dictionary<string, FR2_Ref> refs, string assetPath)
        {
            var api = FR2_SceneCache.Api;
            if (api == null) return 0;

            int hitCount = 0;

            for (int i = 0; i < FR2_Unity.SceneCount; i++)
            {
                Scene scene = FR2_Unity.GetSceneAt(i);
                if (!scene.isLoaded) continue;

                SceneCache2 sceneCache = api.GetSceneCacheForScene(scene);
                if (sceneCache == null) continue;
                if (sceneCache.CurrentStatus != SceneCache2.Status.Ready && sceneCache.CurrentStatus != SceneCache2.Status.Partial) continue;

                hitCount += AddDetailedResults(refs, sceneCache.FindDetailedReferencesToAsset(assetPath));
            }

            var prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
            if (prefabStage != null)
            {
                SceneCache2 prefabCache = api.GetSceneCacheForScene(prefabStage.scene);
                if (prefabCache != null && (prefabCache.CurrentStatus == SceneCache2.Status.Ready || prefabCache.CurrentStatus == SceneCache2.Status.Partial))
                {
                    hitCount += AddDetailedResults(refs, prefabCache.FindDetailedReferencesToAsset(assetPath));
                }
                else if (hitCount == 0)
                {
                    FR2_SceneCache.onReady -= FindRefInScene;
                    FR2_SceneCache.onReady += FindRefInScene;
                    api.RefreshSceneCache2(false);
                }
            }

            return hitCount;
        }

        private static int AddDetailedResults(Dictionary<string, FR2_Ref> refs, Dictionary<GameObject, List<SceneRefInfo>> detailed)
        {
            int count = 0;
            foreach (var kvp in detailed)
            {
                var go = kvp.Key;
                if (!go) continue;
                count++;

                var targetId = FR2_Unity.GetInstanceId(go).ToString();
                if (!refs.ContainsKey(targetId))
                    refs.Add(targetId, new FR2_SceneRef(1, go));

                if (kvp.Value.Count > 0 && refs[targetId] is FR2_SceneRef sr)
                {
                    sr.sourceRefs.AddRange(kvp.Value);
                    sr.MarkGroupingDirty();
                }
            }
            return count;
        }

        private static void Add(Dictionary<string, FR2_Ref> refs, FR2_Asset asset, int depth)
        {
            string targetId = asset.guid;
            if (!refs.ContainsKey(targetId)) refs.Add(targetId, new FR2_Ref(0, depth, asset, null));
        }

        private static void Add(Dictionary<string, FR2_Ref> refs, UnityObject target, int depth)
        {
            if (target == null) return;
            var targetId = FR2_Unity.GetInstanceId(target).ToString();
            if (!refs.ContainsKey(targetId)) refs.Add(targetId, new FR2_SceneRef(depth, target));
        }
    }
}
