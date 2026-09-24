using OC.Communication;
using UnityEngine;
using UnityEngine.UIElements;

namespace OC.UI.Panel
{
    public class PanelLinkStatus : VisualElement
    {
        private const string USS = "StyleSheet/panel-field";
        private const string USS_BINARY_STATUS = "panel-field-binary-status_checkbox";
        private const string USS_BOX_INDICATOR_ACTIVE = "panel-field-progress-bar_indicator-active";
        
        private readonly PanelStringField _typeField;
        private readonly PanelStringField _pathField;
        private Link _link;

        public bool Status
        {
            get => _status;
            set
            {
                if (_status == value) return;
                _status = value;
                if (_status)
                {
                    _indicator.AddToClassList(USS_BOX_INDICATOR_ACTIVE);
                }
                else
                {
                    _indicator.RemoveFromClassList(USS_BOX_INDICATOR_ACTIVE);
                }
            }
        }

        private bool _status;
        private readonly VisualElement _indicator;
        
        public PanelLinkStatus()
        {
            styleSheets.Add(Resources.Load<StyleSheet>(USS));
            
            var group = new PanelGroupContainer("Communication");
            _indicator = new VisualElement();
            _indicator.AddDefaultTheme();
            _indicator.AddToClassList(USS_BINARY_STATUS);
            group.AddToHeader(_indicator);

            _typeField = new PanelStringField("");
            _pathField = new PanelStringField("");

            _typeField.SetTextInputAlign(TextAnchor.MiddleLeft);
            _pathField.SetTextInputAlign(TextAnchor.MiddleLeft);

            group.Add(_typeField);
            group.Add(_pathField);
            Add(group);
        }

        public PanelLinkStatus(Link link) : this()
        {
            Bind(link);
        }

        public void Bind(Link link)
        {
            _link = link;
            _typeField.SetValueWithoutNotify(_link.Type);
            _pathField.SetValueWithoutNotify(_link.ClientPath);
            
            Status = _link.Connected.Value;
            _link.Connected.Subscribe(OnConnectedChanged);
        }

        public void Unbind()
        {
            _link?.Connected.Unsubscribe(OnConnectedChanged);
        }
        
        private void OnConnectedChanged(bool value)
        {
            Status = value;
        }
    }
}