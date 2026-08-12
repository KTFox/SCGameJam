# Game Overview

## Concept

A mobile puzzle game inspired by **Bus Escape: Traffic Jam**.

The player manages a crowded parking area containing buses and passengers. The goal is to move buses out of the traffic jam and match passengers with buses of the corresponding color.

The game focuses on simple interactions, spatial reasoning, and solving traffic-blocking situations.

## Core Gameplay

* Buses are placed in a crowded parking/traffic area.
* Each bus has a specific color.
* Passengers waiting outside the parking area also have colors.
* Buses can only move when their path is not blocked.
* When a bus reaches the pickup area, passengers with the matching color board it.
* The player must determine the correct order to release buses.
* The level is completed when all required passengers are transported and the parking area is cleared.

## Core Loop

1. Observe the current traffic layout.
2. Identify buses that can move.
3. Release buses in the correct order.
4. Match buses with passengers.
5. Clear all passengers/buses.
6. Complete the level and continue to the next puzzle.

## Technical Direction

* Engine: Unity.
* Level-based architecture.
* Levels should be data-driven rather than hard-coded.
* Gameplay systems should be modular and reusable.
* Game state and visual presentation should remain separated where practical.
* Architecture should support adding new levels, mechanics, and boosters without rewriting the core gameplay.
