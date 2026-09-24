using System.Collections.Generic;
using System.Linq;
using NaughtyAttributes;
using OC.UI.Interactions;
using OC.UI.Panel;
using UnityEngine;
using UnityEngine.UIElements;

namespace OC.UI.Toolbar
{
    public class HideToolWindow : ToolbarWindow
    {
        private List<HideGroup> _hideGroups;
        private List<PanelToggleIcon> _toggleIcons;

        private void Start()
        {
            _hideGroups = Utils.FindObjectsByType<HideGroup>().ToList().OrderBy(x => x.Name).ToList();
        }

        protected override void AddContent(SubsystemPanel subsystemPanel)
        {
            _toggleIcons = new List<PanelToggleIcon>();
            
            foreach (var group in _hideGroups)
            {
                var toggle = new PanelToggleIcon(group.name, _defaultIcon, _activeIcon, Color.white, new Color(1f, 0.33f, 0.29f));
                toggle.RegisterValueChangedCallback( evt =>
                {
                    group.Transparent = evt.newValue;
                });

                toggle.value = group.Transparent;

                _toggleIcons.Add(toggle);

                subsystemPanel.Add(toggle);
            }
            
            var horizontalGroup = new PanelHorizontalGroup();
            horizontalGroup.Add(new PanelButton("Hide all", HideAll));
            horizontalGroup.Add(new PanelButton("Show all", ShowAll));
            subsystemPanel.Add(horizontalGroup);
        }

        private void HideAll()
        {
            foreach (var toggle in _toggleIcons)
            {
                toggle.value = true;
            }
        }

        private void ShowAll()
        {
            foreach (var toggle in _toggleIcons)
            {
                toggle.value = false;
            }
        }

        [Button]
        public void HideVisual()
        {
            foreach (var group in _hideGroups)
            {
                group.Transparent = true;
            }
        }
        
        [Button]
        public void ShowVisual()
        {
            foreach (var group in _hideGroups)
            {
                group.Transparent = false;
            }
        }
    }
}
