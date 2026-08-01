using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using UnityEditor;
using UnityEngine;
using UnityEngine.Serialization;


namespace vietlabs.fr2
{
    internal enum FR2_CacheStatus
    {
        None,
        NotExist,
        Found,
        
        Created,
        Incompatible,
        UsingCache, // not yet perform first refresh
        
        PendingChanges, // dirty, already perform first refresh
        Clean // clean, already perform first refresh
    }

    internal enum FR2_Status
    {
        None,
        Initialized,
        Search4Cache,
        
        Wait4CacheCreate, // In case cache not exist
        ValidateCache, 
        
        Wait4Refresh, // In case cache is Incompatible
        InitCacheMap, // build AssetMap from _assets
        
        RefreshDB,
        ReadAsset, // Incremental changes (asset import / moved / deleted)
        BuildUsedByMap, 
        Ready
    }

    partial class FR2_Cache
    {
        internal static bool isReady => _status == FR2_Status.Ready;
        internal static bool hasCache => _cacheStatus >= FR2_CacheStatus.Found;
        internal static bool hasDirtyAsset => _dirtyAssets.Count > 0;
        internal static FR2_Setting settings => _inst != null ? _inst._setting : null;
        public static FR2_Status status => _status;
        public static FR2_CacheStatus cacheStatus => _cacheStatus;
        
        [SerializeField] private int _timeStamp;
        
        internal static float refreshProgress
        {
            get
            {
                if (readTS == null) return 0f;
                
                var c = readTS.currentIndex;
                var total =  _reading.Count;
                return c / (float)total;
            }
        }
        
        internal static (int current, int total, string assetPath) GetReadFileProgress()
        {
            if (readTS == null) return default;
            
            var c = readTS.currentIndex;
            var total = _reading.Count;
            
            if (total == 0) return (0, 0, string.Empty);
            if (c >= total) c = total - 1;
            
            var assetPath = _reading[c].assetPath;
            return (c, total, assetPath);
        }
        
        internal static (float progress, string text) GetOverallProgress()
        {
            if (_status == FR2_Status.ReadAsset && readTS != null)
            {
                var c = readTS.currentIndex;
                var total = _inst._assets.Count;
                var progress = (c / (float)total) * 0.95f;
                var text = $"Scanning assets: {c}/{total}";
                return (progress, text);
            }
            
            if (_status == FR2_Status.BuildUsedByMap && buildUsedByTS != null)
            {
                var c = buildUsedByTS.currentIndex;
                var total = _inst._assets.Count;
                var progress = 0.95f + (c / (float)total) * 0.05f;
                var text = $"Building reference map: {c}/{total}";
                return (progress, text);
            }
            
            return (0f, "Initializing...");
        }
        
        internal static int delayRefreshCounter;
        [NonSerialized] private static bool _refreshScheduled;
        
        internal static void IncrementalRefresh()
        {
            if (FR2_SettingExt.disable) return;
            if (_status <= FR2_Status.Wait4Refresh) return;
            
            // If already scheduled, don't restart — prevents re-entrant loops
            if (_refreshScheduled) return;
            _refreshScheduled = true;
            
            FR2_LOG.Log($"{nameof(FR2_Cache)}::{nameof(IncrementalRefresh)}()");
            delayRefreshCounter = 3;
            EditorApplication.update -= CheckDelayRefresh;
            EditorApplication.update += CheckDelayRefresh;
        }

        internal static void CheckDelayRefresh()
        {
            if (FR2_SettingExt.disable)
            {
                EditorApplication.update -= CheckDelayRefresh;
                _refreshScheduled = false;
                return;
            }
            
            if (_status <= FR2_Status.Wait4Refresh)
            {
                EditorApplication.update -= CheckDelayRefresh;
                _refreshScheduled = false;
                FR2_LOG.Log($"{nameof(FR2_Cache)}::{nameof(CheckDelayRefresh)}() | Invalid status: Cache not ready!");
                return;
            }
            
            // Wait for editor to be idle before starting any work
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            
            if (delayRefreshCounter-- > 0) return;
            
            EditorApplication.update -= CheckDelayRefresh;
            _refreshScheduled = false;
            ReadAssetContent();
        }
        
        [NonSerialized] private static bool _pendingDirty;
        
        internal static void MarkDirty()
        {
            if (_inst == null) return;
            
            // During active processing (ReadAsset/BuildUsedByMap/RefreshDB),
            // do NOT call SetDirty immediately — it triggers reimport of FR2_Cache.asset
            // which fires OnPostprocessAllAssets and creates a feedback loop.
            // Instead, defer until processing completes.
            if (_status == FR2_Status.ReadAsset || _status == FR2_Status.BuildUsedByMap || _status == FR2_Status.RefreshDB)
            {
                _pendingDirty = true;
                return;
            }
            
            _pendingDirty = false;
            EditorUtility.SetDirty(_inst);
        }
        
        private static void FlushPendingDirty()
        {
            if (_pendingDirty && _inst != null)
            {
                _pendingDirty = false;
                EditorUtility.SetDirty(_inst);
            }
        }

        internal static void MarkAssetPathContentDirty(params string[] assetPaths)
        {
            if (assetPaths.Length > 0)
                FR2_LOG.Log($"MarkAssetPathContentDirty: {assetPaths.Length} assets, suppressRefresh={suppressRefresh}");
            for (var i = 0; i < assetPaths.Length; i++)
            {
                var guid = AssetPathToGUID(assetPaths[i]);
                if (string.IsNullOrEmpty(guid)) continue;
                if (guid == _instGUID) continue;
                _dirtyAssets.Add(guid);
                
                var asset = GetAssetByGUID(guid, false);
                asset?.MarkAsDirty();
            }
        }
        
        internal static void MarkAssetPathDeleted(params string[] assetPaths)
        {
            for (var i = 0; i < assetPaths.Length; i++)
            {
                var path = assetPaths[i];
                var guid = AssetPathToGUID(path);
                if (guid == _instGUID) continue;
                
                // Remove stale cache entries for deleted assets
                if (!string.IsNullOrEmpty(guid))
                {
                    guidToPathCache.Remove(guid);
                    missingGUIDs.Remove(guid);
                }
                if (!string.IsNullOrEmpty(path)) pathToGuidCache.Remove(path);
                
                var asset = GetAssetByGUID(guid, false);
                asset?.MarkAsDeleted();
            }
            
            MarkDirty();
        }

        internal static void MarkAssetPathDeleted(IList<string> assetPaths)
        {
            for (var i = 0; i < assetPaths.Count; i++)
            {
                var path = assetPaths[i];
                var guid = AssetPathToGUID(path);
                if (guid == _instGUID) continue;
                
                // Remove stale cache entries for deleted assets
                if (!string.IsNullOrEmpty(guid))
                {
                    guidToPathCache.Remove(guid);
                    missingGUIDs.Remove(guid);
                }
                if (!string.IsNullOrEmpty(path)) pathToGuidCache.Remove(path);
                
                var asset = GetAssetByGUID(guid, false);
                asset?.MarkAsDeleted();
            }
            
            MarkDirty();
        }
        
        internal static void MarkAssetPathChanged(params string[] assetPaths)
        {
            for (var i = 0; i < assetPaths.Length; i++)
            {
                var path = assetPaths[i];
                var guid = AssetPathToGUID(path);
                if (guid == _instGUID) continue;
                
                // Invalidate stale path<->guid cache entries for the old path
                if (!string.IsNullOrEmpty(guid) && guidToPathCache.TryGetValue(guid, out string oldPath))
                {
                    pathToGuidCache.Remove(oldPath);
                    guidToPathCache.Remove(guid);
                }
                
                var asset = GetAssetByGUID(guid, false);
                asset?.MarkAsMoved();
            }
            
            MarkDirty();
        }
        
        
        internal static void Reload()
        {
            if (FR2_SettingExt.disable) return;
            if (_status == FR2_Status.None) return;
            if (_status < FR2_Status.Ready) return;
            FR2_LOG.Log($"{nameof(FR2_Cache)}::{nameof(Reload)}()");
            Initialize();
        }

        public static FR2_Asset GetAsset(string guid, bool autoNew = false)
        {
            if (_status != FR2_Status.Ready)
            {
                FR2_LOG.LogWarning("External call GetAsset() when cache is not Ready!");
            }
            
            return GetAssetByGUID(guid, autoNew);
        }
        
        public static void RefreshAsset(string guid, bool force)
        {
            FR2_LOG.Log($"{nameof(FR2_Cache)}::{nameof(RefreshAsset)}() : {guid}, {force}");
            MarkAssetPathContentDirty(GUIDToAssetPath(guid));
        }
        
        public static void RefreshSelection()
        {
            FR2_LOG.Log($"{nameof(FR2_Cache)}::{nameof(RefreshSelection)}()");
            MarkAssetPathContentDirty(Selection.assetGUIDs
                .Select(GUIDToAssetPath)
                .ToArray());
        }

        public static void ClearCacheCompletely()
        {
            FR2_LOG.Log($"{nameof(FR2_Cache)}::{nameof(ClearCacheCompletely)}()");
            _status = FR2_Status.InitCacheMap;
            _cacheStatus = FR2_CacheStatus.Created;
            
            _inst._assets.Clear();
            _map.Clear();
            _dirtyAssets.Clear();
            _reading.Clear();
            
            // Clear performance caches
            ClearPerformanceCaches();
            
            EditorUtility.SetDirty(_inst);
        }
        
        public static void ClearPerformanceCaches()
        {
            guidToPathCache.Clear();
            pathToGuidCache.Clear();
            instanceIdStringCache.Clear();
            instanceIdToGuidLocalIdCache.Clear();
            instanceIdToGuidCache.Clear();
            
            // Clear object to GUID cache in FR2_Ref
            FR2_Ref.ClearObjectToGuidCache();
        }
        
        public static string GUIDToAssetPath(string guid)
        {
            if (string.IsNullOrEmpty(guid)) return string.Empty;
            if (guidToPathCache.TryGetValue(guid, out string cachedPath)) return cachedPath;
            
            cachedPath = AssetDatabase.GUIDToAssetPath(guid);
            if (!string.IsNullOrEmpty(cachedPath))
            {
                guidToPathCache[guid] = cachedPath;
                pathToGuidCache[cachedPath] = guid;
            }
            
            return cachedPath ?? string.Empty;
        }
        
        public static string AssetPathToGUID(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath)) return string.Empty;
            if (pathToGuidCache.TryGetValue(assetPath, out string cachedGuid)) return cachedGuid;
            
            cachedGuid = AssetDatabase.AssetPathToGUID(assetPath);
            if (!string.IsNullOrEmpty(cachedGuid))
            {
                pathToGuidCache[assetPath] = cachedGuid;
                guidToPathCache[cachedGuid] = assetPath;
            }
            
            return cachedGuid ?? string.Empty;
        }
        
        public static string GetInstanceIdString(int instanceId)
        {
            if (instanceIdStringCache.TryGetValue(instanceId, out string cached)) return cached;

            string result = instanceId.ToString();
            instanceIdStringCache[instanceId] = result;
            return result;
        }
        
        public static bool TryGetGUIDAndLocalFileIdentifier(UnityEngine.Object obj, out string guid, out long localId)
        {
            if (!EditorUtility.IsPersistent(obj))
            {
                guid = string.Empty;
                localId = -1;
                return false;
            }

            int instanceId = FR2_Unity.GetInstanceId(obj);
            if (instanceIdToGuidLocalIdCache.TryGetValue(instanceId, out var cached))
            {
                guid = cached.guid;
                localId = cached.localId;
                return !string.IsNullOrEmpty(guid);
            }
            
            if (AssetDatabase.TryGetGUIDAndLocalFileIdentifier(obj, out guid, out localId))
            {
                instanceIdToGuidLocalIdCache[instanceId] = (guid, localId);
                return true;
            }
            
            guid = string.Empty;
            localId = -1;
            return false;
        }

        public static string GetGuidByInstanceId(int instanceId)
        {
            if (instanceIdToGuidCache.TryGetValue(instanceId, out string cached)) return cached;

            string assetPath = FR2_Unity.GetAssetPathByInstanceId(instanceId);
            string guid = string.IsNullOrEmpty(assetPath) ? string.Empty : AssetPathToGUID(assetPath);
            instanceIdToGuidCache[instanceId] = guid;
            return guid;
        }

        public static void Check4Changes(bool force = false)
        {
            FR2_LOG.Log($"{nameof(FR2_Cache)}::{nameof(Check4Changes)}()");
            RefreshDB();
        }
        
        public static void DelayCheck4Changes()
        {
            FR2_LOG.Log($"{nameof(FR2_Cache)}::{nameof(DelayCheck4Changes)}()");
        }

        public static void RefreshAsset(FR2_Asset asset, bool force)
        {
            FR2_LOG.Log($"{nameof(FR2_Cache)}::{nameof(RefreshAsset)}() | {asset.assetPath}, {force}");
        }
    }
    
    
    
    internal partial class FR2_Cache : ScriptableObject
    {
        private const string CACHE_VERSION = "2.6.17";
        
        [FormerlySerializedAs("setting")] 
        [SerializeField] internal FR2_Setting _setting = new FR2_Setting();
        
        [FormerlySerializedAs("AssetList")]
        [SerializeField] internal List<FR2_Asset> _assets = new List<FR2_Asset>();
        
        [FormerlySerializedAs("_curCacheVersion")]
        [SerializeField] private string _cacheVersion;
        
        [NonSerialized] private static FR2_Status _status = FR2_Status.None;
        [NonSerialized] private static FR2_CacheStatus _cacheStatus = FR2_CacheStatus.None;
        [NonSerialized] internal static bool isInvoking_onReady = false;
        
        [NonSerialized] internal static FR2_Cache _inst;
        [NonSerialized] internal static string _instGUID;
        [NonSerialized] internal static readonly Dictionary<string, FR2_Asset> _map = new Dictionary<string, FR2_Asset>(10000);
        [NonSerialized] internal static readonly HashSet<string> _dirtyAssets = new HashSet<string>();
        [NonSerialized] internal static readonly HashSet<string> missingGUIDs = new HashSet<string>();
        
        // OPTIMIZED: Centralized caches for performance
        [NonSerialized] internal static readonly Dictionary<string, string> guidToPathCache = new Dictionary<string, string>(10000);
        [NonSerialized] internal static readonly Dictionary<string, string> pathToGuidCache = new Dictionary<string, string>(10000);
        [NonSerialized] internal static readonly Dictionary<int, string> instanceIdStringCache = new Dictionary<int, string>(5000);
        [NonSerialized] internal static readonly Dictionary<int, (string guid, long localId)> instanceIdToGuidLocalIdCache = new Dictionary<int, (string, long)>(5000);
        [NonSerialized] internal static readonly Dictionary<int, string> instanceIdToGuidCache = new Dictionary<int, string>(5000);
        
        private static FR2_Asset GetAssetByGUID(string guid, bool autoNew = false)
        {
            return GetAssetByGUID(guid, null, autoNew);
        }
        
        private static FR2_Asset GetAssetByGUID(string guid, string knownPath, bool autoNew)
        {
            if (_instGUID == guid) return null;
            if (_map.TryGetValue(guid, out var result)) return result;
            
            if (string.IsNullOrEmpty(knownPath) && missingGUIDs.Contains(guid)) return null;
            if (!autoNew) return null;
            
            string assetPath = knownPath;
            if (string.IsNullOrEmpty(assetPath))
            {
                assetPath = GUIDToAssetPath(guid);
                if (string.IsNullOrEmpty(assetPath))
                {
                    missingGUIDs.Add(guid);
                    return null;
                }
            }
            else
            {
                guidToPathCache[guid] = assetPath;
            }
            
            missingGUIDs.Remove(guid);
            
            var asset = new FR2_Asset(guid);
            asset.LoadPathInfo();
            asset.refreshStamp = cacheStamp;
            _map.Add(guid, asset);
            if (!asset.IsCriticalAsset()) return asset;
            
            _inst._assets.Add(asset);
            _dirtyAssets.Add(guid);
            asset.MarkAsDirty();
            MarkDirty();
            
            return asset;
        }
        
        [HideInCallstack]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool MatchesStatus(string funcName, params FR2_Status[] expected)
        {
            for (var i = 0; i < expected.Length; i++)
            {
                if (_status != expected[i]) continue;
#if FR2_DEBUG || FR2_DEV
                FR2_LOG.Log($"{nameof(FR2_Cache)}::{funcName}()");
#endif
                return true;
            }
            
#if FR2_DEBUG || FR2_DEV
            var str = string.Join(", ", expected.Select(item=>item.ToString()).ToArray());
            FR2_LOG.LogWarning($"{nameof(FR2_Cache)}::{funcName}() : Invalid status {_status}, expected {str}");
#endif
            return false;
        }

        [HideInCallstack]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void If(bool cond, Action onTrue, Action onFalse)
        {
            if (cond)
            {
                onTrue();
            }
            else
            {
                onFalse();
            }
        }
        
        
        [InitializeOnLoadMethod]
        internal static void Initialize()
        {
            _cacheStatus = FR2_CacheStatus.None;
            _status = FR2_Status.None;

            if (readTS != null) readTS.Stop();
            if (refreshDBTS != null) refreshDBTS.Stop();
            if (buildUsedByTS != null) buildUsedByTS.Stop();

            FR2_LOG.Log($"{nameof(FR2_Cache)}::{nameof(Initialize)}()");
            EditorApplication.update -= DelayInit;
            EditorApplication.update += DelayInit;
        }

        static void DelayInit()
        {
            if (EditorApplication.isCompiling) return;
            
            if (FR2_SettingExt.userDisabled)
            {
                EditorApplication.update -= DelayInit;
                return;
            }
            
            EditorApplication.update -= DelayInit;
            RepaintFR2Windows();
            if (!MatchesStatus(nameof(Initialize), FR2_Status.None)) return;
            
            if (_inst == null)
            {
                _status = FR2_Status.Initialized;    
                Search4Cache();
                return;
            }
            
            _cacheStatus = FR2_CacheStatus.Found;
            _status = FR2_Status.Search4Cache;
            ValidateCacheVersion();
        }

        internal static void Search4Cache()
        {
            if (_inst != null)
            {
                FR2_LOG.LogWarning($"{nameof(FR2_Cache)}::{nameof(Search4Cache)}() | Something wrong? _inst existed | status = {_status}");
            }

            {
                _dirtyAssets.Clear();
                _reading.Clear();
                _map.Clear();
                missingGUIDs.Clear();
                _instGUID = string.Empty;
                _cacheStatus = FR2_CacheStatus.None;
                _status = FR2_Status.Search4Cache;    
            }
            
            var guids = AssetDatabase.FindAssets("t:fr2_cache");
            if (guids.Length > 0)
            {
                for (var i = 0; i < guids.Length; i++)
                {
                    var path = FR2_Cache.GUIDToAssetPath(guids[i]);
                    _inst = AssetDatabase.LoadAssetAtPath<FR2_Cache>(path);
                    if (_inst != null) break;
                }
            }
            
            if (_inst == null)
            {
                _cacheStatus = FR2_CacheStatus.NotExist;
                _status = FR2_Status.Wait4CacheCreate;
                FR2_LOG.LogWarning($"{nameof(FR2_Cache)}::{nameof(Search4Cache)}() | FR2_Cache not found - wait for create!");
                return;
            }
            
            _cacheStatus = FR2_CacheStatus.Found;
            _instGUID = AssetPathToGUID(AssetDatabase.GetAssetPath(_inst));
            ValidateCacheVersion();
        }
        
        internal static void ValidateCacheVersion()
        {
            if (!MatchesStatus(nameof(ValidateCacheVersion), FR2_Status.Search4Cache)) return;
            
            _status = FR2_Status.ValidateCache;
            
            if (_inst._cacheVersion != CACHE_VERSION)
            {
                _cacheStatus = FR2_CacheStatus.Incompatible;
                _status = FR2_Status.Wait4Refresh;
                FR2_LOG.LogWarning($"{nameof(FR2_Cache)}::{nameof(ValidateCacheVersion)}() | Incompatible cache version - Waiting for Refresh!");
                return;
            }
            
            InitCacheMap();
        }

        internal static void CreateCache()
        {
            if (!MatchesStatus(nameof(CreateCache), FR2_Status.Wait4CacheCreate, FR2_Status.Wait4Refresh)) return;
            _inst = CreateInstance<FR2_Cache>();
            _inst._cacheVersion = CACHE_VERSION;
            
            try
            {
                AssetDatabase.CreateAsset(_inst, DEFAULT_CACHE_PATH);
            }
            catch (Exception e)
            {
                if (AssetDatabase.LoadAssetAtPath<FR2_Cache>(DEFAULT_CACHE_PATH) == null)
                {
                    Debug.LogError($"[FR2] CreateAsset failed: {e.Message}\nA third-party AssetPostprocessor may be broken. Check your Firebase/Google VersionHandler plugin.");
                    return;
                }
            }
            
            EditorUtility.SetDirty(_inst);
            
            _cacheStatus = FR2_CacheStatus.Created;
            _instGUID = AssetPathToGUID(AssetDatabase.GetAssetPath(_inst));
            RefreshDB();
        }
        
        internal static void DeleteCache()
        {
            if (_inst == null) return;
            try
            {
                var path = AssetDatabase.GetAssetPath(_inst);
                _inst._assets.Clear();
                _inst = null;
                _map.Clear();

                if (readTS != null)
                {
                    readTS.Stop();
                    readTS = null;
                }
                
                if (refreshDBTS != null)
                {
                    refreshDBTS.Stop();
                    refreshDBTS = null;
                }
                
                if (buildUsedByTS != null)
                {
                    buildUsedByTS.Stop();
                    buildUsedByTS = null;
                }
                
                _status = FR2_Status.Wait4CacheCreate;
                _cacheStatus = FR2_CacheStatus.NotExist;
                
                AssetDatabase.DeleteAsset(path);
            }
            catch (Exception e)
            {
                FR2_LOG.Log(e);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
        
        
        private static void InitCacheMap()
        {
            if (!MatchesStatus( nameof(InitCacheMap), FR2_Status.ValidateCache)) return;
            
            _status = FR2_Status.InitCacheMap;
            _cacheStatus = FR2_CacheStatus.UsingCache;
            RepaintFR2Windows();
            _map.Clear();
            
            var arr = _inst._assets;
            for (var i = arr.Count-1; i >= 0; i--)
            {
                var asset = arr[i];
                if (_map.TryAdd(asset.guid, asset))
                {
                    if (asset.isDirty && asset.hasBeenScanned) _dirtyAssets.Add(asset.guid);
                    continue;
                }
                
                FR2_LOG.LogWarning($"{nameof(FR2_Cache)}::{nameof(InitCacheMap)}() | Duplicated asset found <{asset.guid}>!");
                arr.RemoveAt(i);
            }
            
            MarkDirty();
            if (_inst._assets.Count == 0) { RefreshDB(); return; }
            If(hasDirtyAsset, ReadAssetContent, BuildUsedByMap);
        }
        
        private static void RefreshDB()
        {
            if (!MatchesStatus(nameof(RefreshDB)
                    , FR2_Status.Ready, FR2_Status.InitCacheMap, FR2_Status.Wait4CacheCreate)) return;
            
            _status = FR2_Status.RefreshDB;
            _cacheStatus = FR2_CacheStatus.PendingChanges;
            RepaintFR2Windows();

            var ts = ++_inst._timeStamp;
            var allAssetPaths = AssetDatabase.GetAllAssetPaths();
            
            refreshDBTS = new FR2_TimeSlice(
                () => allAssetPaths.Length,
                (idx) => {
                    var path = allAssetPaths[idx];
                    if (path.Contains("FindReference2") || path.Contains("FR2_Cache")) return;
                    if (AssetDatabase.IsValidFolder(path)) return;
                    if (!FR2_Parser.IsReadable(path))
                    {
                        #if FR2_DEBUG
                        {
                            var guid1 = AssetPathToGUID(path);
                            var asset1 = GetAssetByGUID(guid1, true);
                            if (asset1.IsCriticalAsset()) Debug.LogWarning($"Asset isCritical() found but why it's not readable???\n{path}");
                        }
                        #endif       
                        return;
                    }
                    if (path.EndsWith("/")) return;
                    
                    var guid = AssetPathToGUID(path);
                    var asset = GetAssetByGUID(guid, path, true);
                    if (asset != null) asset.refreshStamp = ts;
                },
                RefreshDBComplete
            );
            refreshDBTS.jobName = "RefreshDB";
            
            refreshDBTS.Start();
        }
        
        private static void RefreshDBComplete()
        {
            if (!MatchesStatus(nameof(RefreshDBComplete), FR2_Status.RefreshDB)) return;
            
            var ts = _inst._timeStamp;
            var arr = _inst._assets;
            
            for (var i = arr.Count-1; i >= 0; i--)
            {
                var asset = arr[i];
                if (asset.refreshStamp == ts) continue;
                
                asset.MarkAsDeleted();
                arr.RemoveAt(i);
                FR2_LOG.Log($"Asset removed at {i}? {asset.assetPath} | {asset.IsCriticalAsset()} | {asset.IsMissing}");
            }
            
            MarkDirty();
            FR2_LOG.Log($"{nameof(FR2_Cache)}::{nameof(RefreshDBComplete)}() | Processed {arr.Count} assets");
            RepaintFR2Windows();
            
            If(hasDirtyAsset, ReadAssetContent, BuildUsedByMap);
        }
        
        private static FR2_TimeSlice readTS;
        private static int _totalRead;
        private static readonly List<FR2_Asset> _reading = new List<FR2_Asset>();
        private static bool _needsFullBuildAfterRead;
        
        private static FR2_TimeSlice refreshDBTS;
        private static FR2_TimeSlice buildUsedByTS;
        
        private enum UsedByDiffStatus : byte { Remove, Exist, New }
        private static readonly Dictionary<string, UsedByDiffStatus> _usedByDiff = new Dictionary<string, UsedByDiffStatus>(64);
        
        internal static void ReadAssetContent()
        {
            if (_status != FR2_Status.RefreshDB && _status != FR2_Status.Ready && _status != FR2_Status.InitCacheMap)
            {
                FR2_LOG.LogWarning($"{nameof(FR2_Cache)}::{nameof(ReadAssetContent)}() | status={_status}, dirtyCount={_dirtyAssets.Count}, readingCount={_reading.Count}");
                return;
            }

            _needsFullBuildAfterRead = _status != FR2_Status.Ready;

            if (_reading.Count > 0)
            {
                FR2_LOG.Log($"Reading before: {_reading.Count}");
                for (var i = 0; i < _reading.Count; i++)
                {
                    _dirtyAssets.Add(_reading[i].guid);
                }
                
                _reading.Clear();
            }
            
            _status = FR2_Status.ReadAsset;
            RepaintFR2Windows();
            _totalRead = 0;
            
            FR2_LOG.Log($"ReadAssetContent: _dirtyAssets.Count={_dirtyAssets.Count}");
            
            foreach (var guid in _dirtyAssets)
            {
                missingGUIDs.Remove(guid);
            }

            foreach (var guid in _dirtyAssets)
            {
                if (guid == _instGUID) continue;
                
                var asset = GetAssetByGUID(guid, true);
                if (asset == null)
                {
                    FR2_LOG.LogWarning($"{nameof(FR2_Cache)}::{nameof(ReadAssetContent)}() | Asset not found <{guid}>!");
                    continue;
                }
                
                asset.MarkAsDirty();
                _reading.Add(asset);
            }
            
            _dirtyAssets.Clear();
            readTS = new FR2_TimeSlice(()=> _reading.Count, (idx) =>
            {
                var asset = _reading[idx];
                asset.LoadFileInfo();
                
                if (!asset.isDirty) return;
                
                if (!_needsFullBuildAfterRead)
                {
                    _usedByDiff.Clear();
                    foreach (var key in asset.UseGUIDs.Keys)
                        _usedByDiff[key] = UsedByDiffStatus.Remove;
                }
                
                asset.LoadContentFast();
                
                if (!_needsFullBuildAfterRead)
                {
                    foreach (var key in asset.UseGUIDs.Keys)
                    {
                        if (_usedByDiff.ContainsKey(key)) _usedByDiff[key] = UsedByDiffStatus.Exist;
                        else _usedByDiff[key] = UsedByDiffStatus.New;
                    }
                    
                    foreach (var kvp in _usedByDiff)
                    {
                        if (kvp.Value == UsedByDiffStatus.Exist) continue;
                        
                        if (kvp.Value == UsedByDiffStatus.Remove)
                        {
                            var target = GetAssetByGUID(kvp.Key, false);
                            target?.RemoveUsedBy(asset.guid);
                        }
                        else
                        {
                            var target = GetAssetByGUID(kvp.Key, true);
                            target?.AddUsedBy(asset.guid, asset);
                        }
                    }
                }
                
#if FR2_DEBUG
                if (asset.isDirty)
                {
                    FR2_LOG.LogWarning($"Dirty right after read? {asset.assetPath}\n{asset.isDirty} | {asset.fileInfoDirty} | {asset.fileContentDirty} | {asset.hasBeenScanned} | {asset.type}");
                }
#endif
                
                _totalRead++;
            },
            ReadAssetComplete);
            readTS.jobName = "ReadAsset";
            
            readTS.Start();
        }

        private static void ReadAssetComplete()
        {
            if (!MatchesStatus(nameof(ReadAssetComplete), FR2_Status.ReadAsset)) return;
            
            _reading.Clear();
            MarkDirty();
            FR2_LOG.Log($"{nameof(FR2_Cache)}::{nameof(ReadAssetComplete)}() | LastContentRead: {_totalRead}");
            RepaintFR2Windows();
            
            if (_needsFullBuildAfterRead)
            {
                _needsFullBuildAfterRead = false;
                BuildUsedByMap();
            }
            else
            {
                SetAsReady();
            }
        }
        
        private static void ClearUsedBy()
        {   
            foreach (var asset in _inst._assets)
            {
                if (asset == null)
                {
                    FR2_LOG.LogWarning($"{nameof(FR2_Cache)}::{nameof(ClearUsedBy)}() | Asset is NULL???");
                    continue;
                }
                
                asset.UsedByMap?.Clear();
            }
            
            foreach (var kvp in _map)
            {
                kvp.Value.UsedByMap?.Clear();
            }
        }

        private static void InjectAtlasReverseEdges()
        {
            foreach (var asset in _inst._assets)
                asset.ClearTransientUseGUIDs();

            foreach (var asset in _inst._assets)
            {
                if (asset.extension != ".spriteatlas" && asset.extension != ".spriteatlasv2") continue;

                var deps = AssetDatabase.GetDependencies(asset.assetPath, false);
                foreach (var dep in deps)
                {
                    var depGuid = AssetPathToGUID(dep);
                    if (string.IsNullOrEmpty(depGuid)) continue;
                    var depAsset = GetAssetByGUID(depGuid, false);
                    if (depAsset == null) continue;

                    if (depAsset.IsFolder)
                    {
                        string prefix = depAsset.assetPath + "/";
                        foreach (var mapKvp in _map)
                        {
                            var child = mapKvp.Value;
                            if (child.IsFolder || child.IsMissing) continue;
                            if (!child.assetPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) continue;
                            child.AddUseGUIDTransient(asset.guid);
                        }
                    }
                    else
                    {
                        depAsset.AddUseGUIDTransient(asset.guid);
                    }
                }
            }
        }
        
        private static void BuildUsedByMap()
        {
            if (!MatchesStatus(nameof(BuildUsedByMap)
                    , FR2_Status.InitCacheMap
                    , FR2_Status.RefreshDB
                    , FR2_Status.ReadAsset)
            ) return;
            
            ClearUsedBy();
            InjectAtlasReverseEdges();
            _status = FR2_Status.BuildUsedByMap;
            RepaintFR2Windows();
            
            var arr = _inst._assets;
            buildUsedByTS = new FR2_TimeSlice(
                () => arr.Count,
                (idx) => {
                    var asset = arr[idx];
                    if (asset.IsMissing) return;
                    
                    foreach (var kvp in asset.UseGUIDs)
                    {
                        var toAsset = GetAssetByGUID(kvp.Key, true);
                        if (toAsset == null) continue;
                        toAsset.AddUsedBy(asset.guid, asset);
                    }

                    if (asset._transientUseGUIDs == null) return;
                    foreach (var tGuid in asset._transientUseGUIDs)
                    {
                        var toAsset = GetAssetByGUID(tGuid, false);
                        if (toAsset == null) continue;
                        toAsset.AddUsedBy(asset.guid, asset);
                    }
                },
                BuildUsedByMapComplete
            );
            buildUsedByTS.jobName = "BuildUsedByMap";
            
            buildUsedByTS.Start();
        }
        
        private static void BuildUsedByMapComplete()
        {
            if (!MatchesStatus(nameof(BuildUsedByMapComplete), FR2_Status.BuildUsedByMap)) return;
            
            FR2_LOG.Log($"{nameof(FR2_Cache)}::{nameof(BuildUsedByMapComplete)}() | Built UsedBy map for {_inst._assets.Count} assets");
            SetAsReady();
        }

        private static void SetAsReady()
        {
            if (!MatchesStatus(nameof(SetAsReady), FR2_Status.InitCacheMap, FR2_Status.BuildUsedByMap, FR2_Status.ReadAsset)) return;
            
            _status = FR2_Status.Ready;
            _cacheStatus = hasDirtyAsset ? FR2_CacheStatus.PendingChanges : FR2_CacheStatus.Clean;
            
            // NOW it's safe to persist — all processing is done, no more feedback loop risk
            FlushPendingDirty();
            
            FR2_LOG.Log($"{nameof(FR2_Cache)}::{nameof(SetAsReady)}() | onReady subscribers: {onReady?.GetInvocationList()?.Length ?? 0}");
            isInvoking_onReady = true;
            onReady?.Invoke();
            FR2_Event.DispatchGlobal<CacheReadyEvent>();
            isInvoking_onReady = false;
            
            // If new dirty assets accumulated during the read/build cycle, schedule another refresh
            if (hasDirtyAsset)
            {
                IncrementalRefresh();
            }
            
            RepaintFR2Windows();
        }
        
        private static void RepaintFR2Windows()
        {
            var windows = Resources.FindObjectsOfTypeAll<EditorWindow>();
            for (var i = 0; i < windows.Length; i++)
            {
                var w = windows[i];
                if (w == null) continue;
                if (w is FR2_WindowAll)
                {
                    w.Repaint();
                }
            }
        }
    }
}