using UnityEngine;

namespace SCJam.LevelSystem
{
    /// <summary>
    /// Optional scene view binding for a vehicle runtime state.
    /// Runtime state does not depend on this component.
    /// </summary>
    public sealed class VehicleView : MonoBehaviour
    {
        private VehicleRuntimeState _runtimeState;

        /// <summary>
        /// Gets the bound runtime state, if any.
        /// </summary>
        public VehicleRuntimeState RuntimeState => _runtimeState;

        /// <summary>
        /// Binds this view to a runtime vehicle state.
        /// </summary>
        /// <param name="runtimeState">Runtime state to bind.</param>
        public void Bind(VehicleRuntimeState runtimeState)
        {
            _runtimeState = runtimeState;
        }

        /// <summary>
        /// Clears the runtime binding.
        /// </summary>
        public void Unbind()
        {
            _runtimeState = null;
        }

        /// <summary>
        /// Applies pose from the bound runtime state using board conversion and type offsets.
        /// </summary>
        /// <param name="converter">Board coordinate converter.</param>
        public void ApplyTransformFromRuntime(GridCoordinateConverter converter)
        {
            if (_runtimeState == null || converter == null)
            {
                return;
            }

            VehicleTypeDefinition vehicleType = _runtimeState.VehicleType;
            int width = _runtimeState.FootprintWidth;
            int length = _runtimeState.FootprintLength;

            Vector3 localCenter = VehicleFootprintUtility.GetFootprintCenterLocal(
                _runtimeState.AnchorCell,
                _runtimeState.Direction,
                width,
                length,
                converter.CellSize);

            Vector3 visualOffset = vehicleType != null
                ? vehicleType.VisualLocalPositionOffset
                : Vector3.zero;
            Vector3 rotationOffset = vehicleType != null
                ? vehicleType.VisualLocalRotationOffset
                : Vector3.zero;

            Transform boardRoot = converter.BoardRoot;
            if (boardRoot != null)
            {
                transform.SetParent(boardRoot, false);
            }

            transform.localPosition = localCenter + visualOffset;
            float yRotation = GridDirectionUtility.ToLocalYRotation(_runtimeState.Direction);
            transform.localRotation = Quaternion.Euler(rotationOffset + new Vector3(0f, yRotation, 0f));
        }
    }
}
