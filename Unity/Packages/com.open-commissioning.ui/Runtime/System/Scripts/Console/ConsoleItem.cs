using UnityEngine;
using UnityEngine.UIElements;

namespace OC.UI.Console
{
    [UxmlElement]
    public partial class ConsoleItem : VisualElement
    {
        public string Message
        {
            get => _label.text;
            set => _label.text = value;
        }

        public Label Label => _label;
        public VisualElement Icon => _icon;

        private const string STYLE_RESOURCE = "StyleSheet/console";
        private const string USS_CONSOLE_ITEM = "console-item";
        private const string USS_CONSOLE_ITEM_LABEL = "console-item__label";
        private const string USS_CONSOLE_ITEM_ICON_CONTAINER = "console-item__icon-container";
        private const string USS_CONSOLE_ITEM_ICON = "console-item__icon";
        
        private readonly Label _label;
        private readonly VisualElement _icon;

        public ConsoleItem()
        {
            var container = new VisualElement();
            this.AddDefaultTheme();
            container.AddToClassList(USS_CONSOLE_ITEM_ICON_CONTAINER);

            _icon = new VisualElement();
            _icon.AddToClassList(USS_CONSOLE_ITEM_ICON);

            container.Add(_icon);

            styleSheets.Add(Resources.Load<StyleSheet>(STYLE_RESOURCE));
            AddToClassList(USS_CONSOLE_ITEM);

            _label = new Label();
            _label.AddToClassList(USS_CONSOLE_ITEM_LABEL);

            Add(container);
            Add(_label);
        }
    } 
}
