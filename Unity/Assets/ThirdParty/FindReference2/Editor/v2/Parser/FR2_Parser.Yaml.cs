using System;
using System.Collections.Generic;
using System.IO;

namespace vietlabs.fr2
{
    internal static partial class FR2_Parser // Yaml
    {
        private static readonly HashSet<string> YAML_FILES = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".anim", ".controller", ".mat", ".unity", ".guiskin", ".prefab", 
            ".overridecontroller", ".mask", ".rendertexture", ".cubemap", ".flare", 
            ".playable", ".physicsmaterial", ".fontsettings", ".asset", ".prefs", 
            ".spriteatlas", ".terrainlayer", ".asmdef", ".preset", ".spriteLib", ".texture2darray",
            ".vfx", ".vfxoperator", ".vfxblock"
        };
        
        private static void ReadContent_YAML(string filePath, Action<string, long> callback)
        {
            int lastDot = filePath.LastIndexOf('.');
            string ext = lastDot >= 0 ? filePath.Substring(lastDot) : string.Empty;
            bool isBinary = BINARY_ASSET.Contains(ext) && Read_VerifyBinaryAsset(filePath);

            if (isBinary)
            {
                ReadContent_BinaryAsset(filePath, callback);
                return;
            }
            
            Read(filePath, ParseLine_Yaml, callback);
        }
        
        private static (string guid, long fileId) ParseLine_Yaml(string line)
        {
            // Try standard patterns
            var result = FindRef(line, "guid:", "fileID:", ",");
            if (result.guid != null) return result;
    
            result = FindRef(line, "guid:", null, null);
            if (result.guid != null) return result;
    
            result = FindRef(line, "m_AssetGUID:", null, null);
            if (result.guid != null) return result;
    
            result = FindRef(line, "m_AssetGuid:", null, null);
            if (result.guid != null) return result;
    
            result = FindRef(line, "AssetGUID:", null, null);
            if (result.guid != null) return result;
    
            result = FindRef(line, "GUID:", null, null);
            if (result.guid != null) return result;
    
            // Try dash-prefixed: "- <GUID>: <fileID>"
            int dashIdx = line.IndexOf("- ");
            if (dashIdx == -1) return (null, -1);
            
            int colonIdx = line.IndexOf(':', dashIdx + 2);
            if (colonIdx <= dashIdx + 2 || colonIdx - dashIdx - 2 != 32) return (null, -1);
    
            string guid = line.Substring(dashIdx + 2, 32);
    
            // Parse fileId
            int fileIdStart = colonIdx + 1;
            while (fileIdStart < line.Length && line[fileIdStart] == ' ') fileIdStart++;
            if (fileIdStart >= line.Length) return (guid, -1);
    
            int fileIdEnd = fileIdStart;
            while (fileIdEnd < line.Length && char.IsDigit(line[fileIdEnd])) fileIdEnd++;
    
            long fileId = fileIdEnd > fileIdStart && long.TryParse(line.Substring(fileIdStart, fileIdEnd - fileIdStart), out long fid) ? fid : -1;
            return (guid, fileId);
        }
    }
}
