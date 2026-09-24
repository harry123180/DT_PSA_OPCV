using UnityEngine;
using UnityEngine.UI;

namespace OC.UI
{
    public static class Utils 
    {
        private static readonly Color BaseColor = Color.white;
        
        public static void SetColorBlock(this Selectable selectable, Color color)
        {
            var colorBlock = new ColorBlock
            {
                normalColor = color,
                highlightedColor = color,
                pressedColor = GetOverlayColor(color, 0.25f),
                disabledColor = GetOverlayColor(Color.black, 0.5f),
                selectedColor = color,
                fadeDuration = 0.1f,
                colorMultiplier = 1f
            };

            selectable.colors = colorBlock;
        }

        public static void SetColorBlock(this Selectable selectable, Color color, float factor)
        {
            var newColor = color.ScaleRGB(factor);
            SetColorBlock(selectable, newColor);
        }
        
        public static Color GetOverlayColor(Color color, float percentage)
        {
            percentage = Mathf.Clamp01(percentage);
            var newColor = color + BaseColor * percentage;
            newColor.a = color.a;
            return newColor;
        }

        public static Color ScaleRGB(this Color color, float factor)
        {
            return new Color(color.r * factor, color.g * factor, color.b * factor, color.a);
        }

        public static bool IsPointerOverScreen(Vector2 pointer)
        {
            return !(0 > pointer.x || 0 > pointer.y || Screen.width < pointer.x || Screen.height < pointer.y);
        }
        
        public static T[] FindObjectsByType<T>(FindObjectsInactive findObjectsInactive = FindObjectsInactive.Exclude) where T : Component
        {
#if UNITY_6000_5_OR_NEWER
            return Object.FindObjectsByType<T>(findObjectsInactive);
#else
            return Object.FindObjectsByType<T>(findObjectsInactive, FindObjectsSortMode.None);
#endif 
        }
    }
}
