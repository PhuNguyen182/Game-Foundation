using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine.Pool;

namespace DracoRuan.CoreSystems.DesignPatterns.Command.Core
{
    public class CommandInvoker : IDisposable
    {
        private readonly Stack<ICommand> _commandHistory = new();
        private readonly Stack<ICommand> _undoHistory = new();
        
        public bool CanUndo => _commandHistory.Count > 0;
        public bool CanRedo => _undoHistory.Count > 0;

        public UniTask ExecuteCommand(ICommand command)
        {
            if (command == null || !command.CanExecute())
                return UniTask.CompletedTask;

            this._commandHistory.Push(command);
            this._undoHistory.Clear(); // Clear undo history when new command is executed
            return command.Execute();
        }

        public UniTask Undo()
        {
            if (this._commandHistory.Count == 0)
                return UniTask.CompletedTask;

            ICommand command = this._commandHistory.Pop();
            this._undoHistory.Push(command);
            return command.Undo();
        }

        public UniTask Redo()
        {
            if (this._undoHistory.Count == 0)
                return UniTask.CompletedTask;

            ICommand command = this._undoHistory.Pop();
            if (!command.CanExecute())
                return UniTask.CompletedTask;

            this._commandHistory.Push(command);
            return command.Execute();
        }

        public async UniTask ExecuteCommandsSequence(IEnumerable<ICommand> commands)
        {
            foreach (ICommand command in commands)
                await command.Execute();
        }

        public async UniTask ExecuteCommandsSimultaneously(IEnumerable<ICommand> commands)
        {
            using (ListPool<UniTask>.Get(out List<UniTask> tasks))
            {
                foreach (ICommand command in commands)
                    tasks.Add(command.Execute());
                
                await UniTask.WhenAll(tasks);
            }
        }

        public void Dispose()
        {
            this._commandHistory.Clear();
            this._undoHistory.Clear();
        }
    }
} 