using UnityEngine;
using UnityEngine.UIElements;

namespace OC.UI
{
    [RequireComponent(typeof(UIDocument))]
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(100)]
    public class ToolWindowsManager : MonoBehaviour
    {
        private UIDocument _uiDocument;
        private VisualElement _toolbar;
        
        private const string UXML = "UXML/toolbar_subsystem";
        private const string STYLE_SHEET = "StyleSheet/toolbar";
        
        private void Start()
        {
            var uiDocument = GetComponent<UIDocument>();
            _toolbar = Resources.Load<VisualTreeAsset>(UXML).Instantiate().Q("toolbar");
            _toolbar.AddDefaultTheme();
            _toolbar.styleSheets.Add(Resources.Load<StyleSheet>(STYLE_SHEET));
            uiDocument.rootVisualElement.Add(_toolbar);
            PopulateVisualTree();
        }

        private void PopulateVisualTree()
        {
            foreach (var item in GetComponentsInChildren<IPopulateVisualTree>(false))
            {
                item.Populate(_toolbar);
            }
        }
    }
}