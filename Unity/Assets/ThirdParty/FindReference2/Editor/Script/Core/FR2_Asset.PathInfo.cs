using System;
using System.Globalization;
using UnityEditor;

namespace vietlabs.fr2
{
    internal partial class FR2_Asset
    {
        // ----------------------- PATH INFO ------------------------

        [NonSerialized] private string m_assetFolder;
        [NonSerialized] private string m_assetName;
        [NonSerialized] private string m_assetPath;
        [NonSerialized] private string m_extension;
        [NonSerialized] private bool m_inEditor;
        [NonSerialized] private bool m_inPackage;
        [NonSerialized] private bool m_inPlugins;
        [NonSerialized] private bool m_inResources;
        [NonSerialized] private bool m_inStreamingAsset;
        [NonSerialized] private bool m_pathLoaded;

        public string assetName => LoadPathInfo().m_assetName;
        public string assetPath
        {
            get
            {
                if (!string.IsNullOrEmpty(m_assetPath)) return m_assetPath;
                m_assetPath = FR2_Cache.GUIDToAssetPath(guid);
                if (string.IsNullOrEmpty(m_assetPath)) state = AssetState.MISSING;
                return m_assetPath;
            }
        }

        public string parentFolderPath => LoadPathInfo().m_assetFolder;
        public string assetFolder => LoadPathInfo().m_assetFolder;
        public string extension => LoadPathInfo().m_extension;
        public bool inEditor => LoadPathInfo().m_inEditor;
        public bool inPlugins => LoadPathInfo().m_inPlugins;
        public bool inPackages => LoadPathInfo().m_inPackage;
        public bool inResources => LoadPathInfo().m_inResources;
        public bool inStreamingAsset => LoadPathInfo().m_inStreamingAsset;

        internal bool IsExcluded
        {
            get
            {
                if (excludeTS >= ignoreTS) return _isExcluded;

                excludeTS = ignoreTS;
                _isExcluded = false;

                var h = FR2_Setting.IgnoreAsset;
                foreach (string item in h)
                {
                    if (!m_assetPath.StartsWith(item, false, CultureInfo.InvariantCulture)) continue;
                    _isExcluded = true;
                    return true;
                }

                return false;
            }
        }

        public FR2_Asset LoadPathInfo()
        {
            if (m_pathLoaded) return this;
            
            InvalidateDrawCache();
            m_pathLoaded = true;

            m_assetPath = FR2_Cache.GUIDToAssetPath(guid);
            if (string.IsNullOrEmpty(m_assetPath))
            {
                state = AssetState.MISSING;
                return this;
            }

            // OPTIMIZED: Inline path parsing to avoid struct allocation and reduce string operations
            int lastSlash = m_assetPath.LastIndexOf('/');
            int lastDot = m_assetPath.LastIndexOf('.');
            
            // Extension: from last dot to end (if dot exists after last slash)
            m_extension = (lastDot > lastSlash) ? m_assetPath.Substring(lastDot) : string.Empty;
            
            // Asset name: from last slash+1 to last dot (or end if no extension)
            int nameStart = lastSlash + 1;
            int nameEnd = (lastDot > lastSlash) ? lastDot : m_assetPath.Length;
            m_assetName = m_assetPath.Substring(nameStart, nameEnd - nameStart);
            
            // Folder: from start to last slash+1 (include trailing slash)
            string rawFolder = (lastSlash >= 0) ? m_assetPath.Substring(0, lastSlash + 1) : string.Empty;
            
            // OPTIMIZED: Check first char to reduce StartsWith calls
            char firstChar = m_assetPath.Length > 0 ? m_assetPath[0] : '\0';
            bool startsWithAssets = firstChar == 'A' && m_assetPath.StartsWith("Assets/");
            m_inPackage = firstChar == 'P' && m_assetPath.StartsWith("Packages/");
            
            if (startsWithAssets)
            {
                m_assetFolder = rawFolder.Length > 7 ? rawFolder.Substring(7) : string.Empty;
            }
            else if (m_inPackage)
            {
                m_assetFolder = rawFolder;
            }
            else if (firstChar == 'P' && m_assetPath.StartsWith("ProjectSettings/"))
            {
                m_assetFolder = rawFolder;
            }
            else if (firstChar == 'L' && m_assetPath.StartsWith("Library/"))
            {
                m_assetFolder = rawFolder;
            }
            else
            {
                m_assetFolder = "built-in/";
            }
            
            // Special folder detection
            m_inEditor = m_assetPath.Contains("/Editor/") || m_assetPath.Contains("/Editor Default Resources/");
            m_inResources = m_assetPath.Contains("/Resources/");
            m_inStreamingAsset = m_assetPath.Contains("/StreamingAssets/");
            m_inPlugins = m_assetPath.Contains("/Plugins/");
            
            return this;
        }
    }
}
