using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

namespace OC.UI
{
    public class DebugManager : MonoBehaviour
    {
        [Header("Input Actions")]
        [SerializeField]
        private InputActionReference _debug;
        
        private InputAction _debugAction;

        public UnityEvent OnDebugAction; 

        private void OnEnable()
        {
            _debugAction = _debug.action;
            _debugAction.Enable();
            _debugAction.performed += DebugAction;
        }
        
        private void OnDisable()
        {
            if (_debugAction != null)
            {
                _debugAction.performed -= DebugAction;
            }
        }

        private void DebugAction(InputAction.CallbackContext ctx)
        {
            if (ctx.performed)
            {
                OnDebugAction?.Invoke();
            }
        }
    }
}
