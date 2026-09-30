using System;
using DracoRuan.PrebuildServices.PlayerLoopSystem.Core.Handlers;
using Unity.Profiling;
using UnityEngine;

namespace DracoRuan.PrebuildServices.PlayerLoopSystem.UpdateServices
{
    /// <summary>
    /// Ticks <see cref="IFixedUpdateHandler"/>s once per physics step from the player loop; same contract as
    /// <see cref="UpdateServiceManager"/>.
    /// </summary>
    public static class FixedUpdateServiceManager
    {
        private static readonly HandlerRegistry<IFixedUpdateHandler> Registry =
            new(UpdateHandlerConstants.InitializedCapacity);

        private static readonly ProfilerMarker TickMarker =
            new("DracoRuan.FixedUpdateServiceManager.FixedUpdateTime");

        public static void FixedUpdateTime()
        {
            MainThreadGuard.Capture();

            if (!Registry.BeginTick())
            {
                Debug.LogError(
                    $"[{nameof(FixedUpdateServiceManager)}] {nameof(FixedUpdateTime)} was called re-entrantly from " +
                    "inside a handler; the nested call was ignored.");
                return;
            }

            TickMarker.Begin();
            try
            {
                IFixedUpdateHandler[] slots = Registry.Slots;
                int i = Registry.SlotCount - 1;

                // One try block per run of healthy handlers instead of one per handler: on an exception, log it and
                // resume below the handler that threw.
                while (i >= 0)
                {
                    try
                    {
                        for (; i >= 0; i--)
                        {
                            IFixedUpdateHandler handler = slots[i];
                            if (handler != null)
                                handler.Tick();
                        }
                    }
                    catch (Exception exception)
                    {
                        Debug.LogException(exception);
                        i--;
                    }
                }
            }
            finally
            {
                TickMarker.End();
                Registry.EndTick();
            }
        }

        public static void RegisterFixedUpdateHandler(IFixedUpdateHandler updateHandler)
        {
            MainThreadGuard.Verify(nameof(RegisterFixedUpdateHandler));
            Registry.Register(updateHandler);
        }

        public static void DeregisterFixedUpdateHandler(IFixedUpdateHandler updateHandler)
        {
            MainThreadGuard.Verify(nameof(DeregisterFixedUpdateHandler));
            Registry.Deregister(updateHandler);
        }

        public static void Clear()
        {
            MainThreadGuard.Verify(nameof(Clear));
            Registry.Clear();
        }
    }
}