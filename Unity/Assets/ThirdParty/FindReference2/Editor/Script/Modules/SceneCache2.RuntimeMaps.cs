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
        private static List<UnityObject> _batchObjects;
        private static List<UnityObject> _skippedObjects;
        private static GlobalObjectId[] _batchGlobalIds;
        private static UnityObject[] _batchBuffer;
        private static readonly Dictionary<Type, bool> _managedRefTypeCache = new Dictionary<Type, bool>(64);

        private const int BATCH_SIZE = 1024;
        private const ulong RUNTIME_ID_FLAG = 0x8000_0000_0000_0000UL;

        private static ulong RuntimeID(UnityObject obj)
        {
            int instanceId = FR2_Unity.GetInstanceId(obj);
            if (instanceId == 0) return 0;
            return RUNTIME_ID_FLAG | (uint)instanceId;
        }

        public void BuildRuntimeMaps(Scene scene)
        {
            if (FR2_SettingExt.disable) return;
            if (!scene.IsValid()) return;
            if (!scene.isLoaded) return;

            _objToID.Clear();
            _idToObj.Clear();

            if (_batchObjects == null) _batchObjects = new List<UnityObject>(1024);
            if (_skippedObjects == null) _skippedObjects = new List<UnityObject>(32);
            _batchObjects.Clear();
            _skippedObjects.Clear();

            CollectSceneObjects(scene, _batchObjects);

            int totalCount = _batchObjects.Count;
            if (totalCount == 0) return;

            FilterManagedReferenceObjects(_batchObjects, _skippedObjects);
            totalCount = _batchObjects.Count;

            if (_batchBuffer == null) _batchBuffer = new UnityObject[BATCH_SIZE];

            if (_batchGlobalIds == null || _batchGlobalIds.Length < BATCH_SIZE)
                _batchGlobalIds = new GlobalObjectId[BATCH_SIZE];

            for (int offset = 0; offset < totalCount; offset += BATCH_SIZE)
            {
                int count = Mathf.Min(BATCH_SIZE, totalCount - offset);

                for (int i = 0; i < count; i++)
                    _batchBuffer[i] = _batchObjects[offset + i];
                for (int i = count; i < BATCH_SIZE; i++)
                    _batchBuffer[i] = null;

                GlobalObjectId.GetGlobalObjectIdsSlow(_batchBuffer, _batchGlobalIds);

                for (int i = 0; i < count; i++)
                {
                    var obj = _batchObjects[offset + i];
                    if (ReferenceEquals(obj, null)) continue;
                    if (!obj) continue;

                    var globalId = _batchGlobalIds[i];
                    ulong id = globalId.identifierType != 0
                        ? ConvertToUlong(globalId)
                        : RuntimeID(obj);

                    if (id == 0) continue;

                    _objToID[obj] = id;
                    _idToObj[id] = obj;
                }
            }

            for (int i = 0; i < _skippedObjects.Count; i++)
            {
                var obj = _skippedObjects[i];
                if (ReferenceEquals(obj, null)) continue;
                if (!obj) continue;

                ulong id = RuntimeID(obj);
                if (id == 0) continue;

                _objToID[obj] = id;
                _idToObj[id] = obj;
            }
        }

        private void CollectSceneObjects(Scene scene, List<UnityObject> objects)
        {
            var rootObjects = scene.GetRootGameObjects();
            for (int i = 0; i < rootObjects.Length; i++)
            {
                var root = rootObjects[i];
                if (ReferenceEquals(root, null)) continue;
                if (!root) continue;
                CollectGameObjectRecursive(root, objects);
            }
        }

        private void CollectGameObjectRecursive(GameObject go, List<UnityObject> objects)
        {
            if (ReferenceEquals(go, null)) return;
            if (!go) return;

            objects.Add(go);

            var components = go.GetComponents<Component>();
            for (int i = 0; i < components.Length; i++)
            {
                var comp = components[i];
                if (ReferenceEquals(comp, null)) continue;
                if (!comp) continue;
                objects.Add(comp);
            }

            var transform = go.transform;
            int childCount = transform.childCount;
            for (int i = 0; i < childCount; i++)
            {
                var child = transform.GetChild(i);
                if (ReferenceEquals(child, null)) continue;
                if (!child) continue;
                CollectGameObjectRecursive(child.gameObject, objects);
            }
        }

        private static void FilterManagedReferenceObjects(List<UnityObject> objects, List<UnityObject> skipped)
        {
            for (int i = objects.Count - 1; i >= 0; i--)
            {
                var obj = objects[i];
                if (ReferenceEquals(obj, null)) continue;
                if (!obj) continue;
                if (obj is GameObject) continue;

                if (HasManagedReferenceType(obj))
                {
                    skipped.Add(obj);
                    objects.RemoveAt(i);
                }
            }
        }

        private static bool HasManagedReferenceType(UnityObject obj)
        {
            var type = obj.GetType();
            if (_managedRefTypeCache.TryGetValue(type, out bool cached)) return cached;

            bool result = CheckTypeForManagedReference(obj);
            _managedRefTypeCache[type] = result;
            return result;
        }

        private static bool CheckTypeForManagedReference(UnityObject obj)
        {
            var so = new SerializedObject(obj);
            var prop = so.GetIterator();
            bool enterChildren = true;
            while (prop.Next(enterChildren))
            {
                if (prop.propertyType == SerializedPropertyType.ManagedReference)
                {
                    so.Dispose();
                    return true;
                }
                enterChildren = prop.propertyType == SerializedPropertyType.Generic;
            }
            so.Dispose();
            return false;
        }

        private static ulong ConvertToUlong(GlobalObjectId globalId)
        {
            return (ulong)globalId.targetObjectId | ((ulong)globalId.targetPrefabId << 32);
        }

        public void ClearRuntimeMaps()
        {
            _objToID.Clear();
            _idToObj.Clear();
            _goUsedByCount.Clear();
        }

        public ulong GetID(UnityObject obj)
        {
            if (ReferenceEquals(obj, null)) return 0;
            if (!obj) return 0;

            if (_objToID.TryGetValue(obj, out ulong id)) return id;

            GlobalObjectId globalId = GlobalObjectId.GetGlobalObjectIdSlow(obj);
            id = globalId.identifierType != 0
                ? ConvertToUlong(globalId)
                : RuntimeID(obj);

            if (id == 0) return 0;

            _objToID[obj] = id;
            _idToObj[id] = obj;

            return id;
        }

        public UnityObject GetObject(ulong id)
        {
            if (id == 0) return null;

            if (_idToObj.TryGetValue(id, out UnityObject obj))
            {
                if (!obj)
                {
                    _idToObj.Remove(id);
                    _objToID.Remove(obj);
                    return null;
                }
                return obj;
            }

            return null;
        }
    }
}
