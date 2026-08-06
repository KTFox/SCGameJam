using UnityEngine;

namespace SCJam.LevelSystem
{
    /// <summary>
    /// Pure helpers for converting and rotating <see cref="GridDirection"/> values.
    /// </summary>
    public static class GridDirectionUtility
    {
        /// <summary>
        /// Converts a logical direction into a unit grid step.
        /// </summary>
        /// <param name="direction">Direction to convert.</param>
        /// <returns>Unit step in grid coordinates.</returns>
        public static Vector2Int ToVector(GridDirection direction)
        {
            switch (direction)
            {
                case GridDirection.North:
                    return new Vector2Int(0, 1);
                case GridDirection.East:
                    return new Vector2Int(1, 0);
                case GridDirection.South:
                    return new Vector2Int(0, -1);
                case GridDirection.West:
                    return new Vector2Int(-1, 0);
                default:
                    return Vector2Int.zero;
            }
        }

        /// <summary>
        /// Returns the opposite facing of the given direction.
        /// </summary>
        /// <param name="direction">Source direction.</param>
        /// <returns>Opposite direction.</returns>
        public static GridDirection GetOpposite(GridDirection direction)
        {
            return (GridDirection)(((int)direction + 2) % 4);
        }

        /// <summary>
        /// Rotates the direction 90 degrees clockwise.
        /// </summary>
        /// <param name="direction">Source direction.</param>
        /// <returns>Clockwise-rotated direction.</returns>
        public static GridDirection RotateClockwise(GridDirection direction)
        {
            return (GridDirection)(((int)direction + 1) % 4);
        }

        /// <summary>
        /// Rotates the direction 90 degrees counterclockwise.
        /// </summary>
        /// <param name="direction">Source direction.</param>
        /// <returns>Counterclockwise-rotated direction.</returns>
        public static GridDirection RotateCounterClockwise(GridDirection direction)
        {
            return (GridDirection)(((int)direction + 3) % 4);
        }

        /// <summary>
        /// Converts a logical direction into a local Euler Y rotation in degrees.
        /// North faces Unity local +Z.
        /// </summary>
        /// <param name="direction">Direction to convert.</param>
        /// <returns>Local Y euler angle in degrees.</returns>
        public static float ToLocalYRotation(GridDirection direction)
        {
            switch (direction)
            {
                case GridDirection.North:
                    return 0f;
                case GridDirection.East:
                    return 90f;
                case GridDirection.South:
                    return 180f;
                case GridDirection.West:
                    return 270f;
                default:
                    return 0f;
            }
        }

        /// <summary>
        /// Returns true when the direction is East or West.
        /// </summary>
        /// <param name="direction">Direction to inspect.</param>
        /// <returns>True if horizontal.</returns>
        public static bool IsHorizontal(GridDirection direction)
        {
            return direction == GridDirection.East || direction == GridDirection.West;
        }

        /// <summary>
        /// Returns true when the direction is North or South.
        /// </summary>
        /// <param name="direction">Direction to inspect.</param>
        /// <returns>True if vertical.</returns>
        public static bool IsVertical(GridDirection direction)
        {
            return direction == GridDirection.North || direction == GridDirection.South;
        }

        /// <summary>
        /// Returns true when the value is one of the four supported directions.
        /// </summary>
        /// <param name="direction">Direction to validate.</param>
        /// <returns>True if the enum value is valid.</returns>
        public static bool IsValid(GridDirection direction)
        {
            int value = (int)direction;
            return value >= 0 && value <= 3;
        }
    }
}
