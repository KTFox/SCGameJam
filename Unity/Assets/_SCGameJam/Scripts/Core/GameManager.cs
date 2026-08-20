using System;
using SCJam.SceneSystem;
using UnityEngine;

namespace SCJam.Core
{
    public class GameManager : MonoSingleton<GameManager>
    {
        // ===== Serialized Fields ===== //


        // ===== Private Fields ===== //

        private GameState _state;
        private GameState _pendingState;


        // ===== Public Properties ===== //

        public GameState State => _state;


        // ===== Events ===== //

        public event Action<GameState> OnGameStateChanged;


        // ===== Methods ===== //

        protected override void Awake()
        {
            base.Awake();
            SceneLoader.SceneLoaded += HandleSceneLoaded;
        }

        private void Start()
        {
            RequestSceneLoad(SceneId.Gameplay, GameState.Playing);
        }

        private void OnDestroy()
        {
            SceneLoader.SceneLoaded -= HandleSceneLoaded;
        }

        public void RequestSceneLoad(SceneId sceneId, GameState stateOnLoaded)
        {
            if (SceneLoader.Instance == null)
            {
                Debug.LogError($"[{nameof(GameManager)}] Missing {nameof(SceneLoader)} instance.", this);
                return;
            }

            ChangeState(GameState.Loading);
            _pendingState = stateOnLoaded;
            SceneLoader.Instance.LoadScene(sceneId);
        }

        private void ChangeState(GameState newState)
        {
            _state = newState;
            OnGameStateChanged?.Invoke(newState);
        }

        private void HandleSceneLoaded(SceneId sceneId)
        {
            ChangeState(_pendingState);
        }
    }
}
