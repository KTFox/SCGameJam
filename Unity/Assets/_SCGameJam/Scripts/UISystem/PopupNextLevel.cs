using UnityEngine;
using UnityEngine.UI;

namespace SCJam.UISystem
{
    public class PopupNextLevel : PopupBase, IPopupWithData<int>
    {
        // ===== Serialized Fields ===== //

        [SerializeField] private Button _nextLevelButton;


        // ===== Private Fields ===== //

        private int _completedLevel;


        // ===== Methods ===== //

        public void Setup(int completedLevel)
        {
            _completedLevel = completedLevel;
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
            PopupManager.Instance.Close();
        }
    }
}
