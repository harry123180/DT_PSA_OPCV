using UnityEngine;
using UnityEngine.UIElements;

namespace OC.UI.Panel
{
#if UNITY_6000_3_OR_NEWER
    [UxmlElement]
    public partial class PanelSliderInt : UnityEngine.UIElements.SliderInt
    {
#else
    public class PanelSliderInt : UnityEngine.UIElements.SliderInt
    {
        public new class UxmlFactory : UxmlFactory<PanelSliderInt, UxmlTraits> { }
#endif

        private const string STYLE_SHEET = "StyleSheet/panel-field";
        private const string USS_CONTAINER = "panel-field-container";
        private const string USS_SLIDER = "panel-field-slider";

        public PanelSliderInt() : this(""){}
        
        public PanelSliderInt(string label) : base(label)
        {
            styleSheets.Add(Resources.Load<StyleSheet>(STYLE_SHEET));
            AddToClassList(USS_CONTAINER);
            AddToClassList(USS_SLIDER);
        }

        public PanelSliderInt(string label, Property<int> property) : this(label)
        {
            this.BindProperty(property);
        }
    }
}