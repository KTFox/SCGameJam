# Core Entities

## Vehicle
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

## Passenger
A passenger is a color-coded unit waiting to board a matching vehicle.
Each passenger contains the following properties:
- Color
- QueueIndex
- State:
    + Queued
    + MovingToVehicle
    + Completed
    + ...

## Passenger Queue
The passenger queue determines the order in which passengers may be processed.
Only passengers at the accessible front section of the queue may board vehicles.
Under a grouped-front rule, all consecutive passengers of the same color at the front may be processed together.

## Waiting Area
The waiting area contains a fixed number of vehicle slots.
A vehicle occupies one waiting slot after leaving the parking area.

## Parking Board
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