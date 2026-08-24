using System;
using SCJam.InputSystem;
using SCJam.CameraSystem;
using UnityEngine;

namespace SCJam.VehicleSystem
{
    public class VehicleSelectionController : MonoBehaviour
    {
        // ===== Serialized Fields ===== //

        [SerializeField] private LayerMask _vehicleLayerMask = ~0;
        [SerializeField] private float _maxRayDistance = 100f;


        // ===== Events ===== //

        public event Action<VehicleController> VehicleSelected;


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
            Ray ray = CameraController.Instance.MainCamera.ScreenPointToRay(screenPosition);
            if (!Physics.Raycast(ray, out RaycastHit hit, _maxRayDistance, _vehicleLayerMask))
                return;

            if (hit.collider.TryGetComponent(out VehicleController vehicleController))
            {
                VehicleSelected?.Invoke(vehicleController);
                vehicleController.RequestMove();
            }
        }
    }
}
