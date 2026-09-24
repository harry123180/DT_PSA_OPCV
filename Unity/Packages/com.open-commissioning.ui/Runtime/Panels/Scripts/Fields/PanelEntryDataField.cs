using OC.Data;
using UnityEngine;
using UnityEngine.UIElements;

namespace OC.UI.Panel
{
    [UxmlElement]
    public partial class PanelEntryDataField : VisualElement
    {
        public bool IsReadonly
        {
            get => _isReadonly;
            set
            {
                if (_isReadonly == value) return;
                SetReadonly(value);
            }
        }
        
        public EntryData EntryData
        {
            get => _entryData;
            set => SetValueWithoutNotify(value);
        }

        private const string USS = "StyleSheet/panel-field";
        private const string USS_ENTRY_DATA_FIELD = "panel-field-entrydata";

        private readonly Label _label;
        private readonly PanelStringField _field;
        private bool _isReadonly;
        
        private EntryData _entryData;

        public PanelEntryDataField()
        {
            styleSheets.Add(Resources.Load<StyleSheet>(USS));
            AddToClassList(USS_ENTRY_DATA_FIELD);
            Add(_label = new Label());
            Add(_field = new PanelStringField());
            _field.RegisterCallback<ChangeEvent<string>>(OnFieldValueChanged);
            _field.isDelayed = true;
            _field.multiline = false;
        }

        public void Bind(EntryData entryData)
        {
            _entryData = entryData;
            
            if (_entryData == null) return;
            
            _label.text = _entryData.Type.ToString();
            _field.label = _entryData.Key;
            _field.SetValueWithoutNotify(_entryData.Value);
        }

        public void Unbind()
        {
            _entryData = null;
        }

        private void OnFieldValueChanged(ChangeEvent<string> evt)
        {
            if (_entryData == null) return;
            
            if (_entryData.Validate(evt.newValue))
            {
                _entryData.Value = evt.newValue;
            }
            else
            {
                _field.SetValueWithoutNotify(evt.previousValue);
            }
        }

        private void SetValueWithoutNotify(EntryData entryData)
        {
            _entryData = entryData;
            _field.SetValueWithoutNotify(entryData.Value);
        }

        private void SetReadonly(bool isReadonly)
        {
            _isReadonly = isReadonly;
            _field.isReadOnly = _isReadonly;
        }
    }
}