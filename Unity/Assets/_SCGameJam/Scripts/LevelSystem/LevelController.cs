using System;
using System.Collections.Generic;
using SCJam.BoardSystem;
using SCJam.Common;
using SCJam.PassengerSystem;
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
        [SerializeField] private PassengerController _passengerPrefab;


        // ===== Private Fields ===== //

        private readonly IWaitingVehicleSelector _waitingVehicleSelector = new DefaultWaitingVehicleSelector();
        private readonly Dictionary<int, Vehicle> _vehiclesById = new();
        private readonly Dictionary<int, VehicleController> _vehicleControllersById = new();
        private readonly Dictionary<int, PassengerController> _passengerControllersById = new();
        private readonly Dictionary<int, List<Passenger>> _pendingBoardingByVehicleId = new();
        private readonly List<GameObject> _spawnedGameObjects = new();

        private LevelState _levelState;
        private BoardGrid _boardGrid;
        private VehicleMovementResolver _movementResolver;
        private WaitingAreaManager _waitingAreaManager;
        private PassengerQueue _passengerQueue;
        private BoardingResolver _boardingResolver;


        // ===== Public Properties ===== //

        public LevelState State => _levelState;


        // ===== Events ===== //

        public event Action OnLevelCompleted;


        // ===== Methods ===== //

        public void LoadLevel(LevelConfig levelConfig)
        {
            if (levelConfig == null)
                return;

            ClearLevel();
            _levelState = LevelState.Loading;

            BuildBoard(levelConfig);
            //SpawnVehicles(levelConfig);
            //BuildPassengerQueue(levelConfig);
            //RefreshQueueVisuals();

            _levelState = LevelState.Playing;
        }

        private void Update()
        {
            if (_levelState != LevelState.Playing)
                return;

            //TryMatchWaitingVehicleToFrontGroup();
            //ProcessBoardingCompletions();
            //ProcessFullVehicleDepartures();
            //EvaluateWinCondition();
        }

        private void BuildBoard(LevelConfig levelConfig)
        {
            _boardGrid = new BoardGrid(levelConfig.BoardWidth, levelConfig.BoardHeight);
            _movementResolver = new VehicleMovementResolver(_boardGrid);
            _waitingAreaManager = new WaitingAreaManager(levelConfig.WaitingSlotCount);

            _boardView.Initialize(_boardGrid);
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

                IReadOnlyList<Vector2Int> footprintCells = ComputeFootprintCells(placement.OriginCell, placement.VehicleConfig.FootprintSize);
                Vehicle vehicle = new(i, placement.VehicleConfig.Color, placement.VehicleConfig.Capacity, footprintCells, placement.MovementDirection);

                _boardGrid.PlaceVehicle(vehicle.Id, footprintCells);
                _vehiclesById[vehicle.Id] = vehicle;

                if (placement.VehicleConfig.Prefab == null || _boardView == null)
                    continue;

                Vector3 spawnPosition = _boardView.CellToWorld(placement.OriginCell);
                GameObject instance = Instantiate(placement.VehicleConfig.Prefab, spawnPosition, Quaternion.identity, _vehicleSpawnRoot);
                if (!instance.TryGetComponent(out VehicleController controller))
                    controller = instance.AddComponent<VehicleController>();

                controller.Initialize(vehicle, _boardGrid, _boardView, _movementResolver, _waitingAreaManager, _waitingAreaView);
                _vehicleControllersById[vehicle.Id] = controller;
                _spawnedGameObjects.Add(instance);
            }
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

            List<WaitingVehicleEntry> entries = BuildWaitingVehicleEntries();
            if (entries.Count == 0)
                return;

            Vehicle selectedVehicle = _waitingVehicleSelector.SelectVehicle(entries, frontGroup[0].Color);
            if (selectedVehicle == null)
                return;

            IReadOnlyList<Passenger> boardedPassengers = _boardingResolver.TryBoard(selectedVehicle);
            if (boardedPassengers.Count == 0)
                return;

            Vector3 boardingPosition = _vehicleControllersById[selectedVehicle.Id].transform.position;
            List<Passenger> pending = new(boardedPassengers.Count);

            foreach (Passenger passenger in boardedPassengers)
            {
                pending.Add(passenger);

                if (_passengerControllersById.TryGetValue(passenger.Id, out PassengerController passengerController))
                {
                    _passengerControllersById.Remove(passenger.Id);
                    passengerController.MoveToVehicle(boardingPosition);
                }
                else
                {
                    // No spawned view for this passenger (prefab not wired yet) — nothing to animate.
                    passenger.ChangeState(PassengerState.Completed);
                }
            }

            _pendingBoardingByVehicleId[selectedVehicle.Id] = pending;
            RefreshQueueVisuals();
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
                _boardingResolver.CompleteBoarding(_vehiclesById[vehicleId]);
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
            OnLevelCompleted?.Invoke();
        }

        private void RefreshQueueVisuals()
        {
            if (_passengerPrefab == null || _passengerQueueView == null)
                return;

            int visibleCount = Mathf.Min(_passengerQueueView.VisiblePositionCount, _passengerQueue.Passengers.Count);

            for (int i = 0; i < visibleCount; i++)
            {
                Passenger passenger = _passengerQueue.Passengers[i];
                Vector3 position = _passengerQueueView.GetQueueWorldPosition(i);

                if (_passengerControllersById.TryGetValue(passenger.Id, out PassengerController controller))
                {
                    controller.transform.position = position;
                    continue;
                }

                controller = Instantiate(_passengerPrefab, position, Quaternion.identity, _passengerSpawnRoot);
                controller.Initialize(passenger);
                _passengerControllersById[passenger.Id] = controller;
                _spawnedGameObjects.Add(controller.gameObject);
            }
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
    }
}
