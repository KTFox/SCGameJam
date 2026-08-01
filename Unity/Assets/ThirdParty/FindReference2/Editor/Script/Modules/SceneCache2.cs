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
        public enum Status
        {
            None,
            Dirty,
            Scanning,
            Ready,
            Partial
        }

        public string SceneGUID { get; private set; }
        public string ScenePath { get; private set; }
        public Status CurrentStatus { get; internal set; }
        public bool IsRuntimeScene => SceneGUID.StartsWith("__runtime_", StringComparison.Ordinal);

        private string _cachePath;
        private bool _hasDiskCache;
        private bool _diskCacheChecked;

        public string CachePath
        {
            get
            {
                if (_cachePath == null) _cachePath = GetCachePath(SceneGUID);
                return _cachePath;
            }
        }

        public bool HasDiskCache
        {
            get
            {
                if (!_diskCacheChecked)
                {
                    _hasDiskCache = System.IO.File.Exists(CachePath);
                    _diskCacheChecked = true;
                }
                return _hasDiskCache;
            }
        }

        private Dictionary<ulong, GOCacheEntry> _goIDs;
        private HashSet<ulong> _scannedGOIDs;

        private Dictionary<UnityObject, ulong> _objToID;
        private Dictionary<ulong, UnityObject> _idToObj;

        private Dictionary<ulong, int> _goUsedByCount;

        private Dictionary<ulong, List<ulong>> _usedByMap;

        private FR2_TimeSlice _scanTimeSlice;
        private List<GameObject> _pendingScanList;
        private Action _onScanComplete;

        public Action<int, int> OnScanProgress { get; set; }

        private Dictionary<Component, SerializedObject> _serializedObjectCache;

        private bool _excludeSelfRef = true;
        private int _lastKnownRootCount;
        private readonly List<Component> _reusableComponentList = new List<Component>(32);

        public SceneCache2(string sceneGUID, string scenePath)
        {
            if (string.IsNullOrEmpty(sceneGUID))
                throw new ArgumentException("Scene GUID cannot be null or empty", nameof(sceneGUID));

            SceneGUID = sceneGUID;
            ScenePath = scenePath ?? string.Empty;
            CurrentStatus = Status.None;

            _goIDs = new Dictionary<ulong, GOCacheEntry>(256);
            _scannedGOIDs = new HashSet<ulong>();

            _objToID = new Dictionary<UnityObject, ulong>(512);
            _idToObj = new Dictionary<ulong, UnityObject>(512);

            _goUsedByCount = new Dictionary<ulong, int>(256);

            _usedByMap = new Dictionary<ulong, List<ulong>>(256);

            _pendingScanList = new List<GameObject>(128);

            _serializedObjectCache = new Dictionary<Component, SerializedObject>(256);
        }

        public bool HasGameObject(GameObject go)
        {
            if (!go) return false;
            ulong id = GetID(go);
            return id != 0 && _scannedGOIDs.Contains(id);
        }

        public void SetDirty()
        {
            CurrentStatus = Status.Dirty;
        }
    }
}
