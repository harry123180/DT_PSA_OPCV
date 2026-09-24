using System.Globalization;
using UnityEngine.UIElements;

namespace OC.UI.Panel
{
#if UNITY_6000_3_OR_NEWER
    [UxmlElement]
    public partial class PanelTextFieldInt : PanelTextField<int>
    {
#else
    public class PanelTextFieldInt : PanelTextField<int>
    {
        public new class UxmlFactory : UxmlFactory<PanelTextFieldInt, UxmlTraits> { }
#endif

        public PanelTextFieldInt() : this(null,"", false) { }
        
        public PanelTextFieldInt(Property<int> property, string label, bool isReadOnly) : base(property, label, isReadOnly) {}
        
        protected override bool Validate(string newValue, out int validValue)
        {
            return int.TryParse(newValue, NumberStyles.Integer, CultureInfo.CurrentCulture, out validValue);
        }
    }
}