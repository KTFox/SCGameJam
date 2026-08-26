using UnityEditor;
using UnityEngine;

namespace SCJam.LevelSystem.Editor
{
    [CustomEditor(typeof(LevelConfig))]
    public sealed class LevelConfigEditor : UnityEditor.Editor
    {
        // ===== Private Fields ===== //

        private SerializedProperty _boardSizeProperty;
        private SerializedProperty _waitingSlotCountProperty;
        private SerializedProperty _vehiclePlacementsProperty;
        private SerializedProperty _passengerPrefabMappingsProperty;
        private SerializedProperty _passengerColorSequenceProperty;

        private GUIStyle _passengerSectionTitleStyle;
        private string _levelJsonPath = "";


        // ===== Methods ===== //

        private void OnEnable()
        {
            _boardSizeProperty = serializedObject.FindProperty("_boardSize");
            _waitingSlotCountProperty = serializedObject.FindProperty("_waitingSlotCount");
            _vehiclePlacementsProperty = serializedObject.FindProperty("_vehiclePlacements");
            _passengerPrefabMappingsProperty = serializedObject.FindProperty("_passengerPrefabMappings");
            _passengerColorSequenceProperty = serializedObject.FindProperty("_passengerColorSequence");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.PropertyField(_boardSizeProperty, new GUIContent("Board Size"));
            EditorGUILayout.IntSlider(_waitingSlotCountProperty, 1, 7, new GUIContent("Waiting Slot Count"));

            EditorGUILayout.Space();
            DrawJsonImportSection();

            EditorGUILayout.Space();
            DrawSolvabilitySection();

            EditorGUILayout.Space();
            EditorGUILayout.PropertyField(_vehiclePlacementsProperty, true);

            EditorGUILayout.Space();
            DrawPassengerSection();

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawJsonImportSection()
        {
            EditorGUILayout.BeginVertical(GUI.skin.box);
            EditorGUILayout.LabelField("Import From JSON", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            _levelJsonPath = EditorGUILayout.TextField("Level JSON File", _levelJsonPath);
            if (GUILayout.Button("Browse...", GUILayout.Width(80)))
            {
                string selectedPath = EditorUtility.OpenFilePanel("Select Level JSON", Application.dataPath, "json");
                if (!string.IsNullOrEmpty(selectedPath))
                    _levelJsonPath = selectedPath;
            }
            EditorGUILayout.EndHorizontal();

            if (GUILayout.Button("Import Vehicle Placements From JSON"))
            {
                if (LevelJsonImporter.ImportFromJsonFile(serializedObject, _levelJsonPath))
                    serializedObject.Update();
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawSolvabilitySection()
        {
            EditorGUILayout.BeginVertical(GUI.skin.box);
            EditorGUILayout.LabelField("Solvability", EditorStyles.boldLabel);

            if (GUILayout.Button("Check Solvability"))
                CheckSolvability();

            EditorGUILayout.EndVertical();
        }

        /// <summary>
        /// Runs LevelSolvabilityChecker against this level and logs the verdict. On success, logs each step
        /// of a winning vehicle order (which vehicle exits to the waiting area, and every passenger-boarding
        /// / vehicle-departure event that follows) so a designer can see exactly how the level is cleared.
        /// </summary>
        private void CheckSolvability()
        {
            LevelConfig levelConfig = (LevelConfig)target;
            LevelSolvabilityChecker.SolveResult result = LevelSolvabilityChecker.Solve(levelConfig);

            if (result.IsSolved)
            {
                Debug.Log($"Level '{levelConfig.name}': SOLVABLE in {result.MoveOrder.Count} moves (explored {result.VisitedNodeCount} states).", levelConfig);
                foreach (string step in result.StepLog)
                    Debug.Log(step, levelConfig);
            }
            else if (result.IsInconclusive)
            {
                Debug.LogWarning($"Level '{levelConfig.name}': INCONCLUSIVE, {result.Message}", levelConfig);
            }
            else
            {
                Debug.LogError($"Level '{levelConfig.name}': UNSOLVABLE, {result.Message}", levelConfig);
            }
        }

        private void DrawPassengerSection()
        {
            _passengerSectionTitleStyle ??= new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 14,
                alignment = TextAnchor.MiddleCenter
            };

            EditorGUILayout.BeginVertical(GUI.skin.box);
            EditorGUILayout.LabelField("Passenger", _passengerSectionTitleStyle);

            if (GUILayout.Button("Init Passenger Prefab Mappings"))
            {
                LevelJsonImporter.InitPassengerPrefabMappings(serializedObject);
                serializedObject.Update();
            }

            EditorGUILayout.PropertyField(_passengerPrefabMappingsProperty, true);

            EditorGUILayout.Space();

            if (GUILayout.Button("Generate Color Sequence"))
            {
                LevelJsonImporter.GenerateColorSequence(serializedObject);
                serializedObject.Update();
            }

            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.PropertyField(_passengerColorSequenceProperty, true);
            }

            EditorGUILayout.EndVertical();
        }
    }
}
