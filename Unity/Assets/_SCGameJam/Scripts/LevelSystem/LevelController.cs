using System;
using System.Collections.Generic;
using SCJam.AudioSystem;
using SCJam.BoardSystem;
using SCJam.Common;
using SCJam.PassengerSystem;
using SCJam.UISystem;
using SCJam.VehicleSystem;
using SCJam.WaitingAreaSystem;
using UnityEngine;

namespace SCJam.LevelSystem
{
    /// <summary>
    /// Single scene-resident orchestrator (not a MonoSingleton) that builds every gameplay system from a
    /// LevelConfig, drives waiting-vehicle/passenger matching each frame, and evaluates the win condition.
    /// </summary>
    public class LevelController : MonoBehaviour
    {
        // ===== Serialized Fields ===== //

        [SerializeField] private BoardView _boardView;
        [SerializeField] private WaitingAreaView _waitingAreaView;
        [SerializeField] private PassengerQueueView _passengerQueueView;
        [SerializeField] private Transform _vehicleSpawnRoot;
        [SerializeField] private Transform _passengerSpawnRoot;


        // ===== Private Fields ===== //

        private readonly IWaitingVehicleSelector _waitingVehicleSelector = new DefaultWaitingVehicleSelector();
        private readonly Dictionary<int, Vehicle> _vehiclesById = new();
        private readonly Dictionary<int, VehicleController> _vehicleControllersById = new();
        private readonly Dictionary<int, PassengerController> _passengerControllersById = new();
        private readonly Dictionary<int, List<Passenger>> _pendingBoardingByVehicleId = new();
        private readonly List<GameObject> _spawnedGameObjects = new();
        private readonly HashSet<PuzzleColor> _loggedMissingPrefabColors = new();

        private LevelState _levelState;
        private BoardGrid _boardGrid;
        private VehicleMovementResolver _movementResolver;
        private WaitingAreaManager _waitingAreaManager;
        private PassengerQueue _passengerQueue;
        private BoardingResolver _boardingResolver;
        private PassengerPrefabLookup _passengerPrefabLookup;
        private PopupNextLevel _openNextLevelPopup;


        // ===== Public Properties ===== //

        public LevelState State => _levelState;


        // ===== Events ===== //

        public event Action OnLevelCompleted;
        public event Action OnLevelFailed;


        // ===== Unity Lifecycle Methods ===== //

        private void Start()
        {
            LoadCurrentLevel();
        }

        private void Update()
        {
            if (_levelState != LevelState.Playing)
                return;

            TryMatchWaitingVehicleToFrontGroup();
            ProcessBoardingCompletions();
            ProcessFullVehicleDepartures();
            EvaluateWinCondition();
            EvaluateLoseCondition();
        }

        private void OnDisable()
        {
            UnsubscribeFromResultPopup();
        }


        // ===== Methods ===== //

        public void LoadCurrentLevel()
        {
            if (LevelDatabase.Instance == null)
            {
                Debug.LogError($"[{nameof(LevelController)}] Missing {nameof(LevelDatabase)} instance.", this);
                return;
            }

            LoadLevel(LevelDatabase.Instance.CurrentLevel);
        }

        public void LoadLevel(LevelConfig levelConfig)
        {
            if (levelConfig == null)
                return;

            ClearLevel();
            _levelState = LevelState.Loading;

            BuildBoard(levelConfig);
            SpawnVehicles(levelConfig);
            BuildPassengerPrefabLookup(levelConfig);
            BuildPassengerQueue(levelConfig);
            RefreshQueueVisuals();

            _levelState = LevelState.Playing;
            AudioManager.Instance?.PlaySound(levelConfig.BackgroundMusic);
        }

        private void BuildBoard(LevelConfig levelConfig)
        {
            _boardGrid = new BoardGrid(levelConfig.BoardSize.x, levelConfig.BoardSize.y);
            _movementResolver = new VehicleMovementResolver(_boardGrid);
            _waitingAreaManager = new WaitingAreaManager(levelConfig.WaitingSlotCount);

            _boardView.Initialize(_boardGrid);
            _waitingAreaView.ApplyActiveSlotCount(levelConfig.WaitingSlotCount);
        }

        private void SpawnVehicles(LevelConfig levelConfig)
        {
            IReadOnlyList<VehiclePlacement> placements = levelConfig.VehiclePlacements;
            if (placements == null)
                return;

            for (int i = 0; i < placements.Count; i++)
            {
                VehiclePlacement placement = placements[i];
                if (placement.VehicleConfig == null)
                    continue;

                Vector2Int footprintSize = GetOrientedFootprintSize(
                    placement.VehicleConfig.FootprintSize,
                    placement.MovementDirection);
                IReadOnlyList<Vector2Int> footprintCells = ComputeFootprintCells(placement.OriginCell, footprintSize);
                Vehicle vehicle = new(i, placement.VehicleConfig.Color, placement.VehicleConfig.Capacity, footprintCells, placement.MovementDirection);

                _boardGrid.PlaceVehicle(vehicle.Id, footprintCells);
                _vehiclesById[vehicle.Id] = vehicle;

                if (placement.VehicleConfig.Prefab == null || _boardView == null)
                    continue;

                Vector3 spawnPosition = GetVehicleSpawnPosition(placement.OriginCell, footprintSize);
                Quaternion spawnRotation = GetVehicleSpawnRotation(placement.MovementDirection);
                VehicleController vehicleController = Instantiate(placement.VehicleConfig.Prefab, spawnPosition, spawnRotation, _vehicleSpawnRoot);
                vehicleController.Initialize(vehicle, _boardGrid, _boardView, _movementResolver, _waitingAreaManager, _waitingAreaView);

                _vehicleControllersById[vehicle.Id] = vehicleController;
                _spawnedGameObjects.Add(vehicleController.gameObject);
            }
        }

        private void BuildPassengerPrefabLookup(LevelConfig levelConfig)
        {
            List<string> errors = new();
            _passengerPrefabLookup = PassengerPrefabLookup.Build(
                levelConfig.PassengerPrefabMappings,
                levelConfig.PassengerColorSequence,
                errors);

            foreach (string error in errors)
                Debug.LogError($"Level '{levelConfig.name}': {error}", this);
        }

        private void BuildPassengerQueue(LevelConfig levelConfig)
        {
            IReadOnlyList<PuzzleColor> colorSequence = levelConfig.PassengerColorSequence;
            List<Passenger> passengers = new(colorSequence?.Count ?? 0);

            if (colorSequence != null)
            {
                for (int i = 0; i < colorSequence.Count; i++)
                {
                    passengers.Add(new Passenger(i, colorSequence[i], i));
                }
            }

            _passengerQueue = new PassengerQueue(passengers);
            _boardingResolver = new BoardingResolver(_passengerQueue);
        }

        private void TryMatchWaitingVehicleToFrontGroup()
        {
            if (_passengerQueue.Passengers.Count == 0)
                return;

            IReadOnlyList<Passenger> frontGroup = _passengerQueue.GetAccessibleFrontGroup();
            if (frontGroup.Count == 0)
                return;

            // The front passenger must have physically finished walking into queue slot 0 before it can be
            // matched — boarding may never pull a passenger that's still mid-step between queue slots.
            if (_passengerControllersById.TryGetValue(frontGroup[0].Id, out PassengerController frontController)
                && !frontController.IsSettledAtQueueFront)
                return;

            List<WaitingVehicleEntry> entries = BuildWaitingVehicleEntries();
            if (entries.Count == 0)
                return;

            Vehicle selectedVehicle = _waitingVehicleSelector.SelectVehicle(entries, frontGroup[0].Color);
            if (selectedVehicle == null)
                return;

            IReadOnlyList<Passenger> boardedPassengers = _boardingResolver.TryBoard(selectedVehicle);
            if (boardedPassengers.Count == 0)
                return;

            VehicleController vehicleController = _vehicleControllersById[selectedVehicle.Id];
            Vector3 queueFrontPosition = _passengerQueueView.GetQueueTransform(0).position;
            int firstSeat = selectedVehicle.OccupiedSeatCount - boardedPassengers.Count;

            if (!_pendingBoardingByVehicleId.TryGetValue(selectedVehicle.Id, out List<Passenger> pending))
            {
                pending = new List<Passenger>(boardedPassengers.Count);
                _pendingBoardingByVehicleId[selectedVehicle.Id] = pending;
            }

            for (int i = 0; i < boardedPassengers.Count; i++)
            {
                Passenger passenger = boardedPassengers[i];
                pending.Add(passenger);

                if (_passengerControllersById.TryGetValue(passenger.Id, out PassengerController passengerController))
                {
                    _passengerControllersById.Remove(passenger.Id);
                    passengerController.MoveToVehicle(vehicleController, firstSeat + i, queueFrontPosition, i);
                }
                else
                {
                    // No spawned view for this passenger (prefab not wired yet) — nothing to animate.
                    passenger.ChangeState(PassengerState.Completed);
                }
            }

            CompactQueueVisuals();
        }

        private List<WaitingVehicleEntry> BuildWaitingVehicleEntries()
        {
            List<WaitingVehicleEntry> entries = new();

            foreach (WaitingSlot slot in _waitingAreaManager.Slots)
            {
                if (slot.State != WaitingSlotState.Occupied || slot.VehicleId == null)
                    continue;

                if (_vehiclesById.TryGetValue(slot.VehicleId.Value, out Vehicle vehicle))
                    entries.Add(new WaitingVehicleEntry(vehicle, slot));
            }

            return entries;
        }

        private void ProcessBoardingCompletions()
        {
            if (_pendingBoardingByVehicleId.Count == 0)
                return;

            List<int> completedVehicleIds = null;

            foreach (KeyValuePair<int, List<Passenger>> pending in _pendingBoardingByVehicleId)
            {
                if (!IsAllCompleted(pending.Value))
                    continue;

                completedVehicleIds ??= new List<int>();
                completedVehicleIds.Add(pending.Key);
            }

            if (completedVehicleIds == null)
                return;

            foreach (int vehicleId in completedVehicleIds)
            {
                _pendingBoardingByVehicleId.Remove(vehicleId);

                Vehicle vehicle = _vehiclesById[vehicleId];
                _boardingResolver.CompleteBoarding(vehicle);

                if (vehicle.State == VehicleState.Full)
                    _vehicleControllersById[vehicleId].PlayFullFeedback();
            }
        }

        private static bool IsAllCompleted(List<Passenger> passengers)
        {
            foreach (Passenger passenger in passengers)
            {
                if (passenger.State != PassengerState.Completed)
                    return false;
            }

            return true;
        }

        private void ProcessFullVehicleDepartures()
        {
            foreach (VehicleController controller in _vehicleControllersById.Values)
            {
                if (controller.Vehicle.State == VehicleState.Full)
                    controller.RequestDepart();
            }
        }

        private void EvaluateWinCondition()
        {
            if (_passengerQueue.Passengers.Count > 0 || _pendingBoardingByVehicleId.Count > 0)
                return;

            // "Parking area is cleared": every vehicle has left the board, though not all of them
            // necessarily filled up or departed the waiting area (there may be no matching passengers).
            foreach (Vehicle vehicle in _vehiclesById.Values)
            {
                if (vehicle.State == VehicleState.Parked || vehicle.State == VehicleState.MovingToExit)
                    return;
            }

            _levelState = LevelState.Won;
            Debug.Log("Level completed");

            OnLevelCompleted?.Invoke();
            ShowNextLevelPopup();
        }

        /// <summary>
        /// "Stuck" lose condition: the queue can't advance because every waiting slot is already
        /// occupied by a parked vehicle, and none of those vehicles match the front group's color.
        /// A slot that is merely Reserved (a vehicle is still on its way in) still counts as an
        /// opportunity for the front group to be relieved, so it does not count toward "full".
        /// </summary>
        private void EvaluateLoseCondition()
        {
            if (_passengerQueue.Passengers.Count == 0)
                return;

            IReadOnlyList<Passenger> frontGroup = _passengerQueue.GetAccessibleFrontGroup();
            if (frontGroup.Count == 0)
                return;

            // Passengers still mid-boarding may complete and free up a vehicle/slot once their
            // animation finishes, so the lose check must wait for them to settle first.
            if (_pendingBoardingByVehicleId.Count > 0)
                return;

            if (!AreAllWaitingSlotsOccupied())
                return;

            if (HasWaitingVehicleOfColor(frontGroup[0].Color))
                return;

            _levelState = LevelState.Lost;
            Debug.Log("Level failed");

            OnLevelFailed?.Invoke();
            ShowLosePopup();
        }

        private bool AreAllWaitingSlotsOccupied()
        {
            foreach (WaitingSlot slot in _waitingAreaManager.Slots)
            {
                if (slot.State != WaitingSlotState.Occupied)
                    return false;
            }

            return true;
        }

        private bool HasWaitingVehicleOfColor(PuzzleColor color)
        {
            foreach (WaitingSlot slot in _waitingAreaManager.Slots)
            {
                if (slot.State != WaitingSlotState.Occupied || slot.VehicleId == null)
                    continue;

                if (_vehiclesById.TryGetValue(slot.VehicleId.Value, out Vehicle vehicle) && vehicle.Color == color)
                    return true;
            }

            return false;
        }

        private void ShowNextLevelPopup()
        {
            if (PopupManager.Instance == null)
            {
                Debug.LogError($"[{nameof(LevelController)}] Missing {nameof(PopupManager)} instance.", this);
                return;
            }

            int completedLevel = LevelDatabase.Instance.CurrentLevelIndex;
            bool hasNextLevel = completedLevel + 1 < LevelDatabase.Instance.LevelCount;
            PopupNextLevelData data = new(completedLevel, hasNextLevel);

            _openNextLevelPopup = PopupManager.Instance.Show<PopupNextLevel, PopupNextLevelData>(PopupId.NextLevel, data);
            if (_openNextLevelPopup != null)
                _openNextLevelPopup.NextLevelRequested += OnNextLevelRequested;
        }

        private void ShowLosePopup()
        {
            if (PopupManager.Instance == null)
            {
                Debug.LogError($"[{nameof(LevelController)}] Missing {nameof(PopupManager)} instance.", this);
                return;
            }

            PopupManager.Instance.Show(PopupId.Lose);
        }

        private void UnsubscribeFromResultPopup()
        {
            if (_openNextLevelPopup != null)
            {
                _openNextLevelPopup.NextLevelRequested -= OnNextLevelRequested;
                _openNextLevelPopup = null;
            }
        }

        private void OnNextLevelRequested()
        {
            UnsubscribeFromResultPopup();

            if (LevelDatabase.Instance == null)
            {
                Debug.LogError($"[{nameof(LevelController)}] Missing {nameof(LevelDatabase)} instance.", this);
                return;
            }

            int nextLevelIndex = LevelDatabase.Instance.CurrentLevelIndex + 1;
            if (nextLevelIndex >= LevelDatabase.Instance.LevelCount)
                return;

            LevelDatabase.Instance.SetCurrentLevelIndex(nextLevelIndex);
            LoadCurrentLevel();
        }

        /// <summary>
        /// Initial spawn/layout of the passenger queue on level load: snaps every visible passenger
        /// straight to its anchor since nothing is mid-walk yet.
        /// </summary>
        private void RefreshQueueVisuals()
        {
            int visibleCount = Mathf.Min(_passengerQueueView.VisiblePositionCount, _passengerQueue.Passengers.Count);

            for (int i = 0; i < visibleCount; i++)
            {
                Passenger passenger = _passengerQueue.Passengers[i];

                if (_passengerControllersById.TryGetValue(passenger.Id, out PassengerController controller))
                {
                    controller.SnapToQueueSlot(i);
                    continue;
                }

                SpawnPassengerController(passenger, i);
            }
        }

        /// <summary>
        /// Re-lays the queue out after boarding removes a passenger from the front: still-Queued passengers
        /// step toward their shifted slot one adjacent anchor at a time instead of jumping straight there,
        /// and only newly-visible slots spawn a new controller. Passengers already boarding (removed from
        /// _passengerControllersById) are untouched.
        /// </summary>
        private void CompactQueueVisuals()
        {
            int visibleCount = Mathf.Min(_passengerQueueView.VisiblePositionCount, _passengerQueue.Passengers.Count);

            for (int i = 0; i < visibleCount; i++)
            {
                Passenger passenger = _passengerQueue.Passengers[i];

                if (_passengerControllersById.TryGetValue(passenger.Id, out PassengerController controller))
                {
                    controller.MoveToQueueSlot(i);
                    continue;
                }

                SpawnPassengerController(passenger, i);
            }
        }

        private void SpawnPassengerController(Passenger passenger, int slotIndex)
        {
            if (_passengerPrefabLookup == null || !_passengerPrefabLookup.TryGetPrefab(passenger.Color, out PassengerController prefab))
            {
                if (_loggedMissingPrefabColors.Add(passenger.Color))
                    Debug.LogError($"No passenger prefab available for color {passenger.Color}; passenger {passenger.Id} will not be spawned.", this);

                return;
            }

            Transform queueTransform = _passengerQueueView.GetQueueTransform(slotIndex);
            PassengerController controller = Instantiate(prefab, queueTransform.position, queueTransform.rotation, _passengerSpawnRoot);
            controller.Initialize(passenger, _passengerQueueView);
            controller.SnapToQueueSlot(slotIndex);

            _passengerControllersById[passenger.Id] = controller;
            _spawnedGameObjects.Add(controller.gameObject);
        }

        private void ClearLevel()
        {
            foreach (GameObject spawned in _spawnedGameObjects)
            {
                if (spawned != null)
                    Destroy(spawned);
            }

            _spawnedGameObjects.Clear();
            _vehicleControllersById.Clear();
            _vehiclesById.Clear();
            _passengerControllersById.Clear();
            _pendingBoardingByVehicleId.Clear();
            _loggedMissingPrefabColors.Clear();
            _passengerPrefabLookup = null;

            _boardGrid = null;
            _movementResolver = null;
            _waitingAreaManager = null;
            _passengerQueue = null;
            _boardingResolver = null;
        }

        private static IReadOnlyList<Vector2Int> ComputeFootprintCells(Vector2Int originCell, Vector2Int footprintSize)
        {
            List<Vector2Int> cells = new(footprintSize.x * footprintSize.y);

            for (int x = 0; x < footprintSize.x; x++)
            {
                for (int y = 0; y < footprintSize.y; y++)
                {
                    cells.Add(originCell + new Vector2Int(x, y));
                }
            }

            return cells;
        }

        private static Vector2Int GetOrientedFootprintSize(Vector2Int footprintSize, GridDirection movementDirection)
        {
            return movementDirection is GridDirection.Left or GridDirection.Right
                ? new Vector2Int(footprintSize.y, footprintSize.x)
                : footprintSize;
        }

        private Vector3 GetVehicleSpawnPosition(Vector2Int originCell, Vector2Int footprintSize)
        {
            Vector3 footprintCenterOffset = new(
                (footprintSize.x - 1) * _boardView.CellSize * 0.5f,
                0f,
                (footprintSize.y - 1) * _boardView.CellSize * 0.5f);

            return _boardView.CellToWorld(originCell)
                   + _boardView.GridOrigin.rotation * footprintCenterOffset;
        }

        private Quaternion GetVehicleSpawnRotation(GridDirection movementDirection)
        {
            float localAngle = movementDirection switch
            {
                GridDirection.Up => 0f,
                GridDirection.Right => 90f,
                GridDirection.Down => 180f,
                GridDirection.Left => -90f,
                _ => 0f
            };

            return _boardView.GridOrigin.rotation * Quaternion.Euler(0f, localAngle, 0f);
        }
    }
}
