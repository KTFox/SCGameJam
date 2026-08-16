using SCJam.Common;
using UnityEngine;

namespace SCJam.VehicleSystem
{
    /// <summary>
    /// Defines a vehicle in its canonical Up orientation. The prefab root must be positioned at the
    /// horizontal center of the vehicle footprint with its pivot at ground level. Its local +Z axis must
    /// point toward the front of the vehicle, and its root rotation and scale should be identity and one.
    /// If the imported model does not follow this convention, place it below a normalized prefab root and
    /// adjust only the visual child's local transform.
    /// </summary>
    [CreateAssetMenu(fileName = "Vehicle_", menuName = "SCJam/Vehicle Config")]
    public class VehicleConfig : ScriptableObject
    {
        [SerializeField] private PuzzleColor _color;
        [SerializeField] private int _capacity = 4;

        [SerializeField, Tooltip("Grid footprint while the vehicle faces Up (+Z). X is width and Y is length.")]
        private Vector2Int _footprintSize = new(1, 2);

        [SerializeField, Tooltip("Prefab root pivot must be at the footprint center on ground level, face local +Z, use rotation (0,0,0), and scale (1,1,1).")]
        private VehicleController _vehicleModel;


        public PuzzleColor Color => _color;
        public int Capacity => _capacity;
        public Vector2Int FootprintSize => _footprintSize;
        public VehicleController Prefab => _vehicleModel;


        // ===== Methods ===== //

        private void OnValidate()
        {
            _vehicleModel?.EnsureSeatAnchorCount(_capacity);
        }
    }
}
