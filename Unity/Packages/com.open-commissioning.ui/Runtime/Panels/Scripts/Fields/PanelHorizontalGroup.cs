using UnityEngine;
using UnityEngine.UIElements;

namespace OC.UI.Panel
{
    public class PanelHorizontalGroup : VisualElement
    {
        private const string USS = "StyleSheet/panel-field";
        private const string USS_CONTAINER = "panel-field-group_horizontal";
        
        public PanelHorizontalGroup()
        {
            this.AddDefaultTheme();
            styleSheets.Add(Resources.Load<StyleSheet>(USS));
            AddToClassList(USS_CONTAINER);

            style.flexDirection = new StyleEnum<FlexDirection>(FlexDirection.Row);
        }

        public new void Add(VisualElement visualElement)
        {
            base.Add(visualElement);
            visualElement.style.flexGrow = 1;
        }
    }
}