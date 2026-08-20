using System.Collections.Generic;
using SCJam.Core;
using UnityEngine;

namespace SCJam.LevelSystem
{
    public class LevelDatabase : MonoSingleton<LevelDatabase>
    {
        // ===== Serialized Fields ===== //

        [SerializeField] private List<LevelConfig> _levels;


        // ===== Private Fields ===== //

        private int _currentLevelIndex;


        // ===== Public Properties ===== //

        public int LevelCount => _levels.Count;
        public int CurrentLevelIndex => _currentLevelIndex;
        public LevelConfig CurrentLevel => GetLevel(_currentLevelIndex);


        // ===== Methods ===== //

        public LevelConfig GetLevel(int index)
        {
            if (index < 0 || index >= _levels.Count)
            {
                Debug.LogError($"[{nameof(LevelDatabase)}] Level index {index} is out of range (0-{_levels.Count - 1}).", this);
                return null;
            }

            return _levels[index];
        }

        public void SetCurrentLevelIndex(int index)
        {
            if (index < 0 || index >= _levels.Count)
            {
                Debug.LogError($"[{nameof(LevelDatabase)}] Level index {index} is out of range (0-{_levels.Count - 1}).", this);
                return;
            }

            _currentLevelIndex = index;
        }
    }
}
