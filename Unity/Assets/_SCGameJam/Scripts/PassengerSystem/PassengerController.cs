using System;
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


        // ===== Public Properties ===== //

        public Passenger Passenger => _passenger;
        public bool IsMoving => _isMoving;


        // ===== Methods ===== //

        public void Initialize(Passenger passenger)
        {
            _passenger = passenger;
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

            BoardVehicleRoutine(vehicleController, seatIndex, queueFrontPosition, indexInGroup).Forget();
        }

        private async UniTaskVoid BoardVehicleRoutine(VehicleController vehicleController, int seatIndex, Vector3 queueFrontPosition, int indexInGroup)
        {
            _isBoarding = true;

            if (indexInGroup > 0 && _boardStaggerDelay > 0f)
                await UniTask.Delay(TimeSpan.FromSeconds(indexInGroup * _boardStaggerDelay));

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
            await transform.DOMove(targetPosition, ComputeDuration(targetPosition)).SetEase(Ease.Linear).ToUniTask();
        }

        private async UniTask JumpToSeatRoutine(VehicleController vehicleController, int seatIndex)
        {
            Transform seat = vehicleController.GetSeatAnchor(seatIndex);

            await UniTask.WhenAll(
                transform.DOJump(seat.position, _jumpPower, 1, _jumpDuration).ToUniTask(),
                transform.DORotateQuaternion(seat.rotation, _jumpDuration).ToUniTask(),
                transform.DOScale(seat.lossyScale, _jumpDuration).ToUniTask());

            transform.SetParent(seat, true);
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
            transform.localScale = Vector3.one;

            vehicleController.PlayBoardingFeedback();
        }

        /// <summary>
        /// Moves an already-spawned, still-Queued passenger to its new queue anchor after the front of the
        /// queue compacts. No-ops for passengers mid-boarding so a compaction never interrupts their walk.
        /// </summary>
        public void MoveToQueueSlot(Vector3 targetPosition, Quaternion targetRotation)
        {
            if (_isBoarding || _passenger.State != PassengerState.Queued)
                return;

            transform.DOKill();
            transform.rotation = targetRotation;
            transform.DOMove(targetPosition, ComputeDuration(targetPosition)).SetEase(Ease.Linear);
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
    }
}
