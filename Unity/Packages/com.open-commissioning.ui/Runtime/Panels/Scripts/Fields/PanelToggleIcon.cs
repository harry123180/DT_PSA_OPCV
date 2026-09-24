using UnityEngine;
using UnityEngine.UIElements;

namespace OC.UI.Panel
{
#if UNITY_6000_3_OR_NEWER
    [UxmlElement]
    public partial class PanelToggleIcon : BaseBoolField
    {
#else
    public class PanelToggleIcon : BaseBoolField
    {
        public new class UxmlFactory : UxmlFactory<PanelToggleIcon, UxmlTraits> { }
        public new class UxmlTraits : BaseFieldTraits<bool, UxmlBoolAttributeDescription> { }
#endif

        private const string STYLE_SHEET = "StyleSheet/panel-field";
        private const string USS_CONTAINER = "panel-field-container";
        private const string USS_TOGGLE = "panel-field-toggle";
        private const string USS_LABEL = "panel-field-label";
        private const string USS_ICON = "panel-field-icon";

        private readonly VisualElement _icon;
        private readonly StyleBackground _defaultIcon;
        private readonly StyleBackground _activeIcon;
        private readonly StyleColor _defaultColor;
        private readonly StyleColor _activeColor;

        public override bool value
        {
            get => base.value;
            set 
            {
                base.value = value;
                SetToggle(base.value);
            }
        }

        public PanelToggleIcon() : this("",null, null, Color.white, Color.yellow) { }

        public PanelToggleIcon(string label, Sprite defaultIcon, Sprite activeIcton, Color defaultColor, Color activeColor) : base(label)
        {
            styleSheets.Add(Resources.Load<StyleSheet>(STYLE_SHEET));
            AddToClassList(USS_CONTAINER);
            AddToClassList(USS_TOGGLE);
            labelElement.AddToClassList(USS_LABEL);

            focusable = false;

            _defaultIcon = new StyleBackground(defaultIcon);
            _activeIcon = new StyleBackground(activeIcton);
            _defaultColor = new StyleColor(defaultColor);
            _activeColor = new StyleColor(activeColor);

            _icon = this.Q<VisualElement>("unity-checkmark");
            _icon.AddToClassList(USS_ICON);
            _icon.style.backgroundImage = _defaultIcon;
        }

        private void SetToggle(bool toggle)
        {
            _icon.style.backgroundImage = toggle ? _activeIcon : _defaultIcon;
            _icon.style.unityBackgroundImageTintColor = toggle ? _activeColor : _defaultColor;
        }
    }
}
