using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
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
        // ===== Constants ===== //

        /// <summary>
        /// Level index (0-based) that still shows the guide-finger hint automatically on load as a tutorial.
        /// Every later level requires the player to spend a hint booster charge instead.
        /// </summary>
        private const int TUTORIAL_LEVEL_INDEX = 0;


        // ===== Serialized Fields ===== //

        [SerializeField] private BoardView _boardView;
        [SerializeField] private WaitingAreaView _waitingAreaView;
        [SerializeField] private PassengerQueueView _passengerQueueView;
        [SerializeField] private Transform _vehicleSpawnRoot;
        [SerializeField] private Transform _passengerSpawnRoot;
        [SerializeField] private SoundSO _backgroundMusic;
        [SerializeField] private SoundSO _winSound;
        [SerializeField] private SoundSO _loseSound;
        [SerializeField] private VehicleSelectionController _vehicleSelectionController;
        [SerializeField] private GuideFingerController _guideFingerController;
        [SerializeField] private PlayerHandSimulator _playerHandSimulator;
        [SerializeField] private float _queueSpawnStaggerDelay;
        [SerializeField] private float _nextLevelPopupDelay;
        [SerializeField] private int _addWaitingSlotCharges;
        [SerializeField] private int _hintCharges;


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
        private PopupLose _openLosePopup;
        private PopupSetting _openSettingPopup;
        private VehicleController _hintedVehicleController;
        private CancellationTokenSource _queueSpawnCancellationSource;
        private CancellationToken _destroyCancellationToken;

        private int _remainingAddWaitingSlotCharges;
        private bool _hasInitializedWaitingSlotCharges;
        private int _remainingHintCharges;
        private bool _hasInitializedHintCharges;


        // ===== Public Properties ===== //

        public LevelState State => _levelState;

        /// <summary>
        /// Passengers still waiting in the queue for the current level (those not yet matched to a vehicle).
        /// Drops by one each time a passenger is dequeued to board, and reaches zero on a win.
        /// </summary>
        public int RemainingPassengerCount => _passengerQueue?.Passengers.Count ?? 0;

        /// <summary>
        /// "Add waiting slot" booster uses left. Seeded once from the serialized starting amount and carried
        /// over between levels (not reset on level load / retry). Zero once every charge has been spent, or
        /// once the waiting area has grown to the view's maximum slot count.
        /// </summary>
        public int RemainingAddWaitingSlotCharges => _remainingAddWaitingSlotCharges;

        public bool CanAddWaitingSlot =>
            _remainingAddWaitingSlotCharges > 0
            && _waitingAreaManager != null
            && _waitingAreaManager.Slots.Count < _waitingAreaView.SlotCount;

        /// <summary>
        /// Hint booster uses left. Seeded once from the serialized starting amount and carried over between
        /// levels (not reset on level load / retry). The tutorial level's automatic hint does not consume a
        /// charge.
        /// </summary>
        public int RemainingHintCharges => _remainingHintCharges;

        public bool CanShowHint => _remainingHintCharges > 0 && _levelState == LevelState.Playing;


        // ===== Events ===== //

        public event Action OnLevelCompleted;
        public event Action OnLevelFailed;
        public event Action<int> RemainingPassengerCountChanged;
        public event Action<int> AddWaitingSlotChargesChanged;
        public event Action<int> HintChargesChanged;


        // ===== Unity Lifecycle Methods ===== //

        private void Awake()
        {
            _destroyCancellationToken = this.GetCancellationTokenOnDestroy();
        }

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

        private void OnEnable()
        {
            PopupManager.PopupOpened += OnPopupOpened;

            if (_vehicleSelectionController != null)
                _vehicleSelectionController.VehicleSelected += OnVehicleSelected;
        }

        private void OnDisable()
        {
            PopupManager.PopupOpened -= OnPopupOpened;
            UnsubscribeFromResultPopup();
            UnsubscribeFromSettingPopup();

            if (_vehicleSelectionController != null)
                _vehicleSelectionController.VehicleSelected -= OnVehicleSelected;

            _queueSpawnCancellationSource?.Cancel();
            _queueSpawnCancellationSource?.Dispose();
            _queueSpawnCancellationSource = null;
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
            RemainingPassengerCountChanged?.Invoke(RemainingPassengerCount);

            _levelState = LevelState.Playing;

            // Booster-charge listeners are notified only after the level is in Playing state, since their
            // "can use" checks gate on it — firing earlier would latch the buttons disabled for the level.
            AddWaitingSlotChargesChanged?.Invoke(_remainingAddWaitingSlotCharges);
            HintChargesChanged?.Invoke(_remainingHintCharges);

            AudioManager.Instance?.PlayMusic(_backgroundMusic);

            LoadLevelQueueRoutine().Forget(HandleQueueSpawnException);
        }

        /// <summary>
        /// Spends one "add waiting slot" charge to unlock one more waiting slot for the rest of the run.
        /// Returns false (and changes nothing) when no charges remain or the waiting area is already at the
        /// view's maximum slot count.
        /// </summary>
        public bool TryAddWaitingSlot()
        {
            if (!CanAddWaitingSlot)
                return false;

            _remainingAddWaitingSlotCharges--;

            _waitingAreaManager.AddSlot();
            _waitingAreaView.ApplyActiveSlotCount(_waitingAreaManager.Slots.Count, animate: true);

            AddWaitingSlotChargesChanged?.Invoke(_remainingAddWaitingSlotCharges);
            return true;
        }

        /// <summary>
        /// Spends one hint booster charge to show the guide-finger hint at the vehicle the player should tap
        /// next. Returns false (and changes nothing) when no charges remain, the level is not in play, or
        /// there is currently no solvable vehicle to point at.
        /// </summary>
        public bool TryShowHint()
        {
            if (!CanShowHint)
                return false;

            if (!UpdateGuideFinger())
                return false;

            _remainingHintCharges--;
            HintChargesChanged?.Invoke(_remainingHintCharges);
            return true;
        }

        private void InitializeBoosterChargesOnce()
        {
            if (!_hasInitializedWaitingSlotCharges)
            {
                _remainingAddWaitingSlotCharges = Mathf.Max(0, _addWaitingSlotCharges);
                _hasInitializedWaitingSlotCharges = true;
            }

            if (!_hasInitializedHintCharges)
            {
                _remainingHintCharges = Mathf.Max(0, _hintCharges);
                _hasInitializedHintCharges = true;
            }
        }

        private void BuildBoard(LevelConfig levelConfig)
        {
            _boardGrid = new BoardGrid(levelConfig.BoardSize.x, levelConfig.BoardSize.y);
            _movementResolver = new VehicleMovementResolver(_boardGrid);

            InitializeBoosterChargesOnce();

            // Each level starts from its own configured slot count; booster-added slots do not carry over
            // between levels (the remaining booster charges do — they are seeded once above).
            int activeSlotCount = levelConfig.WaitingSlotCount;

            _waitingAreaManager = new WaitingAreaManager(activeSlotCount);

            _boardView.Initialize(_boardGrid);
            _waitingAreaView.ApplyActiveSlotCount(activeSlotCount);
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

        /// <summary>
        /// Points the guide finger at the parked vehicle the player should tap next, i.e. the one matching
        /// the front passenger group's color with a clear path to the exit. Shown automatically on the
        /// tutorial level and on demand via the hint booster on every later level; hidden again as soon as
        /// the player taps any vehicle. Returns true when a target was found and the hint is now visible.
        /// </summary>
        private bool UpdateGuideFinger()
        {
            SetHintedVehicle(null);

            if (_guideFingerController == null)
                return false;

            VehicleController targetVehicleController = FindSolvableVehicleController();
            if (targetVehicleController == null)
            {
                _guideFingerController.Hide();
                return false;
            }

            _guideFingerController.Show(targetVehicleController.transform);
            SetHintedVehicle(targetVehicleController);
            return true;
        }

        private void SetHintedVehicle(VehicleController vehicleController)
        {
            if (_hintedVehicleController == vehicleController)
                return;

            if (_hintedVehicleController != null)
                _hintedVehicleController.SetHintOutlineEnabled(false);

            _hintedVehicleController = vehicleController;

            if (_hintedVehicleController != null)
                _hintedVehicleController.SetHintOutlineEnabled(true);
        }

        private VehicleController FindSolvableVehicleController()
        {
            IReadOnlyList<Passenger> frontGroup = _passengerQueue.GetAccessibleFrontGroup();
            if (frontGroup.Count == 0)
                return null;

            PuzzleColor frontColor = frontGroup[0].Color;

            foreach (Vehicle vehicle in _vehiclesById.Values)
            {
                if (vehicle.State != VehicleState.Parked || vehicle.Color != frontColor)
                    continue;

                if (!_movementResolver.IsPathClear(vehicle))
                    continue;

                if (_vehicleControllersById.TryGetValue(vehicle.Id, out VehicleController controller))
                    return controller;
            }

            return null;
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

            RemainingPassengerCountChanged?.Invoke(RemainingPassengerCount);

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

            AudioManager.Instance?.StopMusic();
            AudioManager.Instance?.PlaySound(_winSound);
            OnLevelCompleted?.Invoke();
            ShowNextLevelPopupRoutine().Forget(HandleQueueSpawnException);
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

            AudioManager.Instance?.StopMusic();
            AudioManager.Instance?.PlaySound(_loseSound);
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

        private async UniTask ShowNextLevelPopupRoutine()
        {
            if (_nextLevelPopupDelay > 0f)
                await UniTask.Delay(TimeSpan.FromSeconds(_nextLevelPopupDelay), cancellationToken: _destroyCancellationToken);

            ShowNextLevelPopup();
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

            _openLosePopup = PopupManager.Instance.Show<PopupLose>(PopupId.Lose);
            if (_openLosePopup != null)
            {
                _openLosePopup.RetryRequested += OnRetryRequested;
                _openLosePopup.QuitRequested += OnQuitRequested;
            }
        }

        private void UnsubscribeFromResultPopup()
        {
            if (_openNextLevelPopup != null)
            {
                _openNextLevelPopup.NextLevelRequested -= OnNextLevelRequested;
                _openNextLevelPopup = null;
            }

            if (_openLosePopup != null)
            {
                _openLosePopup.RetryRequested -= OnRetryRequested;
                _openLosePopup.QuitRequested -= OnQuitRequested;
                _openLosePopup = null;
            }
        }

        private void UnsubscribeFromSettingPopup()
        {
            if (_openSettingPopup == null)
                return;

            _openSettingPopup.RetryRequested -= OnRetryRequested;
            _openSettingPopup.QuitRequested -= OnQuitRequested;
            _openSettingPopup = null;
        }

        private void OnPopupOpened(PopupId popupId)
        {
            if (popupId != PopupId.Setting)
                return;

            if (!PopupManager.Instance.TryGetPopup(PopupId.Setting, out PopupBase popup) || popup is not PopupSetting settingPopup)
                return;

            _openSettingPopup = settingPopup;
            _openSettingPopup.RetryRequested += OnRetryRequested;
            _openSettingPopup.QuitRequested += OnQuitRequested;
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

        private void OnRetryRequested()
        {
            UnsubscribeFromResultPopup();
            UnsubscribeFromSettingPopup();
            LoadCurrentLevel();
        }

        private void OnQuitRequested()
        {
            UnsubscribeFromResultPopup();
            UnsubscribeFromSettingPopup();

#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void OnVehicleSelected(VehicleController vehicleController)
        {
            _guideFingerController?.Hide();
            SetHintedVehicle(null);
        }

        /// <summary>
        /// Initial spawn/layout of the passenger queue on level load: passengers spawn one at a time at the
        /// back-most (last) queue pivot and walk forward into their target slot, instead of appearing
        /// instantly at their final anchor. Boarding naturally waits for the front slot to settle via
        /// PassengerController.IsSettledAtQueueFront, so no extra gating is needed here. On the tutorial
        /// level the guide finger hint is shown automatically once this spawn-in finishes (so it never
        /// appears while passengers are still walking in); later levels leave it hidden until the player
        /// spends a hint booster charge.
        /// </summary>
        private async UniTask LoadLevelQueueRoutine()
        {
            int visibleCount = Mathf.Min(_passengerQueueView.VisiblePositionCount, _passengerQueue.Passengers.Count);

            _queueSpawnCancellationSource?.Cancel();
            _queueSpawnCancellationSource?.Dispose();
            _queueSpawnCancellationSource = new CancellationTokenSource();

            await SpawnQueueRoutine(visibleCount, _queueSpawnCancellationSource.Token);

            if (IsTutorialLevel())
                UpdateGuideFinger();
        }

        private static bool IsTutorialLevel()
        {
            return LevelDatabase.Instance != null && LevelDatabase.Instance.CurrentLevelIndex == TUTORIAL_LEVEL_INDEX;
        }

        private async UniTask SpawnQueueRoutine(int visibleCount, CancellationToken cancellationToken)
        {
            int lastPivotIndex = _passengerQueueView.VisiblePositionCount - 1;

            for (int i = 0; i < visibleCount; i++)
            {
                Passenger passenger = _passengerQueue.Passengers[i];

                if (_passengerControllersById.TryGetValue(passenger.Id, out PassengerController controller))
                {
                    controller.SnapToQueueSlot(i);
                }
                else
                {
                    controller = SpawnPassengerController(passenger, lastPivotIndex);
                    if (controller != null)
                        controller.MoveToQueueSlot(i);
                }

                if (i < visibleCount - 1 && _queueSpawnStaggerDelay > 0f)
                    await UniTask.Delay(TimeSpan.FromSeconds(_queueSpawnStaggerDelay), cancellationToken: cancellationToken);
            }
        }

        private static void HandleQueueSpawnException(Exception exception)
        {
            if (exception is not OperationCanceledException)
                Debug.LogException(exception);
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

        private PassengerController SpawnPassengerController(Passenger passenger, int slotIndex)
        {
            if (_passengerPrefabLookup == null || !_passengerPrefabLookup.TryGetPrefab(passenger.Color, out PassengerController prefab))
            {
                if (_loggedMissingPrefabColors.Add(passenger.Color))
                    Debug.LogError($"No passenger prefab available for color {passenger.Color}; passenger {passenger.Id} will not be spawned.", this);

                return null;
            }

            Transform queueTransform = _passengerQueueView.GetQueueTransform(slotIndex);
            PassengerController controller = Instantiate(prefab, queueTransform.position, queueTransform.rotation, _passengerSpawnRoot);
            controller.Initialize(passenger, _passengerQueueView);
            controller.SnapToQueueSlot(slotIndex);

            _passengerControllersById[passenger.Id] = controller;
            _spawnedGameObjects.Add(controller.gameObject);

            return controller;
        }

        private void ClearLevel()
        {
            _playerHandSimulator?.Hide();
            _guideFingerController?.Hide();
            SetHintedVehicle(null);

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
            _hintedVehicleController = null;

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
