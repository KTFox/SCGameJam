using System.Collections.Generic;
using System.Linq;
using SCJam.Common;
using SCJam.PassengerSystem;
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
        private SerializedProperty _backgroundMusicProperty;
        private SerializedProperty _vehiclePlacementsProperty;
        private SerializedProperty _passengerPrefabMappingsProperty;
        private SerializedProperty _passengerColorSequenceProperty;

        private GUIStyle _passengerSectionTitleStyle;


        // ===== Methods ===== //

        private void OnEnable()
        {
            _boardSizeProperty = serializedObject.FindProperty("_boardSize");
            _waitingSlotCountProperty = serializedObject.FindProperty("_waitingSlotCount");
            _backgroundMusicProperty = serializedObject.FindProperty("_backgroundMusic");
            _vehiclePlacementsProperty = serializedObject.FindProperty("_vehiclePlacements");
            _passengerPrefabMappingsProperty = serializedObject.FindProperty("_passengerPrefabMappings");
            _passengerColorSequenceProperty = serializedObject.FindProperty("_passengerColorSequence");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.PropertyField(_boardSizeProperty, new GUIContent("Board Size"));
            EditorGUILayout.IntSlider(_waitingSlotCountProperty, 1, 7, new GUIContent("Waiting Slot Count"));
            EditorGUILayout.PropertyField(_backgroundMusicProperty);

            EditorGUILayout.Space();
            EditorGUILayout.PropertyField(_vehiclePlacementsProperty, true);

            EditorGUILayout.Space();
            DrawPassengerSection();

            serializedObject.ApplyModifiedProperties();
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
                InitPassengerPrefabMappings();

            EditorGUILayout.PropertyField(_passengerPrefabMappingsProperty, true);

            EditorGUILayout.Space();

            if (GUILayout.Button("Generate Color Sequence"))
                GenerateColorSequence();

            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.PropertyField(_passengerColorSequenceProperty, true);
            }

            EditorGUILayout.EndVertical();
        }

        /// <summary>
        /// Rebuilds the passenger color sequence from the level's vehicle placements. Each placement
        /// contributes one contiguous block sized to its vehicle's capacity and colored to match, since the
        /// boarding rule (see BoardingResolver.TryBoard) only removes a contiguous same-color run from the
        /// front of the queue up to a waiting vehicle's remaining capacity. Sizing every block to exactly one
        /// vehicle's capacity guarantees that block is always fully consumable by that vehicle regardless of
        /// arrival timing, so the level stays solvable no matter which order vehicles reach the waiting area.
        /// </summary>
        private void GenerateColorSequence()
        {
            LevelConfig levelConfig = (LevelConfig)target;
            List<VehiclePlacement> placements = GetValidVehiclePlacements(levelConfig);

            if (placements.Count == 0)
            {
                Debug.LogWarning($"Level '{levelConfig.name}': no valid vehicle placements to generate a color sequence from.", levelConfig);
                return;
            }

            List<PuzzleColor> colorSequence = new();

            foreach (VehiclePlacement placement in placements)
            {
                PuzzleColor color = placement.VehicleConfig.Color;
                int capacity = Mathf.Max(1, placement.VehicleConfig.Capacity);

                for (int i = 0; i < capacity; i++)
                    colorSequence.Add(color);
            }

            _passengerColorSequenceProperty.arraySize = colorSequence.Count;
            for (int i = 0; i < colorSequence.Count; i++)
            {
                _passengerColorSequenceProperty.GetArrayElementAtIndex(i).enumValueIndex = (int)colorSequence[i];
            }

            serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(levelConfig);
        }

        /// <summary>
        /// Syncs passenger prefab mappings to the set of PuzzleColors actually used by this level's vehicle
        /// placements: removes mappings for colors no longer present, adds missing ones (prefab left unset),
        /// and collapses duplicate mappings for the same color down to one.
        /// </summary>
        private void InitPassengerPrefabMappings()
        {
            LevelConfig levelConfig = (LevelConfig)target;
            List<VehiclePlacement> placements = GetValidVehiclePlacements(levelConfig);

            HashSet<PuzzleColor> requiredColors = placements
                .Select(placement => placement.VehicleConfig.Color)
                .ToHashSet();

            List<PassengerPrefabMapping> existingMappings = new(levelConfig.PassengerPrefabMappings ?? System.Array.Empty<PassengerPrefabMapping>());
            Dictionary<PuzzleColor, PassengerPrefabMapping> mappingsByColor = new();

            foreach (PassengerPrefabMapping mapping in existingMappings)
            {
                if (!requiredColors.Contains(mapping.Color))
                    continue;

                mappingsByColor.TryAdd(mapping.Color, mapping);
            }

            foreach (PuzzleColor color in requiredColors)
            {
                if (!mappingsByColor.ContainsKey(color))
                    mappingsByColor[color] = new PassengerPrefabMapping(color, null);
            }

            List<PassengerPrefabMapping> resultMappings = requiredColors
                .OrderBy(color => (int)color)
                .Select(color => mappingsByColor[color])
                .ToList();

            _passengerPrefabMappingsProperty.arraySize = resultMappings.Count;
            for (int i = 0; i < resultMappings.Count; i++)
            {
                SerializedProperty element = _passengerPrefabMappingsProperty.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("_color").enumValueIndex = (int)resultMappings[i].Color;
                element.FindPropertyRelative("_prefab").objectReferenceValue = resultMappings[i].Prefab;
            }

            serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(levelConfig);
        }

        private static List<VehiclePlacement> GetValidVehiclePlacements(LevelConfig levelConfig)
        {
            IReadOnlyList<VehiclePlacement> placements = levelConfig.VehiclePlacements;
            if (placements == null)
                return new List<VehiclePlacement>();

            return placements
                .Where(placement => placement?.VehicleConfig != null)
                .ToList();
        }
    }
}
