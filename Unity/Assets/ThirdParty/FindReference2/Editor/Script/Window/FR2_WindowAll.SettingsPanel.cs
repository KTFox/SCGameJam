using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using static vietlabs.fr2.FR2_Scope;
using UnityObject = UnityEngine.Object;

namespace vietlabs.fr2
{
    internal partial class FR2_WindowAll
    {
        private void DrawSettings()
        {
            if (bottomTabs == null || bottomTabs.current == -1) return;

            using (VtLayout(FR2_Theme.Current.SettingsPanelHeight))
            {
                GUILayout.Space(2f);
                switch (bottomTabs.current)
                {
                case 0:
                    {
                        DrawMainSettings();
                        break;
                    }

                case 1:
                    {
                        DrawIgnoreSettings();
                        break;
                    }

                case 2:
                    {
                        DrawFilterSettings();
                        break;
                    }
                }
            }

            Rect rect = GUILayoutUtility.GetLastRect();
            rect.height = 1f;
            GUI2.Rect(rect, Color.black, 0.4f);
        }

        private void DrawMainSettings()
        {
            using (HzLayout())
            {
                using (VtLayout(GUILayout.Width(200f)))
                {
                    EditorGUILayout.LabelField("Usage Count", EditorStyles.miniLabel);
                    
                    bool referenceCount = EditorGUILayout.ToggleLeft("Asset usage", FR2_Setting.ShowReferenceCount);
                    if (referenceCount != FR2_Setting.ShowReferenceCount)
                        FR2_Setting.ShowReferenceCount = referenceCount;
                    
                    bool showHierarchyCount = EditorGUILayout.ToggleLeft("SceneObject usage", FR2_SettingExt.showHierarchyReferenceCount);
                    if (showHierarchyCount != FR2_SettingExt.showHierarchyReferenceCount)
                        FR2_SettingExt.showHierarchyReferenceCount = showHierarchyCount;
                    
                    using (GUIEnable(FR2_SettingExt.showHierarchyReferenceCount))
                    {
                        using (HzLayout())
                        {
                            GUILayout.Label("  Scene icon offset", GUILayout.Width(110f));
                            float offset = FR2_SettingExt.hierarchyReferenceCountOffset;
                            offset = EditorGUILayout.FloatField(offset, GUILayout.Width(40f));
                            if (offset < 0f) offset = 0f;
                            if (!Mathf.Approximately(offset, FR2_SettingExt.hierarchyReferenceCountOffset))
                                FR2_SettingExt.hierarchyReferenceCountOffset = offset;
                        }
                    }
                }
                
                using (VtLayout(GUILayout.Width(180f)))
                {
                    EditorGUILayout.LabelField("Display", EditorStyles.miniLabel);
                    
                    bool alternateColor = EditorGUILayout.ToggleLeft("Alternate Row Color", FR2_Setting.AlternateRowColor);
                    if (alternateColor != FR2_Setting.AlternateRowColor)
                        FR2_Setting.AlternateRowColor = alternateColor;
                    
                    using (HzLayout())
                    {
                        GUILayout.Label("Max Selection", GUILayout.Width(90f));
                        int maxSel = FR2_SettingExt.maxSelectionCount;
                        maxSel = EditorGUILayout.IntField(maxSel, GUILayout.Width(50f));
                        if (maxSel != FR2_SettingExt.maxSelectionCount)
                            FR2_SettingExt.maxSelectionCount = maxSel;
                    }
                }
                
                if (FR2_Define.IsDebugModeEnabled())
                {
                    using (VtLayout(GUILayout.Width(220f)))
                    {
                        EditorGUILayout.LabelField("Developer", EditorStyles.miniLabel);
                        
                        bool dbValidation = EditorGUILayout.ToggleLeft("DB Validation", FR2_SettingExt.dbValidation);
                        if (dbValidation != FR2_SettingExt.dbValidation)
                            FR2_SettingExt.dbValidation = dbValidation;
                        
                        bool parserVerbose = EditorGUILayout.ToggleLeft("Parser Verbose (Missed GUID)", FR2_SettingExt.parserVerbose);
                        if (parserVerbose != FR2_SettingExt.parserVerbose)
                            FR2_SettingExt.parserVerbose = parserVerbose;
                        
                        settings.writeImportLog = EditorGUILayout.ToggleLeft("Write Import Log", settings.writeImportLog);
                    }
                }
            }
            
            if (FR2_SettingExt.isGitProject)
            {
                DrawGitSettings();
            }
        }

        private void DrawGitSettings()
        {
            GUILayout.Space(5f);
            EditorGUILayout.LabelField("Git Settings", EditorStyles.boldLabel);
            
            if (FR2_SettingExt.gitIgnoreAdded)
            {
                EditorGUILayout.HelpBox("FR2_Cache.asset* is already in your .gitignore file.", MessageType.Info);
            }
            else
            {
                using (FR2_Scope.HzLayout())
                {
                    EditorGUILayout.LabelField("Add FR2_Cache.asset* to .gitignore");
                    if (GUILayout.Button("Apply", FR2_Theme.Current.ApplyButtonWidth))
                    {
                        FR2_GitUtil.AddFR2CacheToGitIgnore();
                        FR2_SettingExt.gitIgnoreAdded = true;
                        FR2_SettingExt.hideGitIgnoreWarning = true;
                    }
                }
            }
        }

        private void DrawIgnoreSettings()
        {
            if (FR2_AssetGroupDrawer.DrawIgnoreFolder()) 
            {
                MarkDirty();
            }
        }

        private void DrawFilterSettings()
        {
            if (FR2_AssetGroupDrawer.DrawSearchFilter()) 
            {
                MarkDirty();
            }
        }
    }
} 
