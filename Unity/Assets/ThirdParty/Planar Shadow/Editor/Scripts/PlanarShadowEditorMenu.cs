using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Supercent.Rendering.Shadow.Editor
{
    public static class PlanarShadowEditorMenu
    {
        [MenuItem("Supercent/Planar Shadow/Add and Remove Shadows/Add Shadow to Selected Objects", false, 2)]
        private static void AddShadow()
        {
            if (false == EditorUtility.DisplayDialog("Operation Warning", "This operation cannot be undone. Do you want to continue?", "Yes", "No"))
                return;

            int count = 0;

            foreach (GameObject go in Selection.gameObjects)
            {
                if (!go.TryGetComponent(out PlanarShadow _) && (go.TryGetComponent(out MeshRenderer _) || go.TryGetComponent(out SkinnedMeshRenderer _)))
                {
                    PlanarShadow planarShadow = go.AddComponent<PlanarShadow>();
                    planarShadow.Editor_FindComponents();
                    planarShadow.Editor_AddPlanarShadowMaterial();
                    ++count;
                }
            }

            Debug.Log($"<color=cyan>[Planar Shadow Editor] Shadows were added to {count} object(s).</color>");

            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog("Operation Complete", "The operation has been completed.", "OK");
        }

        [MenuItem("Supercent/Planar Shadow/Add and Remove Shadows/Remove Shadow from Selected Objects", false, 3)]
        private static void RemoveShadow()
        {
            if (false == EditorUtility.DisplayDialog("Operation Warning", "This operation cannot be undone. Do you want to continue?", "Yes", "No"))
                return;

            int count = 0;

            foreach (GameObject go in Selection.gameObjects)
            {
                if (go.TryGetComponent(out PlanarShadow planarShadow))
                {
                    planarShadow.Editor_RemovePlanarShadowMaterial();
                    GameObject.DestroyImmediate(planarShadow, true);
                    ++count;
                }
            }

            Debug.Log($"<color=cyan>[Planar Shadow Editor] Shadows were removed from {count} object(s).</color>");

            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog("Operation Complete", "The operation has been completed.", "OK");
        }

        #region Refresh All
        [MenuItem("Supercent/Planar Shadow/Shadow Stabilization Tools/🚨 All Shadows (Prefabs in Assets Folder and All Scene Objects)", false, 4)]
        public static void ResetAllSettingsOnAnywhere()
        {
            if (false == EditorUtility.DisplayDialog("Operation Warning", "This operation cannot be undone. Do you want to continue?", "Yes", "No"))
                return;

            RefreshPlanarShadowOnAssetsFolder();
            RefreshPlanarShadowOnAllScenes();

            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog("Operation Complete", "The operation has been completed.", "OK");
        }

        [MenuItem("Supercent/Planar Shadow/Shadow Stabilization Tools/All Prefabs in Assets Folder", false, 5)]
        public static void ResetAllSettingsOnAssetsFolder()
        {
            if (false == EditorUtility.DisplayDialog("Operation Warning", "This operation cannot be undone. Do you want to continue?", "Yes", "No"))
                return;

            RefreshPlanarShadowOnAssetsFolder();
            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog("Operation Complete", "The operation has been completed.", "OK");
        }

        [MenuItem("Supercent/Planar Shadow/Shadow Stabilization Tools/All Objects in Current Scene", false, 6)]
        private static void ResetAllSettingsOnCurrentScene()
        {
            if (false == EditorUtility.DisplayDialog("Operation Warning", "This operation cannot be undone. Do you want to continue?", "Yes", "No"))
                return;            

            RefreshPlanarShadowOnCurrentScene();
            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog("Operation Complete", "The operation has been completed.", "OK");
        }

        public static void RefreshPlanarShadowOnAssetsFolder()
        {
            string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab");
            int total = prefabGuids.Length;
            int processed = 0;

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

                    RefreshPlanarShadowInGameObject(asset, true, path);
                    processed++;
                    EditorUtility.DisplayProgressBar("Refreshing Planar Shadow", $"Processing assets folder prefabs... ({processed}/{total})", (float)processed / total);
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        private static void RefreshPlanarShadowOnCurrentScene()
        {
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
                        RefreshPlanarShadowInGameObject(rootObject, false, currentScenePath);
                        processedObjects++;
                        EditorUtility.DisplayProgressBar("Refreshing Planar Shadow", $"Processing current scene objects... ({processedObjects}/{totalObjects})", (float)processedObjects / totalObjects);
                    }

                    EditorSceneManager.SaveScene(currentScene);
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        private static void RefreshPlanarShadowOnAllScenes()
        {
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
                }

                string[] allScenePaths = AssetDatabase.FindAssets("t:Scene")
                    .Select(AssetDatabase.GUIDToAssetPath)
                    .ToArray();

                int totalScenes = allScenePaths.Length;
                int processedScenes = 0;

                foreach (string scenePath in allScenePaths)
                {
                    Scene loadedScene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                    GameObject[] rootGameObjects = loadedScene.GetRootGameObjects();
                    int totalObjects = rootGameObjects.Length;
                    int processedObjects = 0;

                    foreach (var rootObject in rootGameObjects)
                    {
                        RefreshPlanarShadowInGameObject(rootObject, false, scenePath);
                        processedObjects++;
                        EditorUtility.DisplayProgressBar("Refreshing Planar Shadow", $"Processing scene: {scenePath}... ({processedObjects}/{totalObjects})", (float)processedObjects / totalObjects);
                    }

                    EditorSceneManager.SaveScene(loadedScene);
                    processedScenes++;
                    EditorUtility.DisplayProgressBar("Refreshing Planar Shadow", $"Processing all scenes... ({processedScenes}/{totalScenes})", 0.5f + ((float)processedScenes / totalScenes) * 0.5f);
                }

                if (!string.IsNullOrEmpty(currentScenePath))
                {
                    EditorSceneManager.OpenScene(currentScenePath, OpenSceneMode.Single);
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        private static void RefreshPlanarShadowInGameObject(GameObject target, bool isPrefab, string assetPath)
        {
            PlanarShadow[] planarShadows = target.GetComponentsInChildren<PlanarShadow>(true);
            bool isModified = false;

            foreach (var planarShadow in planarShadows)
            {
                if (!PrefabUtility.IsPartOfPrefabInstance(planarShadow.gameObject) ||
                    PrefabUtility.HasPrefabInstanceAnyOverrides(planarShadow.gameObject, false))
                {
                    planarShadow.Editor_ResetSettings();
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
        #endregion

        #region Refresh Selection
        [MenuItem("Supercent/Planar Shadow/Shadow Stabilization Tools/Selected Objects", false, 7)]
        private static void ResetSettingsOnSelection()
        {
            if (false == EditorUtility.DisplayDialog("Operation Warning", "This operation cannot be undone. Do you want to continue?", "Yes", "No"))
                return;

            List<PlanarShadow> planarShadows = Selection.gameObjects
                                        .Where(x => x.GetComponent<PlanarShadow>() != null)
                                        .Select(x => x.GetComponent<PlanarShadow>())
                                        .ToList();

            foreach (PlanarShadow planarShadow in planarShadows)
            {
                planarShadow.Editor_ResetSettings();
                EditorUtility.SetDirty(planarShadow);
            }

            EditorUtility.DisplayDialog("Operation Complete", "The operation has been completed.", "OK");
        }
        #endregion

        #region Features
        #endregion

        #region Disable Undefined Pivot Shadow Offsets
        [MenuItem("Supercent/Planar Shadow/Other/🚨 Disable Undefined Pivot Shadows (Prefabs in Assets Folder and All Scene Objects)", false, 13)]
        private static void CleanNonDefinedPivotRenderSettings()
        {
            if (false == EditorUtility.DisplayDialog("Operation Warning", "This operation cannot be undone. Do you want to continue?", "Yes", "No"))
                return;

            CleanNonDefinedPivotRenderSettingsOnAssetsFolder();
            CleanNonDefinedPivotRenderSettingsOnScenes();

            EditorUtility.DisplayDialog("Operation Complete", "The operation has been completed.", "OK");
        }

        private static void CleanNonDefinedPivotRenderSettingsOnAssetsFolder()
        {
            string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab");
            int total = prefabGuids.Length;
            int processed = 0;

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

                    CleanNonDefinedPivotRenderSetting(asset, true, path);
                    processed++;
                    EditorUtility.DisplayProgressBar("Removing Shadows", $"Processing assets... ({processed}/{total})", (float)processed / total);
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        private static void CleanNonDefinedPivotRenderSettingsOnScenes()
        {
            Scene currentScene = SceneManager.GetActiveScene();
            string currentScenePath = currentScene.path;
            bool isCurrentSceneDirty = currentScene.isDirty;

            try
            {
                if (!string.IsNullOrEmpty(currentScenePath) && isCurrentSceneDirty)
                {
                    if (EditorUtility.DisplayDialog("Save Scene Changes", "The currently open scene has unsaved changes. Do you want to save them?", "Save", "Cancel"))
                    {
                        EditorSceneManager.SaveScene(currentScene);
                    }
                }

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
                        CleanNonDefinedPivotRenderSetting(rootObject, false, scenePath);
                    }

                    EditorSceneManager.SaveScene(loadedScene);
                    processed++;
                    EditorUtility.DisplayProgressBar("Removing Shadows", $"Processing scenes... ({processed}/{total})", 0.5f + (float)processed / total * 0.5f);
                }

                if (!string.IsNullOrEmpty(currentScenePath))
                {
                    EditorSceneManager.OpenScene(currentScenePath, OpenSceneMode.Single);
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        private static void CleanNonDefinedPivotRenderSetting(GameObject target, bool isPrefab, string assetPath)
        {
            PlanarShadow[] planarShadows = target.GetComponentsInChildren<PlanarShadow>(true);
            bool isModified = false;

            foreach (var planarShadow in planarShadows)
            {
                if (PrefabUtility.IsPartOfPrefabInstance(planarShadow.gameObject))
                {
                    if (PrefabUtility.HasPrefabInstanceAnyOverrides(planarShadow.gameObject, false))
                    {
                        if (planarShadow.Editor_UsePivotShadow)
                        {
                            if (false == planarShadow.Editor_IsPivotShadowOffsetDefined)
                            {
                                planarShadow.Editor_RefreshMaterialArray();
                                Debug.Log($"<color=yellow>[Planar Shadow] {planarShadow.gameObject.name} - Disabled pivot setting on prefab instance</color>");
                                planarShadow.Editor_TogglePivotRenderingMode(false);
                                EditorUtility.SetDirty(planarShadow);
                                isModified = true;
                            }
                        }
                    }
                    continue;
                }

                if (planarShadow.Editor_UsePivotShadow)
                {
                    if (false == planarShadow.Editor_IsPivotShadowOffsetDefined)
                    {
                        planarShadow.Editor_RefreshMaterialArray();
                        Debug.Log($"<color=yellow>[Planar Shadow] {planarShadow.gameObject.name} - Disabled pivot setting</color>");
                        planarShadow.Editor_TogglePivotRenderingMode(false);
                        EditorUtility.SetDirty(planarShadow);
                        isModified = true;
                    }
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
        #endregion
    }
}
