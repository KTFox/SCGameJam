using UnityEditor;
using UnityEngine;

namespace vietlabs.fr2
{
    public static class FR2_Initializer
    {
        [InitializeOnLoadMethod]
        static void Init()
        {
            AssemblyReloadEvents.afterAssemblyReload  -= Reload;
            AssemblyReloadEvents.afterAssemblyReload  += Reload;
            
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged; 
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            switch (state)
            {
                case PlayModeStateChange.EnteredPlayMode:
                    break;
                case PlayModeStateChange.EnteredEditMode:  
                {
                    if (FR2_SettingExt.disable) return;
                    
                    Reload();
                    if (FR2_Cache.autoRefresh) FR2_Cache.IncrementalRefresh();
                    break;
                }
            }
        }
        
        static void Reload()
        {
            if (FR2_SettingExt.disable) return;
            FR2_Addressable.Scan();
            FR2_Cache.Reload();
        }
    }
}
