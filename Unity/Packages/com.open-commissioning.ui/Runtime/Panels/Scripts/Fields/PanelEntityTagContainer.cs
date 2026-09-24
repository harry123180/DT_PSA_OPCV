using System;
using System.Collections.Generic;
using OC.Data;
using OC.MaterialFlow;
using UnityEngine;
using UnityEngine.UIElements;

namespace OC.UI.Panel
{
    public class PanelEntityTagContainer : VisualElement 
    {
        private const string USS = "StyleSheet/panel-field";

        private PayloadTag _entityTag;
        private int _directoryIndex;
        private List<EntryData> _entryData = new ();
        
        private readonly PanelGroupContainer _groupContainer;
        private readonly Property<bool> _overrideProperty = new (false);
        
        private PanelToggleSlide _override;
        private readonly VisualElement _content;
        private readonly PanelButton _buttonWrite;
        private readonly PanelButton _buttonRead;
        private readonly List<PanelEntryDataField> _entryDataFields = new ();

        public PanelEntityTagContainer()
        {
            styleSheets.Add(Resources.Load<StyleSheet>(USS));
            Add(_groupContainer = new PanelGroupContainer());
            Add(_override = new PanelToggleSlide("Override"));
            Add(_content = new VisualElement());
            Add(_buttonWrite = new PanelButton("Write", WriteData));
            Add(_buttonRead = new PanelButton("Read", ReadData));
        }

        public void Bind(PayloadTag entityTag, int directoryIndex)
        {
            _override.Bind(_overrideProperty);
            _entityTag = entityTag;
            _directoryIndex = directoryIndex;
            _groupContainer.Label = ProductDataDirectoryManager.Instance.ProductDataDirectories[directoryIndex].Name;
            _entryData = entityTag.GetProductDataContent(directoryIndex);

            foreach (var entryData in _entryData)
            {
                var entryDataFiled = new PanelEntryDataField();
                entryDataFiled.Bind(entryData);
                _content.Add(entryDataFiled);
                _entryDataFields.Add(entryDataFiled);
            }
            
            _overrideProperty.Subscribe(OnOverrideChanged);
        }

        public void Unbind()
        {
            _overrideProperty.Unsubscribe(OnOverrideChanged);
            
            _override.Unbind();
            foreach (var dataField in _entryDataFields)
            {
                dataField.Unbind();
            }
            
            _entryDataFields.Clear();
            _content.Clear();
        }

        private void OnOverrideChanged(bool value)
        {
            foreach (var dataField in _entryDataFields)
            {
                dataField.IsReadonly = !value;
            }
            
            _buttonWrite.SetEnabled(value);
        }

        private void WriteData()
        {
            var content = new List<EntryData>();
            foreach (var dataField in _entryDataFields)
            {
                content.Add(dataField.EntryData);
            }
            
            _entityTag.OverwriteProductData(_directoryIndex, content);
        }

        private void ReadData()
        {
            var entryData = _entityTag.GetProductDataContent(_directoryIndex);
            if (entryData.Count != _entryDataFields.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(entryData));
            }

            for (var i = 0; i < entryData.Count; i++)
            {
                _entryDataFields[i].EntryData = entryData[i];
            }
        }
    }
}