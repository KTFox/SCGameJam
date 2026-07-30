using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Supercent.Rendering.Shadow.Editor
{
    public static class PlanarShadowRemovalTool
    {
        #region Remove Shadows
        private static class PlanarShadowCleaner
        {
            [MenuItem("Supercent/Planar Shadow/Shadow Removal Tools/🚨 Remove All Shadows (Prefabs in Assets Folder and All Scene Objects)", false, 10)]
            private static void CleanAllShadows()
            {
                if (false == EditorUtility.DisplayDialog("Operation Warning", "This operation cannot be undone. Do you want to continue?", "Yes", "No"))
                    return;

                try
                {
                    EditorUtility.DisplayProgressBar("Removing Shadows", "Processing prefabs in the Assets folder...", 0.0f);
                    CleanShadowOnAssetsFolder();

                    EditorUtility.DisplayProgressBar("Removing Shadows", "Processing scene files...", 0.5f);
                    CleanShadowOnScenes();
                }
                finally
                {
                    EditorUtility.ClearProgressBar();
                }

                EditorUtility.DisplayDialog("Shadow Removal Complete", "All shadow-related elements have been removed.", "OK");

                AssetDatabase.Refresh();
            }

            private static void CleanShadowOnAssetsFolder()
            {
                string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab");
                int total = prefabGuids.Length;
                int processed = 0;

                foreach (var guid in prefabGuids)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);

                    if (asset == null)
                    {
                        Debug.LogError($"<color=red>[Planar Shadow] Failed to load prefab: {path}</color>");
                        continue;
                    }

                    CleanShadowInGameObject(asset, true, path);
                    processed++;
                    EditorUtility.DisplayProgressBar("Removing Shadows", $"Processing assets... ({processed}/{total})", (float)processed / total);
                }
            }

            private static void CleanShadowOnScenes()
            {
                Scene currentScene = SceneManager.GetActiveScene();
                string currentScenePath = currentScene.path;
                bool isCurrentSceneDirty = currentScene.isDirty;

                if (!string.IsNullOrEmpty(currentScenePath))
                    if (isCurrentSceneDirty)
                        if (EditorUtility.DisplayDialog("Save Scene Changes", "The currently open scene has unsaved changes. Do you want to save them?", "Save", "Cancel"))
                            EditorSceneManager.SaveScene(currentScene);

                string[] allScenePaths = AssetDatabase.FindAssets("t:Scene")
                    .Select(AssetDatabase.GUIDToAssetPath)
                    .ToArray();

                int total = allScenePaths.Length;
                int processed = 0;

                foreach (string scenePath in allScenePaths)
                {
                    Scene loadedScene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                    GameObject[] rootGameObjects = loadedScene.GetRootGameObjects();

                    foreach (var rootObject in rootGameObjects)
                    {
                        CleanShadowInGameObject(rootObject, false, scenePath);
                    }

                    EditorSceneManager.SaveScene(loadedScene);
                    processed++;
                    EditorUtility.DisplayProgressBar("Removing Shadows", $"Processing scenes... ({processed}/{total})", 0.5f + (float)processed / total * 0.5f);
                }

                if (!string.IsNullOrEmpty(currentScenePath))
                    EditorSceneManager.OpenScene(currentScenePath, OpenSceneMode.Single);
            }

            private static void CleanShadowInGameObject(GameObject target, bool isPrefab, string assetPath)
            {
                PlanarShadow[] planarShadows = target.GetComponentsInChildren<PlanarShadow>(true);
                bool isModified = false;

                foreach (var planarShadow in planarShadows)
                {
                    if (PrefabUtility.IsPartOfPrefabInstance(planarShadow.gameObject))
                    {
                        if (PrefabUtility.HasPrefabInstanceAnyOverrides(planarShadow.gameObject, false))
                        {
                            Debug.Log($"<color=yellow>[Planar Shadow] {planarShadow.gameObject.name} - Removed shadow from prefab instance</color>");
                            planarShadow.Editor_RemovePlanarShadowMaterial();
                            GameObject.DestroyImmediate(planarShadow, true);
                            isModified = true;
                        }
                        continue;
                    }

                    Debug.Log($"<color=yellow>[Planar Shadow] {planarShadow.gameObject.name} - Removed shadow</color>");
                    planarShadow.Editor_RemovePlanarShadowMaterial();
                    GameObject.DestroyImmediate(planarShadow, true);
                    isModified = true;
                }

                Renderer[] renderers = target.GetComponentsInChildren<Renderer>(true)
                    .Where(r => r is MeshRenderer || r is SkinnedMeshRenderer)
                    .ToArray();

                foreach (var renderer in renderers)
                {
                    if (PrefabUtility.IsPartOfPrefabInstance(renderer.gameObject))
                    {
                        if (PrefabUtility.HasPrefabInstanceAnyOverrides(renderer.gameObject, false))
                        {
                            Material[] originalMaterials = renderer.sharedMaterials;

                            Material[] filteredMaterials = originalMaterials
                                .Where(m => m == null || (m.shader != PlanarShadowEditorUtility.GetPlanarShadowBakedMat().shader && m.shader != PlanarShadowEditorUtility.GetPlanarShadowOriginalMat().shader))
                                .ToArray();

                            if (!originalMaterials.SequenceEqual(filteredMaterials))
                            {
                                Debug.Log($"<color=yellow>[Planar Shadow] {renderer.gameObject.name} - Materials changed</color>");
                                renderer.sharedMaterials = filteredMaterials;
                                isModified = true;
                            }
                        }
                        continue;
                    }

                    if (renderer.sharedMaterials.Any(m => m != null && m.shader == PlanarShadowEditorUtility.GetPlanarShadowBakedMat().shader))
                    {
                        Debug.Log($"<color=yellow>[Planar Shadow] {renderer.gameObject.name} - Shadow material found and removed</color>");
                        GameObject.DestroyImmediate(renderer.gameObject, true);
                        isModified = true;
                    }
                }

                if (isModified)
                {
                    if (isPrefab)
                    {
                        bool hasMissingScripts = PlanarShadowEditorUtility.HasMissingScripts(target);
                        if (hasMissingScripts)
                        {
                            Debug.LogError($"<color=red>[Planar Shadow] Failed to save prefab (missing script exists): {assetPath}</color>");
                            return;
                        }

                        bool isRootEmptyObject = target.transform.childCount == 0 && target.GetComponents<Component>().Length == 1;
                        if (isRootEmptyObject)
                        {
                            AssetDatabase.DeleteAsset(assetPath);
                            Debug.Log($"<color=red>[Planar Shadow] Prefab deleted: {assetPath}</color>");
                        }
                        else
                        {
                            PrefabUtility.SavePrefabAsset(target);
                            Debug.Log($"<color=cyan>[Planar Shadow] Prefab updated: {assetPath}</color>");
                        }
                    }
                }
            }
        }
        #endregion

        #region Check Shadow Existence
        private class PlanarShadowCheckTool : EditorWindow
        {
            private static Dictionary<GameObject, string> _foundShadows;
            private Vector2 _scrollPosition;

            [MenuItem("Supercent/Planar Shadow/Shadow Removal Tools/Check Shadow Existence in Assets Folder", false, 11)]
            private static void CheckShadowInAssetFolder()
            {
                Dictionary<GameObject, string> foundShadows = new();

                string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab");
                int totalPrefabs = prefabGuids.Length;
                int processedPrefabs = 0;

                try
                {
                    foreach (var guid in prefabGuids)
                    {
                        string path = AssetDatabase.GUIDToAssetPath(guid);
                        GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);

                        if (asset == null)
                        {
                            Debug.LogError($"<color=red>[Planar Shadow] Failed to load prefab: {path}</color>");
                            continue;
                        }

                        CheckShadowInGameObject(asset, path, foundShadows);
                        processedPrefabs++;
                        EditorUtility.DisplayProgressBar("Checking Shadow Existence (Assets Folder)", $"Checking prefabs... ({processedPrefabs}/{totalPrefabs})", (float)processedPrefabs / totalPrefabs);
                    }
                }
                finally
                {
                    EditorUtility.ClearProgressBar();
                }

                if (foundShadows.Count > 0)
                {
                    PlanarShadowCheckTool.ShowWindow(foundShadows);
                }
                else
                {
                    EditorUtility.DisplayDialog("Shadow Existence Check Complete", "No objects using PlanarShadow or shadow materials were found in the Assets folder.", "OK");
                }
            }

            [MenuItem("Supercent/Planar Shadow/Shadow Removal Tools/Check Shadow Existence in Current Scene", false, 12)]
            private static void CheckShadowInCurrentScene()
            {
                Dictionary<GameObject, string> foundShadows = new();

                Scene currentScene = SceneManager.GetActiveScene();
                string currentScenePath = currentScene.path;
                bool isCurrentSceneDirty = currentScene.isDirty;

                try
                {
                    if (!string.IsNullOrEmpty(currentScenePath))
                    {
                        if (isCurrentSceneDirty)
                        {
                            if (EditorUtility.DisplayDialog("Save Scene Changes", "The currently open scene has unsaved changes. Do you want to save them?", "Save", "Cancel"))
                            {
                                EditorSceneManager.SaveScene(currentScene);
                            }
                        }

                        GameObject[] rootGameObjects = currentScene.GetRootGameObjects();
                        int totalObjects = rootGameObjects.Length;
                        int processedObjects = 0;

                        foreach (var rootObject in rootGameObjects)
                        {
                            CheckShadowInGameObject(rootObject, currentScenePath, foundShadows);
                            processedObjects++;
                            EditorUtility.DisplayProgressBar("Checking Shadow Existence (Current Scene)", $"Checking objects... ({processedObjects}/{totalObjects})", (float)processedObjects / totalObjects);
                        }
                    }
                }
                finally
                {
                    EditorUtility.ClearProgressBar();
                }

                if (foundShadows.Count > 0)
                {
                    PlanarShadowCheckTool.ShowWindow(foundShadows);
                }
                else
                {
                    EditorUtility.DisplayDialog("Shadow Existence Check Complete", "No objects using PlanarShadow or shadow materials were found in the current scene.", "OK");
                }
            }

            private static void CheckShadowInGameObject(GameObject target, string path, Dictionary<GameObject, string> foundShadows)
            {
                PlanarShadow[] planarShadows = target.GetComponentsInChildren<PlanarShadow>(true);
                if (planarShadows.Length > 0)
                {
                    foreach (var planarShadow in planarShadows)
                    {
                        string message = $"Contains PlanarShadow component: {planarShadow.gameObject.name} (path: {path})";
                        Debug.LogWarning($"<color=yellow>[Planar Shadow] {message}</color>", planarShadow.gameObject);
                        foundShadows[planarShadow.gameObject] = message;
                    }
                }

                Renderer[] renderers = target.GetComponentsInChildren<Renderer>(true)
                    .Where(r => r is MeshRenderer || r is SkinnedMeshRenderer)
                    .ToArray();

                foreach (var renderer in renderers)
                {
                    if (renderer.sharedMaterials.Any(m => m != null && (m.shader == PlanarShadowEditorUtility.GetPlanarShadowBakedMat().shader || m.shader == PlanarShadowEditorUtility.GetPlanarShadowOriginalMat().shader)))
                    {
                        string message = $"Contains shadow material: {renderer.gameObject.name} (path: {path})";
                        Debug.LogWarning($"<color=yellow>[Planar Shadow] {message}</color>", renderer.gameObject);
                        foundShadows[renderer.gameObject] = message;
                    }
                }
            }

            private static void SelectObjectInHierarchyOrPrefab(GameObject obj, bool isPrefab)
            {
                if (isPrefab)
                {
                    GameObject prefab = PrefabUtility.GetCorrespondingObjectFromOriginalSource(obj);
                    if (prefab != null)
                    {
                        AssetDatabase.OpenAsset(prefab);
                        Selection.activeObject = prefab;
                    }
                }
                else
                {
                    Selection.activeGameObject = obj;
                    EditorGUIUtility.PingObject(obj);

                    if (SceneView.lastActiveSceneView != null)
                    {
                        SceneView.lastActiveSceneView.FrameSelected();
                    }
                }
            }

            public static void ShowWindow(Dictionary<GameObject, string> foundShadows)
            {
                _foundShadows = foundShadows;
                PlanarShadowCheckTool window = GetWindow<PlanarShadowCheckTool>("Planar Shadow Available List");
                window.Show();
            }

            private void OnGUI()
            {
                if (_foundShadows == null || _foundShadows.Count == 0)
                {
                    EditorGUILayout.LabelField("No issues found.");
                    return;
                }

                _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

                foreach (var entry in _foundShadows)
                {
                    GameObject obj = entry.Key;
                    string message = entry.Value;
                    bool isPrefab = PrefabUtility.IsPartOfPrefabAsset(obj);

                    EditorGUILayout.BeginHorizontal();
                    Texture icon = EditorGUIUtility.ObjectContent(obj, typeof(GameObject)).image;
                    GUILayout.Label(icon, GUILayout.Width(20), GUILayout.Height(20));

                    if (GUILayout.Button(obj.name, EditorStyles.label, GUILayout.Width(200)))
                    {
                        SelectObjectInHierarchyOrPrefab(obj, isPrefab);
                    }
                    EditorGUILayout.EndHorizontal();

                    EditorGUI.indentLevel++;
                    EditorGUILayout.LabelField($"- {message}", EditorStyles.miniLabel);
                    EditorGUI.indentLevel--;
                }

                EditorGUILayout.EndScrollView();
            }
        }
        #endregion
    }
}
