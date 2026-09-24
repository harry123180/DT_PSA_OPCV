using System.Globalization;
using UnityEngine.UIElements;

namespace OC.UI.Panel
{
#if UNITY_6000_3_OR_NEWER
    [UxmlElement]
    public partial class PanelTextFieldFloat : PanelTextField<float>
    {
#else
    public class PanelTextFieldFloat : PanelTextField<float>
    {
        public new class UxmlFactory : UxmlFactory<PanelTextFieldFloat, UxmlTraits> { }
#endif

        public PanelTextFieldFloat() : this(null,"", false) { }
        
        public PanelTextFieldFloat(Property<float> property, string label, bool isReadOnly) : base(property, label, isReadOnly) {}

        protected override bool Validate(string newValue, out float validValue)
        {
            return float.TryParse(newValue, NumberStyles.Float, CultureInfo.CurrentCulture, out validValue);
        }
    }
}