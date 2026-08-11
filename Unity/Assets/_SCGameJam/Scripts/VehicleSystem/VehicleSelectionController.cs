using SCJam.InputSystem;
using UnityEngine;

namespace SCJam.VehicleSystem
{
    public class VehicleSelectionController : MonoBehaviour
    {
        // ===== Serialized Fields ===== //

        [SerializeField] private Camera _camera;
        [SerializeField] private LayerMask _vehicleLayerMask = ~0;
        [SerializeField] private float _maxRayDistance = 100f;


        // ===== Methods ===== //

        private void OnEnable()
        {
            InputManager.OnTouchPerformed += HandleTouchPerformed;
        }

        private void OnDisable()
        {
            InputManager.OnTouchPerformed -= HandleTouchPerformed;
        }

        private void HandleTouchPerformed(Vector2 screenPosition)
        {
            if (_camera == null)
                return;

            Ray ray = _camera.ScreenPointToRay(screenPosition);
            if (!Physics.Raycast(ray, out RaycastHit hit, _maxRayDistance, _vehicleLayerMask))
                return;

            if (hit.collider.TryGetComponent(out VehicleController vehicleController))
            {
                vehicleController.RequestMove();
            }
        }
    }
}
