using UnityEngine;

namespace OC.UI.Undo
{
    public class TransformUndoAction : ICommand
    {
        public Vector3 NewPosition
        {
            get => _newPosition;
            set => _newPosition = value;
        }

        public Quaternion NewRotation
        {
            get => _newRotation;
            set => _newRotation = value;
        }

        public Vector3 NewScale
        {
            get => _newScale;
            set => _newScale = value;
        }
        
        private readonly Transform _target;
        
        private readonly Vector3 _oldPosition;
        private readonly Quaternion _oldRotation;
        private readonly Vector3 _oldScale;
        private Vector3 _newPosition;
        private Quaternion _newRotation;
        private Vector3 _newScale;

        public TransformUndoAction(Transform target)
        {
            _target = target;
            _oldPosition = target.localPosition;
            _oldRotation = target.localRotation;
            _oldScale = target.localScale;
            _newPosition = target.localPosition;
            _newRotation = target.localRotation;
            _newScale = target.localScale;
        }

        public void Capture(Transform target)
        {
            _newPosition = target.localPosition;
            _newRotation = target.localRotation;
            _newScale = target.localScale;
        }

        public void Execute()
        {
            RuntimeUndoSystem.Instance.Execute(this);
        }
        
        public void Undo()
        {
            if (_target == null) return;
            
            _target.localPosition = _oldPosition;
            _target.localRotation = _oldRotation;
            _target.localScale = _oldScale;
        }

        public void Redo()
        {
            if (_target == null) return;
            
            _target.localPosition = _newPosition;
            _target.localRotation = _newRotation;
            _target.localScale = _newScale;
        }

        public override string ToString()
        {
            if (_target == null) return "TransformUndoAction (Target destroyed)";

            return
                $"TransformUndoAction [{_target.name}]\n" +
                $"Position: {FormatVector(_oldPosition)} -> {FormatVector(_newPosition)}\n" +
                $"Rotation: {FormatVector(_oldRotation.eulerAngles)} -> {FormatVector(_newRotation.eulerAngles)}\n" +
                $"Scale: {FormatVector(_oldScale)} -> {FormatVector(_newScale)}";
        }
        
        private static string FormatVector(Vector3 v)
        {
            return $"({v.x:F2}, {v.y:F2}, {v.z:F2})";
        }
    }
}