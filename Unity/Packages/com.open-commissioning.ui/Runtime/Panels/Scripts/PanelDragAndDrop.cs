using UnityEngine;
using UnityEngine.UIElements;

namespace OC.UI.Panel
{
    public class PanelDragAndDrop : Manipulator
    {
        private const float THRESHOLD = 5;
        private bool _active;
        private bool _dragging;
        private readonly VisualElement _handle;
        private readonly VisualElement _screen;
        private readonly VisualElement _sideBar;
        private readonly VisualElement _dropzone;
        private readonly IPanel _panelReference;       
        
        private Vector3 _targetStartPosition;
        private Vector3 _pointerStartPosition;
        private Vector2 _pointerOffset;

        public PanelDragAndDrop(IPanel panel)
        {
            _active = false;
            _dragging = false;
            _handle = panel.Root.Q<VisualElement>("header");
            _panelReference = panel;
            _screen = PanelManager.Instance.Screen;
            _sideBar = PanelManager.Instance.Sidebar;
            _dropzone = PanelManager.Instance.Dropzone;
        }

        protected override void RegisterCallbacksOnTarget()
        {
            _handle.RegisterCallback<PointerDownEvent>(OnPointerDown);
            _handle.RegisterCallback<PointerMoveEvent>(OnPointerMove);
            _handle.RegisterCallback<PointerUpEvent>(OnPointerUp);
        }

        protected override void UnregisterCallbacksFromTarget()
        {
            _handle.UnregisterCallback<PointerDownEvent>(OnPointerDown);
            _handle.UnregisterCallback<PointerMoveEvent>(OnPointerMove);
            _handle.UnregisterCallback<PointerUpEvent>(OnPointerUp);
        }

        private void OnPointerDown(PointerDownEvent evt)
        {
            if (evt.button != 0) return;
            
            _active = true;
            _dragging = false;
            
            _handle.CapturePointer(evt.pointerId);
            
            _pointerStartPosition = evt.position;
            _pointerOffset = target.WorldToLocal(evt.position);
        }
        
        private void OnPointerMove(PointerMoveEvent evt)
        {
            if (!_active) return;
            if (!_handle.HasPointerCapture(evt.pointerId)) return;
            
            var delta = evt.position - _pointerStartPosition;

            if (!_dragging)
            {                
                if (delta.sqrMagnitude < THRESHOLD * THRESHOLD) return;
                StartDrag(evt);
            }
            
            var pointerInScreen = _screen.WorldToLocal(evt.position);
            
            var newPos = new Vector2(
                pointerInScreen.x - _pointerOffset.x,
                pointerInScreen.y - _pointerOffset.y
            );

            newPos = ClampToScreen(newPos);

            target.style.left = newPos.x;
            target.style.top = newPos.y;
            evt.StopPropagation();
        }

        private void StartDrag(PointerMoveEvent evt)
        {
            _dragging = true;
            
            if (_handle.HasPointerCapture(evt.pointerId)) _handle.ReleasePointer(evt.pointerId);
            
            var targetPosInScreen = target.parent.ChangeCoordinatesTo(_screen, target.layout.position);

            PanelManager.Instance?.AddToScreen(target);
            
            target.style.left = targetPosInScreen.x;
            target.style.top = targetPosInScreen.y;
            target.style.right = StyleKeyword.Auto;
            target.style.bottom = StyleKeyword.Auto;
            
            _panelReference.Pinned = true;
            
            _handle.CapturePointer(evt.pointerId);
            
            PanelManager.Instance?.EnableDropzone(true);
        }

        private void OnPointerUp(PointerUpEvent evt)
        {
            _active = false;
            
            if (_handle.HasPointerCapture(evt.pointerId)) _handle.ReleasePointer(evt.pointerId);
            
            _dragging = false;

            if (_dropzone.worldBound.Contains(evt.position))
            {
                PanelManager.Instance?.AddToSidebar(target);
            }
            
            PanelManager.Instance?.EnableDropzone(false);
            evt.StopPropagation();
        }
        
        private Vector2 ClampToScreen(Vector2 position)
        {
            var panelWidth = target.resolvedStyle.width;
            var panelHeight = target.resolvedStyle.height;
            var screenWidth = _screen.resolvedStyle.width;
            var screenHeight = _screen.resolvedStyle.height;
            var x = Mathf.Clamp(position.x, 0, screenWidth - panelWidth);
            var y = Mathf.Clamp(position.y, 0, screenHeight - panelHeight);

            return new Vector2(x, y);
        }
    }
}