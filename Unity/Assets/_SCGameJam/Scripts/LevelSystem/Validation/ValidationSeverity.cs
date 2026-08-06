namespace SCJam.LevelSystem
{
    /// <summary>
    /// Severity of a level validation issue.
    /// </summary>
    public enum ValidationSeverity
    {
        /// <summary>Informational note that does not block play.</summary>
        Info = 0,

        /// <summary>Suspicious data that should be reviewed.</summary>
        Warning = 1,

        /// <summary>Invalid data that blocks safe initialization.</summary>
        Error = 2
    }
}
