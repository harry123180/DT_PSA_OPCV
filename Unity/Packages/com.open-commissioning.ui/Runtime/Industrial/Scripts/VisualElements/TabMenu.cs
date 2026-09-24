using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace OC.UI.Industrial
{
    public class TabMenu : VisualElement
    {
        private const string STYLE_SHEET = "StyleSheet/industrial-tabs";
        private const string USS_TAB_MENU = "tab-menu";
        private const string USS_TAB_BAR_CONTAINER = "tab-bar-container";
        private const string USS_TAB_BAR = "tab-bar";
        private const string USS_TAB_LABEL = "tab-label";
        private const string USS_TAB_CONTENT = "tab-content";
        private const string USS_TAB_SLECTED = "tab-selected";
        private const string USS_TAB_CONTENT_UNSELECTED = "tab-content-unselected";

        private readonly VisualElement _tabBar;
        private readonly VisualElement _tabsContainer;
        private readonly List<Label> _labels;
        private readonly List<VisualElement> _tabs;

        public TabMenu()
        {
            styleSheets.Add(Resources.Load<StyleSheet>(STYLE_SHEET));
            AddToClassList(USS_TAB_MENU);

            var tabsContainer = new VisualElement();
            tabsContainer.AddToClassList(USS_TAB_BAR_CONTAINER);

            _tabBar = new VisualElement();
            
            _tabBar.AddToClassList(USS_TAB_BAR);
            tabsContainer.Add(_tabBar);
            
            _tabsContainer = new VisualElement();
            
            hierarchy.Add(tabsContainer);
            hierarchy.Add(_tabsContainer);
            _labels = new List<Label>();
            _tabs = new List<VisualElement>();
        }
        
        public VisualElement GetOrCreate(string tabName)
        {
            foreach (var tab in _tabs.Where(tab => tab.name == tabName))
            {
                return tab;
            }

            return CreateTab(tabName);
        }

        private VisualElement CreateTab(string tabName)
        {
            var label = new Label(tabName.ToUpper());
            label.AddToClassList(USS_TAB_LABEL);
            _labels.Add(label);
            _tabBar.Add(label);
            
            var tab = new VisualElement()
            {
                name = tabName
            };
            tab.AddToClassList(USS_TAB_CONTENT);
            _tabs.Add(tab);
            _tabsContainer.Add(tab);

            label.RegisterCallback<ClickEvent>(_ => ChangeTabState(label, tab));
            UnselectAll();
            return tab;
        }

        private void ChangeTabState(VisualElement label, VisualElement tab)
        {
            var isSelected = label.ClassListContains(USS_TAB_SLECTED);
            UnselectAll();
            
            if (!isSelected)
            {
                label.AddToClassList(USS_TAB_SLECTED);
                tab.RemoveFromClassList(USS_TAB_CONTENT_UNSELECTED);
            }
            else
            {
                label.RemoveFromClassList(USS_TAB_SLECTED);
                tab.AddToClassList(USS_TAB_CONTENT_UNSELECTED);
            }
        }

        private void SetTabState(int index, bool select)
        {
            if (select)
            {
                _labels[index].AddToClassList(USS_TAB_SLECTED);
                _tabs[index].RemoveFromClassList(USS_TAB_CONTENT_UNSELECTED);
            }
            else
            {
                _labels[index].RemoveFromClassList(USS_TAB_SLECTED);
                _tabs[index].AddToClassList(USS_TAB_CONTENT_UNSELECTED);
            }
        }

        private void UnselectAll()
        {
            for (var i = 0; i < _tabs.Count; i++)
            {
                SetTabState(i,false);
            }
        }
    }
}
