using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

using UnityObject = UnityEngine.Object;


namespace vietlabs.fr2
{
    internal partial class FR2_SceneCache
    {
        private Dictionary<string, SceneCache2> _sceneCaches;
        private List<string> _scanQueue;
        private string _prioritizedSceneGUID;
        private bool _sceneCache2Initialized;
        private Action<int, int> _cachedProgressCallback;
        private int _totalScenesInQueue;
        private readonly HashSet<int> _pendingNewObjects = new HashSet<int>();
        private int _scenesCompleted;

        private void InitializeSceneCache2System()
        {
            if (_sceneCache2Initialized) return;

            _sceneCaches = new Dictionary<string, SceneCache2>(8);
            _scanQueue = new List<string>(8);
            _sceneCache2Initialized = true;
            _cachedProgressCallback = (cur, tot) => { current = cur; total = tot; };

            RegisterSceneCache2Events();
            InitializeAlreadyLoadedScenes();

            FR2_LOG.Log($"FR2_SceneCache: SceneCache2 coordinator system initialized");
        }

        private void InitializeAlreadyLoadedScenes()
        {
            if (FR2_SettingExt.disable) return;
            if (!FR2_SettingExt.isAutoRefreshEnabled) return;

            for (int i = 0; i < FR2_Unity.SceneCount; i++)
            {
                Scene scene = FR2_Unity.GetSceneAt(i);
                if (!scene.IsValid()) continue;
                if (!scene.isLoaded) continue;

                string key = GetSceneKey(scene);
                if (string.IsNullOrEmpty(key)) continue;
                if (key.StartsWith("__runtime_", StringComparison.Ordinal) && !Application.isPlaying)
                {
                    if (string.IsNullOrEmpty(scene.name) || scene.name == "DontDestroyOnLoad") continue;
                }

                SceneCache2 cache = GetSceneCacheForScene(scene);
                if (cache == null) continue;

                cache.OnSceneLoaded(scene);
            }
        }

        private void RegisterSceneCache2Events()
        {
            SceneManager.sceneLoaded -= OnSceneCache2SceneLoaded;
            SceneManager.sceneLoaded += OnSceneCache2SceneLoaded;

            SceneManager.sceneUnloaded -= OnSceneCache2SceneUnloaded;
            SceneManager.sceneUnloaded += OnSceneCache2SceneUnloaded;

            EditorApplication.hierarchyChanged -= OnSceneCache2HierarchyChanged;
            EditorApplication.hierarchyChanged += OnSceneCache2HierarchyChanged;

#if UNITY_2022_1_OR_NEWER
            ObjectChangeEvents.changesPublished -= OnObjectChangeEvents;
            ObjectChangeEvents.changesPublished += OnObjectChangeEvents;
#endif

            PrefabStage.prefabStageOpened -= OnPrefabStageOpened;
            PrefabStage.prefabStageOpened += OnPrefabStageOpened;

            PrefabStage.prefabStageClosing -= OnPrefabStageClosing;
            PrefabStage.prefabStageClosing += OnPrefabStageClosing;
        }

        private void UnregisterSceneCache2Events()
        {
            SceneManager.sceneLoaded -= OnSceneCache2SceneLoaded;
            SceneManager.sceneUnloaded -= OnSceneCache2SceneUnloaded;
            EditorApplication.hierarchyChanged -= OnSceneCache2HierarchyChanged;
#if UNITY_2022_1_OR_NEWER
            ObjectChangeEvents.changesPublished -= OnObjectChangeEvents;
#endif
            PrefabStage.prefabStageOpened -= OnPrefabStageOpened;
            PrefabStage.prefabStageClosing -= OnPrefabStageClosing;
        }

        private string GetSceneKey(Scene scene)
        {
            if (!scene.IsValid()) return null;

            if (!string.IsNullOrEmpty(scene.path) && scene.path.StartsWith("Assets/", StringComparison.Ordinal))
            {
                string guid = FR2_Cache.AssetPathToGUID(scene.path);
                return string.IsNullOrEmpty(guid) ? null : guid;
            }

            var prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
            if (prefabStage != null && prefabStage.scene == scene)
            {
                string prefabGUID = FR2_Cache.AssetPathToGUID(prefabStage.assetPath);
                return string.IsNullOrEmpty(prefabGUID) ? null : prefabGUID;
            }

            return $"__runtime_{scene.handle}";
        }

        public SceneCache2 GetSceneCacheForScene(Scene scene)
        {
            InitializeSceneCache2System();

            if (!scene.IsValid()) return null;

            if (!string.IsNullOrEmpty(scene.path) && scene.path.StartsWith("Assets/", StringComparison.Ordinal))
            {
                var sceneGUID = FR2_Cache.AssetPathToGUID(scene.path);
                return string.IsNullOrEmpty(sceneGUID) ? null : GetOrCreateSceneCache(sceneGUID, scene.path);
            }

            var prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
            if (prefabStage != null && prefabStage.scene == scene)
            {
                string prefabAssetPath = prefabStage.assetPath;
                if (string.IsNullOrEmpty(prefabAssetPath)) return null;
                string prefabGUID = FR2_Cache.AssetPathToGUID(prefabAssetPath);
                if (string.IsNullOrEmpty(prefabGUID)) return null;
                return GetOrCreateSceneCache(prefabGUID, prefabAssetPath);
            }

            string key = $"__runtime_{scene.handle}";
            string path = string.IsNullOrEmpty(scene.name) ? key : scene.name;
            return GetOrCreateSceneCache(key, path);
        }

        public void DropRuntimeSceneCaches()
        {
            if (_sceneCaches == null) return;

            List<string> toRemove = null;
            foreach (var kvp in _sceneCaches)
            {
                if (!kvp.Key.StartsWith("__runtime_", StringComparison.Ordinal)) continue;
                if (toRemove == null) toRemove = new List<string>(_sceneCaches.Count);
                toRemove.Add(kvp.Key);
            }

            if (toRemove == null) return;

            for (int i = 0; i < toRemove.Count; i++)
            {
                string key = toRemove[i];
                if (_sceneCaches.TryGetValue(key, out SceneCache2 cache))
                    cache.OnSceneUnloaded();
                _sceneCaches.Remove(key);
                _scanQueue.Remove(key);
            }
        }

        private SceneCache2 GetOrCreateSceneCache(string sceneGUID, string scenePath)
        {
            if (_sceneCaches.TryGetValue(sceneGUID, out SceneCache2 existingCache))
                return existingCache;

            var newCache = new SceneCache2(sceneGUID, scenePath);
            _sceneCaches[sceneGUID] = newCache;

            FR2_LOG.Log($"FR2_SceneCache: Created SceneCache2 for {scenePath} ({sceneGUID})");

            if (newCache.HasDiskCache)
            {
                for (int i = 0; i < FR2_Unity.SceneCount; i++)
                {
                    Scene scene = FR2_Unity.GetSceneAt(i);
                    if (!scene.IsValid() || !scene.isLoaded) continue;
                    string key = GetSceneKey(scene);
                    if (key != sceneGUID) continue;
                    newCache.OnSceneLoaded(scene);
                    break;
                }
            }

            return newCache;
        }

        private static Scene GetDontDestroyOnLoadScene()
        {
            var temp = new GameObject();
            UnityObject.DontDestroyOnLoad(temp);
            var scene = temp.scene;
            UnityObject.DestroyImmediate(temp);
            return scene;
        }

        private Scene _ddolScene;
        private string _ddolSceneGUID;

        private bool TryInitDDOLScene()
        {
            if (!Application.isPlaying) return false;
            if (_ddolScene.IsValid()) return true;

            _ddolScene = GetDontDestroyOnLoadScene();
            if (!_ddolScene.IsValid()) return false;

            _ddolSceneGUID = $"__runtime_{_ddolScene.handle}";
            FR2_LOG.Log($"FR2_SceneCache: DDOL scene initialized — handle={_ddolScene.handle}, name='{_ddolScene.name}', rootCount={_ddolScene.rootCount}, guid={_ddolSceneGUID}");
            return true;
        }

        // Called when user selects a GO — if it lives in DDOL, lazily init and queue a scan.
        // Returns true if the GO is in DDOL. Scans the DDOL scene directly (bypasses the queue).
        // Does NOT cache to disk (CanPersist=false for DDOL ScenePath).
        private bool TryQueueDDOLSceneForSelection(GameObject go)
        {
            if (go == null) return false;
            if (!TryInitDDOLScene()) return false;
            if (go.scene != _ddolScene) return false;

            SceneCache2 cache = GetOrCreateSceneCache(_ddolSceneGUID, "DontDestroyOnLoad");

            FR2_LOG.Log($"FR2_SceneCache: DDOL GO '{go.name}' — cache status={cache.CurrentStatus}");

            // Already scanned — nothing to do.
            if (cache.CurrentStatus == SceneCache2.Status.Ready) return true;

            // Already scanning — nothing to do.
            if (_status == SceneCacheStatus.Scanning) return true;

            // Run a direct scan, bypassing the queue entirely.
            _status = SceneCacheStatus.Scanning;
            currentSceneName = "DontDestroyOnLoad";

            cache.BuildRuntimeMapsOnly(_ddolScene);
            cache.OnScanProgress = _cachedProgressCallback;

            FR2_LOG.Log($"FR2_SceneCache: Starting direct DDOL scan ({_ddolSceneGUID})");
            cache.ScanFull(_ddolScene, () => {
                FR2_LOG.Log($"FR2_SceneCache: DDOL scan complete");
                _status = SceneCacheStatus.Ready;
                currentSceneName = null;
                current = 0;
                total = 0;
                onReady?.Invoke();
                FR2_Event.DispatchGlobal<SceneCacheReadyEvent>();
            });

            return true;
        }

        public void PrioritizeSceneForSelection(GameObject selection)
        {
            InitializeSceneCache2System();

            if (ReferenceEquals(selection, null)) return;
            if (!selection) return;

            Scene selectionScene = selection.scene;
            if (!selectionScene.IsValid()) return;

            string key = GetSceneKey(selectionScene);
            if (string.IsNullOrEmpty(key)) return;

            _prioritizedSceneGUID = key;

            // Lazily init and scan DDOL if the selected GO lives there (direct scan, bypasses queue).
            TryQueueDDOLSceneForSelection(selection);

            FR2_LOG.Log($"FR2_SceneCache: Prioritized scene {selectionScene.name} for selection {selection.name}");
        }

        public bool CheckSelectionStillMatches(Scene scene)
        {
            if (!scene.IsValid()) return false;

            GameObject activeGO = Selection.activeGameObject;
            if (ReferenceEquals(activeGO, null)) return false;
            if (!activeGO) return false;

            return activeGO.scene == scene;
        }

        public void RefreshSceneCache2(bool force)
        {
            if (FR2_SettingExt.disable) return;

            InitializeSceneCache2System();

            var prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
            FR2_LOG.Log($"FR2_SceneCache: RefreshSceneCache2 - prefabStage={prefabStage != null}");

            if (prefabStage != null)
            {
                Scene prefabScene = prefabStage.scene;
                FR2_LOG.Log($"FR2_SceneCache: Prefab stage detected - scene.IsValid={prefabScene.IsValid()}, scene.name={prefabScene.name}, scene.path={prefabScene.path}");

                string prefabAssetPath = prefabStage.assetPath;
                string prefabGUID = FR2_Cache.AssetPathToGUID(prefabAssetPath);
                FR2_LOG.Log($"FR2_SceneCache: Prefab asset path={prefabAssetPath}, GUID={prefabGUID}");

                if (prefabScene.IsValid())
                {
                    FR2_LOG.Log($"FR2_SceneCache: Refreshing prefab stage (prefab stage active)");

                    SceneCache2 prefabCache = GetSceneCacheForScene(prefabScene);
                    FR2_LOG.Log($"FR2_SceneCache: GetSceneCacheForScene returned cache={prefabCache != null}, SceneGUID={prefabCache?.SceneGUID}, ScenePath={prefabCache?.ScenePath}");

                    if (prefabCache == null) return;

                    _status = SceneCacheStatus.Scanning;
                    currentSceneName = prefabScene.name;

                    prefabCache.OnSceneLoaded(prefabScene);

                    prefabCache.OnScanProgress = _cachedProgressCallback;

                    if (force)
                    {
                        prefabCache.ScanFull(prefabScene, () => {
                            _status = SceneCacheStatus.Ready;
                            currentSceneName = null;
                            current = 0;
                            total = 0;
                            FR2_LOG.Log($"FR2_SceneCache: Prefab stage scan complete");
                            onReady?.Invoke();
                            FR2_Event.DispatchGlobal<SceneCacheReadyEvent>();
                        });
                    }
                    else
                    {
                        prefabCache.ScanIncremental(prefabScene, () => {
                            _status = SceneCacheStatus.Ready;
                            currentSceneName = null;
                            current = 0;
                            total = 0;
                            FR2_LOG.Log($"FR2_SceneCache: Prefab stage scan complete");
                            onReady?.Invoke();
                            FR2_Event.DispatchGlobal<SceneCacheReadyEvent>();
                        });
                    }

                    return;
                }
            }

            _status = SceneCacheStatus.Scanning;
            _scanQueue.Clear();
            int totalScenes = 0;

            for (int i = 0; i < FR2_Unity.SceneCount; i++)
            {
                Scene scene = FR2_Unity.GetSceneAt(i);
                if (!scene.isLoaded) continue;

                string key = GetSceneKey(scene);
                if (string.IsNullOrEmpty(key)) continue;
                if (key.StartsWith("__runtime_", StringComparison.Ordinal)) continue;

                SceneCache2 cache = GetSceneCacheForScene(scene);
                if (cache == null) continue;

                if (force || cache.CurrentStatus == SceneCache2.Status.Dirty || cache.CurrentStatus == SceneCache2.Status.None || cache.CurrentStatus == SceneCache2.Status.Partial)
                {
                    AddToScanQueue(cache.SceneGUID);
                    totalScenes++;
                }
            }

            // DDOL is scanned lazily on selection, not via the queue.
            // On force refresh, if we already have a DDOL cache, mark it dirty so the next selection triggers a rescan.
            if (force && TryInitDDOLScene() && _sceneCaches.TryGetValue(_ddolSceneGUID, out SceneCache2 ddolCache))
            {
                ddolCache.SetDirty();
            }

            if (totalScenes == 0)
            {
                _status = SceneCacheStatus.Ready;
                currentSceneName = null;
                current = 0;
                total = 0;
                onReady?.Invoke();
                FR2_Event.DispatchGlobal<SceneCacheReadyEvent>();
                return;
            }

            FR2_LOG.Log($"FR2_SceneCache: Starting scan queue with {totalScenes} scene(s)");
            _totalScenesInQueue = totalScenes;
            _scenesCompleted = 0;
            ProcessNextSceneInQueue(force);
        }

        private string GetNextSceneFromQueue()
        {
            if (_scanQueue.Count == 0) return null;

            var prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
            if (prefabStage != null)
            {
                string prefabGUID = FR2_Cache.AssetPathToGUID(prefabStage.assetPath);
                if (!string.IsNullOrEmpty(prefabGUID) && _scanQueue.Contains(prefabGUID))
                    return prefabGUID;
            }

            if (Selection.activeGameObject != null)
            {
                Scene selectedScene = Selection.activeGameObject.scene;
                if (selectedScene.IsValid() && selectedScene.isLoaded)
                {
                    string sceneKey = GetSceneKey(selectedScene);
                    if (!string.IsNullOrEmpty(sceneKey) && _scanQueue.Contains(sceneKey))
                        return sceneKey;
                }
            }

            for (int i = 0; i < _scanQueue.Count; i++)
            {
                string sceneGUID = _scanQueue[i];
                SceneCache2 cache = GetCacheByGUID(sceneGUID);
                if (cache != null && cache.HasDiskCache)
                    return sceneGUID;
            }

            return _scanQueue[0];
        }

        private SceneCache2 GetCacheByGUID(string guid)
        {
            if (string.IsNullOrEmpty(guid)) return null;
            _sceneCaches.TryGetValue(guid, out SceneCache2 cache);
            return cache;
        }

        private void ProcessNextSceneInQueue(bool force)
        {
            string sceneGUID = GetNextSceneFromQueue();

            if (string.IsNullOrEmpty(sceneGUID))
            {
                _status = SceneCacheStatus.Ready;
                currentSceneName = null;
                current = 0;
                total = 0;
                FR2_LOG.Log($"FR2_SceneCache: Scan queue complete");
                onReady?.Invoke();
                FR2_Event.DispatchGlobal<SceneCacheReadyEvent>();
                return;
            }

            ProcessSceneGUID(sceneGUID, force);
        }

        private void ProcessSceneGUID(string sceneGUID, bool force)
        {
            string assetPath = FR2_Cache.GUIDToAssetPath(sceneGUID);
            bool isPrefab = assetPath.EndsWith(".prefab");
            bool isRuntimeScene = sceneGUID.StartsWith("__runtime_", System.StringComparison.Ordinal);

            Scene targetScene = default;
            SceneCache2 cache = null;

            if (isRuntimeScene)
            {
                if (TryInitDDOLScene() && _ddolSceneGUID == sceneGUID)
                {
                    targetScene = _ddolScene;
                    cache = GetOrCreateSceneCache(_ddolSceneGUID, "DontDestroyOnLoad");
                    currentSceneName = "DontDestroyOnLoad";
                    FR2_LOG.Log($"FR2_SceneCache: Processing DDOL scene — sceneValid={targetScene.IsValid()}, rootCount={targetScene.rootCount}, cache.status={cache.CurrentStatus}");
                }
                else
                {
                    for (int i = 0; i < FR2_Unity.SceneCount; i++)
                    {
                        Scene scene = FR2_Unity.GetSceneAt(i);
                        if (!scene.isLoaded) continue;
                        string key = GetSceneKey(scene);
                        if (key != sceneGUID) continue;
                        targetScene = scene;
                        cache = GetSceneCacheForScene(scene);
                        currentSceneName = scene.name;
                        break;
                    }

                    if (!targetScene.IsValid() || cache == null)
                    {
                        _scanQueue.Remove(sceneGUID);
                        if (_scanQueue.Count > 0)
                            EditorApplication.delayCall += () => ProcessNextSceneInQueue(force);
                        else
                        {
                            _status = SceneCacheStatus.Ready;
                            currentSceneName = null;
                            current = 0;
                            total = 0;
                            EditorApplication.delayCall += () =>
                            {
                                onReady?.Invoke();
                                FR2_Event.DispatchGlobal<SceneCacheReadyEvent>();
                            };
                        }
                        return;
                    }
                }
            }
            else if (isPrefab)
            {
                var prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
                if (prefabStage != null && FR2_Cache.AssetPathToGUID(prefabStage.assetPath) == sceneGUID)
                {
                    targetScene = prefabStage.scene;
                    if (targetScene.IsValid())
                    {
                        cache = GetSceneCacheForScene(targetScene);
                        currentSceneName = targetScene.name;
                    }
                }
            }
            else
            {
                for (int i = 0; i < FR2_Unity.SceneCount; i++)
                {
                    Scene scene = FR2_Unity.GetSceneAt(i);
                    if (!scene.isLoaded) continue;

                    string key = GetSceneKey(scene);
                    if (key != sceneGUID) continue;

                    targetScene = scene;
                    cache = GetSceneCacheForScene(scene);
                    currentSceneName = scene.name;
                    break;
                }
            }

            if (!targetScene.IsValid() || cache == null)
            {
                if (!isRuntimeScene)
                    FR2_LOG.LogWarning($"FR2_SceneCache: Scene/prefab with GUID {sceneGUID} not found, skipping");
                _scanQueue.Remove(sceneGUID);
                if (_scanQueue.Count > 0)
                    EditorApplication.delayCall += () => ProcessNextSceneInQueue(force);
                else
                {
                    _status = SceneCacheStatus.Ready;
                    currentSceneName = null;
                    current = 0;
                    total = 0;
                    EditorApplication.delayCall += () =>
                    {
                        onReady?.Invoke();
                        FR2_Event.DispatchGlobal<SceneCacheReadyEvent>();
                    };
                }
                return;
            }

            _status = SceneCacheStatus.Scanning;

            // For DDOL (runtime) scenes, OnSceneLoaded was already called in TryQueueDDOLSceneForSelection.
            // Calling it again would reset the cache state and start a second scan on top of the first.
            if (!isRuntimeScene)
            {
                if (!force)
                    cache.OnSceneLoaded(targetScene);
                else
                    cache.BuildRuntimeMapsOnly(targetScene);
            }

            if (force || cache.CurrentStatus == SceneCache2.Status.None)
            {
                FR2_LOG.Log($"FR2_SceneCache: Scanning {currentSceneName} (status={cache.CurrentStatus})");

                cache.OnScanProgress = _cachedProgressCallback;

                cache.ScanFull(targetScene, () => {
                    _scenesCompleted++;
                    _scanQueue.Remove(sceneGUID);
                    EditorApplication.delayCall += () => ProcessNextSceneInQueue(force);
                });
            }
            else if (cache.CurrentStatus == SceneCache2.Status.Partial)
            {
                FR2_LOG.Log($"FR2_SceneCache: Resuming partial scan {currentSceneName}");

                _scanQueue.Remove(sceneGUID);

                // Signal partial-ready so UI can display cached data while scan continues
                if (!_isInvokingOnReady)
                {
                    _isInvokingOnReady = true;
                    _status = SceneCacheStatus.Ready;
                    onReady?.Invoke();
                    FR2_Event.DispatchGlobal<SceneCacheReadyEvent>();
                    _isInvokingOnReady = false;
                }

                currentSceneName = null;
                current = 0;
                total = 0;

                cache.OnScanProgress = _cachedProgressCallback;

                _status = SceneCacheStatus.Scanning;
                cache.ScanIncremental(targetScene, () => {
                    if (_scanQueue.Count > 0)
                        EditorApplication.delayCall += () => ProcessNextSceneInQueue(force);
                    else
                    {
                        _status = SceneCacheStatus.Ready;
                        currentSceneName = null;
                        current = 0;
                        total = 0;
                        FR2_LOG.Log($"FR2_SceneCache: Partial scan resumed and complete for {currentSceneName}");
                        onReady?.Invoke();
                        FR2_Event.DispatchGlobal<SceneCacheReadyEvent>();
                    }
                });
            }
            else if (cache.CurrentStatus == SceneCache2.Status.Dirty)
            {
                FR2_LOG.Log($"FR2_SceneCache: Background rescan {currentSceneName} (stale cache)");

                _scanQueue.Remove(sceneGUID);

                // Signal partial-ready so UI can display stale data while rescan runs
                if (!_isInvokingOnReady)
                {
                    _isInvokingOnReady = true;
                    _status = SceneCacheStatus.Ready;
                    onReady?.Invoke();
                    FR2_Event.DispatchGlobal<SceneCacheReadyEvent>();
                    _isInvokingOnReady = false;
                }

                currentSceneName = null;
                current = 0;
                total = 0;

                cache.OnScanProgress = _cachedProgressCallback;

                _status = SceneCacheStatus.Scanning;
                cache.ScanFull(targetScene, () => {
                    if (_scanQueue.Count > 0)
                        EditorApplication.delayCall += () => ProcessNextSceneInQueue(force);
                    else
                    {
                        _status = SceneCacheStatus.Ready;
                        currentSceneName = null;
                        current = 0;
                        total = 0;
                        FR2_LOG.Log($"FR2_SceneCache: Background rescan complete for {currentSceneName}");
                        onReady?.Invoke();
                        FR2_Event.DispatchGlobal<SceneCacheReadyEvent>();
                    }
                });
            }
            else if (cache.CurrentStatus == SceneCache2.Status.Ready)
            {
                FR2_LOG.Log($"FR2_SceneCache: {currentSceneName} loaded from cache, moving to next");
                _scenesCompleted++;
                _scanQueue.Remove(sceneGUID);
                EditorApplication.delayCall += () => ProcessNextSceneInQueue(force);
            }
            else
            {
                FR2_LOG.Log($"FR2_SceneCache: Scanning {currentSceneName} (status={cache.CurrentStatus})");

                cache.OnScanProgress = _cachedProgressCallback;

                cache.ScanIncremental(targetScene, () => {
                    _scanQueue.Remove(sceneGUID);
                    EditorApplication.delayCall += () => ProcessNextSceneInQueue(force);
                });
            }
        }

        public void SetSceneCache2Dirty()
        {
            InitializeSceneCache2System();

            foreach (var cache in _sceneCaches.Values)
                cache.SetDirty();
        }

        public void ForceSceneCache2Refresh()
        {
            RefreshSceneCache2(true);
        }

        private void AddToScanQueue(string sceneGUID)
        {
            if (string.IsNullOrEmpty(sceneGUID)) return;
            if (_scanQueue.Contains(sceneGUID)) return;

            if (_status != SceneCacheStatus.Scanning)
            {
                _totalScenesInQueue = 1;
                _scenesCompleted = 0;
            }
            else
            {
                _totalScenesInQueue++;
            }

            _scanQueue.Add(sceneGUID);
        }

        private void OnSceneCache2SceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (FR2_SettingExt.disable) return;
            if (!scene.IsValid()) return;
            if (!scene.isLoaded) return;

            _pendingNewObjects.Clear();

            FR2_LOG.Log($"FR2_SceneCache: Scene loaded event for {scene.name}");

            SceneCache2 cache = GetSceneCacheForScene(scene);
            if (cache == null) return;

            if (!_autoRefresh) return;

            FR2_LOG.Log($"FR2_SceneCache: Adding scene {scene.name} to queue");
            AddToScanQueue(cache.SceneGUID);

            if (_status != SceneCacheStatus.Scanning)
                ProcessNextSceneInQueue(false);
        }

        private void OnSceneCache2SceneUnloaded(Scene scene)
        {
            if (!scene.IsValid()) return;

            _pendingNewObjects.Clear();

            string sceneKey = GetSceneKey(scene);
            if (string.IsNullOrEmpty(sceneKey)) return;

            FR2_LOG.Log($"FR2_SceneCache: Scene unloaded event for {scene.name}");

            if (!_sceneCaches.TryGetValue(sceneKey, out SceneCache2 cache)) return;

            bool wasScanning = cache.CurrentStatus == SceneCache2.Status.Scanning;
            cache.OnSceneUnloaded();
            _sceneCaches.Remove(sceneKey);
            _scanQueue.Remove(sceneKey);

            if (wasScanning || (_status == SceneCacheStatus.Scanning && _scanQueue.Count > 0))
            {
                EditorApplication.delayCall += () => ProcessNextSceneInQueue(false);
            }
            else if (_status == SceneCacheStatus.Scanning && _scanQueue.Count == 0)
            {
                _status = SceneCacheStatus.Ready;
                currentSceneName = null;
                current = 0;
                total = 0;
                EditorApplication.delayCall += () =>
                {
                    onReady?.Invoke();
                    FR2_Event.DispatchGlobal<SceneCacheReadyEvent>();
                };
            }
        }

#if UNITY_2022_1_OR_NEWER
        private void OnObjectChangeEvents(ref ObjectChangeEventStream stream)
        {
            if (FR2_SettingExt.disable) return;

            for (int i = 0; i < stream.length; i++)
            {
                var type = stream.GetEventType(i);
                if (type == ObjectChangeKind.CreateGameObjectHierarchy)
                {
                    stream.GetCreateGameObjectHierarchyEvent(i, out var evt);
                    int id = FR2_Unity.GetCreateEventInstanceId(evt);
                    if (id != 0) _pendingNewObjects.Add(id);
                }
                else if (type == ObjectChangeKind.DestroyGameObjectHierarchy)
                {
                    stream.GetDestroyGameObjectHierarchyEvent(i, out var evt);
                    _pendingNewObjects.Remove(FR2_Unity.GetDestroyEventInstanceId(evt));
                }
            }
        }
#endif

        private void OnSceneCache2HierarchyChanged()
        {
            if (FR2_SettingExt.disable) return;
            if (_status == SceneCacheStatus.Scanning) return;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;

#if UNITY_2022_1_OR_NEWER
            if (_pendingNewObjects.Count == 0) return;
#endif

            bool allDispatched = true;

            for (int i = 0; i < FR2_Unity.SceneCount; i++)
            {
                Scene scene = FR2_Unity.GetSceneAt(i);
                if (!scene.IsValid() || !scene.isLoaded) continue;

                string key = GetSceneKey(scene);
                if (string.IsNullOrEmpty(key)) continue;

                if (!_sceneCaches.TryGetValue(key, out SceneCache2 cache)) continue;
                if (cache.CurrentStatus == SceneCache2.Status.None) continue;

                if (cache.CurrentStatus == SceneCache2.Status.Scanning)
                {
                    cache.SetDirty();
                    allDispatched = false;
                    continue;
                }

                if (!_autoRefresh)
                {
                    cache.SetDirty();
                    _status = SceneCacheStatus.Changed;
                    continue;
                }

#if UNITY_2022_1_OR_NEWER
                cache.ScanNewObjects(_pendingNewObjects, scene, () =>
                {
                    FR2_HierarchyReferenceIndicator.ClearCache();
                    EditorApplication.RepaintHierarchyWindow();
                });
#else
                cache.ScanIncremental(scene, () =>
                {
                    FR2_HierarchyReferenceIndicator.ClearCache();
                    EditorApplication.RepaintHierarchyWindow();
                });
#endif
            }

#if UNITY_2022_1_OR_NEWER
            if (allDispatched) _pendingNewObjects.Clear();
#endif
        }

        private void ScanAllLoadedScenesIncremental()
        {
            InitializeSceneCache2System();

            var prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
            if (prefabStage != null)
            {
                Scene prefabScene = prefabStage.scene;
                if (!prefabScene.IsValid() || !prefabScene.isLoaded) return;
                SceneCache2 cache = GetSceneCacheForScene(prefabScene);
                if (cache == null || cache.CurrentStatus == SceneCache2.Status.None) return;
                cache.ScanIncrementalSync(prefabScene);
                cache.CurrentStatus = SceneCache2.Status.Ready;
                return;
            }

            for (int i = 0; i < FR2_Unity.SceneCount; i++)
            {
                Scene scene = FR2_Unity.GetSceneAt(i);
                if (!scene.isLoaded) continue;

                string key = GetSceneKey(scene);
                if (string.IsNullOrEmpty(key)) continue;
                if (key.StartsWith("__runtime_", System.StringComparison.Ordinal)) continue;

                SceneCache2 cache = GetSceneCacheForScene(scene);
                if (cache == null || cache.CurrentStatus == SceneCache2.Status.None) continue;

                cache.ScanIncrementalSync(scene);
                cache.CurrentStatus = SceneCache2.Status.Ready;
            }
        }

        private Dictionary<int, int> _sceneCache2UsedByCountsResult;

        public Dictionary<int, int> GetSceneCache2UsedByCounts()
        {
            InitializeSceneCache2System();

            if (_sceneCache2UsedByCountsResult == null)
                _sceneCache2UsedByCountsResult = new Dictionary<int, int>(256);
            else
                _sceneCache2UsedByCountsResult.Clear();

            foreach (var kvp in _sceneCaches)
            {
                SceneCache2 cache = kvp.Value;
                if (cache == null) continue;
                if (cache.CurrentStatus != SceneCache2.Status.Ready && cache.CurrentStatus != SceneCache2.Status.Partial) continue;

                AggregateUsageCountsFromCache(cache, _sceneCache2UsedByCountsResult);
            }

            return _sceneCache2UsedByCountsResult;
        }

        private void AggregateUsageCountsFromCache(SceneCache2 cache, Dictionary<int, int> result)
        {
            if (cache == null) return;

            foreach (var scene in GetLoadedScenesForCache(cache))
            {
                if (!scene.IsValid()) continue;
                if (!scene.isLoaded) continue;

                var rootObjects = scene.GetRootGameObjects();
                for (int i = 0; i < rootObjects.Length; i++)
                {
                    var root = rootObjects[i];
                    if (ReferenceEquals(root, null)) continue;
                    if (!root) continue;

                    AggregateUsageCountsRecursive(cache, root, result);
                }
            }
        }

        private void AggregateUsageCountsRecursive(SceneCache2 cache, GameObject go, Dictionary<int, int> result)
        {
            if (ReferenceEquals(go, null)) return;
            if (!go) return;

            int count = cache.GetGameObjectUsageCount(go);
            if (count > 0)
            {
                int instanceId = FR2_Unity.GetInstanceId(go);
                if (result.TryGetValue(instanceId, out int existing))
                    result[instanceId] = existing + count;
                else
                    result[instanceId] = count;
            }

            var transform = go.transform;
            int childCount = transform.childCount;
            for (int i = 0; i < childCount; i++)
            {
                var child = transform.GetChild(i);
                if (ReferenceEquals(child, null)) continue;
                if (!child) continue;

                AggregateUsageCountsRecursive(cache, child.gameObject, result);
            }
        }

        private IEnumerable<Scene> GetLoadedScenesForCache(SceneCache2 cache)
        {
            if (cache == null) yield break;

            string cacheGUID = cache.SceneGUID;
            if (string.IsNullOrEmpty(cacheGUID)) yield break;

            if (cache.IsRuntimeScene)
            {
                if (TryInitDDOLScene() && _ddolSceneGUID == cacheGUID)
                    yield return _ddolScene;
                yield break;
            }

            for (int i = 0; i < FR2_Unity.SceneCount; i++)
            {
                Scene scene = FR2_Unity.GetSceneAt(i);
                if (!scene.IsValid()) continue;
                if (!scene.isLoaded) continue;

                string scenePath = scene.path;
                if (string.IsNullOrEmpty(scenePath)) continue;

                string sceneGUID = FR2_Cache.AssetPathToGUID(scenePath);
                if (sceneGUID == cacheGUID)
                    yield return scene;
            }
        }

        private void OnPrefabStageOpened(PrefabStage prefabStage)
        {
            if (prefabStage == null) return;

            string prefabPath = prefabStage.assetPath;
            if (string.IsNullOrEmpty(prefabPath)) return;

            string prefabGUID = FR2_Cache.AssetPathToGUID(prefabPath);
            if (string.IsNullOrEmpty(prefabGUID)) return;

            FR2_LOG.Log($"FR2_SceneCache: Prefab stage opened for {prefabPath}");

            SceneCache2 cache = GetOrCreateSceneCache(prefabGUID, prefabPath);
            if (cache == null) return;

            cache.SetDirty();

            if (!_autoRefresh) return;

            FR2_LOG.Log($"FR2_SceneCache: Adding prefab to scan queue");
            AddToScanQueue(prefabGUID);

            if (_status != SceneCacheStatus.Scanning)
                ProcessNextSceneInQueue(false);
        }

        private void OnPrefabStageClosing(PrefabStage prefabStage)
        {
            if (prefabStage == null) return;

            GameObject prefabRoot = prefabStage.prefabContentsRoot;
            if (prefabRoot == null) return;

            string prefabPath = prefabStage.assetPath;
            if (string.IsNullOrEmpty(prefabPath)) return;

            string prefabGUID = FR2_Cache.AssetPathToGUID(prefabPath);
            if (string.IsNullOrEmpty(prefabGUID)) return;

            FR2_LOG.Log($"FR2_SceneCache: Prefab stage closing for {prefabRoot.name}");

            if (!_sceneCaches.TryGetValue(prefabGUID, out SceneCache2 cache)) return;

            cache.OnSceneUnloaded();
            _sceneCaches.Remove(prefabGUID);
        }
    }
}
