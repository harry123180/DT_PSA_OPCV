using UnityEngine;
using UnityEngine.UIElements;

namespace OC.UI.Panel
{
#if UNITY_6000_3_OR_NEWER
    [UxmlElement]
    public partial class PanelHashField : Hash128Field
    {
#else
    public class PanelHashField : Hash128Field
    {
        public new class UxmlFactory : UxmlFactory<PanelHashField, UxmlTraits> { }
#endif
        private const string STYLE_SHEET = "StyleSheet/panel-field";
        private const string USS_CONTAINER = "panel-field-container";

        public PanelHashField() : this(""){}
        
        public PanelHashField(string label, bool isReadOnly = false) : base(label)
        {
            this.isReadOnly = isReadOnly;
            styleSheets.Add(Resources.Load<StyleSheet>(STYLE_SHEET));
            AddToClassList(USS_CONTAINER);
        }
    }
}