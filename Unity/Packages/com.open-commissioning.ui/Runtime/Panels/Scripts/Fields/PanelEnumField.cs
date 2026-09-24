using UnityEngine;
using UnityEngine.UIElements;

namespace OC.UI.Panel
{
#if UNITY_6000_3_OR_NEWER
    [UxmlElement]
    public partial class PanelEnumField : UnityEngine.UIElements.EnumField
    {
#else
    public class PanelEnumField : UnityEngine.UIElements.EnumField
    {
        public new class UxmlFactory : UxmlFactory<PanelEnumField, UxmlTraits> { }
#endif

        private const string STYLE_SHEET = "StyleSheet/panel-field";
        private const string USS_CONTAINER = "panel-field-container";

        public PanelEnumField() : this(""){}
        
        public PanelEnumField(string label) : base(label)
        {
            styleSheets.Add(Resources.Load<StyleSheet>(STYLE_SHEET));
            AddToClassList(USS_CONTAINER);
        }
    }
}