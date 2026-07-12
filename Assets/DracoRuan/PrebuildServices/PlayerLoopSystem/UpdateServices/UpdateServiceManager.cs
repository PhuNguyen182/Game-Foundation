using System.Collections.Generic;
using DracoRuan.PrebuildServices.PlayerLoopSystem.Core.Handlers;
using UnityEngine;

namespace DracoRuan.PrebuildServices.PlayerLoopSystem.UpdateServices
{
    public static class UpdateServiceManager
    {
        private static readonly List<IUpdateHandler> UpdateTimeServices =
            new(capacity: UpdateHandlerConstants.InitializedCapacity);

        private static readonly List<IUpdateHandler> PendingUpdateTimeServices =
            new(capacity: UpdateHandlerConstants.InitializedCapacity);
        
        private static int _currentIndex;
        
        public static void UpdateTime()
        {
            for (_currentIndex = UpdateTimeServices.Count - 1; _currentIndex >= 0; _currentIndex--)
                UpdateTimeServices[_currentIndex].Tick(Time.deltaTime);

            UpdateTimeServices.AddRange(PendingUpdateTimeServices);
            PendingUpdateTimeServices.Clear();
        }

        public static void RegisterUpdateHandler(IUpdateHandler updateHandler) =>
            PendingUpdateTimeServices.Add(updateHandler);

        public static void DeregisterUpdateHandler(IUpdateHandler updateHandler)
        {
            UpdateTimeServices.Remove(updateHandler);
            _currentIndex--;
        }

        public static void Clear()
        {
            UpdateTimeServices.Clear();
            PendingUpdateTimeServices.Clear();
        }
    }
}
