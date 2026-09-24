using UnityEngine;
using UnityEngine.UIElements;

namespace OC.UI.Panel
{
#if UNITY_6000_3_OR_NEWER
    [UxmlElement]
    public partial class PanelDropdownField : UnityEngine.UIElements.DropdownField
    {
#else
    public class PanelDropdownField : UnityEngine.UIElements.DropdownField
    {
        public new class UxmlFactory : UxmlFactory<PanelDropdownField, UxmlTraits> { }
#endif

        private const string STYLE_SHEET = "StyleSheet/panel-field";
        private const string USS_CONTAINER = "panel-field-container";

        public PanelDropdownField() : this(""){}

        public PanelDropdownField(string label) : base(label)
        {
            styleSheets.Add(Resources.Load<StyleSheet>(STYLE_SHEET));
            AddToClassList(USS_CONTAINER);
        }
    }
}