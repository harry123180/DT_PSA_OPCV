using OC;
using UnityEngine.UIElements;

namespace OC.UI.Panel
{
    public static class TextInputBaseFieldExtension
    {
        public static void BindReadOnlyProperty<T>(this TextInputBaseField<T> field, IProperty<bool> readOnlyProperty)
        {
            field.isReadOnly = !readOnlyProperty.Value;
            readOnlyProperty.OnValueChanged += value => field.isReadOnly = !value;  
        }
        
    }
}
