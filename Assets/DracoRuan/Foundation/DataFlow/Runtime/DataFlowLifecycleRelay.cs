using System;
using UnityEngine;

namespace DracoRuan.Foundation.DataFlow.Runtime
{
    /// <summary>
    /// Forwards Unity application lifecycle callbacks to plain C# listeners.
    /// </summary>
    /// <remarks>
    /// <para><b>Why a MonoBehaviour is unavoidable.</b> <c>Application.focusChanged</c> and
    /// <c>Application.wantsToQuit</c> are static events a plain class can subscribe to, but
    /// <c>OnApplicationPause</c> is a message and only a component receives it. On Android and iOS
    /// that is the one callback that reliably fires when the player leaves the game, so without it
    /// there is no dependable "flush now" signal.</para>
    ///
    /// <para><b>Quit is not a substitute.</b> Mobile platforms routinely terminate a backgrounded
    /// app without ever calling <c>OnApplicationQuit</c>, which is why a save-on-quit design loses
    /// data in normal use rather than only in a crash.</para>
    /// </remarks>
    [DefaultExecutionOrder(-100)]
    public sealed class DataFlowLifecycleRelay : MonoBehaviour
    {
        private static DataFlowLifecycleRelay _instance;

        /// <summary>Raised when the app is backgrounded or loses focus — the reliable flush point.</summary>
        public event Action OnSuspending;

        /// <summary>Raised when the app is quitting normally.</summary>
        public event Action OnQuitting;

        /// <summary>
        /// Creates the relay if it does not exist. Hidden and marked don't-destroy-on-load so it
        /// survives scene changes and does not clutter the hierarchy.
        /// </summary>
        public static DataFlowLifecycleRelay GetOrCreate()
        {
            if (_instance != null)
                return _instance;

            GameObject host = new(nameof(DataFlowLifecycleRelay))
            {
                hideFlags = HideFlags.HideAndDontSave
            };

            DontDestroyOnLoad(host);
            _instance = host.AddComponent<DataFlowLifecycleRelay>();
            return _instance;
        }

        private void OnApplicationPause(bool pauseStatus)
        {
            if (pauseStatus)
                this.OnSuspending?.Invoke();
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus)
                this.OnSuspending?.Invoke();
        }

        private void OnApplicationQuit() => this.OnQuitting?.Invoke();

        private void OnDestroy()
        {
            if (_instance == this)
                _instance = null;
        }
    }
}
