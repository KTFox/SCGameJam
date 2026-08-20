using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace SCJam.SceneSystem
{
    public class LoadingScreenController : MonoBehaviour
    {
        // ===== Constants ===== //

        private const float PROGRESS_TWEEN_DURATION = 0.25f;


        // ===== Serialized Fields ===== //

        [SerializeField] private Slider _loadingBar;
        [SerializeField] private TextMeshProUGUI _loadingText;


        // ===== Private Fields ===== //

        private Tweener _progressTweener;


        // ===== Methods ===== //

        private void OnEnable()
        {
            SceneLoader.LoadProgressChanged += HandleLoadProgressChanged;
        }

        private void OnDisable()
        {
            SceneLoader.LoadProgressChanged -= HandleLoadProgressChanged;
            _progressTweener?.Kill();
            _progressTweener = null;
        }

        private void HandleLoadProgressChanged(float progress)
        {
            if (_loadingBar == null)
                return;

            _progressTweener?.Kill();
            _progressTweener = DOTween.To(() => _loadingBar.value, SetLoadingBarValue, progress, PROGRESS_TWEEN_DURATION)
                .SetEase(Ease.OutQuad)
                .SetUpdate(true);
        }

        private void SetLoadingBarValue(float value)
        {
            _loadingBar.value = value;

            if (_loadingText != null)
                _loadingText.text = Mathf.RoundToInt(value * 100f) + "%";
        }
    }
}
