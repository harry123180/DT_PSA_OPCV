using System;
using System.Collections.Generic;
using System.Linq;
using NaughtyAttributes;
using OC.Interactions;
using OC.UI.Interactions;
using UnityEngine;
using UnityEngine.UIElements;

namespace OC.UI.Panel
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(1000)]
    public class PanelManager : MonoBehaviourSingleton<PanelManager>
    {
        public VisualElement Sidebar => _sidebar;
        public VisualElement Dropzone => _dropzone;
        
        public VisualElement Screen => _appUiRoot;
        public IReadOnlyList<IPanel> ActivePanels => _activePanels;
        
        [Header("Component Panels")]
        [SerializeField]
        private List<PanelHandler> _panelHandlers = new ();
        
        [Header("Settings")]
        [SerializeField]
        private float _dockThreshold = 10f;
        [SerializeField]
        private bool _debug;

        private VisualElement _appUiRoot;
        private ScrollView _sidebar;
        private VisualElement _dropzone;

        private const string UXML = "UXML/panel-sidebar";
        private const string STYLE_SHEET = "StyleSheet/panel-sidebar";
        private const string USS_SIDEBAR = "sidebar-scrollView";
        private const string USS_DOCKED_PANEL = "panel-container__docked";
        
        private readonly Dictionary<Type, PanelHandler> _factory = new ();
        private readonly List<IPanel> _cachedPanels = new ();
        private readonly List<IPanel> _activePanels = new ();
        private readonly List<IPanel> _removePanels = new ();

        private void OnEnable()
        {
            SelectionManager.Instance.OnSelectionChanged += OnSelectionChanged;
        }

        private void OnDisable()
        {
            SelectionManager.Instance.OnSelectionChanged -= OnSelectionChanged;
        }

        private void Start()
        {
            _appUiRoot = AppUI.Instance.Root;
            var sidebarContainer = Resources.Load<VisualTreeAsset>(UXML).Instantiate();
            sidebarContainer.styleSheets.Add(Resources.Load<StyleSheet>(STYLE_SHEET));
            
            _sidebar = sidebarContainer.Q<ScrollView>("sidebar");
            _sidebar.AddToClassList(USS_SIDEBAR);
            _dropzone = sidebarContainer.Q<VisualElement>("dropzone");
            
            _appUiRoot.Add(_sidebar);
            _appUiRoot.Add(_dropzone);
            _sidebar.SendToBack();
            _dropzone.SendToBack();
            
            _factory.Clear();
            
            foreach (var panelHandler in _panelHandlers)
            {
                _factory.Add(panelHandler.ReferenceType, panelHandler);
            }
            
            RefreshScrollViewStyle();
            _dropzone.style.display = DisplayStyle.None;
        }
        
        public void AddToScreen(VisualElement visualElement)
        {
            visualElement.RemoveFromHierarchy();
            _appUiRoot.Add(visualElement);
            visualElement.style.position = Position.Absolute;
            visualElement.RemoveFromClassList(USS_DOCKED_PANEL);
            RefreshScrollViewStyle();
        }
        
        public void AddToSidebar(VisualElement visualElement)
        {
            visualElement.RemoveFromHierarchy();
            _sidebar.Add(visualElement);
            visualElement.style.position = Position.Relative;
            visualElement.style.left = StyleKeyword.Auto;
            visualElement.style.top = StyleKeyword.Auto;
            visualElement.style.right = StyleKeyword.Auto;
            visualElement.style.bottom = StyleKeyword.Auto;
            visualElement.AddToClassList(USS_DOCKED_PANEL);
            RefreshScrollViewStyle();
        }

        [Button]
        public void CloseLastPanel()
        {
            for (var i = _activePanels.Count - 1; i >= 0; i--)
            {
                var panel = _activePanels[i];
                if (panel.Pinned) continue;
                DisablePanel(panel);
                _activePanels.RemoveAt(i);
                break;
            }
        }

        public void EnableDropzone(bool enable)
        {
            _dropzone.style.display = enable ? DisplayStyle.Flex : DisplayStyle.None;
        }
        
        private void OnSelectionChanged(IReadOnlyList<Interaction> selections)
        {
            _removePanels.Clear();
            
            foreach (var activePanel in _activePanels)
            {
                if (activePanel.Pinned) continue;
                if (selections.Contains(activePanel.Interaction)) continue;
                _removePanels.Add(activePanel);
            }
            
            foreach (var panel in _removePanels)
            {
                DisablePanel(panel);
            }
            
            foreach (var interaction in selections)
            {
                EnablePanel(interaction);
            }
        }

        private void EnablePanel(Interaction interaction)
        {
            if (interaction == null) return;
            if (_activePanels.Find(panel => panel.Interaction == interaction) != null) return;
            
            if (interaction.Interactable == null) return;
            
            var panel = CreateOrGetPanel(interaction.Interactable.ReferenceType);

            if (panel == null)
            {
                Debug.LogError($"No panel found for {interaction.Interactable.ReferenceType}");
                return;
            }
            
            _activePanels.Add(panel);
            
            panel.Enable = true;
            panel.Bind(interaction);
            panel.OnCloseClicked += DisablePanel;
            panel.OnFocusClicked += FocusPanelObject;
            
            AddToSidebar(panel.Root);
        }

        private void DisablePanel(IPanel panel)
        {
            _activePanels.Remove(panel);
            panel.Enable = false;
            panel.Pinned = false;
            panel.Unbind();
            panel.OnCloseClicked -= DisablePanel;
            panel.OnFocusClicked -= FocusPanelObject;
        }

        private void FocusPanelObject(IPanel panel)
        {
            if (panel.Interaction.Interactable != null)
            {
                var activeCamera = CamerasManager.Instance.ActiveCamera;
                if (activeCamera != null)
                {
                    activeCamera.Target.Value = panel.Interaction.Interactable.Component.transform;
                    activeCamera.FocusOnTarget();
                }
            }
        }

        private IPanel CreateOrGetPanel(Type type)
        {
            var panel = _cachedPanels.FirstOrDefault(panel => 
                panel.Enable == false && panel.ReferenceType == type);

            if (panel != null) return panel;
            
            if (_factory.TryGetValue(type, out var panelHandler))
            {
                panel = panelHandler.Create();
                _cachedPanels.Add(panel);
            }

            return panel;
        }
        
        private void RefreshScrollViewStyle()
        {
            var visible = _activePanels.Any(panel => panel.Enable);
            _sidebar.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        [ContextMenu(nameof(FindPanelsInChildren), false, 100)]
        public void FindPanelsInChildren()
        {
            _panelHandlers.Clear();
            var panels = GetComponentsInChildren<PanelHandler>(true);
            foreach (var panel in panels)
            {
                _panelHandlers.Add(panel);
            }
            
#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
#endif
        }
    }
}


