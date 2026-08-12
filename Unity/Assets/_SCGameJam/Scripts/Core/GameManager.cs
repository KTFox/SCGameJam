using System;
using SCJam.AudioSystem;
using SCJam.LevelSystem;
using UnityEngine;

namespace SCJam.Core
{
    public class GameManager : MonoSingleton<GameManager>
    {
        // ===== Serialized Fields ===== //

        [SerializeField] private SoundSO _mainBGM;
        [SerializeField] private LevelController _levelController;
        [SerializeField] private LevelConfig _startingLevel;


        // ===== Private Fields ===== //

        private GameState _state;


        // ===== Public Properties ===== //

        public GameState State => _state;


        // ===== Events ===== //

        public event Action<GameState> OnGameStateChanged;


        // ===== Methods ===== //

        private void Start()
        {
            AudioManager.Instance.PlaySound(_mainBGM);

            if (_levelController != null)
                _levelController.OnLevelCompleted += HandleLevelCompleted;

            if (_startingLevel != null)
                LoadLevel(_startingLevel);
        }

        public void LoadLevel(LevelConfig levelConfig)
        {
            if (_levelController == null || levelConfig == null)
                return;

            ChangeState(GameState.Loading);
            _levelController.LoadLevel(levelConfig);
            ChangeState(GameState.Playing);
        }

        private void HandleLevelCompleted()
        {
            ChangeState(GameState.LevelComplete);
        }

        private void ChangeState(GameState newState)
        {
            _state = newState;
            OnGameStateChanged?.Invoke(newState);
        }
    }
}
