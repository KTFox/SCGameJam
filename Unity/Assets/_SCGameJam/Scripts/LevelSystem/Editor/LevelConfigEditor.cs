using System;
using System.Collections.Generic;
using System.IO;
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
        private const string VEHICLE_CONFIG_SEARCH_FOLDER = "Assets/_SCGameJam/ScriptableObjects/Vehicles";
        private const string PASSENGER_PREFAB_SEARCH_FOLDER = "Assets/_SCGameJam/Prefabs/Passengers";


        // ===== Private Fields ===== //

        private SerializedProperty _boardSizeProperty;
        private SerializedProperty _waitingSlotCountProperty;
        private SerializedProperty _backgroundMusicProperty;
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
                ImportVehiclePlacementsFromJson();

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

        /// <summary>
        /// Imports board size and vehicle placements from a bus-arrow level JSON file (grid size, and per
        /// vehicle: row/column of its head cell, direction, length, color, passenger capacity). A vehicle's
        /// head cell is r,c itself; its body extends len-1 cells opposite its facing direction (e.g. dir "R"
        /// means the head is the rightmost cell and the body extends left). The JSON's row/column origin is
        /// the top-left cell with row increasing downward; Unity's grid origin is the bottom-left cell with Y
        /// increasing upward, so the importer flips the row axis while keeping the column axis (X) unchanged.
        /// Matches each vehicle to an existing VehicleConfig asset under VEHICLE_CONFIG_SEARCH_FOLDER by
        /// (color, capacity).
        /// </summary>
        private void ImportVehiclePlacementsFromJson()
        {
            LevelConfig levelConfig = (LevelConfig)target;

            if (string.IsNullOrEmpty(_levelJsonPath) || !File.Exists(_levelJsonPath))
            {
                Debug.LogError($"Level '{levelConfig.name}': JSON import path is empty or does not point to an existing file.", levelConfig);
                return;
            }

            string json = File.ReadAllText(_levelJsonPath);
            BusArrowLevelJson levelJson = BusArrowLevelJson.Parse(json);

            if (levelJson.Grid <= 0)
            {
                Debug.LogError($"Level '{levelConfig.name}': JSON import failed, could not read a valid \"grid\" size from {_levelJsonPath}.", levelConfig);
                return;
            }

            Dictionary<string, VehicleConfig> vehicleConfigsByKey = LoadVehicleConfigsByColorAndCapacity();
            List<(Vector2Int originCell, GridDirection direction, VehicleConfig vehicleConfig)> resolvedPlacements = new();
            bool hasError = false;

            foreach (BusArrowVehicleJson vehicleJson in levelJson.Vehicles)
            {
                if (!TryGetGridDirection(vehicleJson.Dir, out GridDirection direction))
                {
                    Debug.LogError($"Level '{levelConfig.name}': vehicle id {vehicleJson.Id} has unrecognized direction \"{vehicleJson.Dir}\".", levelConfig);
                    hasError = true;
                    continue;
                }

                if (!TryGetPuzzleColor(vehicleJson.Color, out PuzzleColor color))
                {
                    Debug.LogError($"Level '{levelConfig.name}': vehicle id {vehicleJson.Id} has unrecognized color \"{vehicleJson.Color}\".", levelConfig);
                    hasError = true;
                    continue;
                }

                string vehicleConfigKey = GetVehicleConfigKey(color, vehicleJson.Passengers);
                if (!vehicleConfigsByKey.TryGetValue(vehicleConfigKey, out VehicleConfig vehicleConfig))
                {
                    Debug.LogError($"Level '{levelConfig.name}': vehicle id {vehicleJson.Id} needs a VehicleConfig with color {color} and capacity {vehicleJson.Passengers}, but none was found under {VEHICLE_CONFIG_SEARCH_FOLDER}.", levelConfig);
                    hasError = true;
                    continue;
                }

                Vector2Int originCell = GetOriginCell(vehicleJson.R, vehicleJson.C, vehicleJson.Len, direction, levelJson.Grid);
                resolvedPlacements.Add((originCell, direction, vehicleConfig));
            }

            if (hasError)
            {
                Debug.LogError($"Level '{levelConfig.name}': JSON import aborted due to unresolved vehicles above. No changes were applied.", levelConfig);
                return;
            }

            if (!TryValidatePlacements(levelConfig.name, levelJson.Grid, resolvedPlacements))
                return;

            _boardSizeProperty.vector2IntValue = new Vector2Int(levelJson.Grid, levelJson.Grid);

            _vehiclePlacementsProperty.arraySize = resolvedPlacements.Count;
            for (int i = 0; i < resolvedPlacements.Count; i++)
            {
                SerializedProperty element = _vehiclePlacementsProperty.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("_vehicleConfig").objectReferenceValue = resolvedPlacements[i].vehicleConfig;
                element.FindPropertyRelative("_originCell").vector2IntValue = resolvedPlacements[i].originCell;
                element.FindPropertyRelative("_movementDirection").enumValueIndex = (int)resolvedPlacements[i].direction;
            }

            serializedObject.ApplyModifiedProperties();

            InitPassengerPrefabMappings();
            AssignPassengerPrefabsByColorName();
            GenerateColorSequence();

            EditorUtility.SetDirty(levelConfig);
            Debug.Log($"Level '{levelConfig.name}': imported {resolvedPlacements.Count} vehicle placements from {_levelJsonPath}.", levelConfig);
        }

        /// <summary>
        /// Fills in any passenger prefab mapping that is still missing a prefab by matching PASSENGER_PREFAB_SEARCH_FOLDER
        /// for a prefab named "Passenger_{PuzzleColor}" (e.g. "Passenger_Pink"). Mappings that already have a
        /// prefab assigned are left untouched.
        /// </summary>
        private void AssignPassengerPrefabsByColorName()
        {
            LevelConfig levelConfig = (LevelConfig)target;
            Dictionary<PuzzleColor, PassengerController> passengerPrefabsByColor = LoadPassengerPrefabsByColorName();

            for (int i = 0; i < _passengerPrefabMappingsProperty.arraySize; i++)
            {
                SerializedProperty element = _passengerPrefabMappingsProperty.GetArrayElementAtIndex(i);
                SerializedProperty prefabProperty = element.FindPropertyRelative("_prefab");
                if (prefabProperty.objectReferenceValue != null)
                    continue;

                PuzzleColor color = (PuzzleColor)element.FindPropertyRelative("_color").enumValueIndex;
                if (passengerPrefabsByColor.TryGetValue(color, out PassengerController prefab))
                {
                    prefabProperty.objectReferenceValue = prefab;
                }
                else
                {
                    Debug.LogWarning($"Level '{levelConfig.name}': no passenger prefab named \"Passenger_{color}\" found under {PASSENGER_PREFAB_SEARCH_FOLDER} for color {color}.", levelConfig);
                }
            }

            serializedObject.ApplyModifiedProperties();
        }

        private static Dictionary<PuzzleColor, PassengerController> LoadPassengerPrefabsByColorName()
        {
            Dictionary<PuzzleColor, PassengerController> passengerPrefabsByColor = new();
            string[] guids = AssetDatabase.FindAssets("t:GameObject", new[] { PASSENGER_PREFAB_SEARCH_FOLDER });

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string fileName = Path.GetFileNameWithoutExtension(path);

                if (!fileName.StartsWith("Passenger_", StringComparison.Ordinal))
                    continue;

                string colorName = fileName["Passenger_".Length..];
                if (!Enum.TryParse(colorName, false, out PuzzleColor color))
                    continue;

                PassengerController prefab = AssetDatabase.LoadAssetAtPath<PassengerController>(path);
                if (prefab != null)
                    passengerPrefabsByColor[color] = prefab;
            }

            return passengerPrefabsByColor;
        }

        /// <summary>
        /// Converts a vehicle's JSON head cell (row increasing downward, column increasing rightward; body
        /// extends len-1 cells opposite the facing direction) into Unity's bottom-left, Y-up origin cell used
        /// by VehiclePlacement.
        /// </summary>
        private static Vector2Int GetOriginCell(int r, int c, int len, GridDirection direction, int gridSize)
        {
            int maxRow = direction == GridDirection.Up ? r + len - 1 : r;
            int minCol = direction == GridDirection.Right ? c - len + 1 : c;

            int originY = gridSize - 1 - maxRow;
            return new Vector2Int(minCol, originY);
        }

        private static bool TryValidatePlacements(
            string levelName,
            int gridSize,
            List<(Vector2Int originCell, GridDirection direction, VehicleConfig vehicleConfig)> placements)
        {
            Dictionary<Vector2Int, int> occupiedCells = new();
            bool isValid = true;

            for (int i = 0; i < placements.Count; i++)
            {
                (Vector2Int originCell, GridDirection direction, VehicleConfig vehicleConfig) = placements[i];
                Vector2Int footprintSize = direction is GridDirection.Left or GridDirection.Right
                    ? new Vector2Int(vehicleConfig.FootprintSize.y, vehicleConfig.FootprintSize.x)
                    : vehicleConfig.FootprintSize;

                for (int x = 0; x < footprintSize.x; x++)
                {
                    for (int y = 0; y < footprintSize.y; y++)
                    {
                        Vector2Int cell = originCell + new Vector2Int(x, y);
                        if (cell.x < 0 || cell.x >= gridSize || cell.y < 0 || cell.y >= gridSize)
                        {
                            Debug.LogError($"Level '{levelName}': placement {i} ({vehicleConfig.name}) occupies out-of-bounds cell {cell} for grid size {gridSize}.");
                            isValid = false;
                            continue;
                        }

                        if (occupiedCells.TryGetValue(cell, out int otherIndex))
                        {
                            Debug.LogError($"Level '{levelName}': placement {i} ({vehicleConfig.name}) overlaps placement {otherIndex} at cell {cell}.");
                            isValid = false;
                            continue;
                        }

                        occupiedCells[cell] = i;
                    }
                }
            }

            if (!isValid)
                Debug.LogError($"Level '{levelName}': JSON import aborted due to validation errors above. No changes were applied.");

            return isValid;
        }

        private static Dictionary<string, VehicleConfig> LoadVehicleConfigsByColorAndCapacity()
        {
            Dictionary<string, VehicleConfig> vehicleConfigsByKey = new();
            string[] guids = AssetDatabase.FindAssets("t:VehicleConfig", new[] { VEHICLE_CONFIG_SEARCH_FOLDER });

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                VehicleConfig vehicleConfig = AssetDatabase.LoadAssetAtPath<VehicleConfig>(path);
                if (vehicleConfig == null)
                    continue;

                vehicleConfigsByKey[GetVehicleConfigKey(vehicleConfig.Color, vehicleConfig.Capacity)] = vehicleConfig;
            }

            return vehicleConfigsByKey;
        }

        private static string GetVehicleConfigKey(PuzzleColor color, int capacity)
        {
            return $"{color}_{capacity}";
        }

        private static bool TryGetGridDirection(string dir, out GridDirection direction)
        {
            switch (dir)
            {
                case "U":
                    direction = GridDirection.Up;
                    return true;
                case "D":
                    direction = GridDirection.Down;
                    return true;
                case "L":
                    direction = GridDirection.Left;
                    return true;
                case "R":
                    direction = GridDirection.Right;
                    return true;
                default:
                    direction = default;
                    return false;
            }
        }

        /// <summary>
        /// Maps a bus-arrow JSON color name to a PuzzleColor. JSON levels use "red" for a color with no
        /// direct PuzzleColor equivalent; per project decision this maps to Pink.
        /// </summary>
        private static bool TryGetPuzzleColor(string color, out PuzzleColor puzzleColor)
        {
            if (string.Equals(color, "red", StringComparison.OrdinalIgnoreCase))
            {
                puzzleColor = PuzzleColor.Pink;
                return true;
            }

            return Enum.TryParse(color, true, out puzzleColor);
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

    /// <summary>
    /// Mirrors the bus-arrow level JSON schema (grid size and vehicles) for JsonUtility deserialization.
    /// </summary>
    [Serializable]
    internal sealed class BusArrowLevelJson
    {
        public int grid;
        public BusArrowVehicleJson[] vehicles;

        public int Grid => grid;
        public IReadOnlyList<BusArrowVehicleJson> Vehicles => vehicles ?? Array.Empty<BusArrowVehicleJson>();

        public static BusArrowLevelJson Parse(string json)
        {
            return JsonUtility.FromJson<BusArrowLevelJson>(json);
        }
    }

    [Serializable]
    internal sealed class BusArrowVehicleJson
    {
        public int id;
        public int r;
        public int c;
        public string dir;
        public int len;
        public string color;
        public int passengers;

        public int Id => id;
        public int R => r;
        public int C => c;
        public string Dir => dir;
        public int Len => len;
        public string Color => color;
        public int Passengers => passengers;
    }
}
