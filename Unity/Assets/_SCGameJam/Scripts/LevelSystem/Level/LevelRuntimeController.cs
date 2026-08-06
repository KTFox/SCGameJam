using System.Collections.Generic;
using UnityEngine;

namespace SCJam.LevelSystem
{
    /// <summary>
    /// Scene boundary that loads a <see cref="LevelDefinition"/> into a <see cref="LevelRuntimeState"/>.
    /// Optionally spawns vehicle views through <see cref="VehicleViewFactory"/>.
    /// </summary>
    public sealed class LevelRuntimeController : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Authored level definition to load.")]
        private LevelSO _levelDefinition;

        [SerializeField]
        [Tooltip("Board grid transform used for coordinate conversion and view parenting.")]
        private BoardGridTransform _boardGridTransform;

        [SerializeField]
        [Tooltip("When enabled, vehicle prefabs are spawned after successful initialization.")]
        private bool _spawnVehicleViews = true;

        [SerializeField]
        [Tooltip("When enabled, the level loads automatically on Awake.")]
        private bool _loadOnAwake = true;

        private LevelRuntimeState _runtimeState;
        private VehicleViewFactory _viewFactory;
        private IReadOnlyList<LevelValidationIssue> _lastIssues = System.Array.Empty<LevelValidationIssue>();

        /// <summary>
        /// Gets the assigned level definition.
        /// </summary>
        public LevelSO LevelDefinition => _levelDefinition;

        /// <summary>
        /// Gets the active runtime state, if initialized.
        /// </summary>
        public LevelRuntimeState RuntimeState => _runtimeState;

        /// <summary>
        /// Gets whether a runtime session is currently initialized.
        /// </summary>
        public bool HasActiveRuntime => _runtimeState != null && _runtimeState.IsInitialized;

        /// <summary>
        /// Gets issues from the most recent load attempt.
        /// </summary>
        public IReadOnlyList<LevelValidationIssue> LastIssues => _lastIssues;

        /// <summary>
        /// Gets the board grid transform used by this controller.
        /// </summary>
        public BoardGridTransform BoardGridTransform => _boardGridTransform;

        private void Awake()
        {
            if (_boardGridTransform == null)
            {
                _boardGridTransform = GetComponent<BoardGridTransform>();
            }

            if (_loadOnAwake)
            {
                TryLoadLevel();
            }
        }

        private void OnDestroy()
        {
            UnloadLevel();
        }

        /// <summary>
        /// Loads and initializes the assigned level definition.
        /// </summary>
        /// <returns>True when initialization succeeds.</returns>
        public bool TryLoadLevel()
        {
            return TryLoadLevel(_levelDefinition);
        }

        /// <summary>
        /// Loads and initializes the provided level definition.
        /// </summary>
        /// <param name="definition">Level definition to load.</param>
        /// <returns>True when initialization succeeds.</returns>
        public bool TryLoadLevel(LevelSO definition)
        {
            UnloadLevel();

            _levelDefinition = definition;
            if (!LevelRuntimeState.TryInitialize(definition, out LevelRuntimeState runtimeState, out _lastIssues))
            {
                LevelDefinitionValidator.LogIssues(definition, new LevelValidationResult(_lastIssues));
                return false;
            }

            _runtimeState = runtimeState;

            if (_boardGridTransform != null)
            {
                _boardGridTransform.Configure(definition);
            }

            if (_spawnVehicleViews)
            {
                SpawnViews();
            }

            return true;
        }

        /// <summary>
        /// Reloads the current level definition from scratch.
        /// </summary>
        /// <returns>True when reload succeeds.</returns>
        public bool TryReloadLevel()
        {
            return TryLoadLevel(_levelDefinition);
        }

        /// <summary>
        /// Cleans up runtime state and spawned views.
        /// </summary>
        public void UnloadLevel()
        {
            if (_viewFactory != null)
            {
                _viewFactory.Clear();
                _viewFactory = null;
            }

            if (_runtimeState != null)
            {
                _runtimeState.Dispose();
                _runtimeState = null;
            }
        }

        private void SpawnViews()
        {
            if (_runtimeState == null || _boardGridTransform == null)
            {
                return;
            }

            _viewFactory = new VehicleViewFactory(
                _boardGridTransform.BoardRoot,
                _boardGridTransform.Converter);
            _viewFactory.SpawnAll(_runtimeState);
        }
    }
}
