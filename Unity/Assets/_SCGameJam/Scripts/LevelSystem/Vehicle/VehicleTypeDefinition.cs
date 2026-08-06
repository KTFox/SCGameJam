using UnityEngine;

namespace SCJam.LevelSystem
{
    /// <summary>
    /// Immutable reusable definition of a vehicle type.
    /// Logical footprint is authored explicitly and is never derived from mesh bounds.
    /// </summary>
    [CreateAssetMenu(
        fileName = "VehicleTypeDefinition",
        menuName = "SCJam/Level/Vehicle Type Definition")]
    public sealed class VehicleTypeDefinition : ScriptableObject
    {
        [SerializeField]
        [Tooltip("Stable identifier for this vehicle type.")]
        private string _typeId = "vehicle_type";

        [SerializeField]
        [Tooltip("Designer-facing display name.")]
        private string _displayName = "Vehicle";

        [SerializeField]
        [Tooltip("Optional prefab used by scene presentation systems.")]
        private GameObject _prefab;

        [SerializeField]
        [Min(1)]
        [Tooltip("Logical footprint width in cells (left to right).")]
        private int _footprintWidth = 1;

        [SerializeField]
        [Min(1)]
        [Tooltip("Logical footprint length in cells (front to back).")]
        private int _footprintLength = 2;

        [SerializeField]
        [Min(1)]
        [Tooltip("Maximum passenger seats for future boarding systems.")]
        private int _seatCapacity = 1;

        [SerializeField]
        [Tooltip("Optional local position offset applied when spawning the visual prefab.")]
        private Vector3 _visualLocalPositionOffset;

        [SerializeField]
        [Tooltip("Optional local euler offset applied when spawning the visual prefab.")]
        private Vector3 _visualLocalRotationOffset;

        /// <summary>
        /// Gets the stable type identifier.
        /// </summary>
        public string TypeId => _typeId;

        /// <summary>
        /// Gets the display name.
        /// </summary>
        public string DisplayName => _displayName;

        /// <summary>
        /// Gets the optional visual prefab reference.
        /// </summary>
        public GameObject Prefab => _prefab;

        /// <summary>
        /// Gets the logical footprint width in cells.
        /// </summary>
        public int FootprintWidth => _footprintWidth;

        /// <summary>
        /// Gets the logical footprint length in cells.
        /// </summary>
        public int FootprintLength => _footprintLength;

        /// <summary>
        /// Gets the seat capacity.
        /// </summary>
        public int SeatCapacity => _seatCapacity;

        /// <summary>
        /// Gets the optional local visual position offset.
        /// </summary>
        public Vector3 VisualLocalPositionOffset => _visualLocalPositionOffset;

        /// <summary>
        /// Gets the optional local visual rotation offset in euler degrees.
        /// </summary>
        public Vector3 VisualLocalRotationOffset => _visualLocalRotationOffset;

        /// <summary>
        /// Returns true when footprint and seat values are valid for gameplay.
        /// </summary>
        public bool HasValidGameplayMetrics =>
            _footprintWidth > 0 && _footprintLength > 0 && _seatCapacity > 0;

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (_footprintWidth < 1)
            {
                _footprintWidth = 1;
            }

            if (_footprintLength < 1)
            {
                _footprintLength = 1;
            }

            if (_seatCapacity < 1)
            {
                _seatCapacity = 1;
            }
        }
#endif
    }
}
