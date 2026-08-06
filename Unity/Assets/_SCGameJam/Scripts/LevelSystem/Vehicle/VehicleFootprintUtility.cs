using System.Collections.Generic;
using UnityEngine;

namespace SCJam.LevelSystem
{
    /// <summary>
    /// Authoritative utility for deriving occupied cells from vehicle footprint authoring data.
    /// <para>
    /// Anchor convention: the anchor cell is the <b>front-left</b> cell of the oriented footprint.
    /// Width extends to the vehicle's right. Length extends toward the vehicle's rear.
    /// </para>
    /// <para>
    /// ASCII diagrams (N = North / +Y). Anchor marked with A.
    /// </para>
    /// <code>
    /// Width = 2, Length = 3, Facing North:
    ///
    ///   y+1
    ///     A .     &lt;- front row (left = A)
    ///     . .
    ///     . .     &lt;- rear row
    ///   y-1   x+
    ///
    /// Facing East:
    ///
    ///         A . .   &lt;- front at right, left = top
    ///         . . .
    /// </code>
    /// </summary>
    public static class VehicleFootprintUtility
    {
        /// <summary>
        /// Returns the number of cells occupied by a rectangular footprint.
        /// </summary>
        /// <param name="width">Footprint width in cells (must be &gt; 0).</param>
        /// <param name="length">Footprint length in cells (must be &gt; 0).</param>
        /// <returns>Occupied cell count, or 0 when dimensions are invalid.</returns>
        public static int GetOccupiedCellCount(int width, int length)
        {
            if (width <= 0 || length <= 0)
            {
                return 0;
            }

            return width * length;
        }

        /// <summary>
        /// Calculates occupied cells for an oriented rectangular footprint.
        /// Clears <paramref name="results"/> before writing. The caller owns the list.
        /// </summary>
        /// <param name="anchor">Front-left cell of the oriented vehicle.</param>
        /// <param name="direction">Vehicle facing direction.</param>
        /// <param name="width">Cells across the vehicle (left to right).</param>
        /// <param name="length">Cells along the vehicle (front to back).</param>
        /// <param name="results">Destination list cleared and filled by this method.</param>
        public static void GetOccupiedCells(
            Vector2Int anchor,
            GridDirection direction,
            int width,
            int length,
            List<Vector2Int> results)
        {
            if (results == null)
            {
                return;
            }

            results.Clear();

            if (width <= 0 || length <= 0 || !GridDirectionUtility.IsValid(direction))
            {
                return;
            }

            // Right and rear unit steps derived from facing.
            Vector2Int forward = GridDirectionUtility.ToVector(direction);
            Vector2Int right = GridDirectionUtility.ToVector(GridDirectionUtility.RotateClockwise(direction));
            Vector2Int rear = -forward;

            for (int lengthIndex = 0; lengthIndex < length; lengthIndex++)
            {
                for (int widthIndex = 0; widthIndex < width; widthIndex++)
                {
                    Vector2Int cell = anchor + (right * widthIndex) + (rear * lengthIndex);
                    results.Add(cell);
                }
            }
        }

        /// <summary>
        /// Returns the continuous grid-space center of an oriented footprint.
        /// Cell centers are treated as integer grid coordinates.
        /// </summary>
        /// <param name="anchor">Front-left cell of the oriented vehicle.</param>
        /// <param name="direction">Vehicle facing direction.</param>
        /// <param name="width">Footprint width in cells.</param>
        /// <param name="length">Footprint length in cells.</param>
        /// <returns>Footprint center in grid space, or the anchor when dimensions are invalid.</returns>
        public static Vector2 GetFootprintCenterGrid(
            Vector2Int anchor,
            GridDirection direction,
            int width,
            int length)
        {
            if (width <= 0 || length <= 0 || !GridDirectionUtility.IsValid(direction))
            {
                return anchor;
            }

            Vector2Int forward = GridDirectionUtility.ToVector(direction);
            Vector2Int right = GridDirectionUtility.ToVector(GridDirectionUtility.RotateClockwise(direction));
            Vector2Int rear = -forward;

            Vector2 center = anchor;
            center += (Vector2)right * ((width - 1) * 0.5f);
            center += (Vector2)rear * ((length - 1) * 0.5f);
            return center;
        }

        /// <summary>
        /// Converts a footprint center from grid space into board-local position.
        /// </summary>
        /// <param name="anchor">Front-left cell of the oriented vehicle.</param>
        /// <param name="direction">Vehicle facing direction.</param>
        /// <param name="width">Footprint width in cells.</param>
        /// <param name="length">Footprint length in cells.</param>
        /// <param name="cellSize">World size of one cell along X/Z.</param>
        /// <returns>Board-local position at the footprint center.</returns>
        public static Vector3 GetFootprintCenterLocal(
            Vector2Int anchor,
            GridDirection direction,
            int width,
            int length,
            float cellSize)
        {
            Vector2 centerGrid = GetFootprintCenterGrid(anchor, direction, width, length);
            return GridCoordinateConverter.GridPointToLocalPosition(centerGrid, cellSize);
        }
    }
}
