using System.Collections.Generic;
using UnityEngine;

namespace SCJam.LevelSystem
{
    /// <summary>
    /// Optional gizmo visualization for board cells, vehicle footprints, anchors, and directions.
    /// Does not participate in gameplay logic.
    /// </summary>
    public sealed class LevelBoardDebugView : MonoBehaviour
    {
        [SerializeField]
        private BoardGridTransform _boardGridTransform;

        [SerializeField]
        private LevelRuntimeController _runtimeController;

        [SerializeField]
        private LevelDefinition _levelDefinitionOverride;

        [SerializeField]
        private bool _drawGrid = true;

        [SerializeField]
        private bool _drawCellCenters;

        [SerializeField]
        private bool _drawVehicleFootprints = true;

        [SerializeField]
        private bool _drawAnchors = true;

        [SerializeField]
        private bool _drawDirections = true;

        [SerializeField]
        private Color _gridColor = new Color(1f, 1f, 1f, 0.25f);

        [SerializeField]
        private Color _footprintColor = new Color(0.2f, 0.7f, 1f, 0.35f);

        [SerializeField]
        private Color _anchorColor = new Color(1f, 0.85f, 0.2f, 0.8f);

        [SerializeField]
        private Color _directionColor = new Color(0.2f, 1f, 0.4f, 0.9f);

        [SerializeField]
        private Color _invalidColor = new Color(1f, 0.2f, 0.2f, 0.55f);

        private readonly List<Vector2Int> _footprintBuffer = new List<Vector2Int>(16);
        private readonly HashSet<Vector2Int> _occupiedOnce = new HashSet<Vector2Int>();
        private readonly HashSet<Vector2Int> _overlapCells = new HashSet<Vector2Int>();

        private void Reset()
        {
            _boardGridTransform = GetComponent<BoardGridTransform>();
            _runtimeController = GetComponent<LevelRuntimeController>();
        }

        private void OnDrawGizmos()
        {
            BoardGridTransform board = ResolveBoard();
            if (board == null)
            {
                return;
            }

            LevelDefinition definition = ResolveDefinition();
            int width = definition != null ? definition.GridWidth : board.GridWidth;
            int height = definition != null ? definition.GridHeight : board.GridHeight;
            float cellSize = definition != null ? definition.CellSize : board.CellSize;
            Transform root = board.BoardRoot;
            if (root == null || width <= 0 || height <= 0 || cellSize <= 0f)
            {
                return;
            }

            Matrix4x4 previousMatrix = Gizmos.matrix;
            Gizmos.matrix = root.localToWorldMatrix;

            if (_drawGrid)
            {
                DrawGrid(width, height, cellSize);
            }

            if (_drawCellCenters)
            {
                DrawCellCenters(width, height, cellSize);
            }

            if (_drawVehicleFootprints || _drawAnchors || _drawDirections)
            {
                DrawVehicles(definition, cellSize);
            }

            Gizmos.matrix = previousMatrix;
        }

        private BoardGridTransform ResolveBoard()
        {
            if (_boardGridTransform != null)
            {
                return _boardGridTransform;
            }

            return GetComponent<BoardGridTransform>();
        }

        private LevelDefinition ResolveDefinition()
        {
            if (_levelDefinitionOverride != null)
            {
                return _levelDefinitionOverride;
            }

            if (_runtimeController != null && _runtimeController.LevelDefinition != null)
            {
                return _runtimeController.LevelDefinition;
            }

            BoardGridTransform board = ResolveBoard();
            if (board != null && board.LevelDefinition != null)
            {
                return board.LevelDefinition;
            }

            return null;
        }

        private void DrawGrid(int width, int height, float cellSize)
        {
            Gizmos.color = _gridColor;
            float half = cellSize * 0.5f;
            float minX = -half;
            float maxX = (width - 1) * cellSize + half;
            float minZ = -half;
            float maxZ = (height - 1) * cellSize + half;

            for (int x = 0; x <= width; x++)
            {
                float worldX = x * cellSize - half;
                Gizmos.DrawLine(new Vector3(worldX, 0f, minZ), new Vector3(worldX, 0f, maxZ));
            }

            for (int y = 0; y <= height; y++)
            {
                float worldZ = y * cellSize - half;
                Gizmos.DrawLine(new Vector3(minX, 0f, worldZ), new Vector3(maxX, 0f, worldZ));
            }
        }

        private void DrawCellCenters(int width, int height, float cellSize)
        {
            Gizmos.color = _gridColor;
            float size = cellSize * 0.1f;
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    Vector3 center = new Vector3(x * cellSize, 0f, y * cellSize);
                    Gizmos.DrawWireSphere(center, size);
                }
            }
        }

        private void DrawVehicles(LevelDefinition definition, float cellSize)
        {
            LevelRuntimeState runtime = _runtimeController != null ? _runtimeController.RuntimeState : null;
            if (runtime != null && runtime.IsInitialized)
            {
                DrawRuntimeVehicles(runtime, cellSize);
                return;
            }

            if (definition == null)
            {
                return;
            }

            DrawAuthoredVehicles(definition, cellSize);
        }

        private void DrawRuntimeVehicles(LevelRuntimeState runtime, float cellSize)
        {
            IReadOnlyList<VehicleRuntimeState> vehicles = runtime.Vehicles;
            for (int i = 0; i < vehicles.Count; i++)
            {
                VehicleRuntimeState vehicle = vehicles[i];
                DrawVehicleVisual(
                    vehicle.OccupiedCells,
                    vehicle.AnchorCell,
                    vehicle.Direction,
                    cellSize,
                    isInvalid: false);
            }
        }

        private void DrawAuthoredVehicles(LevelDefinition definition, float cellSize)
        {
            _occupiedOnce.Clear();
            _overlapCells.Clear();

            IReadOnlyList<VehiclePlacementDefinition> placements = definition.VehiclePlacements;
            if (placements == null)
            {
                return;
            }

            for (int i = 0; i < placements.Count; i++)
            {
                VehiclePlacementDefinition placement = placements[i];
                if (placement == null || placement.VehicleType == null)
                {
                    continue;
                }

                VehicleFootprintUtility.GetOccupiedCells(
                    placement.AnchorCell,
                    placement.Direction,
                    placement.VehicleType.FootprintWidth,
                    placement.VehicleType.FootprintLength,
                    _footprintBuffer);

                bool isInvalid = false;
                for (int c = 0; c < _footprintBuffer.Count; c++)
                {
                    Vector2Int cell = _footprintBuffer[c];
                    bool outside = cell.x < 0
                        || cell.y < 0
                        || cell.x >= definition.GridWidth
                        || cell.y >= definition.GridHeight;

                    if (outside)
                    {
                        isInvalid = true;
                    }

                    if (!_occupiedOnce.Add(cell))
                    {
                        _overlapCells.Add(cell);
                        isInvalid = true;
                    }
                }

                DrawVehicleVisual(
                    _footprintBuffer,
                    placement.AnchorCell,
                    placement.Direction,
                    cellSize,
                    isInvalid);
            }

            Gizmos.color = _invalidColor;
            foreach (Vector2Int overlap in _overlapCells)
            {
                DrawCellCube(overlap, cellSize, true);
            }
        }

        private void DrawVehicleVisual(
            IReadOnlyList<Vector2Int> cells,
            Vector2Int anchor,
            GridDirection direction,
            float cellSize,
            bool isInvalid)
        {
            if (_drawVehicleFootprints && cells != null)
            {
                Gizmos.color = isInvalid ? _invalidColor : _footprintColor;
                for (int i = 0; i < cells.Count; i++)
                {
                    DrawCellCube(cells[i], cellSize, solid: true);
                }
            }

            if (_drawAnchors)
            {
                Gizmos.color = _anchorColor;
                DrawCellCube(anchor, cellSize, solid: false);
            }

            if (_drawDirections)
            {
                Gizmos.color = _directionColor;
                Vector3 start = new Vector3(anchor.x * cellSize, 0.05f, anchor.y * cellSize);
                Vector2Int step = GridDirectionUtility.ToVector(direction);
                Vector3 end = start + new Vector3(step.x, 0f, step.y) * cellSize * 0.75f;
                Gizmos.DrawLine(start, end);
                Gizmos.DrawSphere(end, cellSize * 0.08f);
            }
        }

        private static void DrawCellCube(Vector2Int cell, float cellSize, bool solid)
        {
            Vector3 center = new Vector3(cell.x * cellSize, 0.01f, cell.y * cellSize);
            Vector3 size = new Vector3(cellSize * 0.9f, 0.02f, cellSize * 0.9f);
            if (solid)
            {
                Gizmos.DrawCube(center, size);
            }
            else
            {
                Gizmos.DrawWireCube(center, size);
            }
        }
    }
}
