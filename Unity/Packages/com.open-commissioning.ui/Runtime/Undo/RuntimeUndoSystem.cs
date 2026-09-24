using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace OC.UI.Undo
{
    public class RuntimeUndoSystem : MonoBehaviourSingleton<RuntimeUndoSystem>
    {
        [Header("Debug")]
        [SerializeField]
        private bool _debug;
        [Header("Input Actions")]
        [SerializeField]
        private InputActionReference _undo; 
        [SerializeField]
        private InputActionReference _redo; 
        
        private readonly Stack<ICommand> _undoStack = new();
        private readonly Stack<ICommand> _redoStack = new();
        
        private InputAction _undoAction;
        private InputAction _redoAction;

        private void OnEnable()
        {
            _undoAction = _undo.action;
            _redoAction = _redo.action;
            _undoAction?.Enable();
            _redoAction?.Enable();

            if (_undoAction != null)
            {
                _undoAction.performed += UndoActionPerformed;
            }
            
            if (_redoAction != null)
            {
                _redoAction.performed += RedoActionPerformed;
            }
        }

        private void OnDisable()
        {
            _undoAction.performed -= UndoActionPerformed;
        }

        public void Execute(ICommand command)
        {
            if (command == null) return;
            _undoStack.Push(command);
            _redoStack.Clear();
        }

        [NaughtyAttributes.Button]
        public void Undo()
        {
            if (_undoStack.Count == 0) return;
            var command = _undoStack.Pop();
            if (_debug) Debug.Log($"Undo: {command}");
            command.Undo();
            _redoStack.Push(command);
        }
        
        [NaughtyAttributes.Button]
        public void Redo()
        {
            if (_redoStack.Count == 0) return;
            var command = _redoStack.Pop();
            if (_debug) Debug.Log($"Redo: {command}");
            command.Redo();
            _undoStack.Push(command);
        }

        public void Clear()
        {
            _undoStack.Clear();
            _redoStack.Clear();
        }
        
        private void UndoActionPerformed(InputAction.CallbackContext context)
        {
            Undo();
        }
        
        private void RedoActionPerformed(InputAction.CallbackContext context)
        {
            Redo();
        }
    }

    
}
