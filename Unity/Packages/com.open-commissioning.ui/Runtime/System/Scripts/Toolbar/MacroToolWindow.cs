using System;
using System.Collections.Generic;
using OC.UI.Panel;
using UnityEngine;
using UnityEngine.Events;

namespace OC.UI.Toolbar
{
    public class MacroToolWindow : ToolbarWindow
    {
        [SerializeField]
        private List<Macros> _macros;

        protected override void AddContent(SubsystemPanel subsystemPanel)
        {
            foreach (var macro in _macros)
            {
                var button = new PanelButton(macro.Name, () => macro.OnClick?.Invoke());
                subsystemPanel.Add(button);
            }
        }

        [Serializable]
        private class Macros
        {
            public string Name;
            public UnityEvent OnClick;
        }
    }
} 
