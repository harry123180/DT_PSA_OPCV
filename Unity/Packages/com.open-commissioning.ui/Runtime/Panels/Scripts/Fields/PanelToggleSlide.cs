using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace OC.UI.Panel
{
#if UNITY_6000_3_OR_NEWER
    [UxmlElement]
    public partial class PanelToggleSlide : BaseField<bool>
    {
#else
    public class PanelToggleSlide : BaseField<bool>
    {
        public new class UxmlFactory : UxmlFactory<PanelToggleSlide, UxmlTraits> { }

        public new class UxmlTraits : BaseFieldTraits<bool, UxmlBoolAttributeDescription> { }
#endif

        private const string USS_CLASS_NAME = "panel-field-toggle_slide";
        private const string INPUT_USS_CLASS_NAME = "panel-field-toggle_slide__input";
        
        private const string STYLE_SHEET = "StyleSheet/panel-field";
        private const string USS_CONTAINER = "panel-field-container";
        private const string USS_INPUT_KNOB = "panel-field-toggle_slide__input-knob";
        private const string USS_INPUT_CHECKED = "panel-field-toggle_slide__input--checked";
        private const string USS_LABEL = "panel-field-label";
        private bool _isEnabled;

        public bool IsEnabled
        {
            get { return _isEnabled; }
            private set { 
                _isEnabled = value;
                EnableInClassList("panel-field-toggle_slide__disabled", !_isEnabled);
                if (_isEnabled)
                {
                    RegisterCallback<ClickEvent>(OnClick);
                }
                else
                {
                    UnregisterCallback<ClickEvent>(OnClick);
                }
            }
        }

        public event Action<bool> OnValueChanged;


        private readonly VisualElement _inputField;
        
        public PanelToggleSlide() : this(null) { }

        public PanelToggleSlide(string label) : base(label, null)
        {
            styleSheets.Add(Resources.Load<StyleSheet>(STYLE_SHEET));
            AddToClassList(USS_CLASS_NAME);
            AddToClassList(USS_CONTAINER);
            labelElement.AddToClassList(USS_LABEL);

            _inputField = this.Q(className: BaseField<bool>.inputUssClassName);
            _inputField.AddToClassList(INPUT_USS_CLASS_NAME);
            Add(_inputField);

            var knob = new VisualElement();
            knob.AddToClassList(USS_INPUT_KNOB);
            _inputField.Add(knob);

            RegisterCallback<ClickEvent>(OnClick);
            RegisterCallback<KeyDownEvent>(OnKeydownEvent);
            RegisterCallback<NavigationSubmitEvent>(OnSubmit);

            IsEnabled = true;
        }

        public void Bind()
        {
            
        }

        private static void OnClick(ClickEvent evt)
        {
            if (evt.currentTarget is PanelToggleSlide slideToggle) slideToggle.ToggleValue();
            evt.StopPropagation();
        }

        private static void OnSubmit(NavigationSubmitEvent evt)
        {
            if (evt.currentTarget is PanelToggleSlide slideToggle) slideToggle.ToggleValue();
            evt.StopPropagation();
        }

        private static void OnKeydownEvent(KeyDownEvent evt)
        {
            var slideToggle = evt.currentTarget as PanelToggleSlide;
            if (slideToggle is { panel: { contextType: ContextType.Player } }) return;
            if (evt.keyCode is not (KeyCode.KeypadEnter or KeyCode.Return or KeyCode.Space)) return;
            slideToggle?.ToggleValue();
            evt.StopPropagation();
        }

        private void ToggleValue()
        {
            value = !value;
            OnValueChanged?.Invoke(value);
        }

        public sealed override void SetValueWithoutNotify(bool newValue)
        {
            base.SetValueWithoutNotify(newValue);
            if (!IsEnabled) return;
            _inputField.EnableInClassList(USS_INPUT_CHECKED, newValue);
        }
    }
}