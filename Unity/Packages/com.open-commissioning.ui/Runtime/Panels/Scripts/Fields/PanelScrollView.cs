using UnityEngine;
using UnityEngine.UIElements;

namespace OC.UI.Panel
{
#if UNITY_6000_3_OR_NEWER
    [UxmlElement]
    public partial class PanelScrollView : UnityEngine.UIElements.ScrollView
    {
#else
    public class PanelScrollView : UnityEngine.UIElements.ScrollView
    {
        public new class UxmlFactory : UxmlFactory<PanelScrollView, UxmlTraits> { }
        public new class UxmlTraits : UnityEngine.UIElements.ScrollView.UxmlTraits { }
#endif

        private const string SCROLL_VIEW_STYLE_SHEET = "StyleSheet/scrollview";
        private const string USS_SCROLL_VIEW = "scrollview";

        public PanelScrollView()
        {
            styleSheets.Add(Resources.Load<StyleSheet>(SCROLL_VIEW_STYLE_SHEET));
            AddToClassList(USS_SCROLL_VIEW);
            verticalScroller.name = "scroller-vertical";
            horizontalScroller.name = "scroller-horizontal";
        }
    }
}