using SCJam.LevelSystem;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SCJam.UISystem
{
    /// <summary>
    /// HUD booster button that spends one charge to reveal the guide-finger hint for the current level.
    /// The tutorial level shows its hint automatically without a charge; every later level needs this
    /// button. Stays interactable only while <see cref="LevelController.CanShowHint"/> is true, and shows
    /// the remaining charge count when a label is assigned.
    /// </summary>
    [RequireComponent(typeof(Button))]
    public class HintBoosterButton : MonoBehaviour
    {
        // ===== Serialized Fields ===== //

        [SerializeField] private LevelController _levelController;
        [SerializeField] private TextMeshProUGUI _chargeCountText;


        // ===== Private Fields ===== //

        private Button _button;


        // ===== Methods ===== //

        private void Awake()
        {
            _button = GetComponent<Button>();
        }

        private void OnEnable()
        {
            _button.onClick.AddListener(OnClicked);
            _levelController.HintChargesChanged += OnChargesChanged;
            Refresh();
        }

        private void OnDisable()
        {
            _button.onClick.RemoveListener(OnClicked);
            _levelController.HintChargesChanged -= OnChargesChanged;
        }

        private void Refresh()
        {
            _button.interactable = _levelController.CanShowHint;

            if (_chargeCountText != null)
                _chargeCountText.text = _levelController.RemainingHintCharges.ToString();
        }

        private void OnClicked()
        {
            _levelController.TryShowHint();
        }

        private void OnChargesChanged(int remainingCharges)
        {
            Refresh();
        }
    }
}
