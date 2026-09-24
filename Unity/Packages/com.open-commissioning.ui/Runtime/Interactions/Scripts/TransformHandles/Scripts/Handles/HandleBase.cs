using System.Collections.Generic;
using System.Linq;
using OC.UI.Undo;
using UnityEngine;

namespace OC.UI.TransformHandles
{
    public abstract class HandleBase : MonoBehaviour
    {
        public bool Enabled
        {
            get => _enabled;
            set
            {
                _enabled = value;
                Refresh();
            }
        }

        public bool Hovered
        {
            get => _hovered;
            set
            {
                _hovered = value;
                Refresh();
            }
        }

        [SerializeField]
        private bool _enabled;
        [SerializeField]
        private bool _hovered;
        
        [SerializeField]
        protected bool _isInteracting;
        [SerializeField]
        protected List<Vector3> _targetStartPositions = new ();
        [SerializeField]
        protected List<Quaternion> _targetStartRotations = new ();
        [SerializeField]
        protected RuntimeTransformHandle _parentTransformHandle;
        [SerializeField]
        private Color _defaultColor;
        
        protected Camera _camera;

        private readonly UndoCommandGroup _undoCommandGroup = new ();
        protected readonly List<TransformUndoAction> _transformUndoActions = new ();
        private List<Renderer> _renderers = new ();
        private List<Collider> _colliders = new ();

        private void Awake()
        {
            _camera = Camera.main;
            _renderers = GetComponentsInChildren<Renderer>().ToList();
            _colliders = GetComponentsInChildren<Collider>().ToList();
        }
        
        public virtual void StartInteraction(Vector3 mousePosition, Vector3 hitPoint)
        {
            _isInteracting = true;
            
            _undoCommandGroup.Clear();
            _transformUndoActions.Clear();
            
            foreach (var target in _parentTransformHandle.Targets)
            {
                _transformUndoActions.Add(new TransformUndoAction(target.transform));
            }

            foreach (var command in _transformUndoActions)
            {
                _undoCommandGroup.Add(command);
            }
        }
        
        public virtual void Interact(Vector3 mousePosition){}

        public virtual void EndInteraction(Vector3 mousePosition)
        {
            _isInteracting = false;
            SetColor(_defaultColor);
            
            _undoCommandGroup.Execute();
        }

        private void Refresh()
        {
            if (_enabled)
            {
                if (_hovered)
                {
                    SetColor(Color.yellow);
                }
                else
                {
                    SetColor(_defaultColor);
                }
            }
            else
            {
                SetColor(Color.gray);
            }
        }
        
        private void SetColor(Color color)
        {
            foreach (var item in _renderers)
            {
                item.material.color = color;
            }
        }
    }
}