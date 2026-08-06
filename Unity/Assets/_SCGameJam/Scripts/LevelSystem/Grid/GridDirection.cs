namespace SCJam.LevelSystem
{
    /// <summary>
    /// Logical four-direction facing used by grid gameplay.
    /// Does not support diagonal directions.
    /// </summary>
    public enum GridDirection
    {
        /// <summary>Positive grid Y (Unity local +Z).</summary>
        North = 0,

        /// <summary>Positive grid X (Unity local +X).</summary>
        East = 1,

        /// <summary>Negative grid Y (Unity local -Z).</summary>
        South = 2,

        /// <summary>Negative grid X (Unity local -X).</summary>
        West = 3
    }
}
