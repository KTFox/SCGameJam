using System.Collections.Generic;
using SCJam.Common;
using SCJam.VehicleSystem;

namespace SCJam.WaitingAreaSystem
{
    public interface IWaitingVehicleSelector
    {
        /// <summary>
        /// Picks the best waiting vehicle of the given color from candidates, or null if none match.
        /// </summary>
        Vehicle SelectVehicle(IReadOnlyList<WaitingVehicleEntry> waitingVehicles, PuzzleColor color);
    }
}
