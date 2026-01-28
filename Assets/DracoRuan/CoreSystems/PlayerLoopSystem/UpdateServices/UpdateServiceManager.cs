using System.Collections.Generic;
using DracoRuan.CoreSystems.PlayerLoopSystem.Core.Handlers;
using UnityEngine;

namespace DracoRuan.CoreSystems.PlayerLoopSystem.UpdateServices
{
    public static class UpdateServiceManager
    {
        private static readonly List<IUpdateHandler> UpdateTimeServices = new();
        private static readonly List<IUpdateHandler> PendingUpdateTimeServices = new();
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
