namespace SCJam.LevelSystem
{
    /// <summary>
    /// Lifecycle of an active <see cref="LevelRuntimeState"/> session.
    /// </summary>
    public enum LevelLifecycleState
    {
        /// <summary>No session is active.</summary>
        None = 0,

        /// <summary>Runtime state is being built from a level definition.</summary>
        Initializing = 1,

        /// <summary>Level session is active and ready for gameplay systems.</summary>
        Playing = 2,

        /// <summary>Level completed successfully (future use).</summary>
        Completed = 3,

        /// <summary>Level failed (future use).</summary>
        Failed = 4,

        /// <summary>Runtime state has been disposed and must not be reused.</summary>
        Disposed = 5
    }
}
