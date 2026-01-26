using System;

namespace DracoRuan.Foundation.DataFlow.LocalData.DynamicDataControllers
{
    public interface IDynamicGameDataControllerEvent<out TData> where TData : IGameData
    {
        public event Action<TData> OnDataChanged;
    }
}