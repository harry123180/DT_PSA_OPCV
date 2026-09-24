using System.Collections.Generic;
using System.Linq;
using OC.Communication;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace OC.UI.Industrial
{
    [RequireComponent(typeof(UIDocument))]
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(10000)]
    public class IndustrialPanelManager : MonoBehaviour
    {
        private VisualElement _sidebar;
        private Button _button;
        private List<IIndustrialPanel> _industrialComponents;
        
        private const string STYLE_SHEET = "StyleSheet/industrial-sidebar";
        private const string USS_CONTAINER = "industrial-sidebar-container";

        private void Start()
        {
            var uiDocument = GetComponent<UIDocument>();
            _sidebar = new VisualElement();
            _sidebar.AddDefaultTheme();
            _sidebar.styleSheets.Add(Resources.Load<StyleSheet>(STYLE_SHEET));
            _sidebar.AddToClassList(USS_CONTAINER);
            uiDocument.rootVisualElement.Add(_sidebar);
            
            _industrialComponents = FindAllComponentsInScene();
            foreach (var element in _industrialComponents)
            {
                AddInTreeHierarchy(element);
            }
        }

        private List<IIndustrialPanel> FindAllComponentsInScene()
        {
            var industrialVisualElements = new List<IIndustrialPanel>();
            var clients = new List<Client>();
            
            var rootGameObjects = SceneManager.GetActiveScene().GetRootGameObjects();
            foreach (var rootGameObject in rootGameObjects)
            {
                if (rootGameObject.TryGetComponent<Client>(out var client))
                {
                    clients.Add(client);
                }
            }

            foreach (var client in clients)
            {
                var visualElements = client.GetComponentsInChildren<IIndustrialPanel>();
                industrialVisualElements.AddRange(visualElements.Where(item => item.Component.gameObject.activeInHierarchy));
            }

            return industrialVisualElements;
        }

        private void AddInTreeHierarchy(IIndustrialPanel industrialVisualElement)
        {
            var root = _sidebar;
            VisualElement tab = null;
            var path = industrialVisualElement.Path;
            var pathLevels = path.Split(".");

            for (var i = 0; i < pathLevels.Length-1; i++)
            {
                var menu = root.Q<TabMenu>();
                if (menu == null)
                {
                    menu = new TabMenu();
                    root.Add(menu);
                }
                
                tab = menu.GetOrCreate(pathLevels[i]);
                root = tab;
            }

            tab?.Add(industrialVisualElement.Create());
        }
    }
}

