using System;
using System.Collections.Generic;
using SCJam.Core;
using UnityEngine;
using UnityEngine.UI;

namespace SCJam.UISystem
{
    public class PopupManager : MonoSingleton<PopupManager>
    {
        // ===== Serialized Fields ===== //

        [SerializeField] private Image _dimBackground;
        [SerializeField] private PopupBase[] _registeredPopups;


        // ===== Private Fields ===== //

        private readonly Stack<PopupBase> _popupStack = new();
        private Dictionary<PopupId, PopupBase> _popupsById;


        // ===== Events ===== //

        public static event Action<PopupId> PopupOpened;
        public static event Action<PopupId> PopupClosed;


        // ===== Methods ===== //

        protected override void Awake()
        {
            base.Awake();

            _popupsById = new Dictionary<PopupId, PopupBase>(_registeredPopups.Length);
            foreach (PopupBase popup in _registeredPopups)
            {
                if (popup == null)
                    continue;

                if (!_popupsById.TryAdd(popup.PopupId, popup))
                    Debug.LogWarning($"[{nameof(PopupManager)}] Duplicate popup registered for id '{popup.PopupId}'.", popup);

                popup.gameObject.SetActive(false);
            }

            if (_dimBackground != null)
                _dimBackground.gameObject.SetActive(false);
        }

        public void Show(PopupId popupId)
        {
            if (TryGetPopup(popupId, out PopupBase popup))
                OpenPopup(popup);
        }

        public TPopup Show<TPopup, TData>(PopupId popupId, TData data) where TPopup : PopupBase, IPopupWithData<TData>
        {
            if (!TryGetPopup(popupId, out PopupBase popup))
                return null;

            if (popup is not TPopup typedPopup)
            {
                Debug.LogWarning($"[{nameof(PopupManager)}] Popup registered for id '{popupId}' is not of type '{typeof(TPopup).Name}'.", popup);
                return null;
            }

            typedPopup.Setup(data);
            OpenPopup(popup);

            return typedPopup;
        }

        public void Close()
        {
            if (_popupStack.Count == 0)
                return;

            PopupBase popup = _popupStack.Pop();
            popup.Closed += OnPopupClosed;
            popup.Close();
            UpdateDimBackground();
        }

        private bool TryGetPopup(PopupId popupId, out PopupBase popup)
        {
            if (_popupsById.TryGetValue(popupId, out popup))
                return true;

            Debug.LogWarning($"[{nameof(PopupManager)}] No popup registered for id '{popupId}'.", this);
            return false;
        }

        private void OpenPopup(PopupBase popup)
        {
            _popupStack.Push(popup);
            popup.transform.SetAsLastSibling();
            popup.Open();
            UpdateDimBackground();

            PopupOpened?.Invoke(popup.PopupId);
        }

        private void UpdateDimBackground()
        {
            if (_dimBackground == null)
                return;

            bool hasOpenPopup = _popupStack.Count > 0;
            _dimBackground.gameObject.SetActive(hasOpenPopup);

            if (hasOpenPopup)
            {
                int topPopupIndex = _popupStack.Peek().transform.GetSiblingIndex();
                int dimIndex = _dimBackground.transform.GetSiblingIndex();
                int targetIndex = dimIndex < topPopupIndex ? topPopupIndex - 1 : topPopupIndex;
                _dimBackground.transform.SetSiblingIndex(targetIndex);
            }
        }

        private void OnPopupClosed(PopupBase popup)
        {
            popup.Closed -= OnPopupClosed;
            PopupClosed?.Invoke(popup.PopupId);
        }
    }
}
