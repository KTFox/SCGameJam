using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

using UnityObject = UnityEngine.Object;

namespace vietlabs.fr2
{
    internal partial class SceneCache2
    {
        private Scene _currentScene;
        private float _lastCheckpointTime;
        private const float CHECKPOINT_INTERVAL = 5f;

        public void ScanNewObjects(HashSet<int> instanceIds, Scene scene, Action onComplete = null)
        {
            if (!scene.IsValid()) return;
            if (!scene.isLoaded) return;
            if (instanceIds.Count == 0) { onComplete?.Invoke(); return; }
            if (CurrentStatus == Status.Scanning) { SetDirty(); return; }

            _currentScene = scene;
            _pendingScanList.Clear();

            foreach (int id in instanceIds)
            {
                var go = FR2_Unity.InstanceIdToObject(id) as GameObject;
                if (!go) continue;
                if (go.scene != scene) continue;
                CollectNewRecursive(go, _pendingScanList);
            }

            if (_pendingScanList.Count == 0) { onComplete?.Invoke(); return; }

            _onScanComplete = onComplete;
            CurrentStatus = Status.Scanning;

            FR2_LOG.Log($"SceneCache2: ScanNewObjects — {_pendingScanList.Count} new GameObjects in {scene.name}");

            _lastCheckpointTime = Time.realtimeSinceStartup;
            _scanTimeSlice = new FR2_TimeSlice(
                countFunc: () => _pendingScanList.Count,
                action: (index) =>
                {
                    ScanGameObject(_pendingScanList[index]);
                    TryCheckpointSave(index);
                },
                onComplete: () => FinalizeScanNewObjects(),
                onProgress: (current, total) => OnScanProgress?.Invoke(current, total)
            );
            _scanTimeSlice.jobName = $"ScanNew:{scene.name}";
            _scanTimeSlice.Start();
        }

        private void CollectNewRecursive(GameObject go, List<GameObject> result)
        {
            if (!go) return;
            result.Add(go);

            var transform = go.transform;
            for (int i = 0; i < transform.childCount; i++)
            {
                var child = transform.GetChild(i);
                if (child) CollectNewRecursive(child.gameObject, result);
            }
        }

        private void FinalizeScanNewObjects()
        {
            BuildInvertedIndex();
            BuildUsageCountMap();
            SaveToCache();
            DisposeSerializedObjectCache();

            CurrentStatus = Status.Ready;
            _pendingScanList.Clear();

            _onScanComplete?.Invoke();
            _onScanComplete = null;
        }

        internal void ScanGameObject(GameObject go, Scene scene = default)
        {
            if (!go) return;

            ulong goID = GetID(go);
            if (goID == 0) return;
            if (!_scannedGOIDs.Add(goID)) return;

            bool isPrefabInstance = PrefabUtility.IsPartOfPrefabInstance(go);

            if (isPrefabInstance)
                ScanPrefabInstance(go, goID);
            else
                ScanNonPrefabGameObject(go, goID);
        }

        private void ScanNonPrefabGameObject(GameObject go, ulong goID)
        {
            string prefabGUID = GetPrefabGUID(go);
            go.GetComponents(_reusableComponentList);
            for (int i = 0; i < _reusableComponentList.Count; i++)
            {
                if (!_reusableComponentList[i]) continue;
                ScanComponent(_reusableComponentList[i], goID, prefabGUID);
            }
        }

        private void ScanPrefabInstance(GameObject go, ulong goID)
        {
            if (PrefabUtility.IsAnyPrefabInstanceRoot(go))
            {
                var sourcePrefab = PrefabUtility.GetCorrespondingObjectFromOriginalSource(go);
                if (sourcePrefab != null &&
                    FR2_Cache.TryGetGUIDAndLocalFileIdentifier(sourcePrefab, out string sourcePrefabGUID, out long _) &&
                    !string.IsNullOrEmpty(sourcePrefabGUID))
                {
                    bool isPrefabStage = !string.IsNullOrEmpty(ScenePath) && ScenePath.EndsWith(".prefab");
                    if (!isPrefabStage || sourcePrefabGUID != SceneGUID)
                    {
                        if (!_goIDs.TryGetValue(goID, out GOCacheEntry entry))
                        {
                            entry = new GOCacheEntry(goID);
                            _goIDs[goID] = entry;
                        }
                        entry.sourcePrefabGUID = sourcePrefabGUID;

                        var immediatePrefab = PrefabUtility.GetCorrespondingObjectFromSource(go);
                        if (immediatePrefab != null && immediatePrefab != sourcePrefab)
                        {
                            if (FR2_Cache.TryGetGUIDAndLocalFileIdentifier(immediatePrefab, out string variantGUID, out long _2)
                                && !string.IsNullOrEmpty(variantGUID) && variantGUID != sourcePrefabGUID)
                            {
                                entry.variantPrefabGUID = variantGUID;
                            }
                        }
                    }
                }
            }

            string prefabGUID = PrefabUtility.IsPartOfPrefabInstance(go) ? GetContainingPrefabGUID(go) : GetPrefabGUID(go);
            go.GetComponents(_reusableComponentList);
            for (int i = 0; i < _reusableComponentList.Count; i++)
            {
                var comp = _reusableComponentList[i];
                if (!comp) continue;
                if (comp is Transform) continue;
                if (comp is ParticleSystem) continue;
                if (comp is ParticleSystemRenderer) continue;
                ScanComponent(comp, goID, prefabGUID);
            }
        }

        private void ScanPrefabOverrides(GameObject go, ulong goID)
        {
            if (!PrefabUtility.HasPrefabInstanceAnyOverrides(go, false)) return;

            var mods = PrefabUtility.GetPropertyModifications(go);
            if (mods == null || mods.Length == 0) return;

            go.GetComponents(_reusableComponentList);
            for (int i = 0; i < _reusableComponentList.Count; i++)
            {
                var comp = _reusableComponentList[i];
                if (!comp) continue;
                if (comp is Transform) continue;
                if (comp is ParticleSystem) continue;
                if (comp is ParticleSystemRenderer) continue;

                ScanOverriddenComponent(comp, goID, mods);
            }
        }

        private string GetContainingPrefabGUID(GameObject go)
        {
            var outermost = PrefabUtility.GetOutermostPrefabInstanceRoot(go);
            if (!outermost) return null;
            var source = PrefabUtility.GetCorrespondingObjectFromOriginalSource(outermost);
            if (!source) return null;
            if (!FR2_Cache.TryGetGUIDAndLocalFileIdentifier(source, out string guid, out _)) return null;
            return guid;
        }

        private void ScanOverriddenComponent(Component comp, ulong goID, PropertyModification[] mods)
        {
            ulong compID = GetID(comp);
            if (compID == 0) return;

            bool hasOverrideOnThisComp = false;
            for (int i = 0; i < mods.Length; i++)
            {
                if (mods[i].target == comp) { hasOverrideOnThisComp = true; break; }
            }
            if (!hasOverrideOnThisComp) return;

            SerializedObject serialized = GetOrCreateSerializedObject(comp);
            if (serialized == null) return;

            CompRefs compRefs = null;
            SerializedProperty it = serialized.GetIterator();
            bool enterChildren = true;

            while (it.Next(enterChildren))
            {
                enterChildren = it.propertyType == SerializedPropertyType.Generic;

                if (it.propertyType == SerializedPropertyType.ManagedReference)
                {
                    if (it.prefabOverride)
                        ScanManagedReference(it, comp, goID, null, ref compRefs);
                    enterChildren = false;
                    continue;
                }

                if (it.propertyType != SerializedPropertyType.ObjectReference) continue;
                if (!it.prefabOverride) continue;

                CollectObjectReference(it, comp, null, ref compRefs);
            }

            if (compRefs == null || !compRefs.HasReferences) return;

            if (!_goIDs.TryGetValue(goID, out GOCacheEntry entry))
            {
                entry = new GOCacheEntry(goID);
                _goIDs[goID] = entry;
            }

            entry.compsWithRefs[compID] = compRefs;
        }

        private void ScanComponent(Component comp, ulong goID, string prefabGUID)
        {
            if (!comp) return;
            if (comp is Transform) return;
            if (comp is ParticleSystem) return;
            if (comp is ParticleSystemRenderer) return;

            ulong compID = GetID(comp);
            if (compID == 0) return;

            SerializedObject serialized = GetOrCreateSerializedObject(comp);
            if (serialized == null) return;

            CompRefs compRefs = null;
            SerializedProperty it = serialized.GetIterator();
            bool enterChildren = true;

            while (it.Next(enterChildren))
            {
                enterChildren = it.propertyType == SerializedPropertyType.Generic;

                if (it.propertyType == SerializedPropertyType.ManagedReference)
                {
                    ScanManagedReference(it, comp, goID, prefabGUID, ref compRefs);
                    enterChildren = false;
                    continue;
                }

                if (it.propertyType != SerializedPropertyType.ObjectReference) continue;

                CollectObjectReference(it, comp, prefabGUID, ref compRefs);
            }

            if (compRefs == null || !compRefs.HasReferences) return;

            if (!_goIDs.TryGetValue(goID, out GOCacheEntry entry))
            {
                entry = new GOCacheEntry(goID);
                _goIDs[goID] = entry;
            }

            entry.compsWithRefs[compID] = compRefs;
        }

        private static readonly HashSet<long> _visitedManagedRefs = new HashSet<long>();

        private void ScanManagedReference(SerializedProperty managedRefProp, Component comp, ulong goID, string prefabGUID, ref CompRefs compRefs)
        {
            long refId = managedRefProp.managedReferenceId;
            if (refId == 0) return;

            _visitedManagedRefs.Clear();
            _visitedManagedRefs.Add(refId);

            var end = managedRefProp.GetEndProperty();
            var it = managedRefProp.Copy();
            bool enterChildren = true;

            while (it.Next(enterChildren) && !SerializedProperty.EqualContents(it, end))
            {
                if (it.propertyType == SerializedPropertyType.ManagedReference)
                {
                    long nestedId = it.managedReferenceId;
                    if (nestedId == 0 || !_visitedManagedRefs.Add(nestedId))
                    {
                        enterChildren = false;
                        continue;
                    }
                    enterChildren = true;
                    continue;
                }

                enterChildren = it.propertyType == SerializedPropertyType.Generic;

                if (it.propertyType != SerializedPropertyType.ObjectReference) continue;

                CollectObjectReference(it, comp, prefabGUID, ref compRefs);
            }
        }

        private void CollectObjectReference(SerializedProperty prop, Component comp, string prefabGUID, ref CompRefs compRefs)
        {
            var refValue = prop.objectReferenceValue;
            if (!refValue) return;

            string propPath = prop.propertyPath;
            if (propPath == "m_CorrespondingSourceObject" || propPath == "m_PrefabInstance" || propPath == "m_PrefabAsset") return;

            if (refValue is Transform targetTransform)
            {
                Transform sourceTransform = comp.gameObject.transform;
                if (targetTransform.parent == sourceTransform) return;
                if (sourceTransform.parent == targetTransform) return;
            }

            bool isSceneObject = refValue.IsSceneObject();
            
            if (!isSceneObject && PrefabUtility.IsPartOfPrefabInstance(comp))
            {
                var instanceRef = PrefabUtility.GetCorrespondingObjectFromSource(refValue) == refValue
                    ? GetInstanceInScene(comp, refValue)
                    : null;
                if (instanceRef != null)
                {
                    refValue = instanceRef;
                    isSceneObject = true;
                }
            }

            if (!ShouldIncludeReference(comp, refValue, isSceneObject, prefabGUID)) return;

            if (compRefs == null) compRefs = new CompRefs();

            if (isSceneObject)
            {
                ulong targetID = GetID(refValue);
                if (targetID != 0) compRefs.sceneRefs.Add(new SceneObjRef(targetID, propPath));
            }
            else
            {
                if (FR2_Cache.TryGetGUIDAndLocalFileIdentifier(refValue, out string guid, out long localId))
                {
                    if (!string.IsNullOrEmpty(guid))
                        compRefs.assets.Add(new AssetRef(guid, (ulong)localId, propPath));
                }
            }
        }

        private static UnityObject GetInstanceInScene(Component sourceComp, UnityObject prefabObject)
        {
            var root = PrefabUtility.GetNearestPrefabInstanceRoot(sourceComp);
            if (root == null) return null;

            if (prefabObject is Component prefabComp)
                return PrefabUtility.GetCorrespondingObjectFromOriginalSource(prefabComp) == prefabComp
                    ? FindMatchingInstance<Component>(root, prefabComp)
                    : null;

            if (prefabObject is GameObject prefabGO)
                return PrefabUtility.GetCorrespondingObjectFromOriginalSource(prefabGO) == prefabGO
                    ? FindMatchingInstance<GameObject>(root, prefabGO)
                    : null;

            return null;
        }

        private static T FindMatchingInstance<T>(GameObject instanceRoot, T prefabObj) where T : UnityObject
        {
            if (prefabObj is Component prefabComp)
            {
                var allComps = instanceRoot.GetComponentsInChildren(prefabComp.GetType(), true);
                for (int i = 0; i < allComps.Length; i++)
                {
                    if (PrefabUtility.GetCorrespondingObjectFromOriginalSource(allComps[i]) == prefabObj)
                        return allComps[i] as T;
                }
            }
            else if (prefabObj is GameObject prefabGO)
            {
                var allTransforms = instanceRoot.GetComponentsInChildren<Transform>(true);
                for (int i = 0; i < allTransforms.Length; i++)
                {
                    var go = allTransforms[i].gameObject;
                    if (PrefabUtility.GetCorrespondingObjectFromOriginalSource(go) == prefabGO)
                        return go as T;
                }
            }
            return null;
        }

        private bool ShouldIncludeReference(Component source, UnityObject target, bool isSceneObject, string prefabGUID)
        {
            if (!target) return false;

            if (isSceneObject)
            {
                ulong targetID = GetID(target);
                if (targetID == 0) return false;

                GameObject targetGO = target.GetGameObjectFromTarget();
                if (targetGO != null && _currentScene.IsValid() && targetGO.scene != _currentScene)
                {
                    bool isDDOL = string.IsNullOrEmpty(targetGO.scene.path) && targetGO.scene.IsValid();
                    if (!isDDOL) return false;
                }

                if (_excludeSelfRef && source != null && source.gameObject == targetGO) return false;
            }
            else
            {
                if (!string.IsNullOrEmpty(prefabGUID) &&
                    FR2_Cache.TryGetGUIDAndLocalFileIdentifier(target, out string referencedGUID, out _) &&
                    referencedGUID == prefabGUID)
                    return false;
            }

            return true;
        }

        private string GetPrefabGUID(GameObject go)
        {
            if (!go) return null;
            if (!string.IsNullOrEmpty(ScenePath) && ScenePath.EndsWith(".prefab"))
                return SceneGUID;
            return null;
        }

        private SerializedObject GetOrCreateSerializedObject(Component comp)
        {
            if (!comp) return null;

            if (_serializedObjectCache.TryGetValue(comp, out SerializedObject cached))
                return cached;

            var serialized = new SerializedObject(comp);
            _serializedObjectCache[comp] = serialized;
            return serialized;
        }

        public void ScanIncremental(Scene scene, Action onComplete = null)
        {
            if (!scene.IsValid()) return;
            if (!scene.isLoaded) return;

            _currentScene = scene;
            _onScanComplete = onComplete;
            CurrentStatus = Status.Scanning;

            _pendingScanList.Clear();
            CollectGameObjects(scene, _pendingScanList, true);

            if (_pendingScanList.Count == 0)
            {
                FinalizeScan();
                return;
            }

            FR2_LOG.Log($"SceneCache2: Starting incremental scan of {_pendingScanList.Count} GameObjects in {scene.name}");

            _lastCheckpointTime = Time.realtimeSinceStartup;
            _scanTimeSlice = new FR2_TimeSlice(
                countFunc: () => _pendingScanList.Count,
                action: (index) =>
                {
                    ScanGameObject(_pendingScanList[index]);
                    TryCheckpointSave(index);
                },
                onComplete: () => FinalizeScan(),
                onProgress: (current, total) => OnScanProgress?.Invoke(current, total)
            );
            _scanTimeSlice.jobName = $"IncrementalScan:{scene.name}";
            _scanTimeSlice.Start();
        }

        public void ScanFull(Scene scene, Action onComplete = null)
        {
            if (!scene.IsValid()) return;
            if (!scene.isLoaded) return;

            _currentScene = scene;
            _onScanComplete = onComplete;

            _goIDs.Clear();
            _scannedGOIDs.Clear();
            DisposeSerializedObjectCache();

            CurrentStatus = Status.Scanning;

            _pendingScanList.Clear();
            CollectGameObjects(scene, _pendingScanList, false);

            if (_pendingScanList.Count == 0)
            {
                FinalizeScan();
                return;
            }

            FR2_LOG.Log($"SceneCache2: Starting full scan of {_pendingScanList.Count} GameObjects in {scene.name}");

            _lastCheckpointTime = Time.realtimeSinceStartup;
            _scanTimeSlice = new FR2_TimeSlice(
                countFunc: () => _pendingScanList.Count,
                action: (index) =>
                {
                    ScanGameObject(_pendingScanList[index]);
                    TryCheckpointSave(index);
                },
                onComplete: () => FinalizeScan(),
                onProgress: (current, total) => OnScanProgress?.Invoke(current, total)
            );
            _scanTimeSlice.jobName = $"FullScan:{scene.name}";
            _scanTimeSlice.Start();
        }

        private void TryCheckpointSave(int currentIndex)
        {
            float now = Time.realtimeSinceStartup;
            if (now - _lastCheckpointTime < CHECKPOINT_INTERVAL) return;

            _lastCheckpointTime = now;
            SaveToCache(partial: true);
            FR2_LOG.Log($"SceneCache2: Checkpoint save at {currentIndex + 1}/{_pendingScanList.Count} for {ScenePath}");
        }

        private void CollectGameObjects(Scene scene, List<GameObject> result, bool unscanedOnly)
        {
            var rootObjects = scene.GetRootGameObjects();
            for (int i = 0; i < rootObjects.Length; i++)
            {
                if (!rootObjects[i]) continue;
                CollectRecursive(rootObjects[i], result, unscanedOnly);
            }
        }

        private void CollectRecursive(GameObject go, List<GameObject> result, bool unscanedOnly)
        {
            if (!go) return;

            if (!unscanedOnly)
            {
                result.Add(go);
            }
            else
            {
                ulong goID = GetID(go);
                if (goID != 0 && !_scannedGOIDs.Contains(goID))
                    result.Add(go);
            }

            var transform = go.transform;
            for (int i = 0; i < transform.childCount; i++)
            {
                var child = transform.GetChild(i);
                if (child) CollectRecursive(child.gameObject, result, unscanedOnly);
            }
        }

        private void FinalizeScan()
        {
            FR2_LOG.Log($"SceneCache2: Finalizing scan for {ScenePath} — scannedGOs={_scannedGOIDs.Count}, goIDs={_goIDs.Count}");

            if (_currentScene.IsValid() && _currentScene.isLoaded)
                BuildRuntimeMaps(_currentScene);

            BuildInvertedIndex();
            BuildUsageCountMap();
            
            FR2_LOG.Log($"SceneCache2: After maps — usedByMap={_usedByMap.Count}, goUsedByCount={_goUsedByCount.Count}");
            
            SaveToCache();
            
            // Release native SerializedObject allocations — no longer needed after scan
            DisposeSerializedObjectCache();

            CurrentStatus = Status.Ready;
            _pendingScanList.Clear();

            _onScanComplete?.Invoke();
            _onScanComplete = null;
        }

        private void DisposeSerializedObjectCache()
        {
            foreach (var kvp in _serializedObjectCache)
                kvp.Value?.Dispose();
            _serializedObjectCache.Clear();
        }

        public void StopScan()
        {
            if (CurrentStatus == Status.Scanning && _scannedGOIDs.Count > 0)
                SaveToCache(partial: true);

            _scanTimeSlice?.Stop();
            _scanTimeSlice = null;
            _pendingScanList.Clear();
            _onScanComplete = null;

            if (CurrentStatus == Status.Scanning)
                CurrentStatus = Status.Dirty;
        }

        public void ScanIncrementalSync(Scene scene)
        {
            if (!scene.IsValid()) return;
            if (!scene.isLoaded) return;
            if (CurrentStatus == Status.Scanning) return;

            _currentScene = scene;

            int rootCount = scene.rootCount;
            bool rootCountChanged = rootCount != _lastKnownRootCount;
            _lastKnownRootCount = rootCount;

            if (!rootCountChanged) return;

            _pendingScanList.Clear();
            CollectGameObjects(scene, _pendingScanList, true);
            if (_pendingScanList.Count == 0) return;

            FR2_LOG.Log($"SceneCache2.ScanIncrementalSync: {_pendingScanList.Count} new objects in {scene.name}");

            for (int i = 0; i < _pendingScanList.Count; i++)
                ScanGameObject(_pendingScanList[i], scene);

            _pendingScanList.Clear();
            RebuildMaps();
        }

        public void RescanGameObject(GameObject go, Scene scene)
        {
            if (!go) return;

            ulong goID = GetID(go);
            if (goID == 0) return;
            if (_scannedGOIDs.Contains(goID)) return;

            _currentScene = scene;

            _scannedGOIDs.Add(goID);
            DisposeSerializedObjectForGO(go);

            string prefabGUID = PrefabUtility.IsPartOfPrefabInstance(go) ? GetContainingPrefabGUID(go) : GetPrefabGUID(go);

            go.GetComponents(_reusableComponentList);
            for (int i = 0; i < _reusableComponentList.Count; i++)
            {
                var comp = _reusableComponentList[i];
                if (!comp) continue;
                if (comp is Transform) continue;
                if (comp is ParticleSystem) continue;
                if (comp is ParticleSystemRenderer) continue;
                ScanComponent(comp, goID, prefabGUID);
            }

            if (PrefabUtility.IsAnyPrefabInstanceRoot(go))
            {
                var sourcePrefab = PrefabUtility.GetCorrespondingObjectFromOriginalSource(go);
                if (sourcePrefab != null &&
                    FR2_Cache.TryGetGUIDAndLocalFileIdentifier(sourcePrefab, out string sourcePrefabGUID, out long _) &&
                    !string.IsNullOrEmpty(sourcePrefabGUID))
                {
                    if (!_goIDs.TryGetValue(goID, out GOCacheEntry entry))
                    {
                        entry = new GOCacheEntry(goID);
                        _goIDs[goID] = entry;
                    }
                    entry.sourcePrefabGUID = sourcePrefabGUID;
                }
            }

            BuildInvertedIndex();
            BuildUsageCountMap();
            FR2_LOG.Log($"SceneCache2: RescanGameObject done — goIDs={_goIDs.Count}, usedByMap={_usedByMap.Count}, goUsedByCount={_goUsedByCount.Count}");
        }

        private void DisposeSerializedObjectForGO(GameObject go)
        {
            go.GetComponents(_reusableComponentList);
            for (int i = 0; i < _reusableComponentList.Count; i++)
            {
                var comp = _reusableComponentList[i];
                if (!comp) continue;
                if (_serializedObjectCache.TryGetValue(comp, out var so))
                {
                    so?.Dispose();
                    _serializedObjectCache.Remove(comp);
                }
            }
        }

        public void ScanGameObjectRecursive(GameObject go, Scene scene)
        {
            if (!go) return;
            _currentScene = scene;
            ScanRecursive(go);
        }

        private void ScanRecursive(GameObject go)
        {
            if (!go) return;
            ScanGameObject(go);
            var t = go.transform;
            for (int i = 0; i < t.childCount; i++)
            {
                var child = t.GetChild(i);
                if (child) ScanRecursive(child.gameObject);
            }
        }
    }
}
