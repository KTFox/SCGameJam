using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Supercent.Rendering.Shadow.Editor
{
    public static class PlanarShadowValidationTool
    {
        #region Scene Object Validation
        private class PlanarShadowSceneValidationWindow : EditorWindow
        {
            private static Dictionary<PlanarShadow, List<string>> _capturedErrorShadows = new Dictionary<PlanarShadow, List<string>>();
            private static Dictionary<PlanarShadow, List<string>> _errorShadows;
            private Vector2 _scrollPosition;

            private static void ValidateSinglePlanarShadow(PlanarShadow shadow)
            {
                List<string> errors = new List<string>();

                CheckAndAddError(shadow.Editor_ValidateDuplicatedComponent(), errors);
                CheckAndAddError(shadow.Editor_ValidateReferences(), errors);
                CheckAndAddError(shadow.Editor_ValidateMaterialSettings(), errors);
                CheckAndAddError(shadow.Editor_ValidateBounds(), errors);
                CheckAndAddError(shadow.Editor_ValidatePivotShadowConfiguration(), errors);
                CheckAndAddError(shadow.Editor_ValidateMultiplePivotShadowMaterials(), errors);

                if (errors.Count > 0)
                {
                    _capturedErrorShadows[shadow] = errors;
                }
            }

            private static void CheckAndAddError((bool hasError, string message) validationResult, List<string> errors)
            {
                if (validationResult.hasError)
                {
                    errors.Add(validationResult.message);
                }
            }

            [MenuItem("Supercent/Planar Shadow/Shadow Settings Validation Tools/Validate Shadow Settings in Current Scene", false, 8)]
            private static void ValidatePlanarShadowInCurrentScene()
            {
                _capturedErrorShadows.Clear();

                List<PlanarShadow> combinedPlanarShadows = new List<PlanarShadow>();

                PlanarShadow[] shadows = FindObjectsByType<PlanarShadow>(FindObjectsSortMode.None);
                combinedPlanarShadows.AddRange(shadows);

                int totalShadows = combinedPlanarShadows.Count;
                int processedShadows = 0;

                try
                {
                    foreach (PlanarShadow shadow in combinedPlanarShadows)
                    {
                        ValidateSinglePlanarShadow(shadow);
                        processedShadows++;
                        EditorUtility.DisplayProgressBar("Validating Current Scene Shadow Settings", $"Checking... ({processedShadows}/{totalShadows})", (float)processedShadows / totalShadows);
                    }
                }
                finally
                {
                    EditorUtility.ClearProgressBar();
                }

                DisplayErrorWindow();
            }

            private static void DisplayErrorWindow()
            {
                if (_capturedErrorShadows == null || _capturedErrorShadows.Count == 0)
                {
                    EditorUtility.DisplayDialog("Planar Shadow Validation Complete", "No issues found.", "OK");
                    return;
                }

                ShowWindow(_capturedErrorShadows);
            }

            private static void ShowWindow(Dictionary<PlanarShadow, List<string>> errorShadows)
            {
                _errorShadows = errorShadows;
                PlanarShadowSceneValidationWindow window = GetWindow<PlanarShadowSceneValidationWindow>("Planar Shadow Validation Tool");
                window.Show();
            }

            private string _searchQuery = "";

            private void OnGUI()
            {
                if (_errorShadows == null || _errorShadows.Count == 0)
                {
                    EditorGUILayout.LabelField("No issues found.", EditorStyles.boldLabel);
                    return;
                }

                EditorGUILayout.HelpBox("If objects in the scene were created from prefabs, be careful when applying changes as prefab overrides.", MessageType.Warning);

                _searchQuery = EditorGUILayout.TextField("Search", _searchQuery).ToLower().Trim();

                List<KeyValuePair<PlanarShadow, List<string>>> sceneObjects = new();

                foreach (var entry in _errorShadows)
                {
                    if (!PrefabUtility.IsPartOfPrefabAsset(entry.Key.gameObject))
                    {
                        sceneObjects.Add(entry);
                    }
                }

                _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

                if (sceneObjects.Count > 0)
                {
                    EditorGUILayout.Space();

                    GUIStyle sectionTitleStyle = new(EditorStyles.boldLabel)
                    {
                        fontSize = 14,
                        fontStyle = FontStyle.Bold,
                        normal = { textColor = new Color(0.9f, 0.6f, 0.2f) }
                    };

                    EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
                    GUILayout.FlexibleSpace();
                    EditorGUILayout.LabelField("Scene Objects", sectionTitleStyle);
                    GUILayout.FlexibleSpace();
                    EditorGUILayout.EndHorizontal();

                    EditorGUILayout.Space();

                    GUIStyle objectStyle = new(EditorStyles.label)
                    {
                        normal = { textColor = Color.white }
                    };

                    foreach (var entry in sceneObjects)
                    {
                        PlanarShadow shadow = entry.Key;
                        List<string> messages = entry.Value;

                        if (!string.IsNullOrEmpty(_searchQuery) && !shadow.name.ToLower().Contains(_searchQuery))
                        {
                            continue;
                        }

                        EditorGUILayout.BeginHorizontal();
                        Texture icon = EditorGUIUtility.ObjectContent(shadow.gameObject, typeof(GameObject)).image;
                        GUILayout.Label(icon, GUILayout.Width(20), GUILayout.Height(20));

                        if (GUILayout.Button(shadow.name, objectStyle))
                        {
                            SelectObjectInHierarchyOrPrefab(shadow);
                        }

                        EditorGUILayout.EndHorizontal();

                        EditorGUI.indentLevel++;
                        foreach (string message in messages)
                        {
                            EditorGUILayout.LabelField($"- {message}", EditorStyles.miniLabel);
                        }
                        EditorGUI.indentLevel--;
                    }

                    EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
                }

                EditorGUILayout.EndScrollView();
            }

            private static void SelectObjectInHierarchyOrPrefab(PlanarShadow shadow)
            {

                PrefabStage prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
                if (prefabStage != null)
                {
                    StageUtility.GoToMainStage();
                }

                Selection.activeGameObject = shadow.gameObject;
                EditorGUIUtility.PingObject(shadow.gameObject);

                if (SceneView.lastActiveSceneView != null)
                {
                    SceneView.lastActiveSceneView.FrameSelected();
                }
            }
        }
        #endregion

        #region Assets Folder Validation
        private class PlanarShadowPrefabValidationWindow : EditorWindow
        {
            private static Dictionary<PlanarShadow, List<string>> _capturedErrorShadows = new();
            private static Dictionary<PlanarShadow, List<string>> _errorShadows;
            private Vector2 _scrollPosition;
            private string _searchQuery = "";

            private static void ValidateSinglePlanarShadow(PlanarShadow shadow)
            {
                List<string> errors = new();

                CheckAndAddError(shadow.Editor_ValidateDuplicatedComponent(), errors);
                CheckAndAddError(shadow.Editor_ValidateReferences(), errors);
                CheckAndAddError(shadow.Editor_ValidateMaterialSettings(), errors);
                CheckAndAddError(shadow.Editor_ValidateBounds(), errors);
                CheckAndAddError(shadow.Editor_ValidatePivotShadowConfiguration(), errors);
                CheckAndAddError(shadow.Editor_ValidateMultiplePivotShadowMaterials(), errors);

                if (errors.Count > 0)
                {
                    _capturedErrorShadows[shadow] = errors;
                }
            }

            private static void CheckAndAddError((bool hasError, string message) validationResult, List<string> errors)
            {
                if (validationResult.hasError)
                {
                    errors.Add(validationResult.message);
                }
            }

            [MenuItem("Supercent/Planar Shadow/Shadow Settings Validation Tools/Validate Shadow Settings in Assets Folder", false, 9)]
            private static void ValidatePlanarShadowInAssetFolder()
            {
                _capturedErrorShadows.Clear();
                List<PlanarShadow> combinedPlanarShadows = new();

                PlanarShadowEditorUtility.LoadAllPrefabs((GameObject asset) =>
                {
                    PlanarShadow[] planarShadows = asset.GetComponentsInChildren<PlanarShadow>(true);
                    combinedPlanarShadows.AddRange(planarShadows);
                });

                int totalShadows = combinedPlanarShadows.Count;
                int processedShadows = 0;

                try
                {
                    foreach (PlanarShadow shadow in combinedPlanarShadows)
                    {
                        ValidateSinglePlanarShadow(shadow);
                        processedShadows++;
                        EditorUtility.DisplayProgressBar("Validating Assets Folder Shadow Settings", $"Checking... ({processedShadows}/{totalShadows})", (float)processedShadows / totalShadows);
                    }
                }
                finally
                {
                    EditorUtility.ClearProgressBar();
                }

                DisplayErrorWindow();
            }

            private static void DisplayErrorWindow()
            {
                if (_capturedErrorShadows.Count == 0)
                {
                    EditorUtility.DisplayDialog("Planar Shadow Validation Complete", "No issues found.", "OK");
                    return;
                }

                ShowWindow(_capturedErrorShadows);
            }

            private static void ShowWindow(Dictionary<PlanarShadow, List<string>> errorShadows)
            {
                _errorShadows = errorShadows;
                PlanarShadowPrefabValidationWindow window = GetWindow<PlanarShadowPrefabValidationWindow>("Planar Shadow Validation Tool");
                window.Show();
            }

            private void OnGUI()
            {
                if (_errorShadows == null || _errorShadows.Count == 0)
                {
                    EditorGUILayout.LabelField("No issues found.", EditorStyles.boldLabel);
                    return;
                }

                EditorGUILayout.HelpBox("Edit prefabs directly in the project folder, then apply those changes as overrides to scene objects.", MessageType.Info);

                // Add a search field that filters by prefab name only.
                _searchQuery = EditorGUILayout.TextField("Search", _searchQuery).ToLower().Trim();

                Dictionary<GameObject, Dictionary<GameObject, List<(PlanarShadow, List<string>)>>> groupedErrors = new();

                foreach (var entry in _errorShadows)
                {
                    PlanarShadow shadow = entry.Key;
                    GameObject instanceRoot = shadow.gameObject.transform.root.gameObject;
                    GameObject sourcePrefab = PrefabUtility.GetCorrespondingObjectFromSource(instanceRoot) ?? instanceRoot;

                    if (!groupedErrors.ContainsKey(sourcePrefab))
                    {
                        groupedErrors[sourcePrefab] = new Dictionary<GameObject, List<(PlanarShadow, List<string>)>>();
                    }

                    if (!groupedErrors[sourcePrefab].ContainsKey(instanceRoot))
                    {
                        groupedErrors[sourcePrefab][instanceRoot] = new List<(PlanarShadow, List<string>)>();
                    }

                    groupedErrors[sourcePrefab][instanceRoot].Add((shadow, entry.Value));
                }

                _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

                foreach (var sourceEntry in groupedErrors)
                {
                    GameObject sourcePrefab = sourceEntry.Key;
                    Dictionary<GameObject, List<(PlanarShadow, List<string>)>> prefabInstances = sourceEntry.Value;

                    // Apply the search filter by prefab name only.
                    if (!string.IsNullOrEmpty(_searchQuery) && !sourcePrefab.name.ToLower().Contains(_searchQuery))
                    {
                        continue;
                    }

                    EditorGUILayout.Space();

                    // Improved style for prefab name labels.
                    GUIStyle prefabLabelStyle = new(EditorStyles.boldLabel)
                    {
                        fontSize = 14,
                        fontStyle = FontStyle.Bold,
                        normal = { textColor = new Color(0.9f, 0.6f, 0.2f) }
                    };

                    EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button($" 🏗  {sourcePrefab.name}.prefab ", prefabLabelStyle))
                    {
                        AssetDatabase.OpenAsset(sourcePrefab);
                    }
                    GUILayout.FlexibleSpace();
                    EditorGUILayout.EndHorizontal();

                    EditorGUI.indentLevel++;

                    foreach (var instanceEntry in prefabInstances)
                    {
                        GameObject instanceRoot = instanceEntry.Key;
                        List<(PlanarShadow, List<string>)> errors = instanceEntry.Value;

                        EditorGUI.indentLevel++;

                        foreach (var (shadow, messages) in errors)
                        {
                            EditorGUILayout.BeginHorizontal();
                            Texture shadowIcon = EditorGUIUtility.ObjectContent(shadow.gameObject, typeof(GameObject)).image;
                            GUILayout.Label(shadowIcon, GUILayout.Width(20), GUILayout.Height(20));

                            GUIStyle clickableTextStyle = new(EditorStyles.label)
                            {
                                normal = { textColor = Color.cyan }
                            };

                            if (GUILayout.Button(shadow.name, clickableTextStyle))
                            {
                                SelectObjectInHierarchyOrPrefab(instanceRoot, shadow);
                            }

                            EditorGUILayout.EndHorizontal();

                            EditorGUI.indentLevel++;
                            foreach (string message in messages)
                            {
                                EditorGUILayout.LabelField($"- {message}", EditorStyles.miniLabel);
                            }
                            EditorGUI.indentLevel--;
                        }

                        EditorGUI.indentLevel--;
                    }

                    EditorGUI.indentLevel--;

                    EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
                }

                EditorGUILayout.EndScrollView();
            }


            private static void SelectObjectInHierarchyOrPrefab(GameObject rootInstance, PlanarShadow planarShadow)
            {
                GameObject prefab = PrefabUtility.GetCorrespondingObjectFromOriginalSource(rootInstance);
                string prefabPath = AssetDatabase.GetAssetPath(prefab);

                if (!string.IsNullOrEmpty(prefabPath))
                {
                    PrefabStageUtility.OpenPrefab(prefabPath);
                    EditorApplication.delayCall += () =>
                    {
                        PrefabStage prefabStage = PrefabStageUtility.GetCurrentPrefabStage();

                        if (prefabStage != null)
                        {
                            GameObject prefabInstance = prefabStage.prefabContentsRoot;

                            if (prefabInstance != null)
                            {
                                Transform[] allTransforms = prefabInstance.transform.GetComponentsInChildren<Transform>(true);

                                Transform targetTransform = allTransforms
                                    .FirstOrDefault(t => PrefabUtility.GetCorrespondingObjectFromSource(t.gameObject) == planarShadow.gameObject);

                                if (targetTransform == null)
                                {
                                    string pathToTarget = GetGameObjectPath(planarShadow.gameObject);
                                    targetTransform = allTransforms
                                        .FirstOrDefault(t => GetGameObjectPath(t.gameObject) == pathToTarget);
                                }

                                if (targetTransform != null)
                                {
                                    Selection.activeGameObject = targetTransform.gameObject;
                                    EditorGUIUtility.PingObject(targetTransform.gameObject);
                                }
                                else
                                {
                                    Debug.LogWarning($"[isPrefab] Target object not found in prefab: {planarShadow.gameObject.name}");
                                }
                            }
                        }
                    };
                }
            }

            private static string GetGameObjectPath(GameObject obj)
            {
                string path = obj.name;
                Transform current = obj.transform;

                while (current.parent != null)
                {
                    current = current.parent;
                    path = $"{current.name}/{path}";
                }

                return path;
            }

            #endregion
        }
    }
}
