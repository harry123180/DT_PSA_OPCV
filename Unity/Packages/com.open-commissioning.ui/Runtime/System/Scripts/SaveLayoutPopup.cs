using System;
using OC.UI.Panel;
using UnityEngine;
using UnityEngine.UIElements;

namespace OC.UI
{
    public class SaveLayoutPopup : VisualElement, IFloatingPanel
    {
        public bool Enable
        {
            get => _enable;
            set
            {
                if (_enable == value) return;
                EnableWithoutNotification(value);
            }
        }

        public event Action OnSave;
        public event Action OnDiscard;
        public event Action OnCancel;

        private bool _enable;
        private const string Uxml = "UXML/popup-save-layout";

        public SaveLayoutPopup()
        {
            _enable = true;
            var container = Resources.Load<VisualTreeAsset>(Uxml).Instantiate();
            hierarchy.Add(container);
            container.AddDefaultTheme();
            container.Q<Button>("save").clicked += () => OnSave?.Invoke();
            container.Q<Button>("discard").clicked += () => OnDiscard?.Invoke();
            container.Q<Button>("cancel").clicked += () => OnCancel?.Invoke();
        }

        public void Close()
        {
            Enable = false;
        }

        private void EnableWithoutNotification(bool enable)
        {
            _enable = enable;
            style.display = _enable ? DisplayStyle.Flex : DisplayStyle.None;

            if (enable)
            {
                AppUI.Instance.Register(this);
            }
            else
            {
                AppUI.Instance.Unregister(this);
            }
        }
    }
}
