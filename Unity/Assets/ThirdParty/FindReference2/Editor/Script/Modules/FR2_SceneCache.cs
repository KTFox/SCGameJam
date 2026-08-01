using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace vietlabs.fr2
{
    internal class SceneRefInfo : IEquatable<SceneRefInfo>
    {
        public Component sourceComponent;
        public Object target;
        public string propertyPath;
        public bool isSceneObject;

        public bool IsBackwardRef => target != null && sourceComponent != null;
        public Object GetTargetComponent() => target;

        public GameObject GetGameObjectFromTarget()
        {
            if (ReferenceEquals(target, null)) return null;
            if (target is GameObject targetGO) return targetGO;
            if (target is Component targetComp)
            {
                if (ReferenceEquals(targetComp, null)) return null;
                return targetComp.gameObject;
            }
            return null;
        }

        public bool Equals(SceneRefInfo other)
        {
            if (other == null) return false;
            return sourceComponent == other.sourceComponent &&
                   target == other.target &&
                   propertyPath == other.propertyPath;
        }

        public override bool Equals(object obj) => obj is SceneRefInfo other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = sourceComponent?.GetHashCode() ?? 0;
                hash = hash * 31 + (target?.GetHashCode() ?? 0);
                hash = hash * 31 + (propertyPath?.GetHashCode() ?? 0);
                return hash;
            }
        }
    }

    internal enum SceneCacheStatus
    {
        None,
        Changed,
        Scanning,
        Ready
    }

    [Flags]
    internal enum SceneChangeFlags
    {
        None             = 0,
        PropertyChanged  = 1 << 0,
        Structure        = 1 << 1,
        ComponentAdded   = 1 << 2,
        ComponentRemoved = 1 << 3,
        SceneReset       = 1 << 4,
        UserRefresh      = 1 << 5,
        SceneModify      = PropertyChanged | ComponentAdded | ComponentRemoved,
        SceneAdditive    = Structure
    }

    internal partial class FR2_SceneCache
    {
        private static FR2_SceneCache _api;
        public static Action onReady;

        private SceneCacheStatus _status = SceneCacheStatus.None;
        private static readonly Dictionary<int, string> _instanceIdStringCache = new Dictionary<int, string>(1000);
        private bool _isDirty;
        private SceneChangeFlags _changeFlags = SceneChangeFlags.None;
        private readonly HashSet<int> _modifiedInstanceIds = new HashSet<int>();
        private bool _autoRefresh;
        private float _lastDirtyTime;
        private const float DIRTY_DEBOUNCE_TIME = 0.1f;
        private bool _isFR2WindowFocused;
        private bool _isInvokingOnReady;

        public int current;
        public int total;
        public int totalScenes => _totalScenesInQueue;
        public int scenesCompleted => _scenesCompleted;
        public string currentSceneName { get; private set; }

        public FR2_SceneCache()
        {
            EditorSceneManager.activeSceneChangedInEditMode -= OnSceneChanged;
            EditorSceneManager.activeSceneChangedInEditMode += OnSceneChanged;

            SceneManager.activeSceneChanged -= OnSceneChanged;
            SceneManager.activeSceneChanged += OnSceneChanged;

            SceneManager.sceneLoaded -= OnSceneChanged;
            SceneManager.sceneLoaded += OnSceneChanged;

            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;

            _autoRefresh = FR2_SettingExt.isAutoRefreshEnabled;

            FR2_Cache.onReady -= OnAssetCacheReady;
            FR2_Cache.onReady += OnAssetCacheReady;
        }

        private void DeferredInit()
        {
            if (FR2_Cache.hasCache) OnAssetCacheReady();
        }

        public static bool hasCache => _api != null && (_api._status == SceneCacheStatus.Ready || _api._status == SceneCacheStatus.Changed);

        public bool HasPartialCaches
        {
            get
            {
                if (_sceneCaches == null) return false;
                foreach (var kvp in _sceneCaches)
                {
                    if (kvp.Value != null && kvp.Value.CurrentStatus == SceneCache2.Status.Partial)
                        return true;
                }
                return false;
            }
        }

        public static FR2_SceneCache Api
        {
            get
            {
                if (_api != null) return _api;
                if (!FR2_Cache.hasCache) return null;
                var instance = new FR2_SceneCache();
                _api = instance;
                instance.DeferredInit();
                return _api;
            }
        }

        public static bool isReady => _api != null && _api._status == SceneCacheStatus.Ready;
        public static bool hasInit => _api != null && _api._status != SceneCacheStatus.None;

        public SceneCacheStatus Status
        {
            get => _status;
            set => _status = value;
        }

        public static string GetCachedInstanceIdString(int instanceId)
        {
            if (_instanceIdStringCache.TryGetValue(instanceId, out var cached)) return cached;
            var idString = instanceId.ToString();
            _instanceIdStringCache[instanceId] = idString;
            return idString;
        }

        public bool Dirty
        {
            get => _isDirty;
            set => _isDirty = value;
        }

        public bool AutoRefresh
        {
            get => _autoRefresh;
            set
            {
                if (_autoRefresh == value) return;
                _autoRefresh = value;
                if (_autoRefresh && _isDirty)
                    ScanAllLoadedScenesIncremental();
            }
        }

        public void RefreshCache(bool force)
        {
            if (FR2_SettingExt.disable) return;
            if (!FR2_Cache.hasCache) return;

            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                SetDirty();
                return;
            }

            if (_status == SceneCacheStatus.Scanning)
            {
                return;
            }

            if (force || (_changeFlags & SceneChangeFlags.UserRefresh) != 0)
            {
                RefreshSceneCache2(true);
                return;
            }

            bool isSceneReset = (_changeFlags & SceneChangeFlags.SceneReset) != 0;

            if (!_autoRefresh)
            {
                _isDirty = true;
                _status = SceneCacheStatus.Changed;
                return;
            }

            bool isFirstTimeScan = _status == SceneCacheStatus.None;

            if (_autoRefresh && !_isFR2WindowFocused && _isDirty && !isFirstTimeScan && !isSceneReset && (_status == SceneCacheStatus.Ready || _status == SceneCacheStatus.Changed))
            {
                _status = SceneCacheStatus.Changed;
                return;
            }

            if (isFirstTimeScan)
            {
                RefreshSceneCache2(false);
                return;
            }

            if (isSceneReset)
                FR2_LOG.Log($"refreshCache: proceeding with scene reset refresh (play mode change)");

            bool needsScan = _isDirty || _changeFlags != SceneChangeFlags.None;
            if (!needsScan)
            {
                _status = SceneCacheStatus.Ready;
                return;
            }

            RefreshSceneCache2(false);
        }

        private void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (FR2_SettingExt.disable) return;

            switch (state)
            {
                case PlayModeStateChange.EnteredPlayMode:
                    DoneScanning(SceneCacheStatus.None);
                    if (_autoRefresh)
                    {
                        _changeFlags |= SceneChangeFlags.SceneReset;
                        SetDirty();
                        EditorApplication.delayCall += () => RefreshCache(false);
                    }
                    break;

                case PlayModeStateChange.ExitingPlayMode:
                    DoneScanning(SceneCacheStatus.None);
                    _ddolScene = default;
                    _ddolSceneGUID = null;
                    DropRuntimeSceneCaches();
                    break;

                case PlayModeStateChange.EnteredEditMode:
                    if (_autoRefresh)
                    {
                        _changeFlags |= SceneChangeFlags.SceneReset;
                        SetDirty();
                        EditorApplication.delayCall += () => RefreshCache(false);
                    }
                    break;
            }
        }

        private void OnAssetCacheReady()
        {
            if (FR2_SettingExt.disable) return;
            if (_status != SceneCacheStatus.None) return;
            if (!_autoRefresh) return;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;

            if (!GameObjectExtensions.HasAnyLoadedScene())
            {
                return;
            }

            RefreshCache(false);
        }

        public void ScanGameObjectIfNeeded(GameObject go)
        {
            if (FR2_SettingExt.disable) return;
            if (!go) return;
            if (_status == SceneCacheStatus.Scanning) return;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;

            Scene scene = go.scene;
            if (!scene.IsValid()) return;

            if (TryQueueDDOLSceneForSelection(go))
            {
                return;
            }

            if (!_autoRefresh) return;

            SceneCache2 sceneCache = GetSceneCacheForScene(scene);
            if (sceneCache == null) return;

            if (sceneCache.CurrentStatus == SceneCache2.Status.None)
            {
                AddToScanQueue(sceneCache.SceneGUID);
                if (_status != SceneCacheStatus.Scanning)
                    EditorApplication.delayCall += () => ProcessNextSceneInQueue(false);
                return;
            }

            sceneCache.RescanGameObject(go, scene);
        }

        private void NotifyDisplayRefresh()
        {
        }

        private UndoPropertyModification[] OnModify(UndoPropertyModification[] modifications)
        {
            if (FR2_SettingExt.disable) return modifications;

            bool hasRelevantChange = false;

            for (var i = 0; i < modifications.Length; i++)
            {
                var mod = modifications[i];

                if (mod.currentValue.objectReference != null || mod.previousValue.objectReference != null)
                {
                    var target = mod.currentValue.target ?? mod.previousValue.target;
                    if (!ReferenceEquals(target, null) && !EditorUtility.IsPersistent(target))
                    {
                        hasRelevantChange = true;
                        _modifiedInstanceIds.Add(FR2_Unity.GetInstanceId(target));

                        if (target is Component modComp)
                        {
                            var compGO = modComp.gameObject;
                            if (!ReferenceEquals(compGO, null))
                                _modifiedInstanceIds.Add(FR2_Unity.GetInstanceId(compGO));
                        }
                    }
                }
            }

            if (hasRelevantChange)
            {
                _changeFlags |= SceneChangeFlags.SceneModify;
                SetDirty();
            }

            return modifications;
        }

        private void OnWindowFocusChanged(EditorWindow window)
        {
            if (FR2_SettingExt.disable) return;

            bool isFR2Window = window != null && window.GetType().Name.Contains("FR2_Window");
            _isFR2WindowFocused = isFR2Window;
        }

        public void SetDirty()
        {
            float currentTime = Time.realtimeSinceStartup;
            if (currentTime - _lastDirtyTime < DIRTY_DEBOUNCE_TIME && _isDirty) return;

            _lastDirtyTime = currentTime;

            if (_status == SceneCacheStatus.Ready)
                _status = SceneCacheStatus.Changed;

            _isDirty = true;
            SetSceneCache2Dirty();
        }

        public void ForceRefresh()
        {
            _changeFlags |= SceneChangeFlags.UserRefresh;
            SetDirty();
            RefreshCache(true);
        }

        public int GetGameObjectUsageCount(GameObject go)
        {
            if (ReferenceEquals(go, null)) return 0;
            if (!go) return 0;

            // Use the cache for the GameObject's own scene, not the "context" (selection/active).
            // GetCacheForContext() returns the selected/focused scene's cache, which causes
            // objects in non-focused scenes to get 0 because their scene's cache is never queried.
            SceneCache2 sceneCache = GetSceneCacheForScene(go.scene);
            if (sceneCache == null) return 0;
            if (sceneCache.CurrentStatus != SceneCache2.Status.Ready && sceneCache.CurrentStatus != SceneCache2.Status.Partial) return 0;

            return sceneCache.GetGameObjectUsageCount(go);
        }

        private void DoneScanning(SceneCacheStatus updatedStatus)
        {
            _status = updatedStatus;
            current = 0;
            total = 0;
        }

        internal SceneCache2 GetCacheForContext()
        {
            InitializeSceneCache2System();

            var prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
            if (prefabStage != null)
            {
                string prefabPath = prefabStage.assetPath;
                if (!string.IsNullOrEmpty(prefabPath))
                {
                    string prefabGUID = FR2_Cache.AssetPathToGUID(prefabPath);
                    if (!string.IsNullOrEmpty(prefabGUID))
                        return GetOrCreateSceneCache(prefabGUID, prefabPath);
                }
            }

            if (Selection.activeGameObject != null)
            {
                Scene selectedScene = Selection.activeGameObject.scene;
                if (selectedScene.IsValid())
                    return GetSceneCacheForScene(selectedScene);
            }

            Scene activeScene = FR2_Unity.GetActiveScene();
            if (activeScene.IsValid())
                return GetSceneCacheForScene(activeScene);

            for (int i = 0; i < FR2_Unity.SceneCount; i++)
            {
                Scene scene = FR2_Unity.GetSceneAt(i);
                if (scene.isLoaded) return GetSceneCacheForScene(scene);
            }

            return null;
        }

        private Scene GetTargetSceneForScan()
        {
            var prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
            if (prefabStage != null) return prefabStage.scene;

            if (Selection.activeGameObject != null)
            {
                Scene selectedScene = Selection.activeGameObject.scene;
                if (selectedScene.IsValid()) return selectedScene;
            }

            Scene activeScene = FR2_Unity.GetActiveScene();
            if (activeScene.IsValid()) return activeScene;

            for (int i = 0; i < FR2_Unity.SceneCount; i++)
            {
                Scene scene = FR2_Unity.GetSceneAt(i);
                if (scene.isLoaded) return scene;
            }

            return default;
        }

        private static SceneCache2 GetReadyCacheOrScan(GameObject[] targets, Action<Dictionary<string, FR2_Ref>> onComplete)
        {
            Scene targetScene = default;
            for (int i = 0; i < targets.Length; i++)
            {
                if (!targets[i]) continue;
                var s = targets[i].scene;
                if (s.IsValid()) { targetScene = s; break; }
            }

            SceneCache2 sceneCache = targetScene.IsValid()
                ? Api.GetSceneCacheForScene(targetScene)
                : Api.GetCacheForContext();

            if (sceneCache == null)
            {
                onComplete?.Invoke(new Dictionary<string, FR2_Ref>());
                return null;
            }

            if (sceneCache.CurrentStatus == SceneCache2.Status.Ready || sceneCache.CurrentStatus == SceneCache2.Status.Partial)
                return sceneCache;

            if (!Api._autoRefresh)
            {
                onComplete?.Invoke(new Dictionary<string, FR2_Ref>());
                return null;
            }

            if (sceneCache.CurrentStatus == SceneCache2.Status.Scanning)
                return null;

            if (!targetScene.IsValid()) targetScene = Api.GetTargetSceneForScan();
            if (!targetScene.IsValid())
            {
                onComplete?.Invoke(new Dictionary<string, FR2_Ref>());
                return null;
            }

            string sceneGUID = sceneCache.SceneGUID;
            Api.AddToScanQueue(sceneGUID);
            if (Api._status != SceneCacheStatus.Scanning)
                Api.ProcessNextSceneInQueue(false);
            return null;
        }

        private static Dictionary<string, FR2_Ref> BuildTargetResults(GameObject[] targets)
        {
            var results = new Dictionary<string, FR2_Ref>(targets.Length * 10);
            for (int i = 0; i < targets.Length; i++)
            {
                var go = targets[i];
                if (!go || go.IsAssetObject()) continue;
                var key = GetCachedInstanceIdString(FR2_Unity.GetInstanceId(go));
                if (!results.ContainsKey(key))
                    results.Add(key, new FR2_SceneRef(0, go));
            }
            return results;
        }

        public static void FindSceneUseSceneObjectsAsync(GameObject[] targets, Action<Dictionary<string, FR2_Ref>> onComplete)
        {
            if (targets == null || targets.Length == 0) { onComplete?.Invoke(new Dictionary<string, FR2_Ref>()); return; }

            var cache = GetReadyCacheOrScan(targets, onComplete);
            if (cache != null) ScanForwardReferencesAsync(targets, cache, BuildTargetResults(targets), onComplete);
        }

        public static void FindSceneBackwardReferencesAsync(GameObject[] targets, Action<Dictionary<string, FR2_Ref>> onComplete)
        {
            if (targets == null || targets.Length == 0) { onComplete?.Invoke(new Dictionary<string, FR2_Ref>()); return; }

            var cache = GetReadyCacheOrScan(targets, onComplete);
            if (cache != null) ScanBackwardReferencesAsync(targets, cache, BuildTargetResults(targets), onComplete);
        }

        public static void FindSceneInSceneAsync(GameObject[] targets, Action<Dictionary<string, FR2_Ref>> onComplete)
        {
            if (targets == null || targets.Length == 0) { onComplete?.Invoke(new Dictionary<string, FR2_Ref>()); return; }

            var cache = GetReadyCacheOrScan(targets, onComplete);
            if (cache != null) ScanSceneInSceneAsync(targets, cache, BuildTargetResults(targets), onComplete);
        }

        private static void ScanForwardReferencesAsync(GameObject[] targets, SceneCache2 sceneCache, Dictionary<string, FR2_Ref> results, Action<Dictionary<string, FR2_Ref>> onComplete)
        {
            if (targets == null || targets.Length == 0) { onComplete?.Invoke(results); return; }

            var timeSlice = new FR2_TimeSlice(
                () => targets.Length,
                (idx) => {
                    var selectedGO = targets[idx];
                    if (ReferenceEquals(selectedGO, null)) return;

                    var refInfos = sceneCache.GetReferencesFrom(selectedGO);
                    var selectedGOInstanceId = FR2_Unity.GetInstanceId(selectedGO);

                    foreach (var refInfo in refInfos)
                    {
                        if (!refInfo.isSceneObject) continue;

                        var target = refInfo.target;
                        if (ReferenceEquals(target, null)) continue;

                        var targetGO = target.GetGameObjectFromTarget();
                        if (ReferenceEquals(targetGO, null)) continue;
                        if (FR2_Unity.GetInstanceId(targetGO) == selectedGOInstanceId) continue;

                        var targetKey = GetCachedInstanceIdString(FR2_Unity.GetInstanceId(target));
                        if (!results.ContainsKey(targetKey))
                            results.Add(targetKey, new FR2_SceneRef(1, target) { sourceRefs = new List<SceneRefInfo>() });

                        var targetRef = results[targetKey] as FR2_SceneRef;
                        targetRef.sourceRefs.Add(refInfo);
                        targetRef.MarkGroupingDirty();
                    }
                },
                () => onComplete?.Invoke(results)
            );
            timeSlice.jobName = "ForwardRefs";

            timeSlice.Start();
        }

        private static void ScanBackwardReferencesAsync(GameObject[] targets, SceneCache2 sceneCache, Dictionary<string, FR2_Ref> results, Action<Dictionary<string, FR2_Ref>> onComplete)
        {
            if (targets == null || targets.Length == 0) { onComplete?.Invoke(results); return; }

            var targetSet = new HashSet<GameObject>();
            for (int i = 0; i < targets.Length; i++)
            {
                var go = targets[i];
                if (!ReferenceEquals(go, null) && go) targetSet.Add(go);
            }

            if (targetSet.Count == 0) { onComplete?.Invoke(results); return; }

            var backwardRefsDict = sceneCache.GetUsedByReferences(targets);
            var allTargets = new List<GameObject>(backwardRefsDict.Keys);

            var timeSlice = new FR2_TimeSlice(
                () => allTargets.Count,
                (idx) => {
                    var target = allTargets[idx];
                    if (!backwardRefsDict.TryGetValue(target, out var refInfos)) return;

                    foreach (var refInfo in refInfos)
                    {
                        var sourceComp = refInfo.sourceComponent;
                        if (ReferenceEquals(sourceComp, null)) continue;

                        var sourceGO = sourceComp.gameObject;
                        if (ReferenceEquals(sourceGO, null)) continue;
                        if (targetSet.Contains(sourceGO)) continue;

                        var sourceKey = GetCachedInstanceIdString(FR2_Unity.GetInstanceId(sourceGO));
                        if (!results.ContainsKey(sourceKey))
                            results.Add(sourceKey, new FR2_SceneRef(1, sourceGO) { backwardRefs = new List<SceneRefInfo>() });

                        var backwardRef = results[sourceKey] as FR2_SceneRef;
                        backwardRef.backwardRefs.Add(refInfo);
                        backwardRef.MarkGroupingDirty();
                    }
                },
                () => onComplete?.Invoke(results)
            );
            timeSlice.jobName = "BackwardRefs";

            timeSlice.Start();
        }

        private static void ScanSceneInSceneAsync(GameObject[] objs, SceneCache2 sceneCache, Dictionary<string, FR2_Ref> results, Action<Dictionary<string, FR2_Ref>> onComplete)
        {
            if (objs == null || objs.Length == 0) { onComplete?.Invoke(results); return; }

            var targetComponents = new HashSet<Component>();
            var targetGameObjects = new HashSet<GameObject>();

            for (int i = 0; i < objs.Length; i++)
            {
                var go = objs[i];
                if (ReferenceEquals(go, null) || !go) continue;

                targetGameObjects.Add(go);
                var components = go.GetComponents<Component>();
                for (int j = 0; j < components.Length; j++)
                {
                    var comp = components[j];
                    if (!ReferenceEquals(comp, null)) targetComponents.Add(comp);
                }
            }

            if (targetGameObjects.Count == 0) { onComplete?.Invoke(results); return; }

            var backwardRefsDict = sceneCache.GetUsedByReferences(targetGameObjects.ToArray());
            var allTargets = new List<GameObject>(backwardRefsDict.Keys);

            var timeSlice = new FR2_TimeSlice(
                () => allTargets.Count,
                (idx) => {
                    var target = allTargets[idx];
                    if (!backwardRefsDict.TryGetValue(target, out var refInfos)) return;

                    foreach (var refInfo in refInfos)
                    {
                        var sourceComp = refInfo.sourceComponent;
                        if (ReferenceEquals(sourceComp, null)) continue;
                        if (targetComponents.Contains(sourceComp)) continue;

                        var key = GetCachedInstanceIdString(FR2_Unity.GetInstanceId(sourceComp));
                        if (!results.ContainsKey(key))
                            results.Add(key, new FR2_SceneRef(1, sourceComp) { backwardRefs = new List<SceneRefInfo>() });

                        var backwardRef = results[key] as FR2_SceneRef;
                        backwardRef.backwardRefs.Add(refInfo);
                        backwardRef.MarkGroupingDirty();
                    }
                },
                () => onComplete?.Invoke(results)
            );
            timeSlice.jobName = "SceneInScene";

            timeSlice.Start();
        }

        internal class HashValue : IEquatable<HashValue>
        {
            public bool isSceneObject;
            public int targetInstanceId;
            public int targetGameObjectInstanceId;
            public string propertyPath;

            public Object GetTarget()
            {
                return FR2_Unity.InstanceIdToObject(targetInstanceId);
            }

            public bool Equals(HashValue other)
            {
                if (ReferenceEquals(other, null)) return false;
                if (ReferenceEquals(this, other)) return true;
                return isSceneObject == other.isSceneObject &&
                    targetInstanceId == other.targetInstanceId &&
                    propertyPath == other.propertyPath;
            }

            public override bool Equals(object obj) => Equals(obj as HashValue);

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = 17;
                    hash = hash * 23 + isSceneObject.GetHashCode();
                    hash = hash * 23 + targetInstanceId;
                    hash = hash * 23 + (propertyPath?.GetHashCode() ?? 0);
                    return hash;
                }
            }

            public static bool operator ==(HashValue a, HashValue b) => Equals(a, b);
            public static bool operator !=(HashValue a, HashValue b) => !Equals(a, b);
        }

        private void OnSceneChanged(Scene scene, LoadSceneMode mode)
        {
            if (FR2_SettingExt.disable) return;
            _changeFlags |= SceneChangeFlags.SceneReset;
            SetDirty();
        }

        private void OnSceneChanged(Scene arg0, Scene arg1)
        {
            if (FR2_SettingExt.disable) return;
            if (string.IsNullOrEmpty(arg0.path)) { SetDirty(); return; }

            if (_status == SceneCacheStatus.Scanning)
            {
                StopAllScans();
                _scanQueue.Clear();
                _status = SceneCacheStatus.Changed;
            }

            _changeFlags |= SceneChangeFlags.SceneReset;
            SetDirty();
        }

        private void StopAllScans()
        {
            if (_sceneCaches == null) return;
            foreach (var cache in _sceneCaches.Values)
            {
                if (cache.CurrentStatus == SceneCache2.Status.Scanning)
                    cache.StopScan();
            }
        }
    }
}
