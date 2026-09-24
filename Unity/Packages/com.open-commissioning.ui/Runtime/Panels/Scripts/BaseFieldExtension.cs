using System;
using UnityEngine.UIElements;

namespace OC.UI
{
    public static class BaseFieldExtension
    {
        public static BaseField<T> Bind<T>(this BaseField<T> field, IProperty<T> property)
        {
            field.BindProperty(property);
            return field;
        }

        public static BaseField<T> Bind<T>(this BaseField<T> field, IPropertyReadOnly<T> property)
        {
            field.BindProperty(property);
            return field;
        }

        public static BaseField<T> Unbind<T>(this BaseField<T> field)
        {
            field.UnbindProperty();
            return field;
        }

        public static void BindProperty<T>(this BaseField<T> field, IProperty<T> property)
        {
            if (field.userData != null) field.UnbindProperty();

            field.SetValueWithoutNotify(property.Value);
            property.OnValueChanged += OnPropertyValueChange(field);
            field.RegisterValueChangedCallback(OnFieldValueChange);
            field.userData = property;
        }

        public static void BindProperty<T>(this BaseField<T> field, IPropertyReadOnly<T> property)
        {
            if (field.userData != null) field.UnbindProperty();

            field.SetValueWithoutNotify(property.Value);
            property.OnValueChanged += OnPropertyValueChange(field);
            field.userData = property;
        }

        private static void UnbindProperty<T>(this BaseField<T> field)
        {
            ((Property<T>)field.userData).OnValueChanged -= OnPropertyValueChange(field);
            field.UnregisterValueChangedCallback(OnFieldValueChange);
            field.userData = null;
        }

        private static void OnFieldValueChange<T>(ChangeEvent<T> evt)
        {
            if ((evt.target as BaseField<T>)?.userData is Property<T> property) property.Value = evt.newValue;
        }

        private static Action<T> OnPropertyValueChange<T>(BaseField<T> field)
        {
            return field.SetValueWithoutNotify;
        }
    }
}
