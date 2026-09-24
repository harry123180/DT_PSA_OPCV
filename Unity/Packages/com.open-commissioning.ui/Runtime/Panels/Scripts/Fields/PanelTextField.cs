using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace OC.UI.Panel
{
    public abstract class PanelTextField<T> : UnityEngine.UIElements.TextField
    {
        public T ValidValue
        {
            get => _validValue;
            set
            {
                if (EqualityComparer<T>.Default.Equals(_validValue, value))
                {
                    return;
                }
                _validValue = value;
                SetValueWithoutNotify(value.ToString());
            }
        }
        
        private T _validValue;
        private string _text;

        private const string STYLE_SHEET = "StyleSheet/panel-field";
        private const string USS_CONTAINER = "panel-field-container";
        private const string USS_LABEL = "panel-field-label";
        private const string USS_INPUT_TEXT = "panel-field-text_input";
        

        protected PanelTextField(Property<T> property, string label, bool isReadOnly) : base(label)
        {
            styleSheets.Add(Resources.Load<StyleSheet>(STYLE_SHEET));
            AddToClassList(USS_CONTAINER);
            
            labelElement.AddToClassList(USS_LABEL);
            textInputBase.AddToClassList(USS_INPUT_TEXT);

            this.isReadOnly = isReadOnly;
            isDelayed = true;

            if (property == null) return;
            RegisterCallback<ChangeEvent<string>>(evt => ValidateInput(property, evt.newValue));
            property.OnValueChanged += i => ValidValue = i;
            SetValueWithoutNotify(property.Value.ToString());
        }

        public sealed override void SetValueWithoutNotify(string newValue)
        {
            base.SetValueWithoutNotify(newValue);
        }

        private void ValidateInput(Property<T> property, string newText)
        {
            if (Validate(newText, out T validValue))
            {
                property.Value = validValue;
                _text = newText;
            }
            else
            {
                SetValueWithoutNotify(_text);
            }
        }
        
        protected abstract bool Validate(string text, out T validValue);
    } 
}


