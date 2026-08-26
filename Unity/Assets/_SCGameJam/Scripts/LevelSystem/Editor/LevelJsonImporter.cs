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
    /// <summary>
    /// Shared logic for importing a bus-arrow level JSON file into a LevelConfig's SerializedObject: vehicle
    /// placements, board size, passenger prefab mappings, and passenger color sequence. Used by both
    /// LevelConfigEditor (importing into the currently-inspected level) and LevelBatchImportWindow (importing
    /// into many levels at once, one per selected JSON file).
    /// </summary>
    internal static class LevelJsonImporter
    {
        // ===== Constants ===== //

        private const int MAX_COLOR_STREAK_LENGTH = 3;
        private const string VEHICLE_CONFIG_SEARCH_FOLDER = "Assets/_SCGameJam/ScriptableObjects/Vehicles";
        private const string PASSENGER_PREFAB_SEARCH_FOLDER = "Assets/_SCGameJam/Prefabs/Passengers";


        // ===== Methods ===== //

        /// <summary>
        /// Imports board size and vehicle placements from a bus-arrow level JSON file (grid size, and per
        /// vehicle: row/column of its head cell, direction, length, color, passenger capacity) into
        /// levelConfig via serializedObject, then regenerates passenger prefab mappings and color sequence to
        /// match. A vehicle's head cell is r,c itself; its body extends len-1 cells opposite its facing
        /// direction (e.g. dir "R" means the head is the rightmost cell and the body extends left). The
        /// JSON's row/column origin is the top-left cell with row increasing downward; Unity's grid origin is
        /// the bottom-left cell with Y increasing upward, so the importer flips the row axis while keeping the
        /// column axis (X) unchanged. Matches each vehicle to an existing VehicleConfig asset under
        /// VEHICLE_CONFIG_SEARCH_FOLDER by (color, capacity). Returns true if the import succeeded.
        /// </summary>
        public static bool ImportFromJsonFile(SerializedObject serializedObject, string jsonPath)
        {
            LevelConfig levelConfig = (LevelConfig)serializedObject.targetObject;

            if (string.IsNullOrEmpty(jsonPath) || !File.Exists(jsonPath))
            {
                Debug.LogError($"Level '{levelConfig.name}': JSON import path is empty or does not point to an existing file.", levelConfig);
                return false;
            }

            string json = File.ReadAllText(jsonPath);
            BusArrowLevelJson levelJson = BusArrowLevelJson.Parse(json);

            if (levelJson.Grid <= 0)
            {
                Debug.LogError($"Level '{levelConfig.name}': JSON import failed, could not read a valid \"grid\" size from {jsonPath}.", levelConfig);
                return false;
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
                return false;
            }

            if (!TryValidatePlacements(levelConfig.name, levelJson.Grid, resolvedPlacements))
                return false;

            SerializedProperty boardSizeProperty = serializedObject.FindProperty("_boardSize");
            SerializedProperty vehiclePlacementsProperty = serializedObject.FindProperty("_vehiclePlacements");

            boardSizeProperty.vector2IntValue = new Vector2Int(levelJson.Grid, levelJson.Grid);

            vehiclePlacementsProperty.arraySize = resolvedPlacements.Count;
            for (int i = 0; i < resolvedPlacements.Count; i++)
            {
                SerializedProperty element = vehiclePlacementsProperty.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("_vehicleConfig").objectReferenceValue = resolvedPlacements[i].vehicleConfig;
                element.FindPropertyRelative("_originCell").vector2IntValue = resolvedPlacements[i].originCell;
                element.FindPropertyRelative("_movementDirection").enumValueIndex = (int)resolvedPlacements[i].direction;
            }

            serializedObject.ApplyModifiedProperties();

            InitPassengerPrefabMappings(serializedObject);
            AssignPassengerPrefabsByColorName(serializedObject);
            GenerateColorSequence(serializedObject);

            EditorUtility.SetDirty(levelConfig);
            Debug.Log($"Level '{levelConfig.name}': imported {resolvedPlacements.Count} vehicle placements from {jsonPath}.", levelConfig);
            return true;
        }

        /// <summary>
        /// Fills in any passenger prefab mapping that is still missing a prefab by matching PASSENGER_PREFAB_SEARCH_FOLDER
        /// for a prefab named "Passenger_{PuzzleColor}" (e.g. "Passenger_Pink"). Mappings that already have a
        /// prefab assigned are left untouched.
        /// </summary>
        public static void AssignPassengerPrefabsByColorName(SerializedObject serializedObject)
        {
            LevelConfig levelConfig = (LevelConfig)serializedObject.targetObject;
            SerializedProperty passengerPrefabMappingsProperty = serializedObject.FindProperty("_passengerPrefabMappings");
            Dictionary<PuzzleColor, PassengerController> passengerPrefabsByColor = LoadPassengerPrefabsByColorName();

            for (int i = 0; i < passengerPrefabMappingsProperty.arraySize; i++)
            {
                SerializedProperty element = passengerPrefabMappingsProperty.GetArrayElementAtIndex(i);
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

        /// <summary>
        /// Syncs passenger prefab mappings to the set of PuzzleColors actually used by this level's vehicle
        /// placements: removes mappings for colors no longer present, adds missing ones (prefab left unset),
        /// and collapses duplicate mappings for the same color down to one.
        /// </summary>
        public static void InitPassengerPrefabMappings(SerializedObject serializedObject)
        {
            LevelConfig levelConfig = (LevelConfig)serializedObject.targetObject;
            SerializedProperty passengerPrefabMappingsProperty = serializedObject.FindProperty("_passengerPrefabMappings");
            List<VehiclePlacement> placements = GetValidVehiclePlacements(levelConfig);

            HashSet<PuzzleColor> requiredColors = placements
                .Select(placement => placement.VehicleConfig.Color)
                .ToHashSet();

            List<PassengerPrefabMapping> existingMappings = new(levelConfig.PassengerPrefabMappings ?? Array.Empty<PassengerPrefabMapping>());
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

            passengerPrefabMappingsProperty.arraySize = resultMappings.Count;
            for (int i = 0; i < resultMappings.Count; i++)
            {
                SerializedProperty element = passengerPrefabMappingsProperty.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("_color").enumValueIndex = (int)resultMappings[i].Color;
                element.FindPropertyRelative("_prefab").objectReferenceValue = resultMappings[i].Prefab;
            }

            serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(levelConfig);
        }

        /// <summary>
        /// Rebuilds the passenger color sequence from a vehicle order that is actually reachable in play,
        /// instead of the raw _vehiclePlacements array order: at each step it only considers vehicles that
        /// are still Parked and currently have a clear exit path (mirroring VehicleMovementResolver.IsPathClear
        /// and LevelSolvabilityChecker), so a vehicle blocked by another vehicle placed later in the array is
        /// never scheduled before its blocker leaves. Vehicles are processed in batches no larger than the
        /// waiting slot count, taking only currently-movable vehicles per batch (deferring any that are still
        /// blocked to a later batch, once earlier batches have vacated their board cells); within a batch, all
        /// capacity is pooled per color (not per vehicle) since BoardingResolver matches a waiting vehicle by
        /// color alone and cannot tell apart two same-colored vehicles waiting at once — generating distinct
        /// interleaved runs per vehicle would desync from which physical vehicle actually consumes each
        /// passenger and can deadlock the queue. A batch's passengers are fully emitted (every color in it
        /// exhausted) before the next batch's colors enter the sequence. This caps how many distinct colors
        /// must be waiting at once to consume the front of the queue at waiting slot count, and since the
        /// batch order itself is a valid vehicle-release order, the generated sequence is guaranteed solvable
        /// by releasing vehicles in that same order.
        /// </summary>
        public static void GenerateColorSequence(SerializedObject serializedObject)
        {
            LevelConfig levelConfig = (LevelConfig)serializedObject.targetObject;
            SerializedProperty waitingSlotCountProperty = serializedObject.FindProperty("_waitingSlotCount");
            SerializedProperty passengerColorSequenceProperty = serializedObject.FindProperty("_passengerColorSequence");
            List<VehiclePlacement> placements = GetValidVehiclePlacements(levelConfig);

            if (placements.Count == 0)
            {
                Debug.LogWarning($"Level '{levelConfig.name}': no valid vehicle placements to generate a color sequence from.", levelConfig);
                return;
            }

            int waitingSlotCount = Mathf.Max(1, waitingSlotCountProperty.intValue);
            List<List<VehiclePlacement>> releaseBatches = BuildReachableReleaseBatches(levelConfig.BoardSize, placements, waitingSlotCount);

            if (releaseBatches == null)
            {
                Debug.LogError($"Level '{levelConfig.name}': could not find a vehicle release order that clears the board (a vehicle's exit path never becomes clear). Color sequence was not changed.", levelConfig);
                return;
            }

            System.Random random = new();
            List<PuzzleColor> colorSequence = new();

            foreach (List<VehiclePlacement> batch in releaseBatches)
                AppendBatchColorSequence(batch, random, colorSequence);

            passengerColorSequenceProperty.arraySize = colorSequence.Count;
            for (int i = 0; i < colorSequence.Count; i++)
            {
                passengerColorSequenceProperty.GetArrayElementAtIndex(i).enumValueIndex = (int)colorSequence[i];
            }

            serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(levelConfig);
        }

        public static List<VehiclePlacement> GetValidVehiclePlacements(LevelConfig levelConfig)
        {
            IReadOnlyList<VehiclePlacement> placements = levelConfig.VehiclePlacements;
            if (placements == null)
                return new List<VehiclePlacement>();

            return placements
                .Where(placement => placement?.VehicleConfig != null)
                .ToList();
        }

        /// <summary>
        /// Reads only the top-level "level" field from a bus-arrow JSON file, used to name the LevelConfig
        /// asset (Level_{N}.asset) before the rest of the file is parsed/imported.
        /// </summary>
        public static bool TryReadLevelNumber(string jsonPath, out int levelNumber)
        {
            levelNumber = 0;

            if (string.IsNullOrEmpty(jsonPath) || !File.Exists(jsonPath))
                return false;

            string json = File.ReadAllText(jsonPath);
            BusArrowLevelNumberJson levelNumberJson = JsonUtility.FromJson<BusArrowLevelNumberJson>(json);

            if (levelNumberJson == null || levelNumberJson.level <= 0)
                return false;

            levelNumber = levelNumberJson.level;
            return true;
        }

        /// <summary>
        /// Greedily groups placements into release batches of up to waitingSlotCount vehicles: each batch
        /// takes every still-parked vehicle whose exit path is currently clear (mirroring
        /// VehicleMovementResolver.IsPathClear against only the remaining still-parked vehicles), capped at
        /// waitingSlotCount and preferring _vehiclePlacements array order among ties so the result stays as
        /// close as possible to the original authoring order. Returns null if a full pass finds no movable
        /// vehicle while placements remain (the layout itself has no valid release order, independent of
        /// passenger colors).
        /// </summary>
        private static List<List<VehiclePlacement>> BuildReachableReleaseBatches(Vector2Int boardSize, List<VehiclePlacement> placements, int waitingSlotCount)
        {
            List<VehiclePlacement> remaining = new(placements);
            List<List<VehiclePlacement>> batches = new();

            while (remaining.Count > 0)
            {
                List<VehiclePlacement> batch = new();

                foreach (VehiclePlacement placement in remaining)
                {
                    if (batch.Count >= waitingSlotCount)
                        break;

                    if (IsExitPathClear(boardSize, placement, remaining))
                        batch.Add(placement);
                }

                if (batch.Count == 0)
                    return null;

                foreach (VehiclePlacement placement in batch)
                    remaining.Remove(placement);

                batches.Add(batch);
            }

            return batches;
        }

        /// <summary>
        /// Mirrors VehicleMovementResolver.IsPathClear: every cell swept from the vehicle's oriented footprint
        /// to the board boundary along its fixed MovementDirection must be free of every other still-parked
        /// vehicle's footprint.
        /// </summary>
        private static bool IsExitPathClear(Vector2Int boardSize, VehiclePlacement placement, List<VehiclePlacement> stillParked)
        {
            HashSet<Vector2Int> occupiedCells = new();
            foreach (VehiclePlacement other in stillParked)
            {
                if (other == placement)
                    continue;

                Vector2Int otherFootprintSize = GetOrientedFootprintSize(other.VehicleConfig.FootprintSize, other.MovementDirection);
                foreach (Vector2Int cell in GetFootprintCells(other.OriginCell, otherFootprintSize))
                    occupiedCells.Add(cell);
            }

            Vector2Int footprintSize = GetOrientedFootprintSize(placement.VehicleConfig.FootprintSize, placement.MovementDirection);
            Vector2Int step = GetDirectionStep(placement.MovementDirection);

            foreach (Vector2Int footprintCell in GetFootprintCells(placement.OriginCell, footprintSize))
            {
                Vector2Int current = footprintCell + step;
                while (current.x >= 0 && current.x < boardSize.x && current.y >= 0 && current.y < boardSize.y)
                {
                    if (occupiedCells.Contains(current))
                        return false;

                    current += step;
                }
            }

            return true;
        }

        private static Vector2Int GetOrientedFootprintSize(Vector2Int footprintSize, GridDirection movementDirection)
        {
            return movementDirection is GridDirection.Left or GridDirection.Right
                ? new Vector2Int(footprintSize.y, footprintSize.x)
                : footprintSize;
        }

        private static IEnumerable<Vector2Int> GetFootprintCells(Vector2Int originCell, Vector2Int footprintSize)
        {
            for (int x = 0; x < footprintSize.x; x++)
                for (int y = 0; y < footprintSize.y; y++)
                    yield return originCell + new Vector2Int(x, y);
        }

        private static Vector2Int GetDirectionStep(GridDirection direction)
        {
            return direction switch
            {
                GridDirection.Up => new Vector2Int(0, 1),
                GridDirection.Down => new Vector2Int(0, -1),
                GridDirection.Left => new Vector2Int(-1, 0),
                GridDirection.Right => new Vector2Int(1, 0),
                _ => Vector2Int.zero
            };
        }

        /// <summary>
        /// Pools remaining capacity per color across a release batch, then round-robins across colors,
        /// appending to colorSequence, until every color's pool is exhausted. Each round emits a randomized
        /// run of MAX_COLOR_STREAK_LENGTH passengers (capped by that color's remaining pool) of the current
        /// color before moving to the next, so the sequence still interleaves colors but with more same-color
        /// runs than a strict one-at-a-time rotation.
        /// </summary>
        private static void AppendBatchColorSequence(
            List<VehiclePlacement> batch,
            System.Random random,
            List<PuzzleColor> colorSequence)
        {
            List<PuzzleColor> colors = new();
            Dictionary<PuzzleColor, int> remainingCapacityByColor = new();

            foreach (VehiclePlacement placement in batch)
            {
                VehicleConfig vehicleConfig = placement.VehicleConfig;
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


        // ===== Nested Types ===== //

        [Serializable]
        private sealed class BusArrowLevelNumberJson
        {
            public int level;
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
