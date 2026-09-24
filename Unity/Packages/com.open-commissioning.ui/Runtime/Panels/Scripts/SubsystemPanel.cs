using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace OC.UI.Panel
{
    public class SubsystemPanel : VisualElement, IFloatingPanel
    {
        public bool Enable
        {
            get => _enable;
            set
            {
                if (_enable == value) return;
                EnableWithoutNotification(value);
                OnEnableChanged?.Invoke(value);
            }
        }

        public event Action<bool> OnEnableChanged;

        private bool _enable;

        private const string UXML = "UXML/panel_subsystem";
        private const string STYLE_SHEET = "StyleSheet/panel";
        private const string USS_CONTAINER = "panel-container";
        private const string USS_CONTAINER_SUBSYSTEM = "panel-container_subsystem";
        
        private readonly VisualElement _content;
        
        public SubsystemPanel(string title)
        {
            Enable = true;
            this.AddDefaultTheme();
            styleSheets.Add(Resources.Load<StyleSheet>(STYLE_SHEET));
            AddToClassList(USS_CONTAINER);
            AddToClassList(USS_CONTAINER_SUBSYSTEM);
            
            var container = Resources.Load<VisualTreeAsset>(UXML).Instantiate();
            var header = container.Q("header");
            _content = container.Q("content");
            
            hierarchy.Add(header);
            hierarchy.Add(_content);

            this.AddManipulator(new Moveable(header));
            
            header.Q<VisualElement>("close").RegisterCallback<ClickEvent>(_ => Close());
            header.Q<Label>("title").text = title;
            name = title.ToLower().Replace(" ", "_");
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

        public new void Add(VisualElement visualElement)
        {
            _content.Add(visualElement);
        }

        public void Close()
        {
            Enable = false;
        }
    }
}
