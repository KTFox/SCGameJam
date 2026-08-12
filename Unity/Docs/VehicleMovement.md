# Vehicle Movement
- When selected, a vehicle moves continuously in its assigned direction until it exits the parking board.
- The player does not select a destination cell.
- Before movement begins, the game checks every cell between the vehicle and the relevant board boundary.
- A vehicle path is valid only when no occupied cell intersects the swept footprint of the vehicle.
- The selected vehicle itself must be excluded from collision testing.
- A vehicle blocks another vehicle when its footprint intersects the other vehicle's forward exit path. Removing Vehicle B may make Vehicle A movable.