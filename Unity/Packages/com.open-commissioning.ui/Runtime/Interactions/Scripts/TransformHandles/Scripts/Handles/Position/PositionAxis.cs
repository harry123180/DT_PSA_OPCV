using UnityEngine;

namespace OC.UI.TransformHandles
{
    public class PositionAxis : HandleBase
    {
        [SerializeField]
        protected Vector3 _axis;
        
        private Vector3 _startPosition;
        private Vector3 _interactionOffset;
        private Ray _axisRay;
        
        public override void StartInteraction(Vector3 mousePosition, Vector3 hitPoint)
        {
            base.StartInteraction(mousePosition, hitPoint);
            _startPosition = _parentTransformHandle.transform.position;
            
            
            foreach (var target in _parentTransformHandle.Targets)
            {
                _targetStartPositions.Add(target.transform.position);
            }
            
            var direction = _parentTransformHandle.Coordinate.Value == CoordinateSpace.Local ? _parentTransformHandle.transform.rotation * _axis : _axis;
            
            _axisRay = new Ray(_startPosition, direction);
            Ray cameraRay = _camera.ScreenPointToRay(mousePosition);
            float closestPoint = HandleMathUtils.ClosestPointOnRay(_axisRay, cameraRay);
            Vector3 axisHitPoint = _axisRay.GetPoint(closestPoint);
            _interactionOffset = _startPosition - axisHitPoint;
            
        }
        public override void Interact(Vector3 mousePosition)
        {
            Ray cameraRay = _camera.ScreenPointToRay(mousePosition);
            float clostestPoint = HandleMathUtils.ClosestPointOnRay(_axisRay, cameraRay);
            Vector3 hitPoint = _axisRay.GetPoint(clostestPoint);
            Vector3 offset = hitPoint + _interactionOffset - _startPosition;
            Vector3 position = _startPosition + offset;
            _parentTransformHandle.transform.position = position;

            for (int i = 0; i < _parentTransformHandle.Targets.Count; i++)
            {
                var newPosition = _targetStartPositions[i] + offset;
                _parentTransformHandle.Targets[i].transform.position = newPosition;
                _transformUndoActions[i].NewPosition = newPosition;
            }
        }
        public override void EndInteraction(Vector3 mousePosition)
        {
            _targetStartPositions.Clear();
            base.EndInteraction(mousePosition);
        }
    }
}