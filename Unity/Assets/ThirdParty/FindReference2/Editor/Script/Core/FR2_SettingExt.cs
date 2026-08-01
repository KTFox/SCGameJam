using System;
using System.IO;
using UnityEditor;
using UnityEngine;
namespace vietlabs.fr2
{
    [Serializable] internal class FR2_SettingExt
    {
        public static FR2_AutoRefreshMode autoRefreshMode
        {
            get => inst._autoRefresh;
            set
            {
                if (inst._autoRefresh == value) return;
                inst._autoRefresh = value;

                FR2_Cache.MarkDirty();
                ScheduleSave();
                FR2_Event.DispatchGlobal(new SettingChangedEvent(SettingKeys.AutoRefreshMode));
            }
        }

        public static bool isAutoRefreshEnabled
        {
            get
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode) return false;
                return autoRefreshMode == FR2_AutoRefreshMode.On;
            }
        }
        
        public static bool disable
        {
            get => inst.internalDisabled;
            set => inst.internalDisabled = value;
        }
        
        internal static bool userDisabled => inst._disabled;
        
        public static bool hideToolsWarning
        {
            get => inst._hideToolsWarning;
            set
            {
                if (inst._hideToolsWarning == value) return;
                inst._hideToolsWarning = value;

                FR2_Cache.MarkDirty();
                ScheduleSave();
            }
        }

        public static bool isGitProject;
        public static bool gitIgnoreAdded
        {
            get => inst._gitIgnoreAdded;
            set
            {
                if (inst._gitIgnoreAdded == value) return;
                inst._gitIgnoreAdded = value;
                
                FR2_Cache.MarkDirty();
                ScheduleSave();
            }
        }
        
        public static bool hideGitIgnoreWarning
        {
            get => inst._hideGitIgnoreWarning;
            set
            {
                if (inst._hideGitIgnoreWarning == value) return;
                inst._hideGitIgnoreWarning = value;
                
                FR2_Cache.MarkDirty();
                ScheduleSave();
            }
        }
        
        public static bool showHierarchyReferenceCount
        {
            get => inst._showHierarchyReferenceCount;
            set
            {
                if (inst._showHierarchyReferenceCount == value) return;
                inst._showHierarchyReferenceCount = value;
                
                FR2_HierarchyReferenceIndicator.SetEnabled(value);
                EditorApplication.RepaintHierarchyWindow();
                
                ScheduleSave();
                FR2_Event.DispatchGlobal(new SettingChangedEvent(SettingKeys.ShowHierarchyReferenceCount));
            }
        }
        
        public static float hierarchyReferenceCountOffset
        {
            get => inst._hierarchyReferenceCountOffset;
            set
            {
                if (Mathf.Approximately(inst._hierarchyReferenceCountOffset, value)) return;
                inst._hierarchyReferenceCountOffset = value;
                
                EditorApplication.RepaintHierarchyWindow();
                
                ScheduleSave();
                FR2_Event.DispatchGlobal(new SettingChangedEvent(SettingKeys.HierarchyReferenceCountOffset));
            }
        }
        
        public static bool dbValidation
        {
            get => inst._dbValidation;
            set
            {
                if (inst._dbValidation == value) return;
                inst._dbValidation = value;
                
                ScheduleSave();
                FR2_Event.DispatchGlobal(new SettingChangedEvent(SettingKeys.DbValidation));
            }
        }
        
        /// <summary>
        /// When true, log parser fallback warnings (e.g. "Missed GUID") for debugging.
        /// When false (default), suppress these - refs are still found via ExtractGuid fallback.
        /// </summary>
        public static bool parserVerbose
        {
            get => inst._parserVerbose;
            set
            {
                if (inst._parserVerbose == value) return;
                inst._parserVerbose = value;
                
                ScheduleSave();
                FR2_Event.DispatchGlobal(new SettingChangedEvent(SettingKeys.ParserVerbose));
            }
        }
        
        public static bool showPackagesAndBuiltIn
        {
            get => inst._showPackagesAndBuiltIn;
            set
            {
                if (inst._showPackagesAndBuiltIn == value) return;
                inst._showPackagesAndBuiltIn = value;
                
                ScheduleSave();
                FR2_Event.DispatchGlobal(new SettingChangedEvent(SettingKeys.HidePackagesAndBuiltIn));
            }
        }
        
        public static int maxSelectionCount
        {
            get => inst._maxSelectionCount;
            set
            {
                if (inst._maxSelectionCount == value) return;
                inst._maxSelectionCount = Mathf.Max(1, value);
                
                ScheduleSave();
            }
        }
        
        public static int bfsFrontierCap
        {
            get => inst._bfsFrontierCap;
            set
            {
                if (inst._bfsFrontierCap == value) return;
                inst._bfsFrontierCap = Mathf.Clamp(value, 1000, 500000);
                
                ScheduleSave();
            }
        }
        
        private const string path = "Library/FR2/fr2.cfg";
        private static FR2_SettingExt inst;
        
        static FR2_SettingExt()
        {
            inst = new FR2_SettingExt();
            if (!File.Exists(path)) return;

            try
            {
                string content = File.ReadAllText(path);
                JsonUtility.FromJsonOverwrite(content, inst);
            }
            catch (Exception e)
            {
                FR2_LOG.LogWarning(e);
            }
        }

        static void DelaySave()
        {
            EditorApplication.update -= DelaySave;
            
            try
            {
                Directory.CreateDirectory("Library/FR2/");
                File.WriteAllText(path, JsonUtility.ToJson(inst));
            }
            catch (Exception e)
            {
                FR2_LOG.LogWarning(e);
            }
        }

        private static void ScheduleSave()
        {
            EditorApplication.update -= DelaySave;
            EditorApplication.update += DelaySave;
        }
        
        [SerializeField] private bool _disableInPlayMode = true;
        [SerializeField] private bool _disabled;
        [SerializeField] private FR2_AutoRefreshMode _autoRefresh;
        [SerializeField] private bool _hideToolsWarning;
        [SerializeField] private bool _isGitProject;
        [SerializeField] private bool _gitIgnoreAdded;
        [SerializeField] private bool _hideGitIgnoreWarning;
        [SerializeField] private bool _showHierarchyReferenceCount = true;
        [SerializeField] private float _hierarchyReferenceCountOffset = 0f;
        [SerializeField] private bool _dbValidation = false;
        [SerializeField] private bool _parserVerbose = false;
        [SerializeField] private bool _showPackagesAndBuiltIn = true;
        [SerializeField] private int _maxSelectionCount = 100;
        [SerializeField] private int _bfsFrontierCap = 50000;
        
        private bool internalDisabled
        {
            get => _disabled || (_disableInPlayMode && EditorApplication.isPlayingOrWillChangePlaymode);
            set
            {
                ref bool disableRef = ref _disabled;
                if (EditorApplication.isPlayingOrWillChangePlaymode) disableRef = ref _disableInPlayMode;
                
                if (disableRef == value) return;
                disableRef = value;
                
                // disable at runtime: only disable `disableInPlayMode`
                // enable at runtime: enable all
                if (!value) _disabled = false;
                FR2_Cache.MarkDirty();
                ScheduleSave();
                FR2_Event.DispatchGlobal(new SettingChangedEvent(SettingKeys.Disable));
            }
        }
    }
}
