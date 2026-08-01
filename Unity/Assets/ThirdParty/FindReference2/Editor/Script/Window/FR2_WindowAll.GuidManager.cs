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
        private static readonly Color _typeMismatchColor = new Color(1f, 0.4f, 0.4f, 0.35f);

        private Dictionary<string, UnityObject> guidObjs;
        private Vector2 scrollPos;
        private string tempGUID;
        private string tempFileID;
        private UnityObject tempObject;
        private float _guidColWidth;
        private string _selectedGuid;

        private bool HasTypeMismatch(out Type dominantType)
        {
            dominantType = null;
            if (guidObjs == null || guidObjs.Count < 2) return false;

            Dictionary<Type, int> typeCounts = new Dictionary<Type, int>();
            foreach (KeyValuePair<string, UnityObject> kvp in guidObjs)
            {
                if (!kvp.Value) continue;
                Type t = kvp.Value.GetType();
                if (!typeCounts.ContainsKey(t)) typeCounts[t] = 0;
                typeCounts[t]++;
            }

            if (typeCounts.Count <= 1)
            {
                if (typeCounts.Count == 1)
                {
                    foreach (var kvp in typeCounts) dominantType = kvp.Key;
                }
                return false;
            }

            int maxCount = 0;
            foreach (KeyValuePair<Type, int> kvp in typeCounts)
            {
                if (kvp.Value <= maxCount) continue;
                maxCount = kvp.Value;
                dominantType = kvp.Key;
            }

            return true;
        }

        private void DrawGUIDs()
        {
            GUILayout.Label("GUID to Object", EditorStyles.boldLabel);

            using (HzLayout())
            {
                if (GUILayout.Button("Paste", EditorStyles.miniButton, GUI2.GLW_50))
                {
                    string[] split = EditorGUIUtility.systemCopyBuffer.Split('/');
                    tempGUID = split[0];
                    tempFileID = split.Length >= 2 ? split[1] : string.Empty;
                    string fullId = string.IsNullOrEmpty(tempFileID) ? tempGUID : tempGUID + "/" + tempFileID;
                    tempObject = FR2_Unity.LoadAssetAtPath<UnityObject>(FR2_Cache.GUIDToAssetPath(fullId));
                }

                GUILayoutOption[] guidW = _guidColWidth > 0 ? new[] { GUILayout.Width(_guidColWidth) } : null;
                if (guidW != null)
                {
                    string combined = string.IsNullOrEmpty(tempFileID) ? tempGUID ?? string.Empty : (tempGUID ?? string.Empty) + "/" + tempFileID;
                    string newCombined = EditorGUILayout.TextField(combined, guidW);
                    if (newCombined != combined)
                    {
                        string[] parts = newCombined.Split('/');
                        tempGUID = parts[0];
                        tempFileID = parts.Length >= 2 ? parts[1] : string.Empty;
                        string fullId = string.IsNullOrEmpty(tempFileID) ? tempGUID : tempGUID + "/" + tempFileID;
                        tempObject = FR2_Unity.LoadAssetAtPath<UnityObject>(FR2_Cache.GUIDToAssetPath(fullId));
                    }
                }
                else
                {
                    string guid = EditorGUILayout.TextField(tempGUID ?? string.Empty);
                    string fileId = EditorGUILayout.TextField(tempFileID ?? string.Empty, GUILayout.Width(70f));
                    if ((guid != tempGUID || fileId != tempFileID) && !string.IsNullOrEmpty(guid))
                    {
                        tempGUID = guid;
                        tempFileID = fileId;
                        string fullId = string.IsNullOrEmpty(fileId) ? tempGUID : tempGUID + "/" + tempFileID;
                        tempObject = FR2_Unity.LoadAssetAtPath<UnityObject>(FR2_Cache.GUIDToAssetPath(fullId));
                    }
                }

                EditorGUILayout.ObjectField(tempObject, typeof(UnityObject), false);

                if (GUILayout.Button("Apply FileID", EditorStyles.miniButton, GUI2.GLW_80))
                {
                    string fileId = tempFileID;
                    var newDict = new Dictionary<string, UnityObject>();
                    foreach (KeyValuePair<string, UnityObject> kvp in guidObjs)
                    {
                        string key = kvp.Key.Split('/')[0];
                        if (!string.IsNullOrEmpty(fileId)) key = key + "/" + fileId;
                        var value = FR2_Unity.LoadAssetAtPath<UnityObject>(FR2_Cache.GUIDToAssetPath(key));
                        newDict.Add(key, value);
                    }

                    guidObjs = newDict;
                    _guidColWidth = 0;
                    _selectedGuid = null;
                }
            }

            GUILayout.Space(4f);

            if (guidObjs == null)
            {
                GUILayout.FlexibleSpace();
                return;
            }

            bool mismatch = HasTypeMismatch(out Type dominant);
            if (mismatch)
            {
                EditorGUILayout.HelpBox("Assets have mixed types — Merge only works for assets of the same type.", MessageType.Warning);
            }

            if (_guidColWidth <= 0 && guidObjs.Count > 0)
            {
                float maxW = 0;
                GUIStyle style = EditorStyles.textField;
                foreach (KeyValuePair<string, UnityObject> item in guidObjs)
                {
                    float w = style.CalcSize(new GUIContent(item.Key)).x;
                    if (w > maxW) maxW = w;
                }

                _guidColWidth = maxW + 8f;
            }

            GUILayoutOption[] colW = { GUILayout.Width(_guidColWidth) };

            if (_selectedGuid == null && guidObjs.Count > 0)
            {
                foreach (var kvp in guidObjs) { _selectedGuid = kvp.Key; break; }
            }

            scrollPos = GUILayout.BeginScrollView(scrollPos);
            {
                foreach (KeyValuePair<string, UnityObject> item in guidObjs)
                {
                    bool isMismatchRow = mismatch && item.Value && item.Value.GetType() != dominant;
                    bool isSelected = _selectedGuid == item.Key;

                    using (GUIColor(isMismatchRow ? _typeMismatchColor : (Color?)null))
                    using (HzLayout())
                    {
                        if (GUILayout.Button("C", EditorStyles.miniButton, GUI2.GLW_20))
                        {
                            string[] arr = item.Key.Split('/');
                            tempGUID = arr[0];
                            tempFileID = arr.Length >= 2 ? arr[1] : string.Empty;
                            string fullId = string.IsNullOrEmpty(tempFileID) ? tempGUID : tempGUID + "/" + tempFileID;
                            tempObject = FR2_Unity.LoadAssetAtPath<UnityObject>(FR2_Cache.GUIDToAssetPath(fullId));
                        }

                        bool newSelected = GUILayout.Toggle(isSelected, GUIContent.none, EditorStyles.radioButton, GUILayout.Width(14f));
                        if (newSelected && !isSelected) _selectedGuid = item.Key;

                        GUILayout.TextField(item.Key, colW);
                        EditorGUILayout.ObjectField(item.Value, typeof(UnityObject), false);

                        if (isSelected)
                        {
                            using (GUIEnable(!isMismatchRow))
                            {
                                if (GUILayout.Button("Merge", EditorStyles.miniButton, GUI2.GLW_50))
                                {
                                    FR2_Export.MergeDuplicate(item.Key);
                                }
                            }
                        }
                    }
                }
            }
            GUILayout.EndScrollView();
            GUILayout.FlexibleSpace();
        }
    }
}
