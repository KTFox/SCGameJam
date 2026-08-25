using Cysharp.Threading.Tasks;

namespace SCJam.VehicleSystem
{
    /// <summary>
    /// Lets an external system (e.g. a UI hand simulator) delay a selected vehicle's move request until
    /// some presentation step finishes.
    /// </summary>
    public interface IVehicleSelectionDelay
    {
        UniTask WaitForSelectionDelayAsync(VehicleController vehicleController);
    }
}
