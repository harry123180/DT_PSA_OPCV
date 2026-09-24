using OC.Interactions.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace OC.UI.Panel
{
#if UNITY_6000_3_OR_NEWER
    [UxmlElement]
    public partial class PanelPushButton : Label
    {
#else
    public class PanelPushButton : Label
    {
        public new class UxmlFactory : UxmlFactory<PanelPushButton, UxmlTraits> { }
        public new class UxmlTraits : UnityEngine.UIElements.Label.UxmlTraits { }
#endif

        public sealed override string text
        {
            get => base.text;
            set => base.text = value.ToUpper();
        }
        
        public bool Value
        {
            get => _value;
            set
            {
                if (_value == value) return;
                SetValueWithoutNotify(value);
            }
        }
        
        private const string USS = "StyleSheet/panel-field";
        private const string USS_CONTAINER = "panel-field-container";
        private const string USS_BUTTON = "panel-field-button";
        private const string USS_UNITY_BUTTON = "unity-button";
        private const string USS_BUTTON_ACTIVE = "panel-field-button_active";
        
        private IProperty<bool> _property;
        
        private readonly MouseEvents _mouseEvents;
        
        private bool _value;
        
        public PanelPushButton() : this("") { }

        public PanelPushButton(string label)
        {
            if (!string.IsNullOrEmpty(label)) text = label;
            this.AddDefaultTheme();
            styleSheets.Add(Resources.Load<StyleSheet>(USS));
            AddToClassList(USS_CONTAINER);
            AddToClassList(USS_BUTTON);
            AddToClassList(USS_UNITY_BUTTON);
            
            _mouseEvents = new MouseEvents()
            {
                target = this
            };
        }

        public VisualElement Bind(IProperty<bool> property)
        {
            _property = property;
            _mouseEvents.Up += OnMouseUpEvent;
            _mouseEvents.Down += OnMouseDownEvent;
            _property.Subscribe(SetValueWithoutNotify);
            return this;
        }

        public VisualElement Unbind()
        {
            _mouseEvents.Up -= OnMouseUpEvent;
            _mouseEvents.Down -= OnMouseDownEvent;
            _property?.Unsubscribe(SetValueWithoutNotify);
            _property = null;
            return this;
        }
        
        private void SetValueWithoutNotify(bool value)
        {
            _value = value;
            EnableInClassList(USS_BUTTON_ACTIVE, value);
        }
        
        private void OnMouseDownEvent()
        {
            _property.Value = true;
        }

        private void OnMouseUpEvent()
        {
            _property.Value = false;
        }
    }
}