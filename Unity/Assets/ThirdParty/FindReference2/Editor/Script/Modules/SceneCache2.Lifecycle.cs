using UnityEngine.SceneManagement;

namespace vietlabs.fr2
{
    internal partial class SceneCache2
    {
        public void OnSceneLoaded(Scene scene)
        {
            if (!scene.IsValid()) return;
            if (!scene.isLoaded) return;

            FR2_LOG.Log($"SceneCache2: OnSceneLoaded for {scene.name} ({SceneGUID})");

            BuildRuntimeMaps(scene);

            bool cacheLoaded = LoadFromCache();

            if (cacheLoaded)
            {
                BuildInvertedIndex();
                BuildUsageCountMap();
                FR2_LOG.Log($"SceneCache2: LoadFromCache OK — goIDs={_goIDs.Count}, scannedGOs={_scannedGOIDs.Count}, usedByMap={_usedByMap.Count}, goUsedByCount={_goUsedByCount.Count}");

                if (_isPartialCache)
                {
                    FR2_LOG.Log($"SceneCache2: Partial cache for {scene.name} ({_scannedGOIDs.Count} GOs) — resuming scan");
                    CurrentStatus = Status.Partial;
                }
                else
                {
                    bool hashMismatch = NeedsFullRefresh();
                    if (hashMismatch)
                    {
                        FR2_LOG.Log($"SceneCache2: Cache stale for {scene.name} (hash changed) — serving stale, background rescan queued");
                        CurrentStatus = Status.Dirty;
                    }
                    else
                    {
                        FR2_LOG.Log($"SceneCache2: Cache valid for {scene.name}, building runtime structures");
                        CurrentStatus = Status.Ready;
                    }
                }
            }
            else
            {
                FR2_LOG.Log($"SceneCache2: No cache for {scene.name}, triggering full scan");
                CurrentStatus = Status.None;
            }
        }

        public void BuildRuntimeMapsOnly(Scene scene)
        {
            if (!scene.IsValid()) return;
            if (!scene.isLoaded) return;
            BuildRuntimeMaps(scene);
        }

        public void OnSceneUnloaded()
        {
            FR2_LOG.Log($"SceneCache2: OnSceneUnloaded for {ScenePath} ({SceneGUID})");

            StopScan();
            ClearRuntimeMaps();
            DisposeSerializedObjectCache();
        }
    }
}
