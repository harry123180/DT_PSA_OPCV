using UnityEngine;
using UnityEngine.UIElements;

namespace OC.UI.Toolbar
{
#if UNITY_6000_3_OR_NEWER
    [UxmlElement]
    public partial class Toggle : BaseBoolField
    {
#else
    public class Toggle : BaseBoolField
    {
        public new class UxmlFactory : UxmlFactory<Toggle, UxmlTraits>{}
        public new class UxmlTraits : BaseFieldTraits<bool, UxmlBoolAttributeDescription> { }
#endif
        public Sprite DefaultIcon
        {
            get => _defaultIcon;
            set
            {
                _defaultIcon = value;
                _inputField.style.backgroundImage = new StyleBackground(_defaultIcon);
            }
        }

        public new string text { 
            get => _label.text; 

            set {
                if (string.IsNullOrEmpty(value))
                {
                    _label.style.display = DisplayStyle.None;
                } else
                {
                    _label.text = value;
                    _label.style.display = DisplayStyle.Flex;
                }

            } 
        }
        
        public Sprite ActiveIcon
        {
            get => _activeIcon;
            set
            {
                _activeIcon = value;
                SetIconActive(this.value);
            }
        }

        private const string STYLE_SHEET = "StyleSheet/toolbar";
        private const string USS_CONTAINER = "toolbar-button";
        private const string USS_ICON = "toolbar-button-icon";
        private const string USS_LABEL = "toolbar-button__label";

        private readonly VisualElement _inputField;
        private Sprite _defaultIcon;
        private Sprite _activeIcon;
        private readonly Label _label;

        public Toggle() : this(null) { }

        public Toggle(Sprite icon, Sprite activeIcon = null) : base("")
        {
            styleSheets.Add(Resources.Load<StyleSheet>(STYLE_SHEET));
            AddToClassList(USS_CONTAINER);

            _defaultIcon = icon;
            _activeIcon = activeIcon;

            _label = new Label();
            _label.AddToClassList(USS_LABEL);
            Add(_label);

            _inputField = this.Q(className: BaseBoolField.inputUssClassName);
            _inputField.AddToClassList(USS_ICON);
            if (_defaultIcon != null) DefaultIcon = _defaultIcon;
            RegisterCallback<ChangeEvent<bool>>(evt => SetIconActive(evt.newValue));
        }

        private void SetIconActive(bool active)
        {
            if (_activeIcon == null) return;
            
            _inputField.style.backgroundImage = active ? 
                _activeIcon != null ? new StyleBackground(_activeIcon) : null :
                _defaultIcon != null ? new StyleBackground(_defaultIcon) : null;
        }
    }
}
