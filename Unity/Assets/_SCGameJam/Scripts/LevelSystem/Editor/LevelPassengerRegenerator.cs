using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace SCJam.LevelSystem.Editor
{
    /// <summary>
    /// Batch-runs the passenger half of the JSON import pipeline over every LevelConfig asset under
    /// LEVEL_ASSET_FOLDER without touching board size or vehicle placements: for each level it re-syncs
    /// the passenger prefab mappings to the colors its current vehicle placements use
    /// (LevelJsonImporter.InitPassengerPrefabMappings), fills any still-unassigned prefab from a matching
    /// Passenger_{Color} prefab (LevelJsonImporter.AssignPassengerPrefabsByColorName), then regenerates the
    /// passenger color sequence from a solvable vehicle release order
    /// (LevelJsonImporter.GenerateColorSequence). Use this after editing vehicle placements by hand (e.g. a
    /// bulk color swap) so the passenger data stays consistent with the vehicles on the board.
    /// </summary>
    internal static class LevelPassengerRegenerator
    {
        // ===== Constants ===== //

        private const string MENU_PATH = "SCJam/Regenerate All Levels (Passengers + Color Sequence)";
        private const string LEVEL_ASSET_FOLDER = "Assets/_SCGameJam/ScriptableObjects/Levels";


        // ===== Methods ===== //

        [MenuItem(MENU_PATH)]
        private static void RegenerateAll()
        {
            if (!AssetDatabase.IsValidFolder(LEVEL_ASSET_FOLDER))
            {
                Debug.LogError($"Regenerate All Levels: folder {LEVEL_ASSET_FOLDER} does not exist.");
                return;
            }

            List<LevelConfig> levelConfigs = AssetDatabase
                .FindAssets("t:LevelConfig", new[] { LEVEL_ASSET_FOLDER })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<LevelConfig>)
                .Where(levelConfig => levelConfig != null)
                .OrderBy(levelConfig => levelConfig.name, System.StringComparer.Ordinal)
                .ToList();

            if (levelConfigs.Count == 0)
            {
                Debug.LogWarning($"Regenerate All Levels: no LevelConfig assets found under {LEVEL_ASSET_FOLDER}.");
                return;
            }

            if (!EditorUtility.DisplayDialog(
                    "Regenerate All Levels",
                    $"Re-sync passenger prefab mappings and regenerate the passenger color sequence for " +
                    $"{levelConfigs.Count} level(s) under {LEVEL_ASSET_FOLDER}?\n\n" +
                    "Board size and vehicle placements are left untouched. The color sequence uses a " +
                    "non-seeded RNG, so it will differ from run to run.",
                    "Regenerate",
                    "Cancel"))
            {
                return;
            }

            int successCount = 0;
            int failureCount = 0;

            try
            {
                for (int i = 0; i < levelConfigs.Count; i++)
                {
                    LevelConfig levelConfig = levelConfigs[i];
                    EditorUtility.DisplayProgressBar(
                        "Regenerate All Levels",
                        $"{levelConfig.name} ({i + 1}/{levelConfigs.Count})",
                        (float)i / levelConfigs.Count);

                    if (TryRegenerate(levelConfig))
                        successCount++;
                    else
                        failureCount++;
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                AssetDatabase.SaveAssets();
            }

            Debug.Log($"Regenerate All Levels finished: {successCount} succeeded, {failureCount} failed (out of {levelConfigs.Count}).");
        }

        /// <summary>
        /// Runs the three passenger-side importer steps against one level. Returns false (with the importer's
        /// own error already logged) when the level has no valid vehicle placements or no solvable release
        /// order, in which case its color sequence is left unchanged.
        /// </summary>
        private static bool TryRegenerate(LevelConfig levelConfig)
        {
            List<VehiclePlacement> placements = LevelJsonImporter.GetValidVehiclePlacements(levelConfig);
            if (placements.Count == 0)
            {
                Debug.LogError($"Level '{levelConfig.name}': no valid vehicle placements; skipped.", levelConfig);
                return false;
            }

            SerializedObject serializedObject = new(levelConfig);

            LevelJsonImporter.InitPassengerPrefabMappings(serializedObject);
            serializedObject.Update();

            LevelJsonImporter.AssignPassengerPrefabsByColorName(serializedObject);
            serializedObject.Update();

            LevelJsonImporter.GenerateColorSequence(serializedObject);

            EditorUtility.SetDirty(levelConfig);
            return true;
        }
    }
}
