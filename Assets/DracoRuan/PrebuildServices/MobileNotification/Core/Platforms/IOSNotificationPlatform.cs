#if UNITY_IOS
using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using DracoRuan.PrebuildServices.MobileNotification.Data;
using Unity.Notifications.iOS;

namespace DracoRuan.PrebuildServices.MobileNotification.Core.Platforms
{
    /// <summary>
    /// Backend over <c>iOSNotificationCenter</c>.
    /// </summary>
    /// <remarks>
    /// <para>Compiled whenever the build target is iOS, the Editor included, so it is always
    /// type-checked against the package. It is only instantiated on a device, because the package's
    /// native calls are stripped from Editor builds.</para>
    ///
    /// <para><b>Repeating notifications.</b> A repeating <c>UNTimeIntervalNotificationTrigger</c>
    /// uses its delay as its period, so it cannot fire first after one delay and then repeat on
    /// another. Exact hourly and daily periods are scheduled as calendar triggers instead, which keep
    /// the first fire's clock time. Any other period fires first after one period, and a warning is
    /// logged when that differs from the requested first delay.</para>
    ///
    /// <para><b>Remote interception is one-way.</b> The package registers its native handler the
    /// first time <c>OnRemoteNotificationReceived</c> gains a subscriber and never unregisters it, so
    /// iOS keeps suppressing foreground presentation of remote notifications for the rest of the
    /// session. This class therefore stays subscribed once interception starts, and the service
    /// shows the remote content itself whenever no handler is installed.</para>
    /// </remarks>
    internal sealed class IOSNotificationPlatform : INotificationPlatform
    {
        private const long SecondsPerHour = 60 * 60;
        private const long SecondsPerDay = 24 * SecondsPerHour;

        private const PresentationOption ForegroundPresentation =
            PresentationOption.Alert | PresentationOption.Sound | PresentationOption.Badge;

        private readonly MobileNotificationConfig _config;
        private readonly NotificationLogger _logger;

        private QueryLastRespondedNotificationOp _openedQuery;
        private bool _isInterceptingRemote;

        public IOSNotificationPlatform(MobileNotificationConfig config, NotificationLogger logger)
        {
            this._config = config;
            this._logger = logger;

            iOSNotificationCenter.OnNotificationReceived += this.HandleNotificationReceived;
        }

        public bool IsSupported => true;

        public NotificationPermissionStatus PermissionStatus =>
            iOSNotificationCenter.GetNotificationSettings().AuthorizationStatus switch
            {
                AuthorizationStatus.NotDetermined => NotificationPermissionStatus.NotRequested,
                AuthorizationStatus.Denied => NotificationPermissionStatus.Denied,
                _ => NotificationPermissionStatus.Granted,
            };

        public string DeviceToken { get; private set; }

        public int ApplicationBadge
        {
            get => iOSNotificationCenter.ApplicationBadge;
            set => iOSNotificationCenter.ApplicationBadge = value;
        }

        public bool InterceptRemoteNotifications
        {
            get => this._isInterceptingRemote;
            set
            {
                // See the class remarks: the native side cannot be switched back, so neither is this.
                if (!value || this._isInterceptingRemote)
                    return;

                this._isInterceptingRemote = true;
                iOSNotificationCenter.OnRemoteNotificationReceived += this.HandleRemoteNotificationReceived;
            }
        }

        public event Action<ReceivedNotification> NotificationDelivered;

        public event Action<ReceivedNotification> RemoteNotificationReceived;

        public async UniTask<NotificationPermissionStatus> RequestPermissionAsync(CancellationToken cancellationToken)
        {
            AuthorizationOption options = 0;
            if (this._config.iosRequestAlert)
                options |= AuthorizationOption.Alert;
            if (this._config.iosRequestBadge)
                options |= AuthorizationOption.Badge;
            if (this._config.iosRequestSound)
                options |= AuthorizationOption.Sound;

            // Returns straight away, without a prompt, once the player has answered before; it still
            // refreshes the APNs device token when remote registration is on.
            using var request = new AuthorizationRequest(options, this._config.iosRegisterForRemoteNotifications);
            while (!request.IsFinished)
                await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);

            if (!string.IsNullOrEmpty(request.Error))
                this._logger.Warning($"iOS authorization request reported an error: {request.Error}");

            if (!string.IsNullOrEmpty(request.DeviceToken))
                this.DeviceToken = request.DeviceToken;

            return request.Granted ? NotificationPermissionStatus.Granted : NotificationPermissionStatus.Denied;
        }

        public void OpenNotificationSettings() => iOSNotificationCenter.OpenNotificationSettings();

        public void Schedule(int id, NotificationData notification)
        {
            var iosNotification = new iOSNotification(id.ToString())
            {
                Title = notification.title ?? string.Empty,
                Body = notification.body ?? string.Empty,
                Subtitle = notification.subtitle ?? string.Empty,
                ShowInForeground = this._config.showInForeground,
                ForegroundPresentationOption = ForegroundPresentation,
                CategoryIdentifier = Fallback(notification.iosCategoryId, this._config.iosDefaultCategoryId),
                ThreadIdentifier = notification.groupKey ?? string.Empty,
                Data = notification.customData ?? string.Empty,
                Trigger = this.CreateTrigger(notification),
            };

            int badge = notification.badge > 0 ? notification.badge : this._config.defaultBadge;
            if (badge > 0)
                iosNotification.Badge = badge;

            if (notification.attachmentUrls is { Count: > 0 })
            {
                var attachments = new List<iOSNotificationAttachment>(notification.attachmentUrls.Count);
                foreach (string url in notification.attachmentUrls)
                {
                    if (!string.IsNullOrWhiteSpace(url))
                        attachments.Add(new iOSNotificationAttachment { Url = url });
                }

                iosNotification.Attachments = attachments;
            }

            iOSNotificationCenter.ScheduleNotification(iosNotification);
        }

        public NotificationStatus GetStatus(int id)
        {
            string identifier = id.ToString();

            foreach (iOSNotification scheduled in iOSNotificationCenter.GetScheduledNotifications())
            {
                if (scheduled.Identifier == identifier)
                    return NotificationStatus.Scheduled;
            }

            foreach (iOSNotification delivered in iOSNotificationCenter.GetDeliveredNotifications())
            {
                if (delivered.Identifier == identifier)
                    return NotificationStatus.Delivered;
            }

            return NotificationStatus.NotFound;
        }

        public void CancelScheduled(int id) => iOSNotificationCenter.RemoveScheduledNotification(id.ToString());

        public void CancelDisplayed(int id) => iOSNotificationCenter.RemoveDeliveredNotification(id.ToString());

        public void CancelAllScheduled() => iOSNotificationCenter.RemoveAllScheduledNotifications();

        public void CancelAllDisplayed() => iOSNotificationCenter.RemoveAllDeliveredNotifications();

        public void RegisterChannel(NotificationChannelData channel)
        {
            // Channels are an Android concept; iOS groups settings per app.
        }

        public void DeleteChannel(string channelId)
        {
        }

        public void SetCategories(IReadOnlyList<NotificationCategoryData> categories)
        {
            var iosCategories = new List<iOSNotificationCategory>(categories.Count);
            foreach (NotificationCategoryData category in categories)
            {
                var iosCategory = new iOSNotificationCategory(category.categoryId);
                if (category.actions != null)
                {
                    foreach (NotificationActionData action in category.actions)
                        iosCategory.AddAction(CreateAction(action));
                }

                iosCategories.Add(iosCategory);
            }

            iOSNotificationCenter.SetNotificationCategories(iosCategories);
        }

        public bool TryGetLastOpened(out ReceivedNotification notification)
        {
            notification = null;

            // The query can take a few frames on a cold start, so it is kept across calls until done.
            this._openedQuery ??= iOSNotificationCenter.QueryLastRespondedNotification();
            if (this._openedQuery.keepWaiting)
                return false;

            QueryLastRespondedNotificationOp query = this._openedQuery;
            this._openedQuery = null;

            if (query.State != QueryLastRespondedNotificationState.HaveRespondedNotification)
                return false;

            notification = ToReceivedNotification(query.Notification, query.ActionId, query.UserText);
            return notification != null;
        }

        public void Dispose()
        {
            iOSNotificationCenter.OnNotificationReceived -= this.HandleNotificationReceived;
            if (this._isInterceptingRemote)
                iOSNotificationCenter.OnRemoteNotificationReceived -= this.HandleRemoteNotificationReceived;
        }

        private iOSNotificationTrigger CreateTrigger(NotificationData notification)
        {
            // A time interval trigger refuses anything under a second, so "now" means one second.
            if (!notification.repeats)
            {
                return new iOSNotificationTimeIntervalTrigger
                {
                    TimeInterval = TimeSpan.FromSeconds(Math.Max(1, notification.fireTimeInSeconds)),
                };
            }

            DateTime firstFire = DateTime.Now.AddSeconds(notification.fireTimeInSeconds);
            switch (notification.repeatInterval)
            {
                case SecondsPerHour:
                    return new iOSNotificationCalendarTrigger
                    {
                        Minute = firstFire.Minute,
                        Second = firstFire.Second,
                        Repeats = true,
                    };

                case SecondsPerDay:
                    return new iOSNotificationCalendarTrigger
                    {
                        Hour = firstFire.Hour,
                        Minute = firstFire.Minute,
                        Second = firstFire.Second,
                        Repeats = true,
                    };
            }

            if (notification.fireTimeInSeconds != notification.repeatInterval)
            {
                this._logger.Warning($"'{notification.title}' repeats every {notification.repeatInterval}s; " +
                                     $"iOS fires it first after {notification.repeatInterval}s, not after " +
                                     $"the requested {notification.fireTimeInSeconds}s.");
            }

            return new iOSNotificationTimeIntervalTrigger
            {
                TimeInterval = TimeSpan.FromSeconds(notification.repeatInterval),
                Repeats = true,
            };
        }

        private static iOSNotificationAction CreateAction(NotificationActionData action)
        {
            iOSNotificationActionOptions options = iOSNotificationActionOptions.None;
            if (action.foreground)
                options |= iOSNotificationActionOptions.Foreground;
            if (action.destructive)
                options |= iOSNotificationActionOptions.Destructive;
            if (action.authenticationRequired)
                options |= iOSNotificationActionOptions.Required;

            if (!action.textInput)
                return new iOSNotificationAction(action.actionId, action.title, options);

            return new iOSTextInputNotificationAction(action.actionId, action.title, options,
                action.textInputButtonTitle ?? string.Empty)
            {
                TextInputPlaceholder = action.textInputPlaceholder ?? string.Empty,
            };
        }

        private void HandleNotificationReceived(iOSNotification notification) =>
            this.NotificationDelivered?.Invoke(ToReceivedNotification(notification));

        private void HandleRemoteNotificationReceived(iOSNotification notification) =>
            this.RemoteNotificationReceived?.Invoke(ToReceivedNotification(notification));

        private static ReceivedNotification ToReceivedNotification(iOSNotification notification,
            string actionId = null, string userText = null)
        {
            if (notification == null)
                return null;

            return new ReceivedNotification
            {
                // Remote notifications carry APNs ids, which are not numeric.
                Id = int.TryParse(notification.Identifier, out int id) ? id : NotificationData.InvalidId,
                Identifier = notification.Identifier,
                Title = notification.Title,
                Body = notification.Body,
                Subtitle = notification.Subtitle,
                Data = notification.Data,
                CategoryId = notification.CategoryIdentifier,
                GroupKey = notification.ThreadIdentifier,
                ActionId = actionId,
                UserText = userText,
                IsRemote = notification.Trigger is iOSNotificationPushTrigger,
                UserInfo = notification.UserInfo != null
                    ? new Dictionary<string, string>(notification.UserInfo)
                    : null,
            };
        }

        private static string Fallback(string value, string fallback) =>
            string.IsNullOrWhiteSpace(value) ? fallback ?? string.Empty : value;
    }
}
#endif
