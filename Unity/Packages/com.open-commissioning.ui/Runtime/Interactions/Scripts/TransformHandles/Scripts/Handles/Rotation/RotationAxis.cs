using UnityEngine;

namespace OC.UI.TransformHandles
{
    public class RotationAxis : HandleBase
    {
        [SerializeField]
        private Vector3 _axis;
        private Vector3 _rotatedAxis;
        private Plane _axisPlane;
        private Vector3 _tangent;
        private Vector3 _biTangent;
        [SerializeField]
        private Quaternion _startRotation;

        public override void Interact(Vector3 mousePosition)
        {
            Ray cameraRay = _camera.ScreenPointToRay(mousePosition);

            if (!_axisPlane.Raycast(cameraRay, out float hitT))
            {
                return;
            }
            Vector3 hitPoint = cameraRay.GetPoint(hitT);
            Vector3 hitDirection = (hitPoint - _parentTransformHandle.transform.position).normalized;
            float x = Vector3.Dot(hitDirection, _tangent);
            float y = Vector3.Dot(hitDirection, _biTangent);
            float angleRadians = Mathf.Atan2(y, x);
            float angleDegrees = angleRadians * Mathf.Rad2Deg;

            if (_parentTransformHandle.RotationSnap != 0)
            {
                angleDegrees = Mathf.Round(angleDegrees / _parentTransformHandle.RotationSnap) * _parentTransformHandle.RotationSnap;
                angleRadians = angleDegrees * Mathf.Deg2Rad;
            }
            
            if (_parentTransformHandle.Coordinate.Value == CoordinateSpace.Local)
            {
                for (int i = 0; i < _parentTransformHandle.Targets.Count; i++)
                {
                    _parentTransformHandle.Targets[i].transform.rotation = _targetStartRotations[i] * Quaternion.AngleAxis(angleDegrees, _axis);
                    _transformUndoActions[i].Capture(_parentTransformHandle.Targets[i].transform);
                }
            }
            else
            {
                for (int i = 0; i < _parentTransformHandle.Targets.Count; i++)
                {
                    _parentTransformHandle.Targets[i].transform.RotateAround(_parentTransformHandle.transform.position, _axis, angleDegrees);
                    _transformUndoActions[i].Capture(_parentTransformHandle.Targets[i].transform);
                }
                CalculateTangents(mousePosition, hitPoint);
            }
        }

        public override void StartInteraction(Vector3 mousePosition, Vector3 hitPoint)
        {
            base.StartInteraction(mousePosition, hitPoint);
            SetStartRotations();
            CalculateTangents(mousePosition, hitPoint);
        }

        public override void EndInteraction(Vector3 mousePosition)
        {
            base.EndInteraction(mousePosition);
            _targetStartRotations.Clear();
        }

        private void CalculateTangents(Vector3 mousePosition, Vector3 hitPoint)
        {
            _axisPlane = new Plane(_rotatedAxis, _parentTransformHandle.transform.position);
            Vector3 startHitPoint;
            Ray cameraRay = _camera.ScreenPointToRay(mousePosition);
            if (_axisPlane.Raycast(cameraRay, out float lenghtOfPlaneEnterpoint))
            {
                startHitPoint = cameraRay.GetPoint(lenghtOfPlaneEnterpoint);
            }
            else
            {
                startHitPoint = _axisPlane.ClosestPointOnPlane(hitPoint);
            }
            _tangent = (startHitPoint - _parentTransformHandle.transform.position).normalized;
            _biTangent = Vector3.Cross(_rotatedAxis, _tangent);
        }

        public void SetStartRotations()
        {
            _startRotation = _parentTransformHandle.Coordinate.Value == CoordinateSpace.Local ? _parentTransformHandle.transform.localRotation : _parentTransformHandle.transform.rotation;

            foreach (var runtimeInspector in _parentTransformHandle.Targets)
            {
                _targetStartRotations.Add(runtimeInspector.transform.rotation);
            }
            
            if (_parentTransformHandle.Coordinate.Value == CoordinateSpace.Local)
            {
                _rotatedAxis = _startRotation * _axis;
            }
            else
            {
                _rotatedAxis = _axis;
            }
        }
    }
}