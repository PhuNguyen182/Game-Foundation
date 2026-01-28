using Cysharp.Threading.Tasks;

namespace DracoRuan.CoreSystems.DesignPatterns.Command.Core
{
    public interface ICommand
    {
        public UniTask Execute();
        public UniTask Undo();
        public bool CanExecute();
    }

    public interface ICommand<T> : ICommand where T : class
    {
        public T Receiver { get; set; }
    }
} 