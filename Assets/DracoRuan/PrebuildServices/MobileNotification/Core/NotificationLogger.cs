using UnityEngine;

namespace DracoRuan.PrebuildServices.MobileNotification.Core
{
    /// <summary>
    /// Prefixes every message with the service tag. Info is gated by
    /// <see cref="Data.MobileNotificationConfig.enableDebugLogs"/>; warnings and errors never are.
    /// </summary>
    internal sealed class NotificationLogger
    {
        private const string LogTag = "[MobileNotification]";

        public NotificationLogger(bool isVerbose) => this.IsVerbose = isVerbose;

        /// <summary>Check before building an expensive message for <see cref="Info"/>.</summary>
        public bool IsVerbose { get; }

        public void Info(string message)
        {
            if (this.IsVerbose)
                Debug.Log($"{LogTag} {message}");
        }

        public void Warning(string message) => Debug.LogWarning($"{LogTag} {message}");

        public void Error(string message) => Debug.LogError($"{LogTag} {message}");
    }
}
