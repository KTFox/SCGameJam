using System.Collections.Generic;
using UnityEngine;

namespace SCJam.LevelSystem
{
    /// <summary>
    /// Spawns and tracks vehicle views for an active level session.
    /// Kept separate from occupancy and runtime state classes.
    /// </summary>
    public sealed class VehicleViewFactory
    {
        private readonly Transform _parent;
        private readonly GridCoordinateConverter _converter;
        private readonly Dictionary<string, VehicleView> _viewsByVehicleId =
            new Dictionary<string, VehicleView>();

        /// <summary>
        /// Creates a view factory.
        /// </summary>
        /// <param name="parent">Parent transform for spawned views.</param>
        /// <param name="converter">Board coordinate converter.</param>
        public VehicleViewFactory(Transform parent, GridCoordinateConverter converter)
        {
            _parent = parent;
            _converter = converter;
        }

        /// <summary>
        /// Gets the spawned views by vehicle ID.
        /// </summary>
        public IReadOnlyDictionary<string, VehicleView> ViewsByVehicleId => _viewsByVehicleId;

        /// <summary>
        /// Spawns views for all vehicles in the runtime state.
        /// Existing views for the same IDs are replaced.
        /// </summary>
        /// <param name="runtimeState">Active level runtime state.</param>
        public void SpawnAll(LevelRuntimeState runtimeState)
        {
            Clear();

            if (runtimeState == null || !runtimeState.IsInitialized)
            {
                return;
            }

            IReadOnlyList<VehicleRuntimeState> vehicles = runtimeState.Vehicles;
            for (int i = 0; i < vehicles.Count; i++)
            {
                TrySpawn(vehicles[i]);
            }
        }

        /// <summary>
        /// Spawns a single vehicle view when a prefab is available.
        /// </summary>
        /// <param name="vehicle">Runtime vehicle state.</param>
        /// <returns>Created view, or null when spawning is skipped.</returns>
        public VehicleView TrySpawn(VehicleRuntimeState vehicle)
        {
            if (vehicle == null || vehicle.VehicleType == null || vehicle.VehicleType.Prefab == null)
            {
                return null;
            }

            if (_viewsByVehicleId.TryGetValue(vehicle.VehicleId, out VehicleView existing) && existing != null)
            {
                Object.Destroy(existing.gameObject);
                _viewsByVehicleId.Remove(vehicle.VehicleId);
            }

            GameObject instance = Object.Instantiate(vehicle.VehicleType.Prefab, _parent);
            instance.name = $"Vehicle_{vehicle.VehicleId}";

            VehicleView view = instance.GetComponent<VehicleView>();
            if (view == null)
            {
                view = instance.AddComponent<VehicleView>();
            }

            view.Bind(vehicle);
            view.ApplyTransformFromRuntime(_converter);
            _viewsByVehicleId[vehicle.VehicleId] = view;
            return view;
        }

        /// <summary>
        /// Destroys all spawned views and clears the registry.
        /// </summary>
        public void Clear()
        {
            foreach (KeyValuePair<string, VehicleView> pair in _viewsByVehicleId)
            {
                if (pair.Value != null)
                {
                    Object.Destroy(pair.Value.gameObject);
                }
            }

            _viewsByVehicleId.Clear();
        }

        /// <summary>
        /// Re-applies transforms for all tracked views from their runtime states.
        /// </summary>
        public void RefreshTransforms()
        {
            foreach (KeyValuePair<string, VehicleView> pair in _viewsByVehicleId)
            {
                if (pair.Value != null)
                {
                    pair.Value.ApplyTransformFromRuntime(_converter);
                }
            }
        }
    }
}
