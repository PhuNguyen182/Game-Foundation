using System;
using DracoRuan.PrebuildServices.PlayerLoopSystem.Core.Handlers;
using Unity.Profiling;
using UnityEngine;

namespace DracoRuan.PrebuildServices.PlayerLoopSystem.UpdateServices
{
    /// <summary>
    /// Ticks <see cref="IUpdateHandler"/>s once per frame from the player loop. Handlers tick last-registered-first.
    /// <para>
    /// A handler registered before a pass ticks in that pass; one registered during a pass waits for the next.
    /// Handlers may register or deregister anything, themselves included, while ticking. An exception thrown by a
    /// handler is logged and does not stop the rest of the pass. Main thread only.
    /// </para>
    /// </summary>
    public static class UpdateServiceManager
    {
        private static readonly HandlerRegistry<IUpdateHandler> Registry =
            new(UpdateHandlerConstants.InitializedCapacity);

        private static readonly ProfilerMarker TickMarker = new("DracoRuan.UpdateServiceManager.UpdateTime");

        public static void UpdateTime()
        {
            MainThreadGuard.Capture();

            if (!Registry.BeginTick())
            {
                Debug.LogError(
                    $"[{nameof(UpdateServiceManager)}] {nameof(UpdateTime)} was called re-entrantly from inside a " +
                    "handler; the nested call was ignored.");
                return;
            }

            TickMarker.Begin();
            try
            {
                float deltaTime = Time.deltaTime;
                IUpdateHandler[] slots = Registry.Slots;
                int i = Registry.SlotCount - 1;

                // One try block per run of healthy handlers instead of one per handler: on an exception, log it and
                // resume below the handler that threw.
                while (i >= 0)
                {
                    try
                    {
                        for (; i >= 0; i--)
                        {
                            IUpdateHandler handler = slots[i];
                            if (handler != null)
                                handler.Tick(deltaTime);
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

        public static void RegisterUpdateHandler(IUpdateHandler updateHandler)
        {
            MainThreadGuard.Verify(nameof(RegisterUpdateHandler));
            Registry.Register(updateHandler);
        }

        public static void DeregisterUpdateHandler(IUpdateHandler updateHandler)
        {
            MainThreadGuard.Verify(nameof(DeregisterUpdateHandler));
            Registry.Deregister(updateHandler);
        }

        public static void Clear()
        {
            MainThreadGuard.Verify(nameof(Clear));
            Registry.Clear();
        }
    }
}