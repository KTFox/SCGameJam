using System.Collections.Generic;
using UnityEngine;

namespace SCJam.LevelSystem
{
    /// <summary>
    /// Validates authored <see cref="LevelSO"/> assets without entering play mode.
    /// Reuses footprint and occupancy rules used at runtime. Does not mutate the level.
    /// </summary>
    public static class LevelDefinitionValidator
    {
        /// <summary>
        /// Validates the provided level definition.
        /// </summary>
        /// <param name="definition">Level definition to validate.</param>
        /// <returns>Validation result containing all issues found.</returns>
        public static LevelValidationResult Validate(LevelSO definition)
        {
            var issues = new List<LevelValidationIssue>();

            if (definition == null)
            {
                issues.Add(new LevelValidationIssue(
                    ValidationSeverity.Error,
                    "Level definition is null."));
                return new LevelValidationResult(issues);
            }

            var footprintBuffer = new List<Vector2Int>(32);
            ValidateGridMetrics(definition, issues);
            ValidateVehicles(definition, footprintBuffer, issues);

            return new LevelValidationResult(issues);
        }

        /// <summary>
        /// Validates and logs issues for the provided level definition.
        /// </summary>
        /// <param name="definition">Level definition to validate.</param>
        /// <returns>Validation result.</returns>
        public static LevelValidationResult ValidateAndLog(LevelSO definition)
        {
            LevelValidationResult result = Validate(definition);
            LogIssues(definition, result);
            return result;
        }

        /// <summary>
        /// Logs validation issues in a designer-friendly format.
        /// </summary>
        /// <param name="definition">Validated level definition.</param>
        /// <param name="result">Validation result.</param>
        public static void LogIssues(LevelSO definition, LevelValidationResult result)
        {
            if (result == null)
            {
                return;
            }

            string levelLabel = definition != null
                ? (string.IsNullOrEmpty(definition.LevelId) ? definition.name : definition.LevelId)
                : "<null>";

            if (result.IssueCount == 0)
            {
                Debug.Log($"[LevelValidation] '{levelLabel}' is valid.", definition);
                return;
            }

            for (int i = 0; i < result.Issues.Count; i++)
            {
                LevelValidationIssue issue = result.Issues[i];
                Object context = issue.Context != null ? issue.Context : definition;
                string message = $"[LevelValidation] '{levelLabel}': {issue}";

                switch (issue.Severity)
                {
                    case ValidationSeverity.Error:
                        Debug.LogError(message, context);
                        break;
                    case ValidationSeverity.Warning:
                        Debug.LogWarning(message, context);
                        break;
                    default:
                        Debug.Log(message, context);
                        break;
                }
            }
        }

        private static void ValidateGridMetrics(LevelSO definition, List<LevelValidationIssue> issues)
        {
            if (definition.GridWidth <= 0)
            {
                issues.Add(new LevelValidationIssue(
                    ValidationSeverity.Error,
                    $"Grid width must be greater than zero. Current={definition.GridWidth}.",
                    context: definition));
            }

            if (definition.GridHeight <= 0)
            {
                issues.Add(new LevelValidationIssue(
                    ValidationSeverity.Error,
                    $"Grid height must be greater than zero. Current={definition.GridHeight}.",
                    context: definition));
            }

            if (definition.CellSize <= 0f)
            {
                issues.Add(new LevelValidationIssue(
                    ValidationSeverity.Error,
                    $"Cell size must be greater than zero. Current={definition.CellSize}.",
                    context: definition));
            }
        }

        private static void ValidateVehicles(
            LevelSO definition,
            List<Vector2Int> footprintBuffer,
            List<LevelValidationIssue> issues)
        {
            IReadOnlyList<VehiclePlacementDefinition> placements = definition.VehiclePlacements;
            if (placements == null || placements.Count == 0)
            {
                issues.Add(new LevelValidationIssue(
                    ValidationSeverity.Error,
                    "Level has no vehicle placements.",
                    context: definition));
                return;
            }

            bool canBuildOccupancy = definition.GridWidth > 0 && definition.GridHeight > 0;
            VehicleOccupancyGrid occupancy = canBuildOccupancy
                ? new VehicleOccupancyGrid(definition.GridWidth, definition.GridHeight)
                : null;

            var seenIds = new HashSet<string>();

            for (int i = 0; i < placements.Count; i++)
            {
                VehiclePlacementDefinition placement = placements[i];
                if (placement == null)
                {
                    issues.Add(new LevelValidationIssue(
                        ValidationSeverity.Error,
                        $"Vehicle placement at index {i} is null.",
                        context: definition));
                    continue;
                }

                ValidatePlacement(definition, placement, seenIds, occupancy, footprintBuffer, issues);
            }
        }

        private static void ValidatePlacement(
            LevelSO definition,
            VehiclePlacementDefinition placement,
            HashSet<string> seenIds,
            VehicleOccupancyGrid occupancy,
            List<Vector2Int> footprintBuffer,
            List<LevelValidationIssue> issues)
        {
            string vehicleId = placement.VehicleId;
            if (string.IsNullOrWhiteSpace(vehicleId))
            {
                issues.Add(new LevelValidationIssue(
                    ValidationSeverity.Error,
                    "Vehicle ID is empty.",
                    vehicleId: vehicleId,
                    context: definition));
            }
            else if (!seenIds.Add(vehicleId))
            {
                issues.Add(new LevelValidationIssue(
                    ValidationSeverity.Error,
                    $"Duplicate vehicle ID '{vehicleId}'.",
                    vehicleId: vehicleId,
                    context: definition));
            }

            VehicleTypeDefinition vehicleType = placement.VehicleType;
            if (vehicleType == null)
            {
                issues.Add(new LevelValidationIssue(
                    ValidationSeverity.Error,
                    "Vehicle type reference is null.",
                    vehicleId: vehicleId,
                    context: definition));
                return;
            }

            if (vehicleType.FootprintWidth <= 0 || vehicleType.FootprintLength <= 0)
            {
                issues.Add(new LevelValidationIssue(
                    ValidationSeverity.Error,
                    $"Invalid footprint dimensions width={vehicleType.FootprintWidth}, length={vehicleType.FootprintLength}.",
                    vehicleId: vehicleId,
                    context: vehicleType));
            }

            if (vehicleType.SeatCapacity <= 0)
            {
                issues.Add(new LevelValidationIssue(
                    ValidationSeverity.Error,
                    $"Seat capacity must be greater than zero. Current={vehicleType.SeatCapacity}.",
                    vehicleId: vehicleId,
                    context: vehicleType));
            }

            if (!GridDirectionUtility.IsValid(placement.Direction))
            {
                issues.Add(new LevelValidationIssue(
                    ValidationSeverity.Error,
                    $"Invalid grid direction '{placement.Direction}'.",
                    vehicleId: vehicleId,
                    context: definition));
            }

            if (vehicleType.FootprintWidth <= 0 || vehicleType.FootprintLength <= 0)
            {
                return;
            }

            VehicleFootprintUtility.GetOccupiedCells(
                placement.AnchorCell,
                placement.Direction,
                vehicleType.FootprintWidth,
                vehicleType.FootprintLength,
                footprintBuffer);

            if (footprintBuffer.Count == 0)
            {
                issues.Add(new LevelValidationIssue(
                    ValidationSeverity.Error,
                    "Vehicle footprint is empty.",
                    vehicleId: vehicleId,
                    context: definition));
                return;
            }

            bool isOutOfBounds = false;
            for (int i = 0; i < footprintBuffer.Count; i++)
            {
                Vector2Int cell = footprintBuffer[i];
                bool inside = cell.x >= 0
                    && cell.y >= 0
                    && cell.x < definition.GridWidth
                    && cell.y < definition.GridHeight;

                if (!inside)
                {
                    isOutOfBounds = true;
                    issues.Add(new LevelValidationIssue(
                        ValidationSeverity.Error,
                        "Vehicle footprint cell is outside the board.",
                        vehicleId: vehicleId,
                        cell: cell,
                        context: definition));
                }
            }

            if (isOutOfBounds || occupancy == null)
            {
                return;
            }

            var temporaryVehicle = new VehicleRuntimeState(placement);
            if (!occupancy.TryRegister(temporaryVehicle))
            {
                issues.Add(new LevelValidationIssue(
                    ValidationSeverity.Error,
                    "Vehicle footprint overlaps another vehicle.",
                    vehicleId: vehicleId,
                    cell: placement.AnchorCell,
                    context: definition));
            }
        }
    }
}
