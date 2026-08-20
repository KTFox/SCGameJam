using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using SCJam.AudioSystem;
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
        [SerializeField] private float _rotateSpeed;
        [SerializeField] private float _departReverseDistance;
        [SerializeField] private float _departDistance;
        [SerializeField] private Transform _boardingEntry;
        [SerializeField] private Transform[] _seatAnchors;
        [SerializeField] private SoundSO _boardingSound;
        [SerializeField] private float _boardingPunchScaleAmount;
        [SerializeField] private float _boardingPunchScaleDuration;
        [SerializeField] private SoundSO _fullSound;
        [SerializeField] private ParticleSystem _fullParticle;
        [SerializeField] private ParticleSystem _dustParticle;
        [SerializeField] private LayerMask _vehicleCollisionMask;
        [SerializeField] private float _bumpAdvanceDistance;
        [SerializeField] private SoundSO _bumpSound;
        [SerializeField] private float _bumpShakeStrength;
        [SerializeField] private float _bumpShakeDuration;
        [SerializeField] private int _bumpShakeVibrato;
        [SerializeField] private float _waitingLaneApproachOffset;


        // ===== Private Fields ===== //

        private Vehicle _vehicle;
        private BoardGrid _boardGrid;
        private BoardView _boardView;
        private VehicleMovementResolver _movementResolver;
        private WaitingAreaManager _waitingAreaManager;
        private WaitingAreaView _waitingAreaView;
        private WaitingSlot _reservedSlot;
        private Collider _collider;
        private bool _isMoving;
        private Vector3 _originalScale;
        private CancellationToken _destroyCancellationToken;


        // ===== Public Properties ===== //

        public Vehicle Vehicle => _vehicle;
        public bool IsMoving => _isMoving;
        public Vector3 BoardingEntryPosition => _boardingEntry.position;


        // ===== Methods ===== //

        private void Awake()
        {
            _destroyCancellationToken = this.GetCancellationTokenOnDestroy();
            SetDustPlaying(false);
        }

        private void OnDisable()
        {
            SetDustPlaying(false);
        }

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
            _originalScale = transform.localScale;
            _collider = GetComponent<Collider>();

            EnsureSeatAnchorCount(vehicle.Capacity);
        }

        /// <summary>
        /// Resizes the seat anchor array to match capacity, preserving existing references. Called from
        /// VehicleConfig.OnValidate (via the prefab) so seats stay in sync in the editor, and again from
        /// Initialize as a runtime safety net.
        /// </summary>
        public void EnsureSeatAnchorCount(int capacity)
        {
            if (_seatAnchors != null && _seatAnchors.Length == capacity)
                return;

            Transform[] resized = new Transform[capacity];
            int copyCount = _seatAnchors != null ? Mathf.Min(_seatAnchors.Length, capacity) : 0;

            for (int i = 0; i < copyCount; i++)
                resized[i] = _seatAnchors[i];

            _seatAnchors = resized;
        }

        public Transform GetSeatAnchor(int seatIndex)
        {
            return _seatAnchors[seatIndex];
        }

        /// <summary>
        /// Called by a passenger controller once it has parented onto a seat; plays the boarding punch
        /// and SFX on the vehicle itself.
        /// </summary>
        public void PlayBoardingFeedback()
        {
            transform.DOKill();
            transform.localScale = _originalScale;
            transform.DOPunchScale(Vector3.one * _boardingPunchScaleAmount, _boardingPunchScaleDuration)
                .OnComplete(() => transform.localScale = _originalScale);
            AudioManager.Instance.PlaySound(_boardingSound);
        }

        /// <summary>
        /// Called by LevelController right after the vehicle's boarding animations finish and
        /// BoardingResolver.CompleteBoarding settles its state at VehicleState.Full.
        /// </summary>
        public void PlayFullFeedback()
        {
            AudioManager.Instance.PlaySound(_fullSound);

            if (_fullParticle != null)
                _fullParticle.Play();
        }

        public bool CanMove()
        {
            return !_isMoving && _vehicle.State == VehicleState.Parked;
        }

        public void RequestMove()
        {
            if (!CanMove())
                return;

            if (_movementResolver.IsPathClear(_vehicle))
                MoveToWaitingSlotRoutine().Forget(HandleRoutineException);
            else
                BumpAndReverseRoutine().Forget(HandleRoutineException);
        }

        public bool CanDepart()
        {
            return !_isMoving && _vehicle.State == VehicleState.Full;
        }

        public void RequestDepart()
        {
            if (!CanDepart())
                return;

            DepartRoutine().Forget(HandleRoutineException);
        }

        private async UniTask MoveToWaitingSlotRoutine()
        {
            if (!_waitingAreaManager.TryReserveSlot(_vehicle.Id, out WaitingSlot reservedSlot))
                return;

            _reservedSlot = reservedSlot;
            SetIsMoving(true);
            _vehicle.ChangeState(VehicleState.MovingToExit);

            Vector3 exitPosition = ComputeExitWorldPosition();
            await MoveAndFaceRoutine(exitPosition);

            // Footprint-clear rule: the board cells stay occupied until the vehicle has fully left them.
            _boardGrid.RemoveVehicle(_vehicle.Id);
            _vehicle.ClearFootprint();

            Transform slotAnchor = _waitingAreaView.GetSlotAnchor(reservedSlot.Index);
            Vector3 slotPosition = slotAnchor.position;

            Vector3 lanePosition = exitPosition;
            if (_vehicle.MovementDirection == GridDirection.Down)
            {
                lanePosition = ComputeNearestSideEdgePosition(exitPosition, slotAnchor.position);
                await MoveAndFaceRoutine(lanePosition);
            }

            Vector3 approachLanePosition = ComputeApproachLanePosition(lanePosition, slotAnchor.position);
            await MoveAndFaceRoutine(approachLanePosition);

            Vector3 slotEntryPosition = ComputeSlotEntryPosition(slotAnchor, approachLanePosition);
            await MoveAndFaceRoutine(slotEntryPosition);

            await MoveAndFaceRoutine(slotPosition, slotAnchor.rotation);

            _waitingAreaManager.ConfirmOccupied(reservedSlot);
            _vehicle.ChangeState(VehicleState.Waiting);
            SetIsMoving(false);
        }

        /// <summary>
        /// Blocked-path flow: the vehicle stays Parked/footprint-owned (no slot reservation, no grid
        /// mutation) and instead advances toward the exit until its own collider actually overlaps the
        /// first blocking vehicle's collider, shakes that vehicle, then reverses back to its start
        /// transform. Only the vehicle currently bumping is locked (_isMoving); the blocker keeps moving
        /// on its own logic and is only ever shaken, never repositioned by physics.
        /// </summary>
        private async UniTask BumpAndReverseRoutine()
        {
            SetIsMoving(true);
            _vehicle.ChangeState(VehicleState.MovingToExit);

            Vector3 startPosition = transform.position;
            Quaternion startRotation = transform.rotation;

            await AdvanceUntilBlockedRoutine(startPosition);
            await MoveAndFaceRoutine(startPosition, startRotation);

            _vehicle.ChangeState(VehicleState.Parked);
            SetIsMoving(false);
        }

        /// <summary>
        /// Advances toward the exit one frame at a time, re-checking the collider overlap after every step.
        /// Guarantees at least _bumpAdvanceDistance of travel before a blocker can stop the vehicle, so an
        /// already-adjacent blocker still produces a visible bump instead of the vehicle standing still.
        /// </summary>
        private async UniTask AdvanceUntilBlockedRoutine(Vector3 startPosition)
        {
            Vector3 direction = GetWorldDirectionVector(_vehicle.MovementDirection);
            float exitDistance = Vector3.Distance(startPosition, ComputeExitWorldPosition());
            float maxDistance = Mathf.Max(exitDistance, _bumpAdvanceDistance);
            float travelledDistance = 0f;

            while (travelledDistance < maxDistance)
            {
                float step = Mathf.Max(_moveSpeed * Time.deltaTime, 0f);
                travelledDistance = Mathf.Min(travelledDistance + step, maxDistance);
                transform.position = startPosition + direction * travelledDistance;

                await UniTask.Yield(_destroyCancellationToken);

                if (travelledDistance < _bumpAdvanceDistance)
                    continue;

                VehicleController blocker = FindFirstBlockingVehicle(direction);
                if (blocker != null)
                {
                    blocker.PlayBumpedFeedback();
                    return;
                }
            }
        }

        /// <summary>
        /// Physics.OverlapBox against the vehicle's own collider bounds, restricted to _vehicleCollisionMask
        /// and excluding self, so a bump is only ever triggered by an actual collider overlap rather than
        /// grid-cell math. Works against the existing non-trigger colliders directly; no Rigidbody or
        /// isTrigger change is required on the prefabs for this to function.
        /// Overlaps are ranked by how far they sit ahead along the movement direction (not raw distance),
        /// and anything not ahead of the vehicle is ignored, so a vehicle merely sitting alongside is never
        /// mistaken for the one actually blocking the path.
        /// </summary>
        private VehicleController FindFirstBlockingVehicle(Vector3 direction)
        {
            Bounds bounds = _collider.bounds;
            Collider[] overlaps = Physics.OverlapBox(bounds.center, bounds.extents, transform.rotation, _vehicleCollisionMask, QueryTriggerInteraction.Collide);

            VehicleController closestBlocker = null;
            float closestForwardDistance = float.MaxValue;

            foreach (Collider overlap in overlaps)
            {
                if (overlap == _collider)
                    continue;

                if (!overlap.TryGetComponent(out VehicleController otherController) || otherController == this)
                    continue;

                float forwardDistance = Vector3.Dot(overlap.bounds.center - bounds.center, direction);
                if (forwardDistance <= 0f)
                    continue;

                if (forwardDistance >= closestForwardDistance)
                    continue;

                closestForwardDistance = forwardDistance;
                closestBlocker = otherController;
            }

            return closestBlocker;
        }

        /// <summary>
        /// Called on the vehicle that got hit while another vehicle was bumping into it. Never called on
        /// the vehicle currently selected/bumping, and never applies a physics push.
        /// </summary>
        private void PlayBumpedFeedback()
        {
            transform.DOShakePosition(_bumpShakeDuration, _bumpShakeStrength, _bumpShakeVibrato);
            AudioManager.Instance.PlaySound(_bumpSound);
        }

        private async UniTask DepartRoutine()
        {
            SetIsMoving(true);
            _vehicle.ChangeState(VehicleState.Departing);

            // Reverse straight back without rotating; MoveAndFaceRoutine would turn the vehicle
            // to face the destination, which means spinning 180 degrees while backing up.
            Vector3 reversePosition = transform.position - transform.forward * _departReverseDistance;
            await transform.DOMove(reversePosition, ComputeDuration(reversePosition)).SetEase(Ease.Linear)
                .ToUniTask(TweenCancelBehaviour.KillAndCancelAwait, _destroyCancellationToken);

            // The vehicle currently faces slotAnchor.rotation, which is an arbitrary designer-placed
            // angle rather than a grid-aligned one. Turning by a relative +90 off that angle would
            // send the vehicle off at a diagonal, so face the board's actual world-right direction instead.
            Quaternion turnRotation = Quaternion.LookRotation(GetWorldDirectionVector(GridDirection.Right), Vector3.up);
            await RotateTowardsRoutine(turnRotation);

            Vector3 departPosition = transform.position + transform.forward * (_departDistance * _boardView.CellSize);
            await transform.DOMove(departPosition, ComputeDuration(departPosition)).SetEase(Ease.Linear)
                .ToUniTask(TweenCancelBehaviour.KillAndCancelAwait, _destroyCancellationToken);

            // Waiting-slot release timing follows the same footprint-clear rule as the board: released
            // only once the vehicle has fully left the slot, not when departure starts.
            _waitingAreaManager.ReleaseSlot(_reservedSlot);
            _vehicle.ChangeState(VehicleState.Completed);
            SetIsMoving(false);
            gameObject.SetActive(false);
        }

        private void SetIsMoving(bool isMoving)
        {
            _isMoving = isMoving;
            SetDustPlaying(isMoving);
        }

        private void SetDustPlaying(bool isPlaying)
        {
            if (_dustParticle == null)
                return;

            if (isPlaying)
                _dustParticle.Play();
            else
                _dustParticle.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }

        private Vector3 ComputeExitWorldPosition()
        {
            int exitCellDistance = _movementResolver.GetExitCellDistance(_vehicle);
            Vector3 direction = GetWorldDirectionVector(_vehicle.MovementDirection);
            return transform.position + direction * (exitCellDistance * _boardView.CellSize);
        }

        /// <summary>
        /// Point on the side edge (local left/right boundary) nearest the reserved slot, sharing the
        /// given position's local Z. Only used for MovementDirection.Down, whose exit lane runs along
        /// the bottom of the board rather than toward a side edge.
        /// </summary>
        private Vector3 ComputeNearestSideEdgePosition(Vector3 worldPosition, Vector3 slotWorldPosition)
        {
            float slotLocalX = WorldToBoardLocal(slotWorldPosition).x;
            float leftEdgeLocalX = ComputeLeftEdgeLocalX();
            float rightEdgeLocalX = ComputeRightEdgeLocalX();

            float targetLocalX = Mathf.Abs(slotLocalX - leftEdgeLocalX) <= Mathf.Abs(rightEdgeLocalX - slotLocalX)
                ? leftEdgeLocalX
                : rightEdgeLocalX;

            Vector3 local = WorldToBoardLocal(worldPosition);
            local.x = targetLocalX;
            return BoardLocalToWorld(local);
        }

        /// <summary>
        /// Point along the vehicle's current lane (its local X held fixed) where its local Z reaches
        /// slotZ - _waitingLaneApproachOffset, i.e. just below the reserved slot. The vehicle always
        /// approaches the waiting row from below.
        /// </summary>
        private Vector3 ComputeApproachLanePosition(Vector3 worldPosition, Vector3 slotWorldPosition)
        {
            float slotLocalZ = WorldToBoardLocal(slotWorldPosition).z;

            Vector3 local = WorldToBoardLocal(worldPosition);
            local.z = slotLocalZ - _waitingLaneApproachOffset;
            return BoardLocalToWorld(local);
        }

        /// <summary>
        /// Point on the approach lane (at laneWorldPosition's local Z) where the slot's own approach axis
        /// (its local -forward, i.e. the direction a vehicle travels to arrive facing slotAnchor.rotation)
        /// crosses that lane. This follows the slot's rotation rather than assuming a straight board-local
        /// projection.
        /// </summary>
        private Vector3 ComputeSlotEntryPosition(Transform slotAnchor, Vector3 laneWorldPosition)
        {
            float laneLocalZ = WorldToBoardLocal(laneWorldPosition).z;
            float slotLocalZ = WorldToBoardLocal(slotAnchor.position).z;
            float approachLocalZ = WorldToBoardLocal(slotAnchor.position + slotAnchor.forward).z - slotLocalZ;

            if (Mathf.Approximately(approachLocalZ, 0f))
            {
                Vector3 local = WorldToBoardLocal(slotAnchor.position);
                local.z = laneLocalZ;
                return BoardLocalToWorld(local);
            }

            float distanceAlongApproach = (slotLocalZ - laneLocalZ) / approachLocalZ;
            return slotAnchor.position - slotAnchor.forward * distanceAlongApproach;
        }

        private float ComputeLeftEdgeLocalX()
        {
            Vector3 boundaryWorldPosition = _boardView.CellToWorld(new Vector2Int(-1, 0));
            return WorldToBoardLocal(boundaryWorldPosition).x;
        }

        private float ComputeRightEdgeLocalX()
        {
            Vector3 boundaryWorldPosition = _boardView.CellToWorld(new Vector2Int(_boardGrid.Width, 0));
            return WorldToBoardLocal(boundaryWorldPosition).x;
        }

        private Vector3 WorldToBoardLocal(Vector3 worldPosition)
        {
            Transform gridOrigin = _boardView.GridOrigin;
            return Quaternion.Inverse(gridOrigin.rotation) * (worldPosition - gridOrigin.position);
        }

        private Vector3 BoardLocalToWorld(Vector3 localPosition)
        {
            Transform gridOrigin = _boardView.GridOrigin;
            return gridOrigin.position + gridOrigin.rotation * localPosition;
        }

        private async UniTask MoveAndFaceRoutine(Vector3 targetPosition, Quaternion? faceRotation = null)
        {
            Quaternion targetRotation = faceRotation ?? ComputeFacingRotation(targetPosition);
            await UniTask.WhenAll(
                RotateTowardsRoutine(targetRotation),
                transform.DOMove(targetPosition, ComputeDuration(targetPosition)).SetEase(Ease.Linear)
                    .ToUniTask(TweenCancelBehaviour.KillAndCancelAwait, _destroyCancellationToken));
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

        private async UniTask RotateTowardsRoutine(Quaternion targetRotation)
        {
            if (_rotateSpeed <= 0f)
            {
                transform.rotation = targetRotation;
                return;
            }

            float angle = Quaternion.Angle(transform.rotation, targetRotation);
            float duration = angle / _rotateSpeed;
            await transform.DORotateQuaternion(targetRotation, duration).SetEase(Ease.Linear)
                .ToUniTask(TweenCancelBehaviour.KillAndCancelAwait, _destroyCancellationToken);
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

        /// <summary>
        /// Cancellation from GetCancellationTokenOnDestroy is expected whenever a level transition destroys
        /// this vehicle mid-routine, so it is swallowed here instead of surfacing as an unobserved exception.
        /// </summary>
        private static void HandleRoutineException(Exception exception)
        {
            if (exception is not OperationCanceledException)
                Debug.LogException(exception);
        }
    }
}
