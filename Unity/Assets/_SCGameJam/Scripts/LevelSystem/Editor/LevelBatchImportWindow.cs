using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace SCJam.LevelSystem.Editor
{
    /// <summary>
    /// Lets a designer pick several bus-arrow level JSON files at once and generate/update the matching
    /// LevelConfig asset for each: the asset is named Level_{N}.asset where N is the JSON's top-level "level"
    /// field, created under LEVEL_ASSET_FOLDER if it doesn't exist yet, or reused in place (preserving any
    /// manually-set fields such as background music) if it does. Each import runs the same
    /// LevelJsonImporter.ImportFromJsonFile logic used by the single-level "Import Vehicle Placements From
    /// JSON" button in LevelConfigEditor, so board size, vehicle placements, passenger prefab mappings, and
    /// passenger color sequence all end up consistent between the single- and batch-import paths.
    /// </summary>
    public sealed class LevelBatchImportWindow : EditorWindow
    {
        // ===== Constants ===== //

        private const string LEVEL_ASSET_FOLDER = "Assets/_SCGameJam/ScriptableObjects/Levels";


        // ===== Private Fields ===== //

        private readonly List<string> _jsonPaths = new();
        private Vector2 _scrollPosition;


        // ===== Methods ===== //

        [MenuItem("SCJam/Level Batch Import")]
        private static void ShowWindow()
        {
            LevelBatchImportWindow window = GetWindow<LevelBatchImportWindow>(true, "Level Batch Import");
            window.minSize = new Vector2(420, 320);
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Bus-Arrow Level JSON Files", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Add one or more level JSON files, then Generate All to create or update the matching " +
                $"Level_{{N}}.asset (N read from each file's \"level\" field) under {LEVEL_ASSET_FOLDER}.",
                MessageType.Info);

            EditorGUILayout.Space();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Add File..."))
                AddJsonFilesRepeatedly();
            if (GUILayout.Button("Add Folder..."))
                AddJsonFilesFromFolder();
            if (GUILayout.Button("Clear List", GUILayout.Width(80)))
                _jsonPaths.Clear();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();
            DrawFileList();

            EditorGUILayout.Space();
            using (new EditorGUI.DisabledScope(_jsonPaths.Count == 0))
            {
                if (GUILayout.Button($"Generate All ({_jsonPaths.Count})", GUILayout.Height(28)))
                    GenerateAll();
            }
        }

        /// <summary>
        /// Unity has no native multi-select file dialog, so this repeatedly opens a single-file panel —
        /// each pick immediately reopens the dialog — until the user cancels (empty path returned).
        /// </summary>
        private void AddJsonFilesRepeatedly()
        {
            while (true)
            {
                string selectedPath = EditorUtility.OpenFilePanel("Select Level JSON File (Cancel when done)", Application.dataPath, "json");
                if (string.IsNullOrEmpty(selectedPath))
                    break;

                if (!_jsonPaths.Contains(selectedPath))
                    _jsonPaths.Add(selectedPath);
            }
        }

        private void AddJsonFilesFromFolder()
        {
            string selectedFolder = EditorUtility.OpenFolderPanel("Select Folder Containing Level JSON Files", Application.dataPath, "");
            if (string.IsNullOrEmpty(selectedFolder))
                return;

            foreach (string path in Directory.GetFiles(selectedFolder, "*.json", SearchOption.TopDirectoryOnly).OrderBy(path => path))
            {
                if (!_jsonPaths.Contains(path))
                    _jsonPaths.Add(path);
            }
        }

        private void DrawFileList()
        {
            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition, GUI.skin.box, GUILayout.MinHeight(150));

            if (_jsonPaths.Count == 0)
            {
                EditorGUILayout.LabelField("No files added.");
            }
            else
            {
                for (int i = 0; i < _jsonPaths.Count; i++)
                {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField(Path.GetFileName(_jsonPaths[i]));

                    if (GUILayout.Button("Remove", GUILayout.Width(70)))
                    {
                        _jsonPaths.RemoveAt(i);
                        EditorGUILayout.EndHorizontal();
                        break;
                    }

                    EditorGUILayout.EndHorizontal();
                }
            }

            EditorGUILayout.EndScrollView();
        }

        /// <summary>
        /// Generates or updates one LevelConfig asset per JSON path in the list. A file whose "level" field
        /// can't be read, or whose import fails validation, is skipped with an error log (matching
        /// LevelJsonImporter's own error reporting) rather than aborting the whole batch.
        /// </summary>
        private void GenerateAll()
        {
            if (!AssetDatabase.IsValidFolder(LEVEL_ASSET_FOLDER))
            {
                Debug.LogError($"Level batch import: folder {LEVEL_ASSET_FOLDER} does not exist.");
                return;
            }

            int successCount = 0;
            int failureCount = 0;

            foreach (string jsonPath in _jsonPaths)
            {
                if (!LevelJsonImporter.TryReadLevelNumber(jsonPath, out int levelNumber))
                {
                    Debug.LogError($"Level batch import: could not read a valid \"level\" field from {jsonPath}. Skipped.");
                    failureCount++;
                    continue;
                }

                LevelConfig levelConfig = GetOrCreateLevelAsset(levelNumber);
                SerializedObject serializedObject = new(levelConfig);

                if (LevelJsonImporter.ImportFromJsonFile(serializedObject, jsonPath))
                    successCount++;
                else
                    failureCount++;
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"Level batch import finished: {successCount} succeeded, {failureCount} failed (out of {_jsonPaths.Count}).");
        }

        private static LevelConfig GetOrCreateLevelAsset(int levelNumber)
        {
            string assetPath = $"{LEVEL_ASSET_FOLDER}/Level_{levelNumber}.asset";
            LevelConfig existingLevelConfig = AssetDatabase.LoadAssetAtPath<LevelConfig>(assetPath);

            if (existingLevelConfig != null)
                return existingLevelConfig;

            LevelConfig newLevelConfig = ScriptableObject.CreateInstance<LevelConfig>();
            AssetDatabase.CreateAsset(newLevelConfig, assetPath);
            return newLevelConfig;
        }
    }
}
