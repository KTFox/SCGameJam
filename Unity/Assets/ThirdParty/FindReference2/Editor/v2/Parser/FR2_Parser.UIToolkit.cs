using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace vietlabs.fr2
{
    internal static partial class FR2_Parser
    {
        private static readonly HashSet<string> IMAGE_EXTENSIONS = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".png", ".jpg", ".jpeg", ".tga", ".tif", ".tiff", ".psd", ".bmp", ".exr", ".gif"
        };
        
        private static readonly HashSet<string> UI_TOOLKIT_EXTENSIONS = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".tss", ".uxml", ".uss" };

        private static void ReadContent_UIToolkit(string ext, string assetPath, Action<string, long> callback)
        {
            string realPath = GetRealFilePath(assetPath);
            if (string.IsNullOrEmpty(realPath) || !File.Exists(realPath)) return;
            
            parsingFilePath = assetPath;
            Action<string, Action<string, long>> lineParser = ext == ".tss" ? ParseLine_Tss : (Action<string, Action<string, long>>)ParseLine_Uxml_Uss;
            
            using (var fs = new FileStream(realPath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096))
            using (var sr = new StreamReader(fs, Encoding.UTF8, false, 4096))
            {
                string line;
                while ((line = sr.ReadLine()) != null)
                {
                    if (!string.IsNullOrEmpty(line)) lineParser(line, callback);
                }
            }
        }
        
        private static string GetRealFilePath(string assetPath)
        {
            if (File.Exists(assetPath)) return assetPath;
            
            try
            {
                string fullPath = Path.GetFullPath(assetPath);
                if (File.Exists(fullPath)) return fullPath;
            }
            catch { }
            
            if (!assetPath.StartsWith("Packages/")) return null;
            
            string[] pathParts = assetPath.Split('/');
            if (pathParts.Length < 2) return null;
            
            string packageName = pathParts[1];
            string packageCachePath = Path.Combine(Application.dataPath.Replace("/Assets", ""), "Library/PackageCache");
            if (!Directory.Exists(packageCachePath)) return null;
            
            foreach (var dir in Directory.GetDirectories(packageCachePath))
            {
                string dirName = Path.GetFileName(dir);
                if (!dirName.StartsWith(packageName + "@") && dirName != packageName) continue;
                
                string relativePath = assetPath.Substring(("Packages/" + packageName).Length);
                string candidatePath = dir + relativePath;
                if (File.Exists(candidatePath)) return candidatePath;
            }
            
            return null;
        }
        
        private static void ParseLine_Uxml_Uss(string line, Action<string, long> add)
        {
            var result = FindRef(line, "guid=", "fileID=", "&");
            if (result.guid != null)
            {
                add(result.guid, result.fileId);
                return;
            }
            
            if (TryResolve(line, "src=", "src=\"", "\"", add)) return;
            if (TryResolve(line, "icon-image=", "icon-image=\"", "\"", add)) return;
            if (TryResolveCustomImageAttrs(line, add)) return;
            if (TryResolveUrl(line, add)) return;
            if (TryResolveResource(line, add)) return;
            TryResolveImport(line, add);
        }

        private static void ParseLine_Tss(string line, Action<string, long> add)
        {
            string path = Find(line, "@importurl(\"/", "\")");
            if (!string.IsNullOrEmpty(path))
            {
                ResolveAndAdd(path, add);
                return;
            }
            
            if (line.Contains("@import") && TryResolveImportUrl(line, add)) return;
            if (TryResolveUrl(line, add)) return;
            TryResolveResource(line, add);
        }
        
        private static bool TryResolve(string line, string contains, string start, string end, Action<string, long> add)
        {
            if (!line.Contains(contains)) return false;
            string path = Find(line, start, end);
            if (string.IsNullOrEmpty(path)) return false;
            ResolveAndAdd(path, add);
            return true;
        }
        
        private static bool TryResolveCustomImageAttrs(string line, Action<string, long> add)
        {
            if (!line.Contains("-image=") && !line.Contains("-icon=")) return false;
            
            bool found = false;
            foreach (var pattern in new[] { "-image=\"", "-icon=\"" })
            {
                int startIdx = line.IndexOf(pattern);
                if (startIdx < 0) continue;
                
                startIdx += pattern.Length;
                int endIdx = line.IndexOf("\"", startIdx);
                if (endIdx <= startIdx) continue;
                
                string path = line.Substring(startIdx, endIdx - startIdx);
                if (string.IsNullOrEmpty(path) || (!path.Contains("/") && !path.Contains("."))) continue;
                
                ResolveAndAdd(path, add);
                found = true;
            }
            return found;
        }
        
        private static bool TryResolveUrl(string line, Action<string, long> add)
        {
            if (!line.Contains("url(")) return false;
            
            string path = FindQuoted(line, "url(");
            if (string.IsNullOrEmpty(path) || path.StartsWith("unity-theme://")) return false;
            
            ResolveAndAdd(path, add);
            return true;
        }
        
        private static bool TryResolveResource(string line, Action<string, long> add)
        {
            if (!line.Contains("resource(")) return false;
            
            string path = FindQuoted(line, "resource(");
            if (string.IsNullOrEmpty(path)) return false;
            
            ResolveResourcePath(path, add);
            return true;
        }
        
        private static bool TryResolveImport(string line, Action<string, long> add)
        {
            if (!line.Contains("@import")) return false;
            
            if (line.Contains("url(") && TryResolveImportUrl(line, add)) return true;
            
            string path = FindQuoted(line, "@import ");
            if (string.IsNullOrEmpty(path)) return false;
            
            ResolveAndAdd(path, add);
            return true;
        }
        
        private static bool TryResolveImportUrl(string line, Action<string, long> add)
        {
            string path = Find(line, "@import url(\"", "\")") ?? Find(line, "@import url('", "')");
            if (string.IsNullOrEmpty(path) || path.StartsWith("unity-theme://")) return false;
            
            ResolveAndAdd(path, add);
            return true;
        }
        
        private static string FindQuoted(string line, string prefix)
        {
            return Find(line, prefix + "\"", "\")") 
                ?? Find(line, prefix + "'", "')") 
                ?? Find(line, prefix + "\"", "\";") 
                ?? Find(line, prefix + "'", "';");
        }
        
        private static void ResolveAndAdd(string path, Action<string, long> add)
        {
            string resolvedPath = ResolvePath(path);
            if (string.IsNullOrEmpty(resolvedPath)) return;
            
            string normalized = StripLeadingSlashes(resolvedPath);
            if (string.IsNullOrEmpty(normalized)) return;
            
#if FR2_DEBUG || FR2_DEV
            if (normalized != resolvedPath && FR2_SettingExt.parserVerbose)
                FR2_LOG.LogWarning($"[FR2] Normalized: {path} -> {normalized}");
#endif
            
            string guid = FR2_Cache.AssetPathToGUID(normalized);
            if (string.IsNullOrEmpty(guid)) return;
            
            add(guid, -1);
            AddImageVariants(normalized, add);
        }
        
        private static void AddImageVariants(string resolvedPath, Action<string, long> add)
        {
            string ext = FR2_StringCache.GetCachedExtension(resolvedPath);
            if (!IMAGE_EXTENSIONS.Contains(ext)) return;
            
            string baseName = resolvedPath.Substring(0, resolvedPath.Length - ext.Length);
            string variantPath = baseName.EndsWith("@2x") 
                ? baseName.Substring(0, baseName.Length - 3) + ext 
                : baseName + "@2x" + ext;
            
            string variantGuid = FR2_Cache.AssetPathToGUID(variantPath);
            if (!string.IsNullOrEmpty(variantGuid)) add(variantGuid, -1);
        }
        
        private static string ResolvePath(string path)
        {
            if (path.StartsWith("project://database/"))
            {
                string assetPath = path.Substring("project://database/".Length);
                if (AssetExists(assetPath)) return assetPath;
            }
            
            // Handle project:// (double slash) before project:/ (single slash)
            // This prevents project://Packages/... from being treated as project:/... 
            // which would result in //Packages/... (malformed path)
            if (path.StartsWith("project://"))
            {
                string assetPath = path.Substring("project://".Length);
                if (AssetExists(assetPath)) return assetPath;
            }
            
            if (path.StartsWith("project:/"))
            {
                string assetPath = path.Substring("project:/".Length);
                if (AssetExists(assetPath)) return assetPath;
            }
            
            if (path.StartsWith("/Packages/"))
            {
                string assetPath = path.Substring(1);
                if (AssetExists(assetPath)) return assetPath;
            }
            
            if (AssetExists(path)) return path;
            
            string folder = parsingFilePath.Substring(0, parsingFilePath.LastIndexOf('/'));
            
            if (path.StartsWith("./") || path.StartsWith("../"))
            {
                string fullPath = ResolveRelativePath(folder, path);
                if (!string.IsNullOrEmpty(fullPath) && AssetExists(fullPath)) return fullPath;
            }
            else
            {
                string fullPath = folder + "/" + path;
                if (AssetExists(fullPath)) return fullPath;
            }
            
            return null;
        }
        
        private static string StripLeadingSlashes(string path)
        {
            if (string.IsNullOrEmpty(path) || path[0] != '/') return path;
            
            int start = 1;
            while (start < path.Length && path[start] == '/') start++;
            return path.Substring(start);
        }
        
        private static bool AssetExists(string path)
        {
            path = StripLeadingSlashes(path);
            return !string.IsNullOrEmpty(path) && !string.IsNullOrEmpty(FR2_Cache.AssetPathToGUID(path));
        }
        
        private static string ResolveRelativePath(string basePath, string relativePath)
        {
            var segments = basePath.Split('/').ToList();
            
            foreach (var segment in relativePath.Split('/'))
            {
                if (segment == "." || string.IsNullOrEmpty(segment)) continue;
                if (segment == "..") { if (segments.Count > 0) segments.RemoveAt(segments.Count - 1); }
                else segments.Add(segment);
            }
            
            return string.Join("/", segments);
        }
        
        private static void ResolveResourcePath(string resourcePath, Action<string, long> add)
        {
            string[] extensions = { "", ".png", ".jpg", ".jpeg", ".tga", ".psd", ".tif", ".tiff" };
            string[] basePaths = { "Assets/Editor Default Resources/", "Assets/Resources/", "Assets/" };
            
            foreach (string ext in extensions)
            {
                foreach (string basePath in basePaths)
                {
                    string searchPath = basePath + resourcePath + ext;
                    string guid = FR2_Cache.AssetPathToGUID(searchPath);
                    if (string.IsNullOrEmpty(guid)) continue;
                    
                    add(guid, -1);
                    AddImageVariants(searchPath, add);
                    return;
                }
            }
            
            string fileName = Path.GetFileNameWithoutExtension(resourcePath);
            foreach (string guid in AssetDatabase.FindAssets(fileName))
            {
                string path = FR2_Cache.GUIDToAssetPath(guid);
                if (!path.Contains(resourcePath) && !path.EndsWith("/" + fileName + FR2_StringCache.GetCachedExtension(path))) continue;
                
                add(guid, -1);
                AddImageVariants(path, add);
                return;
            }
        }
    }
}
