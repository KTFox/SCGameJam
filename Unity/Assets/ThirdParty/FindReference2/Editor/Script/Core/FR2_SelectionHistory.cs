using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityObject = UnityEngine.Object;


namespace vietlabs.fr2
{
    [Serializable]
    internal class FR2_SelectionHistory
    {
        [Serializable]
        internal struct HistoryGroup
        {
            public string label;
            public string[] guids;
            public string[] globalIds;
            public string sceneGuid;
            public long timestamp;
        }

        private const string SAVE_PATH = "Library/FR2/selection-history.json";
        private const int MAX_GROUPS = 10;

        [SerializeField] private List<HistoryGroup> _groups = new List<HistoryGroup>();

        private static FR2_SelectionHistory _inst;
        [NonSerialized] private Dictionary<int, UnityObject[]> _resolveCache = new Dictionary<int, UnityObject[]>();
        [NonSerialized] private bool _resolveDirty = true;

        internal static FR2_SelectionHistory inst
        {
            get
            {
                if (_inst != null) return _inst;
                _inst = new FR2_SelectionHistory();
                _inst.Load();
                EditorSceneManager.sceneOpened += (_, _) => _inst._resolveDirty = true;
                EditorSceneManager.sceneClosed += _ => _inst._resolveDirty = true;
                return _inst;
            }
        }

        public int Count => _groups.Count;
        public IReadOnlyList<HistoryGroup> Groups => _groups;

        public bool Pin(HashSet<string> guids, HashSet<string> instIds)
        {
            if ((guids == null || guids.Count == 0) && (instIds == null || instIds.Count == 0)) return false;

            string[] guidArr = guids != null && guids.Count > 0 ? ToArray(guids) : Array.Empty<string>();
            string[] globalIdArr = ConvertInstIdsToGlobalIds(instIds);

            for (int i = 0; i < _groups.Count; i++)
            {
                if (!IsSameSelection(_groups[i], guidArr, globalIdArr)) continue;
                var existing = _groups[i];
                existing.timestamp = DateTime.UtcNow.Ticks;
                _groups.RemoveAt(i);
                _groups.Insert(0, existing);
                Save();
                return false;
            }

            var group = new HistoryGroup
            {
                guids = guidArr,
                globalIds = globalIdArr,
                sceneGuid = ExtractSceneGuid(globalIdArr),
                timestamp = DateTime.UtcNow.Ticks
            };
            group.label = BuildLabel(group);

            _groups.Insert(0, group);
            if (_groups.Count > MAX_GROUPS) _groups.RemoveAt(_groups.Count - 1);

            _resolveDirty = true;
            Save();
            return true;
        }

        public void Remove(int index)
        {
            if (index < 0 || index >= _groups.Count) return;
            _groups.RemoveAt(index);
            _resolveDirty = true;
            Save();
        }

        public void RenameGroup(int index, string newLabel)
        {
            if (index < 0 || index >= _groups.Count) return;
            if (string.IsNullOrWhiteSpace(newLabel)) return;
            var g = _groups[index];
            g.label = newLabel.Trim();
            _groups[index] = g;
            Save();
        }

        public void Clear()
        {
            _groups.Clear();
            _resolveDirty = true;
            Save();
        }

        public void Save()
        {
            Directory.CreateDirectory("Library/FR2/");
            File.WriteAllText(SAVE_PATH, JsonUtility.ToJson(this));
        }

        public void Load()
        {
            if (!File.Exists(SAVE_PATH)) return;
            var content = File.ReadAllText(SAVE_PATH);
            if (string.IsNullOrEmpty(content)) return;
            JsonUtility.FromJsonOverwrite(content, this);
            _resolveDirty = true;
        }

        internal UnityObject[] GetResolvedObjects(int groupIndex)
        {
            if (_resolveDirty)
            {
                _resolveCache.Clear();
                _resolveDirty = false;
            }

            if (_resolveCache.TryGetValue(groupIndex, out var cached)) return cached;
            if (groupIndex < 0 || groupIndex >= _groups.Count) return Array.Empty<UnityObject>();

            var group = _groups[groupIndex];
            if (group.globalIds == null || group.globalIds.Length == 0)
            {
                _resolveCache[groupIndex] = Array.Empty<UnityObject>();
                return _resolveCache[groupIndex];
            }

            var result = new List<UnityObject>(group.globalIds.Length);
            for (int i = 0; i < group.globalIds.Length; i++)
            {
                var obj = ResolveGlobalId(group.globalIds[i]);
                if (obj != null) result.Add(obj);
            }
            _resolveCache[groupIndex] = result.ToArray();
            return _resolveCache[groupIndex];
        }

        internal static UnityObject ResolveGlobalId(string globalIdStr)
        {
            if (string.IsNullOrEmpty(globalIdStr)) return null;
            if (!GlobalObjectId.TryParse(globalIdStr, out var globalId)) return null;
            return GlobalObjectId.GlobalObjectIdentifierToObjectSlow(globalId);
        }

        internal static bool IsSceneGroupValid(HistoryGroup group)
        {
            if (string.IsNullOrEmpty(group.sceneGuid)) return true;

            string scenePath = AssetDatabase.GUIDToAssetPath(group.sceneGuid);
            if (string.IsNullOrEmpty(scenePath)) return false;

            for (int i = 0; i < FR2_Unity.SceneCount; i++)
            {
                var scene = FR2_Unity.GetSceneAt(i);
                if (!scene.isLoaded) continue;
                if (scene.path == scenePath) return true;
            }
            return false;
        }

        private static string[] ConvertInstIdsToGlobalIds(HashSet<string> instIds)
        {
            if (instIds == null || instIds.Count == 0) return Array.Empty<string>();

            var result = new List<string>(instIds.Count);
            foreach (string instIdStr in instIds)
            {
                if (!int.TryParse(instIdStr, out int instId)) continue;
                var obj = FR2_Unity.InstanceIdToObject(instId);
                if (obj == null) continue;

                var globalId = GlobalObjectId.GetGlobalObjectIdSlow(obj);
                if (globalId.identifierType == 0) continue;
                result.Add(globalId.ToString());
            }
            return result.Count > 0 ? result.ToArray() : Array.Empty<string>();
        }

        private static string ExtractSceneGuid(string[] globalIds)
        {
            if (globalIds == null || globalIds.Length == 0) return null;
            for (int i = 0; i < globalIds.Length; i++)
            {
                if (!GlobalObjectId.TryParse(globalIds[i], out var gid)) continue;
                if (gid.identifierType == 2) return gid.assetGUID.ToString();
            }
            return null;
        }

        private static bool IsSameSelection(HistoryGroup group, string[] guids, string[] globalIds)
        {
            if ((group.guids?.Length ?? 0) != guids.Length) return false;
            if ((group.globalIds?.Length ?? 0) != globalIds.Length) return false;

            var set = new HashSet<string>(group.guids ?? Array.Empty<string>());
            for (int i = 0; i < guids.Length; i++)
                if (!set.Contains(guids[i])) return false;

            var globalSet = new HashSet<string>(group.globalIds ?? Array.Empty<string>());
            for (int i = 0; i < globalIds.Length; i++)
                if (!globalSet.Contains(globalIds[i])) return false;

            return true;
        }

        internal static string BuildLabel(HistoryGroup group)
        {
            bool hasAssets = group.guids != null && group.guids.Length > 0;
            bool hasScene = group.globalIds != null && group.globalIds.Length > 0;

            int count = (hasAssets ? group.guids.Length : 0) + (hasScene ? group.globalIds.Length : 0);
            if (count == 0) return "(empty)";

            var names = new List<string>(2);

            if (hasAssets)
            {
                for (int i = 0; i < group.guids.Length && names.Count < 2; i++)
                {
                    string path = AssetDatabase.GUIDToAssetPath(group.guids[i]);
                    if (string.IsNullOrEmpty(path)) continue;
                    names.Add(Path.GetFileNameWithoutExtension(path));
                }
            }

            if (hasScene)
            {
                for (int i = 0; i < group.globalIds.Length && names.Count < 2; i++)
                {
                    var obj = ResolveGlobalId(group.globalIds[i]);
                    if (obj != null) names.Add(obj.name);
                }
            }

            if (names.Count == 0)
            {
                string type = hasAssets ? "Assets" : hasScene ? "GameObjects" : "Items";
                return $"{count} {type}";
            }

            if (count <= 2) return string.Join(", ", names);

            string itemType = hasAssets && !hasScene ? "Assets" : hasScene && !hasAssets ? "GameObjects" : "Items";
            return $"{count} {itemType}";
        }

        internal static string FormatRelativeTime(long ticks)
        {
            var elapsed = DateTime.UtcNow - new DateTime(ticks, DateTimeKind.Utc);

            if (elapsed.TotalSeconds < 60) return "just now";
            if (elapsed.TotalMinutes < 60) return $"{(int)elapsed.TotalMinutes}m ago";
            if (elapsed.TotalHours < 24) return $"{(int)elapsed.TotalHours}h ago";
            return $"{(int)elapsed.TotalDays}d ago";
        }

        public HistoryGroup? Get(int index)
        {
            if (index < 0 || index >= _groups.Count) return null;
            return _groups[index];
        }

        public bool IsFull => _groups.Count >= MAX_GROUPS;

        private static string[] ToArray(HashSet<string> set)
        {
            var arr = new string[set.Count];
            set.CopyTo(arr);
            return arr;
        }
    }
}
