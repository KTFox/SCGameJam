using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SCJam.SceneSystem.Editor
{
    public sealed class SceneControlWindow : EditorWindow
    {
        // ===== Constants ===== //

        private const string PERSISTENT_SCENE_NAME = "Persistent";


        // ===== Methods ===== //

        [MenuItem("SCJam/Scene Control")]
        private static void Open()
        {
            GetWindow<SceneControlWindow>("Scene Control");
        }

        private void OnGUI()
        {
            DrawPlaySection();
            EditorGUILayout.Space();
            DrawBuildScenesSection();
        }

        private void DrawPlaySection()
        {
            EditorGUILayout.LabelField("Play", EditorStyles.boldLabel);

            using (new EditorGUI.DisabledScope(EditorApplication.isPlaying))
            {
                if (GUILayout.Button("Play From Persistent Scene", GUILayout.Height(30)))
                    PlayFromPersistentScene();
            }
        }

        private void DrawBuildScenesSection()
        {
            EditorGUILayout.LabelField("Open Scene", EditorStyles.boldLabel);

            EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;
            if (scenes.Length == 0)
            {
                EditorGUILayout.HelpBox("No scenes found in Build Settings.", MessageType.Info);
                return;
            }

            foreach (EditorBuildSettingsScene scene in scenes)
            {
                if (!scene.enabled)
                    continue;

                string sceneName = Path.GetFileNameWithoutExtension(scene.path);
                if (GUILayout.Button(sceneName))
                    OpenScene(scene.path);
            }
        }

        private static void PlayFromPersistentScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            string persistentScenePath = EditorBuildSettings.scenes
                .FirstOrDefault(scene => scene.enabled && Path.GetFileNameWithoutExtension(scene.path) == PERSISTENT_SCENE_NAME)
                ?.path;

            if (string.IsNullOrEmpty(persistentScenePath))
            {
                Debug.LogError($"[{nameof(SceneControlWindow)}] '{PERSISTENT_SCENE_NAME}' was not found in Build Settings.");
                return;
            }

            EditorSceneManager.OpenScene(persistentScenePath);
            EditorApplication.isPlaying = true;
        }

        private static void OpenScene(string scenePath)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            EditorSceneManager.OpenScene(scenePath);
        }
    }
}
