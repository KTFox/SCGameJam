using System.Collections.Generic;
using System.Linq;
using SCJam.Common;
using SCJam.PassengerSystem;
using SCJam.VehicleSystem;
using UnityEditor;
using UnityEngine;

namespace SCJam.LevelSystem.Editor
{
    [CustomEditor(typeof(LevelConfig))]
    public sealed class LevelConfigEditor : UnityEditor.Editor
    {
        // ===== Constants ===== //

        private const int MAX_COLOR_STREAK_LENGTH = 3;


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
        /// Rebuilds the passenger color sequence from the level's vehicle placements. Vehicles are processed
        /// in batches no larger than the waiting slot count: within a batch, all capacity is pooled per
        /// color (not per vehicle) since BoardingResolver matches a waiting vehicle by color alone and cannot
        /// tell apart two same-colored vehicles waiting at once — generating distinct interleaved runs per
        /// vehicle would desync from which physical vehicle actually consumes each passenger and can
        /// deadlock the queue. A batch's passengers are fully emitted (every color in it exhausted) before
        /// the next batch's colors enter the sequence. This caps how many distinct colors must be waiting at
        /// once to consume the front of the queue at waiting slot count, so the waiting area can never end up
        /// deadlocked — full of not-yet-full vehicles with no free slot for the color next in line.
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

            int waitingSlotCount = Mathf.Max(1, _waitingSlotCountProperty.intValue);
            System.Random random = new();
            List<PuzzleColor> colorSequence = new();

            for (int batchStart = 0; batchStart < placements.Count; batchStart += waitingSlotCount)
            {
                int batchSize = Mathf.Min(waitingSlotCount, placements.Count - batchStart);
                AppendBatchColorSequence(placements, batchStart, batchSize, random, colorSequence);
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
        /// Pools remaining capacity per color across the vehicles in placements[batchStart, batchStart +
        /// batchSize), then round-robins across colors, appending to colorSequence, until every color's pool
        /// is exhausted. Each round emits a randomized run of MAX_COLOR_STREAK_LENGTH passengers (capped by
        /// that color's remaining pool) of the current color before moving to the next, so the sequence still
        /// interleaves colors but with more same-color runs than a strict one-at-a-time rotation.
        /// </summary>
        private static void AppendBatchColorSequence(
            List<VehiclePlacement> placements,
            int batchStart,
            int batchSize,
            System.Random random,
            List<PuzzleColor> colorSequence)
        {
            List<PuzzleColor> colors = new();
            Dictionary<PuzzleColor, int> remainingCapacityByColor = new();

            for (int i = 0; i < batchSize; i++)
            {
                VehicleConfig vehicleConfig = placements[batchStart + i].VehicleConfig;
                PuzzleColor color = vehicleConfig.Color;
                int capacity = Mathf.Max(1, vehicleConfig.Capacity);

                if (remainingCapacityByColor.TryGetValue(color, out int existingCapacity))
                {
                    remainingCapacityByColor[color] = existingCapacity + capacity;
                }
                else
                {
                    colors.Add(color);
                    remainingCapacityByColor[color] = capacity;
                }
            }

            int remainingColorCount = colors.Count;

            while (remainingColorCount > 0)
            {
                for (int i = 0; i < colors.Count; i++)
                {
                    PuzzleColor color = colors[i];
                    int remainingCapacity = remainingCapacityByColor[color];
                    if (remainingCapacity <= 0)
                        continue;

                    int streakLength = Mathf.Min(remainingCapacity, random.Next(1, MAX_COLOR_STREAK_LENGTH + 1));
                    for (int j = 0; j < streakLength; j++)
                    {
                        colorSequence.Add(color);
                    }

                    remainingCapacity -= streakLength;
                    remainingCapacityByColor[color] = remainingCapacity;

                    if (remainingCapacity == 0)
                        remainingColorCount--;
                }
            }
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
