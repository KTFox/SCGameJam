using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;

namespace vietlabs.fr2
{
    internal partial class SceneCache2
    {
        private const int CACHE_VERSION = 7;
        private const string CACHE_DIRECTORY = "Library/FR2/SceneCache";

        private string _loadedAssetHash;
        private bool _isPartialCache;

        public static string GetCachePath(string sceneGUID) => $"{CACHE_DIRECTORY}/{sceneGUID}.cache";

        private string GetCacheFilePath() => GetCachePath(SceneGUID);

        public void DeleteCacheFile()
        {
            string filePath = GetCacheFilePath();
            if (File.Exists(filePath)) File.Delete(filePath);
            _loadedAssetHash = null;
        }

        private bool CanPersist => !string.IsNullOrEmpty(ScenePath) && 
                                   (ScenePath.EndsWith(".unity") || ScenePath.EndsWith(".prefab"));

        private string GetAssetHash()
        {
            if (!CanPersist) return null;
            return AssetDatabase.GetAssetDependencyHash(ScenePath).ToString();
        }

        public bool NeedsFullRefresh()
        {
            if (string.IsNullOrEmpty(_loadedAssetHash)) return true;
            return _loadedAssetHash != GetAssetHash();
        }

        public void SaveToCache(bool partial = false)
        {
            if (!CanPersist) return;

            if (!Directory.Exists(CACHE_DIRECTORY))
                Directory.CreateDirectory(CACHE_DIRECTORY);

            string filePath = GetCacheFilePath();
            var sb = new StringBuilder(4096);

            string assetHash = partial ? "" : (GetAssetHash() ?? "");
            sb.Append("{\"v\":");
            sb.Append(CACHE_VERSION);
            sb.Append(",\"hash\":\"");
            sb.Append(assetHash);
            if (partial)
                sb.Append("\",\"partial\":true");
            else
                sb.Append('"');
            sb.AppendLine("}");

            foreach (var goEntry in _goIDs)
            {
                ulong goID = goEntry.Key;
                GOCacheEntry entry = goEntry.Value;

                sb.Append("G:");
                sb.AppendLine(goID.ToString());

                if (!string.IsNullOrEmpty(entry.sourcePrefabGUID))
                {
                    sb.Append("P:");
                    sb.AppendLine(entry.sourcePrefabGUID);
                }

                if (!string.IsNullOrEmpty(entry.variantPrefabGUID))
                {
                    sb.Append("V:");
                    sb.AppendLine(entry.variantPrefabGUID);
                }

                foreach (var compEntry in entry.compsWithRefs)
                {
                    ulong compID = compEntry.Key;
                    CompRefs refs = compEntry.Value;

                    if (refs.sceneRefs != null && refs.sceneRefs.Count > 0)
                    {
                        sb.Append("C:");
                        sb.Append(compID.ToString());
                        sb.Append("->");
                        for (int i = 0; i < refs.sceneRefs.Count; i++)
                        {
                            if (i > 0) sb.Append(' ');
                            sb.Append(refs.sceneRefs[i].targetId.ToString());
                            if (!string.IsNullOrEmpty(refs.sceneRefs[i].propertyPath))
                            {
                                sb.Append('|');
                                sb.Append(refs.sceneRefs[i].propertyPath);
                            }
                        }
                        sb.AppendLine();
                    }

                    if (refs.assets != null)
                    {
                        for (int i = 0; i < refs.assets.Count; i++)
                        {
                            AssetRef assetRef = refs.assets[i];
                            sb.Append("A:");
                            sb.Append(assetRef.guid);
                            sb.Append(' ');
                            sb.Append(assetRef.localFileId.ToString());
                            if (!string.IsNullOrEmpty(assetRef.propertyPath))
                            {
                                sb.Append(' ');
                                sb.Append(assetRef.propertyPath);
                            }
                            sb.AppendLine();
                        }
                    }
                }
            }

            File.WriteAllText(filePath, sb.ToString());
            if (!partial) _loadedAssetHash = assetHash;
            FR2_LOG.Log($"SceneCache2: Saved cache to {filePath} ({_goIDs.Count} GameObjects{(partial ? ", partial" : "")})");
        }

        private static StringBuilder _lineBuffer;
        private static readonly Dictionary<int, string> _stringPool = new Dictionary<int, string>(256);

        private static string InternFromSB(StringBuilder sb, int start, int length)
        {
            int hash = 0;
            for (int i = start; i < start + length; i++)
                hash = hash * 31 + sb[i];

            if (_stringPool.TryGetValue(hash, out string cached))
            {
                if (cached.Length == length)
                {
                    bool match = true;
                    for (int i = 0; i < length; i++)
                    {
                        if (cached[i] != sb[start + i]) { match = false; break; }
                    }
                    if (match) return cached;
                }
            }

            string result = sb.ToString(start, length);
            _stringPool[hash] = result;
            return result;
        }

        public bool LoadFromCache()
        {
            if (!CanPersist) return false;

            string filePath = GetCacheFilePath();
            if (!File.Exists(filePath)) return false;

            if (_lineBuffer == null) _lineBuffer = new StringBuilder(256);

            _goIDs.Clear();
            _scannedGOIDs.Clear();

            GOCacheEntry currentGO = null;
            CompRefs currentCompRefs = null;
            bool headerParsed = false;

            using (var reader = new StreamReader(filePath, Encoding.UTF8, false, 4096))
            {
                while (true)
                {
                    _lineBuffer.Clear();
                    bool eof = !ReadLine(reader, _lineBuffer);
                    if (_lineBuffer.Length == 0 && eof) break;
                    if (_lineBuffer.Length == 0) continue;

                    if (!headerParsed)
                    {
                        headerParsed = true;
                        if (_lineBuffer.Length == 0 || _lineBuffer[0] != '{') return false;
                        if (!ParseHeader(_lineBuffer)) return false;
                        continue;
                    }

                    char prefix = _lineBuffer[0];
                    char delim = _lineBuffer.Length > 1 ? _lineBuffer[1] : '\0';
                    if (delim != ':') continue;

                    switch (prefix)
                    {
                        case 'G':
                            if (!TryParseUlong(_lineBuffer, 2, _lineBuffer.Length, out ulong goID)) return false;
                            currentGO = new GOCacheEntry(goID);
                            _goIDs[goID] = currentGO;
                            _scannedGOIDs.Add(goID);
                            currentCompRefs = null;
                            break;

                        case 'P':
                            if (currentGO != null)
                                currentGO.sourcePrefabGUID = InternFromSB(_lineBuffer, 2, _lineBuffer.Length - 2);
                            break;

                        case 'V':
                            if (currentGO != null)
                                currentGO.variantPrefabGUID = InternFromSB(_lineBuffer, 2, _lineBuffer.Length - 2);
                            break;

                        case 'C':
                            if (currentGO == null) return false;
                            if (!ParseComponentLine(_lineBuffer, currentGO, out currentCompRefs)) return false;
                            break;

                        case 'A':
                            if (currentGO == null) return false;
                            if (!ParseAssetLine(_lineBuffer, currentGO, ref currentCompRefs)) return false;
                            break;
                    }

                    if (eof) break;
                }
            }

            FR2_LOG.Log($"SceneCache2: Loaded cache from {filePath} ({_goIDs.Count} GameObjects, hash={_loadedAssetHash})");
            return true;
        }

        private static bool ReadLine(StreamReader reader, StringBuilder sb)
        {
            while (true)
            {
                int ch = reader.Read();
                if (ch == -1) return false;
                if (ch == '\r') { if (reader.Peek() == '\n') reader.Read(); return true; }
                if (ch == '\n') return true;
                sb.Append((char)ch);
            }
        }

        private bool ParseHeader(StringBuilder sb)
        {
            int version = 0;
            string hash = null;
            bool partial = false;

            int vIndex = IndexOf(sb, "\"v\":", 0);
            if (vIndex >= 0)
            {
                int vStart = vIndex + 4;
                int vEnd = IndexOf(sb, ',', vStart);
                if (vEnd < 0) vEnd = IndexOf(sb, '}', vStart);
                if (vEnd > vStart) version = ParseInt(sb, vStart, vEnd);
            }

            int hIndex = IndexOf(sb, "\"hash\":\"", 0);
            if (hIndex >= 0)
            {
                int hStart = hIndex + 8;
                int hEnd = IndexOf(sb, '"', hStart);
                if (hEnd > hStart) hash = InternFromSB(sb, hStart, hEnd - hStart);
            }

            int pIndex = IndexOf(sb, "\"partial\":true", 0);
            if (pIndex >= 0) partial = true;

            if (version != CACHE_VERSION)
            {
                FR2_LOG.Log($"SceneCache2: Cache version mismatch (file={version}, expected={CACHE_VERSION})");
                return false;
            }

            _loadedAssetHash = hash;
            _isPartialCache = partial;
            return true;
        }

        private static bool ParseComponentLine(StringBuilder sb, GOCacheEntry currentGO, out CompRefs compRefs)
        {
            compRefs = null;
            int arrowIndex = IndexOf(sb, "->", 2);
            if (arrowIndex < 0) return false;

            if (!TryParseUlong(sb, 2, arrowIndex, out ulong compID)) return false;

            if (!currentGO.compsWithRefs.TryGetValue(compID, out compRefs))
            {
                compRefs = new CompRefs();
                currentGO.compsWithRefs[compID] = compRefs;
            }

            int refsStart = arrowIndex + 2;
            if (refsStart >= sb.Length) return true;

            int pos = refsStart;
            while (pos < sb.Length)
            {
                int spaceIdx = IndexOf(sb, ' ', pos);
                int end = spaceIdx >= 0 ? spaceIdx : sb.Length;

                int pipeIdx = IndexOf(sb, '|', pos, end);
                int idEnd = pipeIdx >= 0 ? pipeIdx : end;

                if (!TryParseUlong(sb, pos, idEnd, out ulong refID)) return false;
                string propPath = pipeIdx >= 0 ? InternFromSB(sb, pipeIdx + 1, end - pipeIdx - 1) : null;
                compRefs.sceneRefs.Add(new SceneObjRef(refID, propPath));

                pos = end + 1;
            }

            return true;
        }

        private static bool ParseAssetLine(StringBuilder sb, GOCacheEntry currentGO, ref CompRefs currentCompRefs)
        {
            int firstSpace = IndexOf(sb, ' ', 2);
            if (firstSpace < 0) return false;

            string guid = InternFromSB(sb, 2, firstSpace - 2);

            int secondSpace = IndexOf(sb, ' ', firstSpace + 1);
            int fileIdEnd = secondSpace >= 0 ? secondSpace : sb.Length;

            if (!TryParseUlong(sb, firstSpace + 1, fileIdEnd, out ulong localFileId)) return false;
            string propertyPath = secondSpace >= 0 ? InternFromSB(sb, secondSpace + 1, sb.Length - secondSpace - 1) : null;

            if (currentCompRefs == null)
            {
                currentCompRefs = new CompRefs();
                currentGO.compsWithRefs[0] = currentCompRefs;
            }

            currentCompRefs.assets.Add(new AssetRef(guid, localFileId, propertyPath));
            return true;
        }

        private static int IndexOf(StringBuilder sb, char c, int start)
        {
            for (int i = start; i < sb.Length; i++)
                if (sb[i] == c) return i;
            return -1;
        }

        private static int IndexOf(StringBuilder sb, char c, int start, int end)
        {
            for (int i = start; i < end; i++)
                if (sb[i] == c) return i;
            return -1;
        }

        private static int IndexOf(StringBuilder sb, string s, int start)
        {
            int sLen = s.Length;
            int limit = sb.Length - sLen;
            for (int i = start; i <= limit; i++)
            {
                bool match = true;
                for (int j = 0; j < sLen; j++)
                {
                    if (sb[i + j] != s[j]) { match = false; break; }
                }
                if (match) return i;
            }
            return -1;
        }

        private static int ParseInt(StringBuilder sb, int start, int end)
        {
            int result = 0;
            for (int i = start; i < end; i++)
            {
                char c = sb[i];
                if (c < '0' || c > '9') break;
                result = result * 10 + (c - '0');
            }
            return result;
        }

        private static bool TryParseUlong(StringBuilder sb, int start, int end, out ulong result)
        {
            result = 0;
            if (start >= end) return false;
            for (int i = start; i < end; i++)
            {
                char c = sb[i];
                if (c < '0' || c > '9') return false;
                result = result * 10 + (ulong)(c - '0');
            }
            return true;
        }
    }
}
