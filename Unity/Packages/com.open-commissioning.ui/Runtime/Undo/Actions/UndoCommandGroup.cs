using System.Collections.Generic;

namespace OC.UI.Undo
{
    public class UndoCommandGroup : ICommand
    {
        private readonly List<ICommand> _commands = new();
        
        public ICommand this[int index]
        {
            get => _commands[index];
            set => _commands[index] = value;
        }

        public void Execute()
        {
            RuntimeUndoSystem.Instance.Execute(this);
        }

        public void Undo()
        {
            foreach (var command in _commands)
            {
                command.Undo();
            }
        }

        public void Redo()
        {
            foreach (var command in _commands)
            {
                command.Redo();
            }
        }

        public void Add(ICommand command)
        {
            _commands.Add(command);
        }

        public void Clear()
        {
            _commands.Clear();
        }

        public override string ToString()
        {
            if (_commands.Count == 0)
                return "UndoCommandGroup (Empty)";

            var result = $"UndoCommandGroup ({_commands.Count} commands)\n";

            for (var i = 0; i < _commands.Count; i++)
            {
                result += $"[{i}] {_commands[i]}\n";
            }

            return result;
        }
    }
}