using SCJam.LevelSystem;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SCJam.UISystem
{
    /// <summary>
    /// HUD booster button that spends one charge to unlock an extra waiting slot for the rest of the run.
    /// Stays interactable only while <see cref="LevelController.CanAddWaitingSlot"/> is true, and shows the
    /// remaining charge count when a label is assigned.
    /// </summary>
    [RequireComponent(typeof(Button))]
    public class AddWaitingSlotButton : MonoBehaviour
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
            _levelController.AddWaitingSlotChargesChanged += OnChargesChanged;
            Refresh();
        }

        private void OnDisable()
        {
            _button.onClick.RemoveListener(OnClicked);
            _levelController.AddWaitingSlotChargesChanged -= OnChargesChanged;
        }

        private void Refresh()
        {
            _button.interactable = _levelController.CanAddWaitingSlot;

            if (_chargeCountText != null)
                _chargeCountText.text = _levelController.RemainingAddWaitingSlotCharges.ToString();
        }

        private void OnClicked()
        {
            _levelController.TryAddWaitingSlot();
        }

        private void OnChargesChanged(int remainingCharges)
        {
            Refresh();
        }
    }
}
