using System;
using System.Threading;
using Cysharp.Threading.Tasks;
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
        [SerializeField] private MonoBehaviour _selectionDelaySource;


        // ===== Private Fields ===== //

        private IVehicleSelectionDelay _selectionDelay;
        private CancellationToken _destroyCancellationToken;


        // ===== Public Properties ===== //

        /// <summary>
        /// When true, a tap still resolves the vehicle under the pointer and raises <see cref="VehicleSelected"/>,
        /// but the controller does not drive that vehicle's normal move request. Used while a booster owns the
        /// next vehicle tap (e.g. teleport-to-waiting-slot) and handles the vehicle itself.
        /// </summary>
        public bool SuppressAutoMove { get; set; }


        // ===== Events ===== //

        public event Action<VehicleController> VehicleSelected;


        // ===== Methods ===== //

        private void Awake()
        {
            _destroyCancellationToken = this.GetCancellationTokenOnDestroy();

            if (_selectionDelaySource == null)
                return;

            if (_selectionDelaySource is IVehicleSelectionDelay selectionDelay)
                _selectionDelay = selectionDelay;
            else
                Debug.LogError($"[{nameof(VehicleSelectionController)}] {nameof(_selectionDelaySource)} must implement {nameof(IVehicleSelectionDelay)}.", this);
        }

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

                if (!SuppressAutoMove)
                    RequestMoveRoutine(vehicleController).Forget(HandleRoutineException);
            }
        }

        private async UniTask RequestMoveRoutine(VehicleController vehicleController)
        {
            if (_selectionDelay != null)
                await _selectionDelay.WaitForSelectionDelayAsync(vehicleController).AttachExternalCancellation(_destroyCancellationToken);

            vehicleController.RequestMove();
        }

        private static void HandleRoutineException(Exception exception)
        {
            if (exception is not OperationCanceledException)
                Debug.LogException(exception);
        }
    }
}
