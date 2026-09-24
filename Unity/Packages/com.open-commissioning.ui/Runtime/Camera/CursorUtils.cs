using UnityEngine;
using UnityEngine.InputSystem;

namespace OC.UI
{
    public static class CursorUtils
    {
        public static void WrapCursorOnScreen(this Mouse mouse)
        {
            var screen = new Vector2(Screen.width, Screen.height);
            var position = mouse.position.ReadValue();
            var wrapped = false;
            
            if (position.x < 0)
            {
                position.x = screen.x - 1;
                wrapped = true;
            }
            else if (position.x >= screen.x)
            {
                position.x = 0;
                wrapped = true;
            }

            if (position.y < 0)
            {
                position.y = screen.y - 1;
                wrapped = true;
            }
            else if (position.y >= screen.y)
            {
                position.y = 0;
                wrapped = true;
            }
            
            if (wrapped && Mouse.current != null)
            {
                Mouse.current.WarpCursorPosition(position);
            }
        }
    }
}