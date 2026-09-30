using System;
using System.Threading;
using UnityEngine;

namespace DracoRuan.PrebuildServices.PlayerLoopSystem.UpdateServices
{
    /// <summary>
    /// The handler lists are plain collections, so registering from another thread silently corrupts them. The
    /// managers learn the main thread from the player loop (and at subsystem registration) and refuse calls from any
    /// other thread with an exception instead.
    /// </summary>
    internal static class MainThreadGuard
    {
        // Managed thread ids start at 1, so 0 means "not captured yet": every thread is allowed until a tick has run.
        private static int _mainThreadId;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void CaptureOnLoad() => Capture();

        public static void Capture() => _mainThreadId = Thread.CurrentThread.ManagedThreadId;

        public static void Verify(string operation)
        {
            int mainThreadId = _mainThreadId;
            if (mainThreadId == 0 || Thread.CurrentThread.ManagedThreadId == mainThreadId)
                return;

            throw new InvalidOperationException(
                $"{operation} must be called on the main thread (called from thread " +
                $"{Thread.CurrentThread.ManagedThreadId}, main thread is {mainThreadId}). " +
                "Marshal the call to the main thread first.");
        }
    }
}
