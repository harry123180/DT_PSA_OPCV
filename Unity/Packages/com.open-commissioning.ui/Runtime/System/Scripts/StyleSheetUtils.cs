using UnityEngine;
using UnityEngine.UIElements;

namespace OC.UI
{
    public static class StyleSheetUtils
    {
        private const string THEME_STYLE_SHEET = "StyleSheet/default-theme";
        
        public static void AddDefaultTheme(this VisualElement visualElement)
        {
            visualElement.styleSheets.Add(Resources.Load<StyleSheet>(THEME_STYLE_SHEET));
        }
    }
}