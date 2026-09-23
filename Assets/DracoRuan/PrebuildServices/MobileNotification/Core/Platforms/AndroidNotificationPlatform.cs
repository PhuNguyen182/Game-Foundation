#if UNITY_ANDROID
using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using DracoRuan.PrebuildServices.MobileNotification.Data;
using Unity.Notifications.Android;
using AndroidNotificationStatus = Unity.Notifications.Android.NotificationStatus;
using AndroidPermissionStatus = Unity.Notifications.Android.PermissionStatus;
using NotificationStatus = DracoRuan.PrebuildServices.MobileNotification.Data.NotificationStatus;

namespace DracoRuan.PrebuildServices.MobileNotification.Core.Platforms
{
    /// <summary>
    /// Backend over <c>AndroidNotificationCenter</c>.
    /// </summary>
    /// <remarks>
    /// <para>Compiled whenever the build target is Android, the Editor included, so it is always
    /// type-checked against the package. It is only instantiated on a device, because the package's
    /// JNI bridge does not exist in the Editor.</para>
    ///
    /// <para><b>No remote notifications and no badge counter.</b> Android push arrives through
    /// Firebase Cloud Messaging, which this package does not wrap, and Android has no app-wide badge
    /// number; launchers derive it from the notifications on screen. Those members are no-ops.</para>
    ///
    /// <para><b>Surviving a reboot</b> is not a runtime switch: enable "Reschedule on Device
    /// Restart" in Project Settings > Mobile Notifications, which adds the boot receiver to the
    /// manifest.</para>
    /// </remarks>
    internal sealed class AndroidNotificationPlatform : INotificationPlatform
    {
        private readonly MobileNotificationConfig _config;

        public AndroidNotificationPlatform(MobileNotificationConfig config)
        {
            this._config = config;

            if (!AndroidNotificationCenter.Initialize())
                throw new InvalidOperationException("AndroidNotificationCenter failed to initialize.");

            AndroidNotificationCenter.OnNotificationReceived += this.HandleNotificationReceived;
        }

        public bool IsSupported => true;

        public NotificationPermissionStatus PermissionStatus =>
            ToPermissionStatus(AndroidNotificationCenter.UserPermissionToPost);

        public string DeviceToken => null;

        public int ApplicationBadge
        {
            get => 0;
            set { }
        }

        public bool InterceptRemoteNotifications { get; set; }

        public event Action<ReceivedNotification> NotificationDelivered;

        public event Action<ReceivedNotification> RemoteNotificationReceived
        {
            add { }
            remove { }
        }

        public async UniTask<NotificationPermissionStatus> RequestPermissionAsync(CancellationToken cancellationToken)
        {
            // Below Android 13 the request completes immediately. From 13 on it shows the system
            // dialog, unless the player already answered it, and completes when they respond.
            var request = new PermissionRequest();
            while (request.Status == AndroidPermissionStatus.RequestPending)
                await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);

            return ToPermissionStatus(request.Status);
        }

        public void OpenNotificationSettings() => AndroidNotificationCenter.OpenNotificationSettings();

        public void Schedule(int id, NotificationData notification)
        {
            // The constructor, not an object initializer: it sets the package's sentinel defaults
            // (no repeat, no number), which a default-initialized struct would leave at zero.
            var androidNotification = new AndroidNotification(
                notification.title, notification.body, DateTime.Now.AddSeconds(notification.fireTimeInSeconds))
            {
                SmallIcon = Fallback(notification.smallIcon, this._config.androidSmallIcon),
                LargeIcon = Fallback(notification.largeIcon, this._config.androidLargeIcon),
                ShouldAutoCancel = true,
                ShowInForeground = this._config.showInForeground,
                IntentData = notification.customData ?? string.Empty,
                Group = notification.groupKey ?? string.Empty,
            };

            if (notification.repeats)
                androidNotification.RepeatInterval = TimeSpan.FromSeconds(notification.repeatInterval);

            int badge = notification.badge > 0 ? notification.badge : this._config.defaultBadge;
            if (badge > 0)
                androidNotification.Number = badge;

            string channelId = Fallback(notification.androidChannelId, this._config.androidDefaultChannel.channelId);
            AndroidNotificationCenter.SendNotificationWithExplicitID(androidNotification, channelId, id);
        }

        public NotificationStatus GetStatus(int id) =>
            AndroidNotificationCenter.CheckScheduledNotificationStatus(id) switch
            {
                AndroidNotificationStatus.Scheduled => NotificationStatus.Scheduled,
                AndroidNotificationStatus.Delivered => NotificationStatus.Delivered,
                AndroidNotificationStatus.Unknown => NotificationStatus.NotFound,
                _ => NotificationStatus.Unknown,
            };

        public void CancelScheduled(int id) => AndroidNotificationCenter.CancelScheduledNotification(id);

        public void CancelDisplayed(int id) => AndroidNotificationCenter.CancelDisplayedNotification(id);

        public void CancelAllScheduled() => AndroidNotificationCenter.CancelAllScheduledNotifications();

        public void CancelAllDisplayed() => AndroidNotificationCenter.CancelAllDisplayedNotifications();

        public void RegisterChannel(NotificationChannelData channel) =>
            AndroidNotificationCenter.RegisterNotificationChannel(
                new AndroidNotificationChannel(channel.channelId, channel.channelName,
                    channel.description ?? string.Empty, (Importance)channel.importance)
                {
                    CanShowBadge = channel.canShowBadge,
                    EnableVibration = channel.enableVibration,
                    EnableLights = channel.enableLights,
                });

        public void DeleteChannel(string channelId) => AndroidNotificationCenter.DeleteNotificationChannel(channelId);

        public void SetCategories(IReadOnlyList<NotificationCategoryData> categories)
        {
            // Action buttons are an iOS feature in this package.
        }

        public bool TryGetLastOpened(out ReceivedNotification notification)
        {
            AndroidNotificationIntentData intentData = AndroidNotificationCenter.GetLastNotificationIntent();
            notification = intentData != null ? ToReceivedNotification(intentData) : null;
            return notification != null;
        }

        public void Dispose() => AndroidNotificationCenter.OnNotificationReceived -= this.HandleNotificationReceived;

        private void HandleNotificationReceived(AndroidNotificationIntentData data) =>
            this.NotificationDelivered?.Invoke(ToReceivedNotification(data));

        private static ReceivedNotification ToReceivedNotification(AndroidNotificationIntentData data) => new()
        {
            Id = data.Id,
            Identifier = data.Id.ToString(),
            Title = data.Notification.Title,
            Body = data.Notification.Text,
            Data = data.Notification.IntentData,
            ChannelId = data.Channel,
            GroupKey = data.Notification.Group,
        };

        private static NotificationPermissionStatus ToPermissionStatus(AndroidPermissionStatus status) => status switch
        {
            AndroidPermissionStatus.Allowed => NotificationPermissionStatus.Granted,
            AndroidPermissionStatus.NotRequested => NotificationPermissionStatus.NotRequested,
            AndroidPermissionStatus.RequestPending => NotificationPermissionStatus.Pending,
            _ => NotificationPermissionStatus.Denied,
        };

        private static string Fallback(string value, string fallback) =>
            string.IsNullOrWhiteSpace(value) ? fallback ?? string.Empty : value;
    }
}
#endif
