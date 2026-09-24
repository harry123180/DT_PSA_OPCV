using UnityEngine;
using UnityEngine.UIElements;

namespace OC.UI.Panel
{
    public class Moveable : Manipulator
    {
        private readonly VisualElement _handle;
        private Vector3 _targetStartPosition;
        private Vector3 _pointerStartPosition;
        
        public Moveable(VisualElement handler)
        {
            _handle = handler;
        }

        protected override void RegisterCallbacksOnTarget()
        {
            _handle.RegisterCallback<PointerDownEvent>(DownEvent);
            _handle.RegisterCallback<PointerMoveEvent>(MoveEvent);
            _handle.RegisterCallback<PointerUpEvent>(UpEvent);
        }

        protected override void UnregisterCallbacksFromTarget()
        {
            _handle.UnregisterCallback<PointerDownEvent>(DownEvent);
            _handle.UnregisterCallback<PointerMoveEvent>(MoveEvent);
            _handle.UnregisterCallback<PointerUpEvent>(UpEvent);
        }

        private void DownEvent(PointerDownEvent evt)
        {
            _pointerStartPosition = evt.position;
            _targetStartPosition = target.worldBound.position;
            _handle.CapturePointer(evt.pointerId);
            target.BringToFront();
        }

        private void MoveEvent(PointerMoveEvent evt)
        {
            if (!_handle.HasPointerCapture(evt.pointerId)) return;
            var delta = evt.position - _pointerStartPosition;
            var position = _targetStartPosition + delta;
            target.style.translate = ClampInParent(target, position);
        }

        private void UpEvent(PointerUpEvent evt)
        {
            _handle.ReleasePointer(evt.pointerId);
        }
        
        private Vector2 ClampInParent(VisualElement element, Vector2 position)
        {
            position.x = Mathf.Clamp(position.x, element.parent.worldBound.xMin, element.parent.worldBound.xMax - element.worldBound.width);
            position.y = Mathf.Clamp(position.y, element.parent.worldBound.yMin, element.parent.worldBound.yMax - element.worldBound.height);
            return position - element.layout.position;
        }
    }
}