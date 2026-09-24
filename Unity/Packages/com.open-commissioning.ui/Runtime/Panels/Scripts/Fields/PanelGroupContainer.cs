using UnityEngine;
using UnityEngine.UIElements;

namespace OC.UI.Panel
{
#if UNITY_6000_3_OR_NEWER
    [UxmlElement]
    public partial class PanelGroupContainer : VisualElement
    {
#else
    public class PanelGroupContainer : VisualElement
    {
        public new class UxmlFactory : UxmlFactory<PanelGroupContainer, UxmlTraits> { }

        public new class UxmlTraits : VisualElement.UxmlTraits
        {
            private readonly UxmlStringAttributeDescription _label = new() { name = "Label", defaultValue = "Label" };

            public override void Init(VisualElement ve, IUxmlAttributes bag, CreationContext cc)
            {
                base.Init(ve, bag, cc);
                if (ve is PanelGroupContainer ate) ate.Label = _label.GetValueFromBag(bag, cc);
            }
        }
#endif

#if UNITY_6000_3_OR_NEWER
        [UxmlAttribute("Label")]
#endif
        public string Label
        {
            get => _label.text;
            set => _label.text = value.ToUpper();
        }
        
        private const string USS = "StyleSheet/panel-field";
        private const string USS_CONTAINER = "panel-field-container";
        private const string USS_GROUP = "panel-field-group";
        private const string USS_GROUP_HEADER = "panel-field-group-header";
        private const string USS_GROUP_HEADER_OPTIONS = "panel-field-group-header_options";

        private bool _collapsed;
        private readonly Label _label;
        private readonly VisualElement _header;
        private readonly VisualElement _headerOptions;
        private readonly VisualElement _content;

        public PanelGroupContainer() : this("") { }
        
        public PanelGroupContainer(string label)
        {
            styleSheets.Add(Resources.Load<StyleSheet>(USS));
            AddToClassList(USS_CONTAINER);
            AddToClassList(USS_GROUP);

            var header = new VisualElement()
            {
                name = "header"
            };
            
            _headerOptions = new VisualElement()
            {
                name = "headerOptions"
            };
            
            header.AddToClassList(USS_GROUP_HEADER);
            _headerOptions.AddToClassList(USS_GROUP_HEADER_OPTIONS);

            _label = new Label();
            if (!string.IsNullOrEmpty(label)) _label.text = label.ToUpper();
            
            header.Add(_label);
            header.Add(_headerOptions);

            var bar = new VisualElement
            {
                name = "bar"
            };

            _content = new VisualElement()
            {
                name = "content"
            };
            
            
            hierarchy.Add(header);
            hierarchy.Add(bar);
            hierarchy.Add(_content);
            
            _label.RegisterCallback<ClickEvent>(Collapse);
        }
        
        private void Collapse(ClickEvent evt)
        {
            _collapsed = !_collapsed;
            _label.style.unityFontStyleAndWeight = _collapsed ? FontStyle.Bold : FontStyle.Normal;
            _content.style.display = _collapsed ? DisplayStyle.None : DisplayStyle.Flex;
        }

        public new void Add(VisualElement visualElement)
        {
            _content.Add(visualElement);
        }

        public void AddToHeader(VisualElement visualElement)
        {
            _headerOptions.Add(visualElement);
        }
    }
}
