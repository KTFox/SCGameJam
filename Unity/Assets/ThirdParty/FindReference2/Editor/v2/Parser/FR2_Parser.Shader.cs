using System;
using System.IO;

namespace vietlabs.fr2
{
    internal static partial class FR2_Parser
    {
        private static void ReadContent_Shader(string filePath, Action<string, long> callback)
        {
            Read(filePath, ParseLine_Shader, callback, false);
        }

        private static (string guid, long fileId) ParseLine_Shader(string line)
        {
            int trimStart = 0;
            while (trimStart < line.Length && char.IsWhiteSpace(line[trimStart])) trimStart++;
            if (trimStart + 8 > line.Length) return (null, -1);
            if (line[trimStart] != '#') return (null, -1);
            if (string.Compare(line, trimStart, "#include", 0, 8, StringComparison.Ordinal) != 0) return (null, -1);

            string includePath = Find(line, "#include \"", "\"");
            if (string.IsNullOrEmpty(includePath))
                includePath = Find(line, "#include <", ">");
            if (string.IsNullOrEmpty(includePath)) return (null, -1);

            string resolved = ResolveShaderIncludePath(includePath, parsingFilePath);
            if (string.IsNullOrEmpty(resolved)) return (null, -1);

            string guid = FR2_Cache.AssetPathToGUID(resolved);
            return string.IsNullOrEmpty(guid) ? (null, -1) : (guid, -1L);
        }

        private static string ResolveShaderIncludePath(string includePath, string sourceFilePath)
        {
            if (string.IsNullOrEmpty(includePath)) return null;

            if (includePath.StartsWith("Assets/") || includePath.StartsWith("Packages/"))
                return File.Exists(includePath) ? includePath : null;

            if (!string.IsNullOrEmpty(sourceFilePath))
            {
                string sourceDir = Path.GetDirectoryName(sourceFilePath);
                if (!string.IsNullOrEmpty(sourceDir))
                {
                    string relative = Path.GetFullPath(Path.Combine(sourceDir, includePath)).Replace('\\', '/');
                    string projectPath = UnityEngine.Application.dataPath.Replace("/Assets", "");
                    if (relative.StartsWith(projectPath))
                    {
                        string assetRelative = relative.Substring(projectPath.Length + 1);
                        if (File.Exists(assetRelative)) return assetRelative;
                    }
                }
            }

            string[] searchDirs = {
                "Packages/com.unity.render-pipelines.core/ShaderLibrary/",
                "Packages/com.unity.render-pipelines.universal/ShaderLibrary/",
                "Packages/com.unity.render-pipelines.high-definition/Runtime/ShaderLibrary/",
            };
            string fileName = Path.GetFileName(includePath);
            foreach (string dir in searchDirs)
            {
                string candidate = dir + fileName;
                if (File.Exists(candidate)) return candidate;
            }

            return null;
        }
    }
}
