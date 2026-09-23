using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using DracoRuan.PrebuildServices.MobileNotification.Data;

namespace DracoRuan.PrebuildServices.MobileNotification.Core.Platforms
{
    /// <summary>
    /// The native backend behind <see cref="MobileNotificationService"/>: one implementation per
    /// operating system, each a thin translation onto the Unity Mobile Notifications package.
    /// </summary>
    /// <remarks>
    /// Implementations do no validation and swallow no exceptions. The service validates input
    /// before calling in and turns anything thrown into a logged failure, so every rule and every
    /// log line lives in one place.
    /// </remarks>
    internal interface INotificationPlatform : IDisposable
    {
        /// <summary>False when there is no native backend and every call is a no-op.</summary>
        bool IsSupported { get; }

        /// <summary>Queried from the OS on every read, so it reflects changes made in the system settings.</summary>
        NotificationPermissionStatus PermissionStatus { get; }

        /// <summary>The APNs device token from the last permission request, or null.</summary>
        string DeviceToken { get; }

        int ApplicationBadge { get; set; }

        /// <summary>
        /// While true, remote notifications that arrive in the foreground are not shown and are
        /// raised through <see cref="RemoteNotificationReceived"/> instead.
        /// </summary>
        bool InterceptRemoteNotifications { get; set; }

        /// <summary>Raised when a notification is shown while the app is running.</summary>
        event Action<ReceivedNotification> NotificationDelivered;

        /// <summary>Raised only while <see cref="InterceptRemoteNotifications"/> is true.</summary>
        event Action<ReceivedNotification> RemoteNotificationReceived;

        UniTask<NotificationPermissionStatus> RequestPermissionAsync(CancellationToken cancellationToken);

        void OpenNotificationSettings();

        /// <summary>Schedules <paramref name="notification"/> under <paramref name="id"/>, replacing whatever used that id.</summary>
        void Schedule(int id, NotificationData notification);

        NotificationStatus GetStatus(int id);

        void CancelScheduled(int id);

        void CancelDisplayed(int id);

        void CancelAllScheduled();

        void CancelAllDisplayed();

        void RegisterChannel(NotificationChannelData channel);

        void DeleteChannel(string channelId);

        /// <summary>Replaces every registered category.</summary>
        void SetCategories(IReadOnlyList<NotificationCategoryData> categories);

        /// <summary>
        /// Returns the notification the player last opened the app through. Keeps returning the
        /// same one until another is opened; telling repeats apart is the caller's job.
        /// </summary>
        bool TryGetLastOpened(out ReceivedNotification notification);
    }
}
