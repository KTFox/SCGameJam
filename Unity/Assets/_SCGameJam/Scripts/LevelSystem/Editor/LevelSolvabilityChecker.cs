using System.Collections.Generic;
using System.Linq;
using System.Text;
using SCJam.Common;
using UnityEngine;

namespace SCJam.LevelSystem.Editor
{
    /// <summary>
    /// Searches for a sequence of vehicle selections that wins a LevelConfig, by simulating the same rules
    /// as gameplay (VehicleMovementResolver.IsPathClear, WaitingAreaManager slot reservation, the
    /// DefaultWaitingVehicleSelector tie-break, BoardingResolver's one-passenger-per-call boarding, and
    /// LevelController's win/lose checks), ported here as plain data over a lightweight SimState so states
    /// can be cloned cheaply during backtracking. The search space branches once per still-parked,
    /// currently-movable vehicle, so it uses depth-first backtracking with visited-state memoization and a
    /// hard node budget so a large level cannot hang the editor; if the budget runs out before a verdict is
    /// reached, the result is reported as inconclusive rather than falsely claiming unsolvable.
    /// </summary>
    public static class LevelSolvabilityChecker
    {
        // ===== Constants ===== //

        private const int DEFAULT_MAX_VISITED_NODES = 400000;


        // ===== Methods ===== //

        public static SolveResult Solve(LevelConfig levelConfig, int maxNodes = DEFAULT_MAX_VISITED_NODES)
        {
            IReadOnlyList<VehiclePlacement> placements = (levelConfig.VehiclePlacements ?? System.Array.Empty<VehiclePlacement>())
                .Where(placement => placement?.VehicleConfig != null)
                .ToList();

            if (placements.Count == 0)
                return SolveResult.Failure("Level has no vehicle placements to solve.");

            if (levelConfig.PassengerColorSequence == null || levelConfig.PassengerColorSequence.Count == 0)
                return SolveResult.Failure("Level has no passenger color sequence to solve.");

            SimState rootState = BuildInitialState(levelConfig, placements);
            rootState.ResolveBoardingToFixpoint();

            if (rootState.IsLost())
                return SolveResult.Failure("Level is stuck before any move can be made (initial waiting area deadlock).");

            if (rootState.IsWon())
                return SolveResult.Success(new List<int>(), new List<string> { "Level starts already won (no vehicles to move)." }, 0);

            HashSet<string> visitedStates = new();
            List<int> movePath = new();
            int visitedNodeCount = 0;

            bool solved = Search(rootState, visitedStates, movePath, maxNodes, ref visitedNodeCount);

            if (solved)
            {
                List<string> stepLog = ReplayMovePathForLog(rootState, movePath);
                return SolveResult.Success(movePath, stepLog, visitedNodeCount);
            }

            if (visitedNodeCount >= maxNodes)
                return SolveResult.Inconclusive(visitedNodeCount);

            return SolveResult.Failure($"No winning vehicle order exists (explored {visitedNodeCount} states).");
        }

        /// <summary>
        /// Re-simulates a known winning move path from scratch to produce a human-readable log of each
        /// step's cause (which vehicle was sent to the waiting area) and effect (which passengers boarded,
        /// which vehicles filled up and departed), without needing to track logs through backtracking.
        /// </summary>
        private static List<string> ReplayMovePathForLog(SimState rootState, IReadOnlyList<int> movePath)
        {
            List<string> stepLog = new();
            SimState state = rootState.Clone();

            stepLog.Add(state.DescribeVehicles());
            List<string> initialBoardingEvents = state.ResolveBoardingToFixpoint();
            foreach (string boardingEvent in initialBoardingEvents)
                stepLog.Add($"  {boardingEvent}");

            for (int step = 0; step < movePath.Count; step++)
            {
                int vehicleId = movePath[step];
                string vehicleDescription = state.DescribeVehicle(vehicleId);
                stepLog.Add($"Step {step + 1}: select vehicle {vehicleDescription} -> exits board, enters waiting area.");

                state.SelectVehicle(vehicleId);
                List<string> boardingEvents = state.ResolveBoardingToFixpoint();
                foreach (string boardingEvent in boardingEvents)
                    stepLog.Add($"  {boardingEvent}");
            }

            stepLog.Add(state.IsWon() ? "Result: all passengers delivered and no vehicle remains parked -> WIN." : "Result: move path ended without reaching the win condition.");

            return stepLog;
        }

        private static SimState BuildInitialState(LevelConfig levelConfig, IReadOnlyList<VehiclePlacement> placements)
        {
            List<SimVehicle> vehicles = new(placements.Count);

            for (int i = 0; i < placements.Count; i++)
            {
                VehiclePlacement placement = placements[i];
                Vector2Int footprintSize = GetOrientedFootprintSize(placement.VehicleConfig.FootprintSize, placement.MovementDirection);
                List<Vector2Int> footprintCells = ComputeFootprintCells(placement.OriginCell, footprintSize);

                vehicles.Add(new SimVehicle(
                    i,
                    placement.VehicleConfig.Color,
                    placement.VehicleConfig.Capacity,
                    footprintCells,
                    placement.MovementDirection));
            }

            List<PuzzleColor> passengerQueue = new(levelConfig.PassengerColorSequence);

            return new SimState(
                levelConfig.BoardSize.x,
                levelConfig.BoardSize.y,
                vehicles,
                passengerQueue,
                levelConfig.WaitingSlotCount);
        }

        private static bool Search(SimState state, HashSet<string> visitedStates, List<int> movePath, int maxNodes, ref int visitedNodeCount)
        {
            if (visitedNodeCount >= maxNodes)
                return false;

            string signature = state.GetSignature();
            if (!visitedStates.Add(signature))
                return false;

            visitedNodeCount++;

            foreach (int vehicleId in state.GetMovableVehicleIds())
            {
                SimState nextState = state.Clone();
                nextState.SelectVehicle(vehicleId);
                nextState.ResolveBoardingToFixpoint();

                if (nextState.IsLost())
                    continue;

                movePath.Add(vehicleId);

                if (nextState.IsWon() || Search(nextState, visitedStates, movePath, maxNodes, ref visitedNodeCount))
                    return true;

                movePath.RemoveAt(movePath.Count - 1);

                if (visitedNodeCount >= maxNodes)
                    return false;
            }

            return false;
        }

        private static Vector2Int GetOrientedFootprintSize(Vector2Int footprintSize, GridDirection movementDirection)
        {
            return movementDirection is GridDirection.Left or GridDirection.Right
                ? new Vector2Int(footprintSize.y, footprintSize.x)
                : footprintSize;
        }

        private static List<Vector2Int> ComputeFootprintCells(Vector2Int originCell, Vector2Int footprintSize)
        {
            List<Vector2Int> cells = new(footprintSize.x * footprintSize.y);
            for (int x = 0; x < footprintSize.x; x++)
                for (int y = 0; y < footprintSize.y; y++)
                    cells.Add(originCell + new Vector2Int(x, y));
            return cells;
        }


        // ===== Nested Types ===== //

        public readonly struct SolveResult
        {
            public bool IsSolved { get; }
            public bool IsInconclusive { get; }
            public IReadOnlyList<int> MoveOrder { get; }
            public IReadOnlyList<string> StepLog { get; }
            public string Message { get; }
            public int VisitedNodeCount { get; }

            private SolveResult(bool isSolved, bool isInconclusive, IReadOnlyList<int> moveOrder, IReadOnlyList<string> stepLog, string message, int visitedNodeCount)
            {
                IsSolved = isSolved;
                IsInconclusive = isInconclusive;
                MoveOrder = moveOrder;
                StepLog = stepLog;
                Message = message;
                VisitedNodeCount = visitedNodeCount;
            }

            public static SolveResult Success(IReadOnlyList<int> moveOrder, IReadOnlyList<string> stepLog, int visitedNodeCount) =>
                new(true, false, moveOrder, stepLog, null, visitedNodeCount);

            public static SolveResult Failure(string message) =>
                new(false, false, null, null, message, 0);

            public static SolveResult Inconclusive(int visitedNodeCount) =>
                new(false, true, null, null, "Search budget exhausted before reaching a verdict.", visitedNodeCount);
        }

        private enum SimVehicleState
        {
            Parked,
            Waiting,
            Full,
            Departed
        }

        private enum SimSlotState
        {
            Available,
            Occupied
        }

        /// <summary>
        /// Mirrors the fields of VehicleSystem.Vehicle that the solver needs, as a mutable value the search
        /// can copy cheaply per branch.
        /// </summary>
        private sealed class SimVehicle
        {
            public int Id { get; }
            public PuzzleColor Color { get; }
            public int Capacity { get; }
            public GridDirection MovementDirection { get; }
            public List<Vector2Int> FootprintCells { get; }
            public SimVehicleState State { get; set; }
            public int OccupiedSeatCount { get; set; }

            public SimVehicle(int id, PuzzleColor color, int capacity, List<Vector2Int> footprintCells, GridDirection movementDirection)
            {
                Id = id;
                Color = color;
                Capacity = capacity;
                MovementDirection = movementDirection;
                FootprintCells = footprintCells;
                State = SimVehicleState.Parked;
            }

            public SimVehicle Clone()
            {
                return new SimVehicle(Id, Color, Capacity, FootprintCells, MovementDirection)
                {
                    State = State,
                    OccupiedSeatCount = OccupiedSeatCount
                };
            }
        }

        /// <summary>
        /// Mirrors WaitingAreaSystem.WaitingSlot: which vehicle (if any) occupies this slot and its arrival
        /// order, used to replicate DefaultWaitingVehicleSelector's earliest-arrived tie-break.
        /// </summary>
        private sealed class SimWaitingSlot
        {
            public int Index { get; }
            public SimSlotState State { get; set; }
            public int? VehicleId { get; set; }
            public int ArrivalOrder { get; set; }

            public SimWaitingSlot(int index)
            {
                Index = index;
                State = SimSlotState.Available;
                ArrivalOrder = -1;
            }

            public SimWaitingSlot Clone()
            {
                return new SimWaitingSlot(Index)
                {
                    State = State,
                    VehicleId = VehicleId,
                    ArrivalOrder = ArrivalOrder
                };
            }
        }

        /// <summary>
        /// One simulated snapshot of board occupancy, vehicle states, the waiting area, and the passenger
        /// queue, plus the same rules gameplay applies to them (path-clear checks, boarding, win/lose).
        /// </summary>
        private sealed class SimState
        {
            private readonly int _boardWidth;
            private readonly int _boardHeight;
            private readonly List<SimVehicle> _vehicles;
            private readonly List<PuzzleColor> _passengerQueue;
            private readonly List<SimWaitingSlot> _waitingSlots;
            private int _nextArrivalOrder;

            public SimState(int boardWidth, int boardHeight, List<SimVehicle> vehicles, List<PuzzleColor> passengerQueue, int waitingSlotCount)
            {
                _boardWidth = boardWidth;
                _boardHeight = boardHeight;
                _vehicles = vehicles;
                _passengerQueue = passengerQueue;
                _waitingSlots = new List<SimWaitingSlot>(waitingSlotCount);

                for (int i = 0; i < waitingSlotCount; i++)
                    _waitingSlots.Add(new SimWaitingSlot(i));
            }

            private SimState(int boardWidth, int boardHeight, List<SimVehicle> vehicles, List<PuzzleColor> passengerQueue, List<SimWaitingSlot> waitingSlots, int nextArrivalOrder)
            {
                _boardWidth = boardWidth;
                _boardHeight = boardHeight;
                _vehicles = vehicles;
                _passengerQueue = passengerQueue;
                _waitingSlots = waitingSlots;
                _nextArrivalOrder = nextArrivalOrder;
            }

            public IEnumerable<int> GetMovableVehicleIds()
            {
                if (!HasAvailableWaitingSlot())
                    yield break;

                foreach (SimVehicle vehicle in _vehicles)
                {
                    if (vehicle.State == SimVehicleState.Parked && IsPathClear(vehicle))
                        yield return vehicle.Id;
                }
            }

            /// <summary>
            /// Mirrors VehicleMovementResolver.IsPathClear: every cell swept from the vehicle's footprint to
            /// the board boundary along its fixed MovementDirection must be free of every other parked
            /// vehicle's footprint.
            /// </summary>
            private bool IsPathClear(SimVehicle vehicle)
            {
                HashSet<Vector2Int> occupiedCells = new();
                foreach (SimVehicle other in _vehicles)
                {
                    if (other.Id == vehicle.Id || other.State != SimVehicleState.Parked)
                        continue;

                    foreach (Vector2Int cell in other.FootprintCells)
                        occupiedCells.Add(cell);
                }

                Vector2Int step = GetStep(vehicle.MovementDirection);
                foreach (Vector2Int footprintCell in vehicle.FootprintCells)
                {
                    Vector2Int current = footprintCell + step;
                    while (IsCellInBounds(current))
                    {
                        if (occupiedCells.Contains(current))
                            return false;

                        current += step;
                    }
                }

                return true;
            }

            public void SelectVehicle(int vehicleId)
            {
                SimVehicle vehicle = _vehicles.First(candidate => candidate.Id == vehicleId);
                SimWaitingSlot slot = _waitingSlots.FirstOrDefault(candidate => candidate.State == SimSlotState.Available);
                if (slot == null)
                    return;

                vehicle.State = SimVehicleState.Waiting;
                slot.State = SimSlotState.Occupied;
                slot.VehicleId = vehicle.Id;
                slot.ArrivalOrder = _nextArrivalOrder++;
            }

            /// <summary>
            /// Mirrors LevelController's per-frame TryMatchWaitingVehicleToFrontGroup + ProcessBoardingCompletions
            /// + ProcessFullVehicleDepartures loop, run to a fixpoint: repeatedly match the front passenger
            /// group to the highest-priority eligible waiting vehicle, board exactly one passenger (matching
            /// BoardingResolver.TryBoard's one-passenger-per-call semantics), settle the vehicle to
            /// Full/Waiting, and depart it (freeing its slot) once Full.
            /// </summary>
            public List<string> ResolveBoardingToFixpoint()
            {
                List<string> events = new();
                bool progressed = true;

                while (progressed)
                {
                    progressed = false;

                    if (_passengerQueue.Count == 0)
                        break;

                    PuzzleColor frontColor = _passengerQueue[0];
                    SimVehicle bestVehicle = SelectBestWaitingVehicle(frontColor, out SimWaitingSlot bestSlot);
                    if (bestVehicle == null)
                        break;

                    _passengerQueue.RemoveAt(0);
                    bestVehicle.OccupiedSeatCount++;
                    progressed = true;

                    events.Add($"passenger ({frontColor}) boards vehicle {DescribeVehicle(bestVehicle.Id)} ({bestVehicle.OccupiedSeatCount}/{bestVehicle.Capacity}).");

                    if (bestVehicle.OccupiedSeatCount >= bestVehicle.Capacity)
                    {
                        bestVehicle.State = SimVehicleState.Departed;
                        bestSlot.State = SimSlotState.Available;
                        bestSlot.VehicleId = null;
                        bestSlot.ArrivalOrder = -1;

                        events.Add($"vehicle {DescribeVehicle(bestVehicle.Id)} is full, departs waiting slot {bestSlot.Index}.");
                    }
                }

                return events;
            }

            public string DescribeVehicle(int vehicleId)
            {
                SimVehicle vehicle = _vehicles.First(candidate => candidate.Id == vehicleId);
                return $"#{vehicle.Id} ({vehicle.Color}, capacity {vehicle.Capacity})";
            }

            public string DescribeVehicles()
            {
                return $"Initial state: {_vehicles.Count} vehicles, {_passengerQueue.Count} passengers queued, {_waitingSlots.Count} waiting slots.";
            }

            /// <summary>
            /// Mirrors DefaultWaitingVehicleSelector: among Occupied-slot vehicles matching color with free
            /// capacity, highest OccupiedSeatCount wins, then lowest ArrivalOrder, then lowest slot Index.
            /// </summary>
            private SimVehicle SelectBestWaitingVehicle(PuzzleColor color, out SimWaitingSlot bestSlot)
            {
                SimVehicle best = null;
                bestSlot = null;

                foreach (SimWaitingSlot slot in _waitingSlots)
                {
                    if (slot.State != SimSlotState.Occupied || slot.VehicleId == null)
                        continue;

                    SimVehicle vehicle = _vehicles.First(candidate => candidate.Id == slot.VehicleId.Value);
                    if (vehicle.Color != color || vehicle.OccupiedSeatCount >= vehicle.Capacity)
                        continue;

                    if (best == null || IsHigherPriority(vehicle, slot, best, bestSlot))
                    {
                        best = vehicle;
                        bestSlot = slot;
                    }
                }

                return best;
            }

            private static bool IsHigherPriority(SimVehicle candidate, SimWaitingSlot candidateSlot, SimVehicle current, SimWaitingSlot currentSlot)
            {
                if (candidate.OccupiedSeatCount != current.OccupiedSeatCount)
                    return candidate.OccupiedSeatCount > current.OccupiedSeatCount;

                if (candidateSlot.ArrivalOrder != currentSlot.ArrivalOrder)
                    return candidateSlot.ArrivalOrder < currentSlot.ArrivalOrder;

                return candidateSlot.Index < currentSlot.Index;
            }

            public bool IsWon()
            {
                if (_passengerQueue.Count > 0)
                    return false;

                foreach (SimVehicle vehicle in _vehicles)
                {
                    if (vehicle.State == SimVehicleState.Parked)
                        return false;
                }

                return true;
            }

            public bool IsLost()
            {
                if (_passengerQueue.Count == 0)
                    return false;

                if (!AreAllWaitingSlotsOccupied())
                    return false;

                PuzzleColor frontColor = _passengerQueue[0];
                return !HasWaitingVehicleOfColor(frontColor);
            }

            public SimState Clone()
            {
                List<SimVehicle> clonedVehicles = _vehicles.Select(vehicle => vehicle.Clone()).ToList();
                List<PuzzleColor> clonedQueue = new(_passengerQueue);
                List<SimWaitingSlot> clonedSlots = _waitingSlots.Select(slot => slot.Clone()).ToList();

                return new SimState(_boardWidth, _boardHeight, clonedVehicles, clonedQueue, clonedSlots, _nextArrivalOrder);
            }

            /// <summary>
            /// Captures everything that affects future outcomes: each vehicle's state and seat count, and
            /// the remaining passenger queue. Waiting-slot identity/arrival order is intentionally excluded
            /// since it only breaks ties among equally-progressed vehicles and does not change reachability.
            /// </summary>
            public string GetSignature()
            {
                StringBuilder builder = new();

                foreach (SimVehicle vehicle in _vehicles.OrderBy(vehicle => vehicle.Id))
                    builder.Append(vehicle.Id).Append(':').Append((int)vehicle.State).Append(':').Append(vehicle.OccupiedSeatCount).Append('|');

                foreach (PuzzleColor color in _passengerQueue)
                    builder.Append((int)color).Append(',');

                return builder.ToString();
            }

            private bool HasAvailableWaitingSlot()
            {
                foreach (SimWaitingSlot slot in _waitingSlots)
                {
                    if (slot.State == SimSlotState.Available)
                        return true;
                }

                return false;
            }

            private bool AreAllWaitingSlotsOccupied()
            {
                foreach (SimWaitingSlot slot in _waitingSlots)
                {
                    if (slot.State != SimSlotState.Occupied)
                        return false;
                }

                return true;
            }

            private bool HasWaitingVehicleOfColor(PuzzleColor color)
            {
                foreach (SimWaitingSlot slot in _waitingSlots)
                {
                    if (slot.State != SimSlotState.Occupied || slot.VehicleId == null)
                        continue;

                    SimVehicle vehicle = _vehicles.First(candidate => candidate.Id == slot.VehicleId.Value);
                    if (vehicle.Color == color)
                        return true;
                }

                return false;
            }

            private bool IsCellInBounds(Vector2Int cell)
            {
                return cell.x >= 0 && cell.x < _boardWidth && cell.y >= 0 && cell.y < _boardHeight;
            }

            private static Vector2Int GetStep(GridDirection direction)
            {
                return direction switch
                {
                    GridDirection.Up => new Vector2Int(0, 1),
                    GridDirection.Down => new Vector2Int(0, -1),
                    GridDirection.Left => new Vector2Int(-1, 0),
                    GridDirection.Right => new Vector2Int(1, 0),
                    _ => Vector2Int.zero
                };
            }
        }
    }
}
