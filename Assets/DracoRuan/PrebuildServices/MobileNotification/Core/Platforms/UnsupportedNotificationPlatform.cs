using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using DracoRuan.PrebuildServices.MobileNotification.Data;

namespace DracoRuan.PrebuildServices.MobileNotification.Core.Platforms
{
    /// <summary>
    /// Stands in for a native backend in the Editor and on desktop platforms, so code that uses the
    /// service runs unchanged there. Nothing is ever shown; scheduling calls only log.
    /// </summary>
    internal sealed class UnsupportedNotificationPlatform : INotificationPlatform
    {
        private readonly NotificationLogger _logger;

        public UnsupportedNotificationPlatform(NotificationLogger logger) => this._logger = logger;

        public bool IsSupported => false;

        public NotificationPermissionStatus PermissionStatus => NotificationPermissionStatus.Unsupported;

        public string DeviceToken => null;

        public int ApplicationBadge { get; set; }

        public bool InterceptRemoteNotifications { get; set; }

        public event Action<ReceivedNotification> NotificationDelivered
        {
            add { }
            remove { }
        }

        public event Action<ReceivedNotification> RemoteNotificationReceived
        {
            add { }
            remove { }
        }

        public UniTask<NotificationPermissionStatus> RequestPermissionAsync(CancellationToken cancellationToken) =>
            UniTask.FromResult(NotificationPermissionStatus.Unsupported);

        public void OpenNotificationSettings()
        {
        }

        public void Schedule(int id, NotificationData notification) =>
            this._logger.Info($"No notification backend on this platform; #{id} '{notification.title}' " +
                              $"would fire in {notification.fireTimeInSeconds}s.");

        public NotificationStatus GetStatus(int id) => NotificationStatus.Unknown;

        public void CancelScheduled(int id)
        {
        }

        public void CancelDisplayed(int id)
        {
        }

        public void CancelAllScheduled()
        {
        }

        public void CancelAllDisplayed()
        {
        }

        public void RegisterChannel(NotificationChannelData channel)
        {
        }

        public void DeleteChannel(string channelId)
        {
        }

        public void SetCategories(IReadOnlyList<NotificationCategoryData> categories)
        {
        }

        public bool TryGetLastOpened(out ReceivedNotification notification)
        {
            notification = null;
            return false;
        }

        public void Dispose()
        {
        }
    }
}
