using Cysharp.Threading.Tasks;
using DG.Tweening;
using SCJam.BoardSystem;
using SCJam.Common;
using SCJam.WaitingAreaSystem;
using UnityEngine;

namespace SCJam.VehicleSystem
{
    public class VehicleController : MonoBehaviour
    {
        // ===== Serialized Fields ===== //

        [SerializeField] private float _moveSpeed;
        [SerializeField] private float _departDistance = 3f;


        // ===== Private Fields ===== //

        private Vehicle _vehicle;
        private BoardGrid _boardGrid;
        private BoardView _boardView;
        private VehicleMovementResolver _movementResolver;
        private WaitingAreaManager _waitingAreaManager;
        private WaitingAreaView _waitingAreaView;
        private WaitingSlot _reservedSlot;
        private bool _isMoving;


        // ===== Public Properties ===== //

        public Vehicle Vehicle => _vehicle;
        public bool IsMoving => _isMoving;


        // ===== Methods ===== //

        public void Initialize(
            Vehicle vehicle,
            BoardGrid boardGrid,
            BoardView boardView,
            VehicleMovementResolver movementResolver,
            WaitingAreaManager waitingAreaManager,
            WaitingAreaView waitingAreaView)
        {
            _vehicle = vehicle;
            _boardGrid = boardGrid;
            _boardView = boardView;
            _movementResolver = movementResolver;
            _waitingAreaManager = waitingAreaManager;
            _waitingAreaView = waitingAreaView;
        }

        public bool CanMove()
        {
            return !_isMoving && _vehicle.State == VehicleState.Parked && _movementResolver.IsPathClear(_vehicle);
        }

        public void RequestMove()
        {
            if (!CanMove())
                return;

            MoveToWaitingSlotRoutine().Forget();
        }

        public bool CanDepart()
        {
            return !_isMoving && _vehicle.State == VehicleState.Full;
        }

        public void RequestDepart()
        {
            if (!CanDepart())
                return;

            DepartRoutine().Forget();
        }

        private async UniTask MoveToWaitingSlotRoutine()
        {
            if (!_waitingAreaManager.TryReserveSlot(_vehicle.Id, out WaitingSlot reservedSlot))
                return;

            _reservedSlot = reservedSlot;
            _isMoving = true;
            _vehicle.ChangeState(VehicleState.MovingToExit);

            Vector3 exitPosition = ComputeExitWorldPosition();
            await transform.DOMove(exitPosition, ComputeDuration(exitPosition)).SetEase(Ease.Linear).ToUniTask();

            // Footprint-clear rule: the board cells stay occupied until the vehicle has fully left them.
            _boardGrid.RemoveVehicle(_vehicle.Id);
            _vehicle.ClearFootprint();

            Vector3 slotPosition = _waitingAreaView.GetSlotWorldPosition(reservedSlot.Index);
            await transform.DOMove(slotPosition, ComputeDuration(slotPosition)).SetEase(Ease.Linear).ToUniTask();

            _waitingAreaManager.ConfirmOccupied(reservedSlot);
            _vehicle.ChangeState(VehicleState.Waiting);
            _isMoving = false;
        }

        private async UniTask DepartRoutine()
        {
            _isMoving = true;
            _vehicle.ChangeState(VehicleState.Departing);

            Vector3 direction = GetWorldDirectionVector(_vehicle.MovementDirection);
            Vector3 departPosition = transform.position + direction * (_departDistance * _boardView.CellSize);
            await transform.DOMove(departPosition, ComputeDuration(departPosition)).SetEase(Ease.Linear).ToUniTask();

            // Waiting-slot release timing follows the same footprint-clear rule as the board: released
            // only once the vehicle has fully left the slot, not when departure starts.
            _waitingAreaManager.ReleaseSlot(_reservedSlot);
            _vehicle.ChangeState(VehicleState.Completed);
            _isMoving = false;
            gameObject.SetActive(false);
        }

        private Vector3 ComputeExitWorldPosition()
        {
            int exitCellDistance = _movementResolver.GetExitCellDistance(_vehicle);
            Vector3 direction = GetWorldDirectionVector(_vehicle.MovementDirection);
            return transform.position + direction * (exitCellDistance * _boardView.CellSize);
        }

        private float ComputeDuration(Vector3 targetPosition)
        {
            if (_moveSpeed <= 0f)
                return 0f;

            return Vector3.Distance(transform.position, targetPosition) / _moveSpeed;
        }

        private Vector3 GetWorldDirectionVector(GridDirection direction)
        {
            Vector3 localDirection = direction switch
            {
                GridDirection.Up => Vector3.forward,
                GridDirection.Down => Vector3.back,
                GridDirection.Left => Vector3.left,
                GridDirection.Right => Vector3.right,
                _ => Vector3.zero
            };

            return _boardView.GridOrigin.rotation * localDirection;
        }
    }
}
