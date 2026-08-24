using System;
using UnityEngine;
using UnityEngine.UI;

namespace SCJam.UISystem
{
    public class PopupSetting : PopupBase
    {
        // ===== Serialized Fields ===== //

        [SerializeField] private Button _retryButton;
        [SerializeField] private Button _quitButton;
        [SerializeField] private Button _closeButton;


        // ===== Events ===== //

        public event Action RetryRequested;
        public event Action QuitRequested;


        // ===== Methods ===== //

        private void OnEnable()
        {
            _retryButton.onClick.AddListener(OnRetryClicked);
            _quitButton.onClick.AddListener(OnQuitClicked);
            _closeButton.onClick.AddListener(OnCloseClicked);
        }

        private void OnDisable()
        {
            _retryButton.onClick.RemoveListener(OnRetryClicked);
            _quitButton.onClick.RemoveListener(OnQuitClicked);
            _closeButton.onClick.RemoveListener(OnCloseClicked);
        }

        private void OnRetryClicked()
        {
            RetryRequested?.Invoke();
            PopupManager.Instance.Close();
        }

        private void OnQuitClicked()
        {
            QuitRequested?.Invoke();
            PopupManager.Instance.Close();
        }

        private void OnCloseClicked()
        {
            PopupManager.Instance.Close();
        }
    }
}
