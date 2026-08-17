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


        // ===== Private Fields ===== //

        private Vehicle _vehicle;
        private BoardGrid _boardGrid;
        private BoardView _boardView;
        private VehicleMovementResolver _movementResolver;
        private WaitingAreaManager _waitingAreaManager;
        private WaitingAreaView _waitingAreaView;
        private WaitingSlot _reservedSlot;
        private bool _isMoving;
        private Vector3 _originalScale;


        // ===== Public Properties ===== //

        public Vehicle Vehicle => _vehicle;
        public bool IsMoving => _isMoving;
        public Vector3 BoardingEntryPosition => _boardingEntry.position;


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
            _originalScale = transform.localScale;

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
            transform.DOPunchScale(Vector3.one * _boardingPunchScaleAmount, _boardingPunchScaleDuration)
                .OnComplete(() => transform.localScale = _originalScale);
            AudioManager.Instance.PlaySound(_boardingSound);
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
            await MoveAndFaceRoutine(exitPosition);

            // Footprint-clear rule: the board cells stay occupied until the vehicle has fully left them.
            _boardGrid.RemoveVehicle(_vehicle.Id);
            _vehicle.ClearFootprint();

            Transform slotAnchor = _waitingAreaView.GetSlotAnchor(reservedSlot.Index);
            Vector3 slotPosition = slotAnchor.position;

            Vector3 topEdgePosition = ComputeTopEdgePosition(exitPosition);
            await MoveAndFaceRoutine(topEdgePosition);

            Vector3 slotEntryPosition = ComputeSlotEntryPosition(slotAnchor);
            await MoveAndFaceRoutine(slotEntryPosition);

            await MoveAndFaceRoutine(slotPosition, slotAnchor.rotation);

            _waitingAreaManager.ConfirmOccupied(reservedSlot);
            _vehicle.ChangeState(VehicleState.Waiting);
            _isMoving = false;
        }

        private async UniTask DepartRoutine()
        {
            _isMoving = true;
            _vehicle.ChangeState(VehicleState.Departing);

            // Reverse straight back without rotating; MoveAndFaceRoutine would turn the vehicle
            // to face the destination, which means spinning 180 degrees while backing up.
            Vector3 reversePosition = transform.position - transform.forward * _departReverseDistance;
            await transform.DOMove(reversePosition, ComputeDuration(reversePosition)).SetEase(Ease.Linear).ToUniTask();

            // The vehicle currently faces slotAnchor.rotation, which is an arbitrary designer-placed
            // angle rather than a grid-aligned one. Turning by a relative +90 off that angle would
            // send the vehicle off at a diagonal, so face the board's actual world-right direction instead.
            Quaternion turnRotation = Quaternion.LookRotation(GetWorldDirectionVector(GridDirection.Right), Vector3.up);
            await RotateTowardsRoutine(turnRotation);

            Vector3 departPosition = transform.position + transform.forward * (_departDistance * _boardView.CellSize);
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

        /// <summary>
        /// Point directly above the board's top edge (local +Z boundary) sharing the given position's
        /// local X, i.e. the lane a vehicle travels along after exiting the board.
        /// </summary>
        private Vector3 ComputeTopEdgePosition(Vector3 worldPosition)
        {
            Vector3 local = WorldToBoardLocal(worldPosition);
            local.z = ComputeTopEdgeLocalZ();
            return BoardLocalToWorld(local);
        }

        /// <summary>
        /// Point on the top-edge lane where the slot's own approach axis (its local -forward, i.e. the
        /// direction a vehicle travels to arrive facing slotAnchor.rotation) crosses the top edge. This
        /// follows the slot's rotation rather than assuming a straight board-local projection.
        /// </summary>
        private Vector3 ComputeSlotEntryPosition(Transform slotAnchor)
        {
            float topEdgeLocalZ = ComputeTopEdgeLocalZ();
            float slotLocalZ = WorldToBoardLocal(slotAnchor.position).z;
            float approachLocalZ = WorldToBoardLocal(slotAnchor.position + slotAnchor.forward).z - slotLocalZ;

            if (Mathf.Approximately(approachLocalZ, 0f))
                return ComputeTopEdgePosition(slotAnchor.position);

            float distanceAlongApproach = (slotLocalZ - topEdgeLocalZ) / approachLocalZ;
            return slotAnchor.position - slotAnchor.forward * distanceAlongApproach;
        }

        private float ComputeTopEdgeLocalZ()
        {
            Vector3 boundaryWorldPosition = _boardView.CellToWorld(new Vector2Int(0, _boardGrid.Height));
            return WorldToBoardLocal(boundaryWorldPosition).z;
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
                transform.DOMove(targetPosition, ComputeDuration(targetPosition)).SetEase(Ease.Linear).ToUniTask());
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
            await transform.DORotateQuaternion(targetRotation, duration).SetEase(Ease.Linear).ToUniTask();
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
