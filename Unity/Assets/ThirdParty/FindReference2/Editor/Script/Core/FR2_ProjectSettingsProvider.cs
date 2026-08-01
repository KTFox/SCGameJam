using UnityEditor;
using UnityEngine;

namespace vietlabs.fr2
{
    internal static class FR2_ProjectSettingsProvider
    {
        [SettingsProvider]
        public static SettingsProvider CreateProvider()
        {
            return new SettingsProvider("Project/Find Reference 2", SettingsScope.Project)
            {
                label = "Find Reference 2",
                guiHandler = DrawSettingsGUI,
                keywords = new[] { "FR2", "Find", "Reference", "Asset", "Cache", "Selection" }
            };
        }

        private static void DrawSettingsGUI(string searchContext)
        {
            EditorGUILayout.Space(8);
            
            var labelWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = 240f;
            
            // === General ===
            EditorGUILayout.LabelField("General", EditorStyles.boldLabel);
            using (new EditorGUI.IndentLevelScope())
            {
                bool disabled = FR2_SettingExt.disable;
                bool newDisabled = EditorGUILayout.Toggle("Disable FR2", disabled);
                if (newDisabled != disabled) FR2_SettingExt.disable = newDisabled;
                
                var mode = FR2_SettingExt.autoRefreshMode;
                var newMode = (FR2_AutoRefreshMode)EditorGUILayout.EnumPopup("Auto Refresh", mode);
                if (newMode != mode) FR2_SettingExt.autoRefreshMode = newMode;
                
                bool dbVal = FR2_SettingExt.dbValidation;
                bool newDbVal = EditorGUILayout.Toggle("AssetDatabase Validation", dbVal);
                if (newDbVal != dbVal) FR2_SettingExt.dbValidation = newDbVal;
            }
            
            EditorGUILayout.Space(12);
            
            // === Performance ===
            EditorGUILayout.LabelField("Performance", EditorStyles.boldLabel);
            using (new EditorGUI.IndentLevelScope())
            {
                int maxSel = FR2_SettingExt.maxSelectionCount;
                int newMaxSel = EditorGUILayout.IntSlider("Max Selection Count", maxSel, 1, 500);
                if (newMaxSel != maxSel) FR2_SettingExt.maxSelectionCount = newMaxSel;
                EditorGUILayout.HelpBox("Skip reference scan when selecting more than this many assets/objects.", MessageType.None);
                
                EditorGUILayout.Space(4);
                
                int bfsCap = FR2_SettingExt.bfsFrontierCap;
                int newBfsCap = EditorGUILayout.IntField("BFS Frontier Cap", bfsCap);
                newBfsCap = Mathf.Clamp(newBfsCap, 1000, 500000);
                if (newBfsCap != bfsCap) FR2_SettingExt.bfsFrontierCap = newBfsCap;
                EditorGUILayout.HelpBox("Maximum nodes explored when finding references. Lower = faster but may miss deep references. Range: 1,000 – 500,000.", MessageType.None);
            }
            
            EditorGUILayout.Space(12);
            
            // === Display ===
            EditorGUILayout.LabelField("Display", EditorStyles.boldLabel);
            using (new EditorGUI.IndentLevelScope())
            {
                bool showRef = FR2_Setting.ShowReferenceCount;
                bool newShowRef = EditorGUILayout.Toggle("Show Reference Count in Project", showRef);
                if (newShowRef != showRef) FR2_Setting.ShowReferenceCount = newShowRef;
                
                bool showHierarchy = FR2_SettingExt.showHierarchyReferenceCount;
                bool newShowHierarchy = EditorGUILayout.Toggle("Show Reference Count in Hierarchy", showHierarchy);
                if (newShowHierarchy != showHierarchy) FR2_SettingExt.showHierarchyReferenceCount = newShowHierarchy;
                
                if (FR2_SettingExt.showHierarchyReferenceCount)
                {
                    float offset = FR2_SettingExt.hierarchyReferenceCountOffset;
                    float newOffset = EditorGUILayout.Slider("  Hierarchy Count Offset", offset, -100f, 100f);
                    if (!Mathf.Approximately(newOffset, offset)) FR2_SettingExt.hierarchyReferenceCountOffset = newOffset;
                }
                
                bool showPkg = FR2_SettingExt.showPackagesAndBuiltIn;
                bool newShowPkg = EditorGUILayout.Toggle("Show Packages & Built-in", showPkg);
                if (newShowPkg != showPkg) FR2_SettingExt.showPackagesAndBuiltIn = newShowPkg;
            }
            
            EditorGUILayout.Space(12);
            
            // === Advanced ===
            EditorGUILayout.LabelField("Advanced", EditorStyles.boldLabel);
            using (new EditorGUI.IndentLevelScope())
            {
                bool parserVerbose = FR2_SettingExt.parserVerbose;
                bool newParserVerbose = EditorGUILayout.Toggle("Parser Verbose Logging", parserVerbose);
                if (newParserVerbose != parserVerbose) FR2_SettingExt.parserVerbose = newParserVerbose;
                
                bool hideGit = FR2_SettingExt.hideGitIgnoreWarning;
                bool newHideGit = EditorGUILayout.Toggle("Hide .gitignore Warning", hideGit);
                if (newHideGit != hideGit) FR2_SettingExt.hideGitIgnoreWarning = newHideGit;
                
                bool hideTools = FR2_SettingExt.hideToolsWarning;
                bool newHideTools = EditorGUILayout.Toggle("Hide Tools Warning", hideTools);
                if (newHideTools != hideTools) FR2_SettingExt.hideToolsWarning = newHideTools;
            }
            
            EditorGUILayout.Space(12);
            
            // === Cache ===
            EditorGUILayout.LabelField("Cache", EditorStyles.boldLabel);
            using (new EditorGUI.IndentLevelScope())
            {
                EditorGUILayout.LabelField("Status", FR2_Cache.status.ToString());
                EditorGUILayout.LabelField("Cache Status", FR2_Cache.cacheStatus.ToString());
                
                if (FR2_Cache._inst != null)
                {
                    int assetCount = FR2_Cache._inst._assets?.Count ?? 0;
                    EditorGUILayout.LabelField("Cached Assets", assetCount.ToString());
                }
                
                EditorGUILayout.Space(4);
                
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Clear Cache", GUILayout.Width(120)))
                {
                    FR2_Cache.DeleteCache();
                }
                if (GUILayout.Button("Rebuild Cache", GUILayout.Width(120)))
                {
                    FR2_Cache.DeleteCache();
                    FR2_Cache.CreateCache();
                }
                EditorGUILayout.EndHorizontal();
            }
            
            EditorGUIUtility.labelWidth = labelWidth;
        }
    }
}
