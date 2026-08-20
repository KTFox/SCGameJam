using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SCJam.UISystem
{
    public class PopupNextLevel : PopupBase, IPopupWithData<PopupNextLevelData>
    {
        // ===== Serialized Fields ===== //

        [SerializeField] private TextMeshProUGUI _currentLevelText;
        [SerializeField] private Button _nextLevelButton;


        // ===== Private Fields ===== //

        private int _completedLevel;


        // ===== Events ===== //

        public event Action NextLevelRequested;


        // ===== Methods ===== //

        public void Setup(PopupNextLevelData data)
        {
            _completedLevel = data.CompletedLevel;
            _nextLevelButton.interactable = data.HasNextLevel;
            _currentLevelText.text = $"Level {_completedLevel + 1}";
        }

        private void OnEnable()
        {
            _nextLevelButton.onClick.AddListener(OnNextLevelClicked);
        }

        private void OnDisable()
        {
            _nextLevelButton.onClick.RemoveListener(OnNextLevelClicked);
        }

        private void OnNextLevelClicked()
        {
            NextLevelRequested?.Invoke();
            PopupManager.Instance.Close();
        }
    }
}
