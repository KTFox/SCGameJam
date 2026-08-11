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


        // ===== Private Fields ===== //

        private Vehicle _vehicle;
        private BoardGrid _boardGrid;
        private BoardView _boardView;
        private VehicleMovementResolver _movementResolver;
        private WaitingAreaManager _waitingAreaManager;
        private WaitingAreaView _waitingAreaView;
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

        private async UniTaskVoid MoveToWaitingSlotRoutine()
        {
            if (!_waitingAreaManager.TryReserveSlot(_vehicle.Id, out WaitingSlot reservedSlot))
                return;

            _isMoving = true;
            _vehicle.ChangeState(VehicleState.MovingToExit);

            Vector3 exitPosition = ComputeExitWorldPosition();
            await AwaitTween(transform.DOMove(exitPosition, ComputeDuration(exitPosition)).SetEase(Ease.Linear));

            // Footprint-clear rule: the board cells stay occupied until the vehicle has fully left them.
            _boardGrid.RemoveVehicle(_vehicle.Id);
            _vehicle.ClearFootprint();

            Vector3 slotPosition = _waitingAreaView.GetSlotWorldPosition(reservedSlot.Index);
            await AwaitTween(transform.DOMove(slotPosition, ComputeDuration(slotPosition)).SetEase(Ease.Linear));

            _waitingAreaManager.ConfirmOccupied(reservedSlot);
            _vehicle.ChangeState(VehicleState.Waiting);
            _isMoving = false;
        }

        private Vector3 ComputeExitWorldPosition()
        {
            int exitCellDistance = _movementResolver.GetExitCellDistance(_vehicle);
            Vector3 direction = GetDirectionVector(_vehicle.MovementDirection);
            return transform.position + direction * (exitCellDistance * _boardView.CellSize);
        }

        private float ComputeDuration(Vector3 targetPosition)
        {
            if (_moveSpeed <= 0f)
                return 0f;

            return Vector3.Distance(transform.position, targetPosition) / _moveSpeed;
        }

        // DOTween is imported as an Asset Store plugin (no UPM package), so UniTask's built-in
        // .ToUniTask() integration isn't compiled in for this project; bridge manually instead.
        private static UniTask AwaitTween(Tween tween)
        {
            UniTaskCompletionSource tcs = new();
            tween.OnComplete(() => tcs.TrySetResult());
            tween.OnKill(() => tcs.TrySetResult());
            return tcs.Task;
        }

        private static Vector3 GetDirectionVector(GridDirection direction)
        {
            return direction switch
            {
                GridDirection.Up => Vector3.forward,
                GridDirection.Down => Vector3.back,
                GridDirection.Left => Vector3.left,
                GridDirection.Right => Vector3.right,
                _ => Vector3.zero
            };
        }
    }
}
