# Waiting Slot
- A waiting slot must be reserved before a vehicle begins leaving the parking board. This prevents multiple vehicles from claiming the same final slot during overlapping animations.
- Slot states: Available, Reserved, Occupied
- The slot may become logically available before the departure animation finishes, provided that visual paths do not overlap incorrectly.
- Waiting slots do not need to function as a queue. Any waiting vehicle may receive matching front passengers. 
- Recommended matching priority when multiple waiting vehicles have the same color:
    1. Vehicle with the highest occupied-seat count
    2. Earliest-arrived vehicle
    3. Lowest waiting-slot index
- Prioritizing the most-filled vehicle helps free slots sooner and produces deterministic behavior.