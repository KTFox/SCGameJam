using System.Collections.Generic;
using System.IO;

namespace vietlabs.fr2
{
    internal static class FR2_StringCache
    {
        private static Dictionary<string, string> _extensionCache = new Dictionary<string, string>(10000);
        
        public static string GetCachedExtension(string path)
        {
            if (string.IsNullOrEmpty(path)) return string.Empty;
            
            if (_extensionCache.TryGetValue(path, out string cached))
                return cached;
            
            string ext = Path.GetExtension(path).ToLowerInvariant();
            _extensionCache[path] = ext;
            return ext;
        }
        
        public static void Clear()
        {
            _extensionCache.Clear();
        }
    }
}
