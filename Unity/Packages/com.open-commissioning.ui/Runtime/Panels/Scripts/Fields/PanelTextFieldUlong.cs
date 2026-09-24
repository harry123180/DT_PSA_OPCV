using System.Globalization;
using UnityEngine.UIElements;

namespace OC.UI.Panel
{
#if UNITY_6000_3_OR_NEWER
    [UxmlElement]
    public partial class PanelTextFieldUlong : PanelTextField<ulong>
    {
#else
    public class PanelTextFieldUlong : PanelTextField<ulong>
    {
        public new class UxmlFactory : UxmlFactory<PanelTextFieldUlong, UxmlTraits> { }
#endif

        public PanelTextFieldUlong() : this(null,"", false) { }
        
        public PanelTextFieldUlong(Property<ulong> property, string label, bool isReadOnly) : base(property, label, isReadOnly) {}
        
        protected override bool Validate(string newValue, out ulong validValue)
        {
            return ulong.TryParse(newValue, NumberStyles.Integer, CultureInfo.CurrentCulture, out validValue);
        }
    }
}