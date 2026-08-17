# Game Design Document

This document consolidates the game's design, gameplay rules, core entities, and development plan.

## Game Overview

### Concept

A mobile puzzle game inspired by **Bus Escape: Traffic Jam**.

The player manages a crowded parking area containing buses and passengers. The goal is to move buses out of the traffic jam and match passengers with buses of the corresponding color.

The game focuses on simple interactions, spatial reasoning, and solving traffic-blocking situations.

### Core Gameplay

* Buses are placed in a crowded parking/traffic area.
* Each bus has a specific color.
* Passengers waiting outside the parking area also have colors.
* A bus may always be selected while parked and idle; if its path is blocked, it bumps into the blocking bus and reverses back instead of exiting.
* When a bus reaches the pickup area, passengers with the matching color board it.
* The player must determine the correct order to release buses.
* The level is completed when all required passengers are transported and the parking area is cleared.

### Core Loop

1. Observe the current traffic layout.
2. Identify buses that can move.
3. Release buses in the correct order.
4. Match buses with passengers.
5. Clear all passengers/buses.
6. Complete the level and continue to the next puzzle.

### Technical Direction

* Engine: Unity.
* Level-based architecture.
* Levels should be data-driven rather than hard-coded.
* Gameplay systems should be modular and reusable.
* Game state and visual presentation should remain separated where practical.
* Architecture should support adding new levels, mechanics, and boosters without rewriting the core gameplay.

---

## Core Entities

### Vehicle
A vehicle is a movable puzzle object placed inside the parking area.
Each vehicle contains the following gameplay properties:
- Color: The vehicle color determines which passengers may board it.
- Capacity: Capacity is the maximum number of passengers the vehicle can carry.
- OccupiedSeatCount
- GridFootprint: A vehicle may occupy one or more grid cells.
- MovementDirection: Each vehicle has one fixed movement direction Up-Down-Left-Right
- State:
    + `Parked`: The vehicle is still inside the parking area.
    + `MovingToExit`: The vehicle is traveling toward the board boundary.
    + `Waiting`: The vehicle occupies a waiting slot.
    + `Boarding`: Matching passengers are currently entering the vehicle.
    + `Full`: The vehicle has reached maximum capacity.
    + `Departing`: The full vehicle is leaving the waiting area.
    + `Completed`: The vehicle is no longer active in the level.
    + ...

### Passenger
A passenger is a color-coded unit waiting to board a matching vehicle.
Each passenger contains the following properties:
- Color
- QueueIndex
- State:
    + Queued
    + MovingToVehicle
    + Completed
    + ...

### Passenger Queue
The passenger queue determines the order in which passengers may be processed.
Only passengers at the accessible front section of the queue may board vehicles.
Under a grouped-front rule, all consecutive passengers of the same color at the front may be processed together.

### Waiting Area
The waiting area contains a fixed number of vehicle slots.
A vehicle occupies one waiting slot after leaving the parking area.

### Parking Board
The parking board is the spatial puzzle area containing vehicles.
ParkingBoard:
- Width
- Height
- Vehicles
- Exit boundaries
The board may be represented by:
- A logical square grid
- A rectangular grid
- Continuous coordinates with lane-based collision checks
A grid-based representation is recommended because it makes level authoring, collision validation, and solvability testing easier.

---

## Vehicle Movement
- When selected, a vehicle moves continuously in its assigned direction until it exits the parking board.
- The player does not select a destination cell.
- Before movement begins, the game checks every cell between the vehicle and the relevant board boundary.
- A vehicle path is valid only when no occupied cell intersects the swept footprint of the vehicle.
- The selected vehicle itself must be excluded from collision testing.
- A vehicle blocks another vehicle when its footprint intersects the other vehicle's forward exit path. Removing Vehicle B may make Vehicle A movable.

### Blocked-Path Bump & Reverse
- A parked, idle vehicle may always be selected, even when its exit path is blocked.
- If the path is clear, movement proceeds as described above: the vehicle reserves a waiting slot and exits normally.
- If the path is blocked, the vehicle does not reserve a waiting slot and its footprint/grid cells are not changed.
- The vehicle still moves forward along its movement direction. It stops the moment it actually touches the first blocking vehicle, then reverses back to its original position and rotation at the same speed it moved forward.
- Only the first vehicle actually touched is affected. Vehicles further along the path are never shaken and are not considered hit.
- The blocked vehicle plays a bump sound and shake feedback; the vehicle currently moving is never shaken.
- While bumping and reversing, only the vehicle performing the bump is locked from new input. Every other vehicle remains selectable.

### Parking-to-Waiting-Slot Path
After a vehicle exits the parking board (footprint cleared) and its waiting slot is reserved, it travels to that slot as follows, using board-local axes (X = left/right, Z = bottom/top of the grid):
- If the vehicle exited moving **Down**, it first runs sideways along the row it just exited into (holding its Z from the exit), toward whichever side edge (left or right) of the board is closer to the reserved slot's local X. From there it continues as below.
- The vehicle then runs along its current lane (holding X fixed — the side edge for a Down exit, or its exit-point X for a Left/Right/Up exit) until its local Z reaches the slot's local Z minus a fixed approach offset. The vehicle always approaches the waiting row from below, never from above or by cutting across it.
- From that point, it moves to where the slot's own approach axis (the direction a vehicle travels to arrive facing the slot's rotation) crosses the vehicle's current lane, then follows that axis into the slot, ending facing the slot's rotation.
- The waiting slot's approach direction is not assumed to be a straight projection along the grid axis; it follows the slot anchor's own facing.

---

## Waiting Slot
- A waiting slot must be reserved before a vehicle begins leaving the parking board. This prevents multiple vehicles from claiming the same final slot during overlapping animations.
- Slot states: Available, Reserved, Occupied
- The slot may become logically available before the departure animation finishes, provided that visual paths do not overlap incorrectly.
- Waiting slots do not need to function as a queue. Any waiting vehicle may receive matching front passengers. 
- Recommended matching priority when multiple waiting vehicles have the same color:
    1. Vehicle with the highest occupied-seat count
    2. Earliest-arrived vehicle
    3. Lowest waiting-slot index
- Prioritizing the most-filled vehicle helps free slots sooner and produces deterministic behavior.

---