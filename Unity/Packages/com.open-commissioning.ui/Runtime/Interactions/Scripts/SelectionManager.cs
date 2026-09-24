using System;
using System.Collections.Generic;
using System.Linq;
using OC.Interactions;
using OC.MaterialFlow;
using OC.UI.TransformHandles;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace OC.UI.Interactions
{
    [DefaultExecutionOrder(-900)]
    public class SelectionManager : MonoBehaviourSingleton<SelectionManager>
    {
        public List<Interaction> SelectedInteractions => _selectedInteractions;
        public List<GameObject> HitGameObjects => _hitGameObjects;
        public HandleRaycastHit HandleRaycastHit => _handleRaycastHit;
        
        public event Action<List<Interaction>> OnSelectionChanged;
        public event Action<Interaction> OnDestroy;

        public bool Enable
        {
            get => _enable;
            set
            {
                if (_enable == value) return;
                _enable = value;
                if (!_enable)
                {
                    ResetHit();
                    ClearSelection();
                }
            }
        }

        [Header("State")] 
        [SerializeField] 
        private bool _enable;
        [SerializeField] 
        private List<GameObject> _hitGameObjects = new ();
        [SerializeField] 
        private HandleRaycastHit _handleRaycastHit;
        [SerializeField]
        private List<Interaction> _selectedInteractions = new();
        
        [Header("Settings")] 
        [SerializeField]
        private LayerMask _layerMask;

        [Header("Input Actions")]
        [SerializeField]
        private InputActionReference _click;
        [SerializeField]
        private InputActionReference _pointer;
        
        [Header("Debug")] 
        [SerializeField]
        private bool _debug;

        private const float MAX_DISTANCE = 500;
        
        private readonly RaycastHit[] _raycastHits = new RaycastHit[10];
        private int _hitsCount;
        
        private Camera _camera;
        private GameObject _closestHitGameObject;
        
        private InputAction _clickAction;
        private InputAction _pointerAction;

        private void OnEnable()
        {
            ResetHit();
            ClearSelection();
            _camera = Camera.main;

            _clickAction = _click.action;
            _pointerAction = _pointer.action;
            
            _clickAction?.Enable();
            _pointerAction?.Enable();

            if (_clickAction != null)
            {
                _clickAction.started += HandleClickAction;
                _clickAction.performed += HandleClickAction;
                _clickAction.canceled += HandleClickAction;
            }
            
            Pool.Instance.PoolManager.OnDestroyAction += PoolManagerOnDestroyAction;
        }
        
        private void OnDisable()
        {
            _clickAction.started -= HandleClickAction;
            _clickAction.performed -= HandleClickAction;
            _clickAction.canceled -= HandleClickAction;
            
            Pool.Instance.PoolManager.OnDestroyAction -= PoolManagerOnDestroyAction;
        }

        private void Update()
        {
            if (!_enable) return;
            if (AppUI.Instance.IsPointerOverUI) return;
            HandleRaycastHits();
        }

        private void HandleClickAction(InputAction.CallbackContext context)
        {
            if (!_enable) return;
            if (_handleRaycastHit.Hit) return;
            if (AppUI.Instance.IsPointerOverUI) return;
            
            if (_debug) Debug.Log($"Handle Click action: {context.phase}");
            
            if (context.performed)
            {
                // Mouse down
                if (_hitGameObjects.Count > 0)
                {
                    var modifier = Keyboard.current.leftCtrlKey.isPressed;
                    
                    if (_selectedInteractions.Count > 0)
                    {
                        var index = (_hitGameObjects.IndexOf(_selectedInteractions.First().gameObject) + 1) % _hitGameObjects.Count;
                        var nextHitGameObject = _hitGameObjects[index];
                        Select(nextHitGameObject, modifier);
                        PointerDownEvent(nextHitGameObject);
                    }
                    else
                    {
                        Select(_closestHitGameObject, modifier);
                        PointerDownEvent(_closestHitGameObject);
                    }
                }
                else
                {
                    ResetHit();
                    ClearSelection();
                }
                
                return;
            }

            if (context.canceled)
            {
                // Mouse up
                
                if (_hitGameObjects.Count > 0)
                {
                    PointerClickEvent(_closestHitGameObject);
                    PointerUpEvent(_closestHitGameObject);
                }
            }
        }

        private void HandleRaycastHits()
        {
            Array.Clear(_raycastHits, 0, _hitsCount);
            _hitGameObjects.Clear();
            _handleRaycastHit.Clear();
            
            var mousePosition = _pointerAction.ReadValue<Vector2>();
            var ray = _camera.ScreenPointToRay(mousePosition);
            _hitsCount = Physics.RaycastNonAlloc(ray.origin, ray.direction, _raycastHits, MAX_DISTANCE, _layerMask.value);

            if (_hitsCount == 0)
            {
                ResetHit();
                return;
            }

            var hits = _raycastHits.OrderBy(hit => hit.distance);
            foreach (var raycast in hits)
            {
                if (raycast.distance < OC.Utils.TOLERANCE) continue;
                if (raycast.collider.gameObject.CompareTag($"Handles"))
                {
                    _handleRaycastHit.Set(raycast.collider, raycast);
                }
                else
                {
                    _hitGameObjects.Add(raycast.collider.gameObject);
                }
            }

            if (_hitGameObjects.Count < 1)
            {
                ResetHit();
                return; 
            }
            
            if (_hitGameObjects.First() == _closestHitGameObject) return;
            if (_closestHitGameObject is not null) PointerExitEvent(_closestHitGameObject);

            _closestHitGameObject = _hitGameObjects.First();
            PointerEnterEvent(_closestHitGameObject);
        }

        private void ResetHit()
        {
            PointerUpEvent(_closestHitGameObject);
            PointerExitEvent(_closestHitGameObject);
            _hitGameObjects.Clear();
            _closestHitGameObject = null;
        }

        public void Deselect(Interaction interaction) => RemoveSelection(interaction);

        private void Select(GameObject go, bool multiple)
        {
            if (!go.TryGetComponent<Interaction>(out var interaction)) return;
            if (interaction.State.Value.HasFlag(InteractionState.Disabled)) return;
            if (!interaction.Mode.HasFlag(Interaction.InteractionMode.Selection)) return;

            if (!multiple) ClearSelection();
            TrySelection(interaction);
        }
        
        private void TrySelection(Interaction interaction)
        {
            if (_selectedInteractions.Contains(interaction))
            {
                RemoveSelection(interaction);
            }
            else
            {
                AddSelection(interaction);
            }
        }

        private void AddSelection(Interaction interaction)
        {
            if (_selectedInteractions.Contains(interaction)) return;
            _selectedInteractions.Add(interaction);
            OnSelect(interaction.gameObject);
            OnSelectionChanged?.Invoke(_selectedInteractions);
        }
        
        private void RemoveSelection(Interaction interaction)
        {
            if (!_selectedInteractions.Contains(interaction)) return;
            _selectedInteractions.Remove(interaction);
            OnDeselect(interaction.gameObject);
            OnSelectionChanged?.Invoke(_selectedInteractions);
        }
        
        private void ClearSelection()
        {
            foreach (var selection in _selectedInteractions)
            {
                OnDeselect(selection.gameObject);
            }
            _selectedInteractions.Clear();
            OnSelectionChanged?.Invoke(_selectedInteractions);
        }
        
        private void PointerEnterEvent(GameObject target)
        {
            ExecuteEvents.Execute(target, new PointerEventData(EventSystem.current), ExecuteEvents.pointerEnterHandler);
        }
        
        private void PoolManagerOnDestroyAction(Payload payload)
        {
            var removeList = new List<Interaction>();
            foreach (var interaction in _selectedInteractions)
            {
                if (interaction.Target.TryGetComponent<Payload>(out var targetPayload))
                {
                    if (targetPayload == payload)
                    {
                        removeList.Add(interaction);
                    }
                }
            }
            
            foreach (var interaction in removeList)
            {
                OnDestroy?.Invoke(interaction);
                RemoveSelection(interaction);
            }
        }
        
        private void PointerExitEvent(GameObject target)
        {
            ExecuteEvents.Execute(target, new PointerEventData(EventSystem.current), ExecuteEvents.pointerExitHandler);
        }

        private void OnSelect(GameObject target)
        {
            ExecuteEvents.Execute(target, new PointerEventData(EventSystem.current), ExecuteEvents.selectHandler);
        }
        
        private void OnDeselect(GameObject target)
        {
            ExecuteEvents.Execute(target, new PointerEventData(EventSystem.current), ExecuteEvents.deselectHandler);
        }

        private void PointerClickEvent(GameObject target)
        {
            ExecuteEvents.Execute(target, new PointerEventData(EventSystem.current), ExecuteEvents.pointerClickHandler);
        }
        
        private void PointerDownEvent(GameObject target)
        {
            ExecuteEvents.Execute(target, new PointerEventData(EventSystem.current), ExecuteEvents.pointerDownHandler);
        }
        
        private void PointerUpEvent(GameObject target)
        {
            ExecuteEvents.Execute(target, new PointerEventData(EventSystem.current), ExecuteEvents.pointerUpHandler);
        }
    }
    
    [Serializable]
    public class HandleRaycastHit
    {
        public bool Hit => _hit;
        public GameObject HitGameObject => _hitGameObject;
        public HandleBase HitHandle => _hitHandle;
        public RaycastHit RaycastHit => _raycastHit;
        
        [SerializeField]
        private bool _hit;
        [SerializeField]
        private GameObject _hitGameObject;
        [SerializeField]
        private HandleBase _hitHandle;
        [SerializeField]
        private RaycastHit _raycastHit;

        public void Set(Collider collider, RaycastHit raycastHit)
        {
            if (collider.TryGetComponent<HandleCollider>(out var handleCollider))
            {
                _hit = true;
                _hitGameObject = handleCollider.gameObject;
                _hitHandle = handleCollider.Handle;
                _raycastHit = raycastHit;
            }
            else
            {
                Clear();
            }
        }
        
        public void Clear()
        {
            _hit = false;
            _hitGameObject = null;
            _hitHandle = null;
            _raycastHit = default;
        }
    }
}
