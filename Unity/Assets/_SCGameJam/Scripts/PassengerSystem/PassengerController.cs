using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using SCJam.VehicleSystem;
using UnityEngine;

namespace SCJam.PassengerSystem
{
    public class PassengerController : MonoBehaviour
    {
        // ===== Static Fields ===== //

        private static readonly int IS_MOVING_HASH = Animator.StringToHash("IsMoving");


        // ===== Serialized Fields ===== //

        [SerializeField] private Animator _animator;
        [SerializeField] private float _moveSpeed;
        [SerializeField] private float _stepDownDistance;
        [SerializeField] private float _jumpPower;
        [SerializeField] private float _jumpDuration;
        [SerializeField] private float _boardStaggerDelay;


        // ===== Private Fields ===== //

        private Passenger _passenger;
        private bool _isBoarding;
        private bool _isMoving;
        private bool _isSteppingThroughQueue;
        private int _currentQueueSlotIndex;
        private int _targetQueueSlotIndex;
        private PassengerQueueView _queueView;
        private CancellationToken _destroyCancellationToken;


        // ===== Public Properties ===== //

        public Passenger Passenger => _passenger;
        public bool IsMoving => _isMoving;

        /// <summary>
        /// True once this passenger has physically arrived at queue slot 0 and is not mid-step toward
        /// another slot. Boarding/matching must wait for this before treating it as the front of the queue.
        /// </summary>
        public bool IsSettledAtQueueFront => !_isSteppingThroughQueue && _currentQueueSlotIndex == 0;


        // ===== Methods ===== //

        private void Awake()
        {
            _destroyCancellationToken = this.GetCancellationTokenOnDestroy();
        }

        public void Initialize(Passenger passenger, PassengerQueueView queueView)
        {
            _passenger = passenger;
            _queueView = queueView;
        }

        /// <summary>
        /// Places this passenger at a queue slot instantly (no tween), used when it first spawns into the
        /// queue. Establishes the current/target slot index that MoveToQueueSlot steps from afterward.
        /// </summary>
        public void SnapToQueueSlot(int slotIndex)
        {
            _currentQueueSlotIndex = slotIndex;
            _targetQueueSlotIndex = slotIndex;

            Transform anchor = _queueView.GetQueueTransform(slotIndex);
            transform.SetPositionAndRotation(anchor.position, anchor.rotation);
        }

        /// <summary>
        /// Plays the walk-to-vehicle and board-jump animation for a passenger already marked
        /// MovingToVehicle by BoardingResolver. queueFrontPosition is the world position of the passenger
        /// queue's first visible anchor; indexInGroup staggers passengers boarding together so they don't
        /// walk on top of each other.
        /// </summary>
        public void MoveToVehicle(VehicleController vehicleController, int seatIndex, Vector3 queueFrontPosition, int indexInGroup)
        {
            if (_isBoarding || _passenger.State != PassengerState.MovingToVehicle)
                return;

            BoardVehicleRoutine(vehicleController, seatIndex, queueFrontPosition, indexInGroup).Forget(HandleRoutineException);
        }

        private async UniTask BoardVehicleRoutine(VehicleController vehicleController, int seatIndex, Vector3 queueFrontPosition, int indexInGroup)
        {
            _isBoarding = true;

            if (indexInGroup > 0 && _boardStaggerDelay > 0f)
                await UniTask.Delay(TimeSpan.FromSeconds(indexInGroup * _boardStaggerDelay), cancellationToken: _destroyCancellationToken);

            SetIsMoving(true);

            await MoveStepRoutine(queueFrontPosition);

            Vector3 stepDownPosition = transform.position + Vector3.back * _stepDownDistance;
            await MoveStepRoutine(stepDownPosition);

            Vector3 boardingEntryPosition = vehicleController.BoardingEntryPosition;
            Vector3 alignXPosition = new(boardingEntryPosition.x, transform.position.y, transform.position.z);
            await MoveStepRoutine(alignXPosition);

            await MoveStepRoutine(boardingEntryPosition);

            SetIsMoving(false);
            await JumpToSeatRoutine(vehicleController, seatIndex);

            _passenger.ChangeState(PassengerState.Completed);
            _isBoarding = false;
        }

        private async UniTask MoveStepRoutine(Vector3 targetPosition)
        {
            if ((targetPosition - transform.position).sqrMagnitude < 0.0001f)
                return;

            transform.rotation = ComputeFacingRotation(targetPosition);
            await transform.DOMove(targetPosition, ComputeDuration(targetPosition)).SetEase(Ease.Linear)
                .ToUniTask(TweenCancelBehaviour.KillAndCancelAwait, _destroyCancellationToken);
        }

        private async UniTask JumpToSeatRoutine(VehicleController vehicleController, int seatIndex)
        {
            Transform seat = vehicleController.GetSeatAnchor(seatIndex);

            await UniTask.WhenAll(
                transform.DOJump(seat.position, _jumpPower, 1, _jumpDuration).ToUniTask(TweenCancelBehaviour.KillAndCancelAwait, _destroyCancellationToken),
                transform.DORotateQuaternion(seat.rotation, _jumpDuration).ToUniTask(TweenCancelBehaviour.KillAndCancelAwait, _destroyCancellationToken),
                transform.DOScale(seat.lossyScale, _jumpDuration).ToUniTask(TweenCancelBehaviour.KillAndCancelAwait, _destroyCancellationToken));

            transform.SetParent(seat, true);
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
            transform.localScale = Vector3.one;

            vehicleController.PlayBoardingFeedback();
        }

        /// <summary>
        /// Requests that an already-spawned, still-Queued passenger walk toward a new queue slot after the
        /// front of the queue compacts, advancing one adjacent slot at a time instead of jumping straight to
        /// the destination. No-ops for passengers mid-boarding so a compaction never interrupts their walk.
        /// If a step is already in progress, only the target is updated — the in-flight step finishes and
        /// the routine then continues on from there toward the latest target.
        /// </summary>
        public void MoveToQueueSlot(int targetSlotIndex)
        {
            if (_isBoarding || _passenger.State != PassengerState.Queued)
                return;

            _targetQueueSlotIndex = targetSlotIndex;

            if (_isSteppingThroughQueue)
                return;

            StepThroughQueueRoutine().Forget(HandleRoutineException);
        }

        private async UniTask StepThroughQueueRoutine()
        {
            _isSteppingThroughQueue = true;
            SetIsMoving(true);

            while (_currentQueueSlotIndex != _targetQueueSlotIndex)
            {
                _currentQueueSlotIndex += _currentQueueSlotIndex < _targetQueueSlotIndex ? 1 : -1;
                Transform anchor = _queueView.GetQueueTransform(_currentQueueSlotIndex);
                await MoveStepRoutine(anchor.position);
                transform.rotation = anchor.rotation;
            }

            SetIsMoving(false);
            _isSteppingThroughQueue = false;
        }

        private void SetIsMoving(bool isMoving)
        {
            _isMoving = isMoving;

            if (_animator != null)
                _animator.SetBool(IS_MOVING_HASH, isMoving);
        }

        private Quaternion ComputeFacingRotation(Vector3 targetPosition)
        {
            Vector3 direction = targetPosition - transform.position;
            direction.y = 0f;

            return direction.sqrMagnitude < 0.0001f ? transform.rotation : Quaternion.LookRotation(direction, Vector3.up);
        }

        private float ComputeDuration(Vector3 targetPosition)
        {
            if (_moveSpeed <= 0f)
                return 0f;

            return Vector3.Distance(transform.position, targetPosition) / _moveSpeed;
        }

        /// <summary>
        /// Cancellation from GetCancellationTokenOnDestroy is expected whenever a level transition destroys
        /// this passenger mid-routine, so it is swallowed here instead of surfacing as an unobserved exception.
        /// </summary>
        private static void HandleRoutineException(Exception exception)
        {
            if (exception is not OperationCanceledException)
                Debug.LogException(exception);
        }
    }
}
