using Cysharp.Threading.Tasks;

namespace DracoRuan.CoreSystems.DesignPatterns.Command.Core
{
    public abstract class BaseCommand<T> : ICommand<T> where T : class
    {
        public T Receiver { get; set; }

        protected BaseCommand(T receiver)
        {
            Receiver = receiver;
        }

        public abstract UniTask Execute();
        public abstract UniTask Undo();
        public virtual bool CanExecute() => Receiver != null;
    }
} 