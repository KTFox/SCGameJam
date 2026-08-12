# Development Plan

Based on `Overview.md`, `CoreEntities.md`, `WaitingSlot.md`, `VehicleMovement.md`, and the project's `CLAUDE.md` conventions.

## Confirmed Decisions

| Question | Decision |
|---|---|
| Board representation | Rectangular logical grid (`Vector2Int` cells), square is the `Width == Height` case. Recommended by the GDD for authoring/collision/solvability testing. |
| AI asset-authoring boundary | AI may create `.asmdef` files and ScriptableObject `.asset` instances (VehicleConfig, LevelConfig) in addition to `.cs` files. Scenes/prefabs/materials/etc. remain off-limits. |
| Test assemblies | Introduce `.asmdef`s in Milestone 0 so pure-logic classes are unit-testable via Unity Test Framework (installed, currently unused). |
| Exit boundaries | Single fixed exit per level (not full multi-directional). |
| Loss condition | None — pure puzzle, no timer/move limit, no `LevelFailed` state. |
| Waiting-slot release timing | Footprint-clear rule: a board cell/slot is released once the vehicle's footprint has fully left that space. Deterministic, no animation-event coupling. |
| Vehicle footprint | Authored explicitly per `VehicleConfig` (a `Vector2Int` size), not derived from capacity. Suggested defaults: 4-slot → 1×2, 6-slot → 1×3 — confirm against actual prefab bounds when authoring assets. |
| Obstacles | Static, immovable collision blockers only for now. Interactive/removable obstacles are stretch scope (M7). |
| Boosters | Not built this pass. Architecture should stay extensible (see `IWaitingVehicleSelector` pattern in M4) but boosters are explicitly out of committed scope. |

## Namespace / Folder Layout

Sibling folders under `Assets/_SCGameJam/Scripts/`, matching the existing flat convention (`AudioSystem`, `InputSystem` are siblings, not nested):

```
Scripts/
  Common/              SCJam.Common            PuzzleColor, GridDirection (shared, zero deps)
  BoardSystem/         SCJam.BoardSystem        grid, obstacles, exit
  VehicleSystem/       SCJam.VehicleSystem      vehicle logic + movement + view
  WaitingAreaSystem/   SCJam.WaitingAreaSystem  slots + priority matching
  PassengerSystem/     SCJam.PassengerSystem    queue + boarding matching
  LevelSystem/         SCJam.LevelSystem        LevelConfig, LevelController (currently empty folder)
  Core/                SCJam.Core               GameManager, MonoSingleton (existing)
  InputSystem/         SCJam.InputSystem        unchanged, raw touch events only
  AudioSystem/         SCJam.AudioSystem        unchanged
  Tests/                SCJam.Runtime.Tests     EditMode unit tests
```

`PuzzleColor` enum (not `Color`, avoids clashing with `UnityEngine.Color`) seeds from the existing prefab variants: `Orange, Pink, White, Yellow`. `GridDirection { Up, Down, Left, Right }`. Grid coordinates use `Vector2Int` directly.

## Integration With Existing Infrastructure

- **`InputManager`** stays untouched. A new `VehicleSelectionController` (MonoBehaviour, `SCJam.VehicleSystem`) subscribes to its existing `OnTouchPerformed`, raycasts (3D — car prefabs use mesh colliders) to resolve the tapped vehicle, and forwards a move-intent.
- **`GameManager`** stays a `MonoSingleton<GameManager>` but grows a small `GameState` enum (`Boot, MainMenu, Loading, Playing, Paused, LevelComplete`) and delegates board/vehicle/passenger logic to a new `LevelController` — it does not own gameplay state itself.
- **`LevelController` is NOT a `MonoSingleton<T>`** — it's a single scene-resident orchestrator with direct serialized references to its child systems. Levels swap via `LevelController.LoadLevel(LevelConfig)` (data-driven, no scene reload), so there's no lifecycle problem a singleton would solve.
- **UniTask** (installed, unused) drives the vehicle movement pipeline (validate path → tween → release cells → reserve slot → tween into slot → board) since it's a multi-step async chain that benefits from cancellation/composability more than coroutines. Existing simple coroutine usage (`AudioManager`) is left as-is.
- **DOTween** used for movement/board tweens, awaited via UniTask. **AI Navigation/Cinemachine** intentionally not used — movement is a fixed-direction grid sweep, not pathfinding; camera work is deferred polish.

## Milestones

### M0 — Foundation (infra only) ✅
- `Scripts/SCJam.Runtime.asmdef`, `Scripts/Tests/SCJam.Runtime.Tests.asmdef` (EditMode, references Runtime + TestRunner)
- `Common/PuzzleColor.cs`, `Common/GridDirection.cs`

### M1 — Board & Grid (pure logic, unit-testable, not playable) ✅
- `BoardSystem/BoardGrid.cs` — occupancy map, `IsCellInBounds`, `IsCellOccupied(cell, excludingVehicleId)`, `IsCellBlocked`, `GetCellsToBoundary`, `PlaceVehicle`/`RemoveVehicle`
- `BoardSystem/ParkingBoardData.cs` — Width, Height, blocked cells, single exit direction
- `BoardSystem/BoardView.cs` (MonoBehaviour, presentation — grid↔world conversion, tile/obstacle visuals)
- Tests: occupancy, bounds, obstacle blocking, exit-direction checks

### M2 — Vehicle Movement, Collision & Waiting-Slot Reservation (first playable increment) ✅
- `VehicleSystem/Vehicle.cs` — Id, Color, Capacity, OccupiedSeatCount, GridFootprint, MovementDirection, State
- `VehicleSystem/VehicleState.cs` (enum: Parked, MovingToExit, Waiting, Boarding, Full, Departing, Completed)
- `VehicleSystem/VehicleMovementResolver.cs` — `IsPathClear`/sweep-to-boundary per `VehicleMovement.md`, excludes self
- `VehicleSystem/VehicleConfig.cs` (ScriptableObject) — Color, Capacity, footprint size, prefab reference
- `VehicleSystem/VehicleController.cs` (MonoBehaviour view — UniTask+DOTween movement, selection collider)
- `VehicleSystem/VehicleSelectionController.cs` (MonoBehaviour — tap-to-move)
- `WaitingAreaSystem/WaitingSlot.cs`, `WaitingSlotState.cs` (Available, Reserved, Occupied)
- `WaitingAreaSystem/WaitingAreaManager.cs` — `TryReserveSlot` (must succeed before `MovingToExit`), `ConfirmOccupied`, `ReleaseSlot`; first-available only (priority logic in M4)
- `WaitingAreaSystem/WaitingAreaView.cs` (MonoBehaviour view)
- Rule: a vehicle's old board cells stay occupied through its exit animation, released only once its footprint fully clears the board (footprint-clear rule applied consistently)
- Human work required: place car prefabs + `VehicleConfig` assets in scene, wire `BoardView`/`WaitingAreaView`

### M3 — Passenger Queue & Basic Boarding (playable increment 2) ✅
- `PassengerSystem/Passenger.cs`, `PassengerState.cs` (Queued, MovingToVehicle, Completed)
- `PassengerSystem/PassengerQueue.cs` — `GetAccessibleFrontGroup()` (grouped-front rule), `Dequeue(count)`
- `PassengerSystem/BoardingResolver.cs` — matches waiting vehicle to front group, updates seats, transitions to Full
- `PassengerSystem/PassengerController.cs` / `PassengerQueueView.cs` (MonoBehaviour views)
- Tests: grouped-front dequeue, capacity math

### M4 — Waiting-Slot Matching Priority (pure logic, no new views) ✅
- `WaitingAreaSystem/IWaitingVehicleSelector.cs`, `DefaultWaitingVehicleSelector.cs` — 3-tier priority (highest occupied-seat count → earliest-arrived → lowest slot index), drop-in for `BoardingResolver`
- `WaitingAreaSystem/WaitingVehicleEntry.cs` — pairs a `Vehicle` with its `WaitingSlot` for the selector
- `WaitingSlot.ArrivalOrder`, stamped by `WaitingAreaManager.ConfirmOccupied` via a monotonic counter, backs the earliest-arrived tier
- `BoardingResolver.TryBoard(IReadOnlyList<WaitingVehicleEntry>, PuzzleColor, IWaitingVehicleSelector)` overload wires the selector in without touching the existing single-vehicle `TryBoard`
- Fully unit-testable with synthetic fixtures, no scene needed

### M5 — Data-Driven Levels & Orchestration (playable increment 3) ✅
- `LevelSystem/LevelConfig.cs` (ScriptableObject) — board size, blocked cells, exit direction, `VehiclePlacement[]`, waiting-slot count, passenger color sequence
- `LevelSystem/VehiclePlacement.cs` — per-vehicle authoring data (`VehicleConfig`, origin cell, movement direction); footprint cells are derived from origin + `VehicleConfig.FootprintSize`
- `LevelSystem/LevelState.cs` (enum: Loading, Playing, Won)
- `LevelSystem/LevelController.cs` — builds `ParkingBoardData`/`BoardGrid`/`VehicleMovementResolver`/`WaitingAreaManager`/`PassengerQueue`/`BoardingResolver` from a `LevelConfig`; spawns vehicle/passenger views (spawning is now data-driven, superseding M2's manual scene placement); each `Update` while `Playing` matches waiting vehicles to the front passenger group via `DefaultWaitingVehicleSelector` (M4), completes boarding once boarded passengers finish animating, requests departure for `Full` vehicles, and evaluates the win condition; exposes `LoadLevel(LevelConfig)` and `OnLevelCompleted`
- `VehicleSystem/VehicleController.cs` extended with `Full → Departing → Completed` (`RequestDepart`/`CanDepart`), releasing its waiting slot only once its footprint has fully left the slot (footprint-clear rule applied consistently)
- `Core/GameState.cs` (enum: Boot, MainMenu, Loading, Playing, Paused, LevelComplete) + `Core/GameManager.cs` updated with delegation to `LevelController`
- Win-condition interpretation (GDD `Overview.md` doesn't give a precise state check): "parking area cleared" = every vehicle has left the board (state is not `Parked`/`MovingToExit`); "passengers transported" = queue empty and no boarding animation in flight. Not all vehicles are required to reach `Full`/`Completed` — flag if this should be stricter.
- Human work required: place `LevelController` in `MainScene`, wire serialized references (`BoardView`, `WaitingAreaView`, `PassengerQueueView`, spawn root transforms, passenger prefab), author `LevelConfig`/`VehicleConfig` assets, ensure car prefabs carry a `VehicleController` (added at runtime as a fallback if missing, but prefab-side is cleaner)

### M6 — UI/HUD, Audio, Juice
- Win panel, remaining-passenger HUD, restart button
- `SoundSO` hookups for vehicle move/board/win via existing `AudioManager.PlaySound`
- DOTween easing polish, optional Cinemachine framing

### M7 — Stretch (explicitly out of committed scope)
- Interactive/removable obstacles, boosters (undo/shuffle/extra-slot/remove-vehicle), level progression/save data. Architecture readiness only.

## Verification

- **M0–M1, M3–M4**: EditMode unit tests in `SCJam.Runtime.Tests`, run via Unity's Test Runner window or `Unity -runTests -testPlatform EditMode`.
- **M2, M5, M6**: require the human-wired scene to actually play; AI-side verification is limited to compiling cleanly and unit tests on the pure-logic pieces underneath (`VehicleMovementResolver`, `WaitingAreaManager`).
- After each milestone, confirm the project still compiles (`Edit > ` no console errors) before moving to the next.
