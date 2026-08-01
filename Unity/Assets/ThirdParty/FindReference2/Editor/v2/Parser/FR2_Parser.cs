// #define FR2_PARSER_DEBUG

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using AddUsageCB = System.Action<string, long>;

namespace vietlabs.fr2
{
    // Public APIs
    internal static partial class FR2_Parser 
    {
        private static readonly HashSet<string> META_FILES = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".texture2darray",".png", ".jpg", ".jpeg", ".tga", ".tif", ".tiff", ".psd", ".bmp", ".exr", ".gif",
            ".shader", ".cs", ".shadergraph", ".vfx", ".vfxoperator", ".vfxblock"
        };
        
        private static readonly HashSet<string> SHADER_FILES = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".shader", ".hlsl", ".cginc", ".glsl"
        };
        
        public static bool IsReadable(string assetPath)
        {
            int lastDot = assetPath.LastIndexOf('.');
            if (lastDot < 0) return false;
            string ext = assetPath.Substring(lastDot);
            return IsReadableExtension(ext);
        }
        
        public static bool IsReadableExtension(string ext)
        {
            return YAML_FILES.Contains(ext) 
                || UI_TOOLKIT_EXTENSIONS.Contains(ext)
                || SHADER_GRAPH_FILES.ContainsKey(ext)
                || META_FILES.Contains(ext)
                || MODEL_FILES.Contains(ext)
                || SHADER_FILES.Contains(ext);
        }
        
        public static void ReadContent(string filePath, AddUsageCB callback)
        {
            int lastDot = filePath.LastIndexOf('.');
            if (lastDot < 0) return;
            
            var ext = filePath.Substring(lastDot);
            var readMeta = META_FILES.Contains(ext);
            if (readMeta)
            {
                ReadContent_YAML(filePath + ".meta", callback);
            }
            
            if (YAML_FILES.Contains(ext))
            {
                ReadContent_YAML(filePath, callback); 
                return;
            }
            
            if (SHADER_GRAPH_FILES.TryGetValue(ext, out var shaderGraphParser))
            {
                ReadContent_ShaderGraph(shaderGraphParser, filePath, callback);
                return;
            }

            if (UI_TOOLKIT_EXTENSIONS.Contains(ext))
            {
                ReadContent_UIToolkit(ext, filePath, callback);
                return;
            }
            
            if (SHADER_FILES.Contains(ext))
            {
                ReadContent_Shader(filePath, callback);
                return;
            }
            
            if (!readMeta) FR2_LOG.Log("Unknown file type: " + filePath);
        }
    }
    
    
    internal static partial class FR2_Parser
    {
        private static string parsingFilePath;
        private static void AddObjectUsage(UnityEngine.Object refObj, AddUsageCB callback)
        {
            if (refObj == null) return;
            var (refGUID, fileId) = FR2_SelectionManager.GetCachedGuidAndLocalId(refObj);
            if (!string.IsNullOrEmpty(refGUID))
            {
                callback(refGUID, fileId);
            }
        }

        private static void Read(string filePath, Func<string, (string, long)> lineHandler, Action<string, long> add, bool doubleCheck = true)
        {
            if (!File.Exists(filePath)) return;

            parsingFilePath = filePath;
            
            // Use a buffer to reduce file I/O overhead
            using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096))
            using (var sr = new StreamReader(fs, Encoding.UTF8, false, 4096))
            {
                string line;

                while ((line = sr.ReadLine()) != null)
                {
                    if (string.IsNullOrEmpty(line)) continue;
                    if (line.Contains("spriteID:")) continue;
                    if (line.Contains("Hash:")) continue;
                    
                    var (guid, fileId) = lineHandler(line);
                    if (!string.IsNullOrEmpty(guid))
                    {
                        add(guid, fileId);

                        // Debug.Log($"Found: <{guid}:{fileId}>");
                        continue;
                    }

                    if (!doubleCheck) continue;
                    guid = ExtractGuid(line);
                    if (!string.IsNullOrEmpty(guid))
                    {
                        if (FR2_SettingExt.parserVerbose)
                            FR2_LOG.LogWarning($"Missed GUID <{guid}>?\n{filePath}\n{line}\n");
                        add(guid, 0);
                    }
                }
            }
        }

        private static string ExtractGuid(string line)
        {
            const int GuidLength = 32;
            int hexCount = 0;

            for (int i = 0; i < line.Length; i++)
            {
                if (!line[i].IsHexChar())
                {
                    hexCount = 0;
                    continue;
                }
                
                // Either longer or shorter hex sequence - it's not the guid we want
                if (++hexCount != GuidLength) continue;
                
                // This is when a longer hex-sequence matches
                if (i + 1 < line.Length && line[i + 1].IsHexChar()) continue;
                
                // Validate against AssetDatabase to reject false positives (commit hashes, UUIDs, etc.)
                string candidate = line.Substring(i - GuidLength + 1, GuidLength);
                string path = AssetDatabase.GUIDToAssetPath(candidate);
                if (!string.IsNullOrEmpty(path)) return candidate;
            }
            
            return null;
        }
        
        private static (string guid, long fileId) FindRef(string source, string guidPattern, string fileIdPattern, string separatorPattern)
        {
            string guid = Find(source, guidPattern, separatorPattern);
            if (string.IsNullOrEmpty(guid)) return (null, -1);

            if (string.IsNullOrEmpty(fileIdPattern)) return (guid, -1);
            string fileIdStr = Find(source, fileIdPattern, separatorPattern);
            if (string.IsNullOrEmpty(fileIdStr)) return (null, -1);
            
            long fileId = long.TryParse(fileIdStr, out long fid) ? fid : -1;
            // Debug.Log($"Found: {guid}/{fileId}\t\t {source}");
            return (guid, fileId);
        }

        private static string Find(string source, string str_begin, string str_end)
        {
            var st = source.IndexOf(str_begin, StringComparison.Ordinal);
            if (st == -1) return null;
            
            st += str_begin.Length;
            while (st < source.Length && char.IsWhiteSpace(source[st]))
            {
                st++;
            }
            
            if (string.IsNullOrEmpty(str_end)) // no end: determine by length
            {
                var remainingLength = source.Length - st;
                return remainingLength < 32 ? null : source.Substring(st, 32);
            }

            var ed = source.IndexOf(str_end, st, StringComparison.Ordinal);
            if (ed == -1) return null;
            while (ed > st && char.IsWhiteSpace(source[ed - 1]))
            {
                ed--;
            }
            return source.Substring(st, ed - st);
        }
    }
}
