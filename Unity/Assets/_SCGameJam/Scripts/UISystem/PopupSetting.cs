using System;
using SCJam.AudioSystem;
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
        [SerializeField] private Button _musicButton;
        [SerializeField] private Button _sfxButton;
        [SerializeField] private GameObject _musicOnState;
        [SerializeField] private GameObject _musicOffState;
        [SerializeField] private GameObject _sfxOnState;
        [SerializeField] private GameObject _sfxOffState;


        // ===== Events ===== //

        public event Action RetryRequested;
        public event Action QuitRequested;


        // ===== Methods ===== //

        private void OnEnable()
        {
            _retryButton.onClick.AddListener(OnRetryClicked);
            _quitButton.onClick.AddListener(OnQuitClicked);
            _closeButton.onClick.AddListener(OnCloseClicked);
            _musicButton.onClick.AddListener(OnMusicClicked);
            _sfxButton.onClick.AddListener(OnSfxClicked);

            RefreshAudioStates();
        }

        private void OnDisable()
        {
            _retryButton.onClick.RemoveListener(OnRetryClicked);
            _quitButton.onClick.RemoveListener(OnQuitClicked);
            _closeButton.onClick.RemoveListener(OnCloseClicked);
            _musicButton.onClick.RemoveListener(OnMusicClicked);
            _sfxButton.onClick.RemoveListener(OnSfxClicked);
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

        private void OnMusicClicked()
        {
            AudioManager.Instance.SetMusicEnabled(!AudioManager.Instance.IsMusicEnabled);
            RefreshAudioStates();
        }

        private void OnSfxClicked()
        {
            AudioManager.Instance.SetSfxEnabled(!AudioManager.Instance.IsSfxEnabled);
            RefreshAudioStates();
        }

        private void RefreshAudioStates()
        {
            if (AudioManager.Instance == null)
                return;

            bool isMusicEnabled = AudioManager.Instance.IsMusicEnabled;
            _musicOnState.SetActive(isMusicEnabled);
            _musicOffState.SetActive(!isMusicEnabled);

            bool isSfxEnabled = AudioManager.Instance.IsSfxEnabled;
            _sfxOnState.SetActive(isSfxEnabled);
            _sfxOffState.SetActive(!isSfxEnabled);
        }
    }
}
