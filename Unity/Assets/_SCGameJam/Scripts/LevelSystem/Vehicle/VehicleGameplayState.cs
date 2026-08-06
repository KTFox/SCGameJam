namespace SCJam.LevelSystem
{
    /// <summary>
    /// Logical vehicle lifecycle states prepared for future gameplay systems.
    /// Vehicles initialize as <see cref="OnBoard"/> in this foundation task.
    /// </summary>
    public enum VehicleGameplayState
    {
        /// <summary>Vehicle is present and interactive on the board.</summary>
        OnBoard = 0,

        /// <summary>Reserved for future systems.</summary>
        Reserved = 1,

        /// <summary>Vehicle is leaving the board along an escape path.</summary>
        Escaping = 2,

        /// <summary>Vehicle is moving toward a waiting slot.</summary>
        DrivingToSlot = 3,

        /// <summary>Vehicle is parked in a waiting slot.</summary>
        Waiting = 4,

        /// <summary>Passengers are boarding the vehicle.</summary>
        Boarding = 5,

        /// <summary>Vehicle is departing after boarding.</summary>
        Departing = 6,

        /// <summary>Vehicle has finished its role in the level.</summary>
        Completed = 7
    }
}
