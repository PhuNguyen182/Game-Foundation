using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using DracoRuan.Foundation.Initializers.Interfaces;
using DracoRuan.PrebuildServices.MobileNotification.Core.Platforms;
using DracoRuan.PrebuildServices.MobileNotification.Data;
using DracoRuan.PrebuildServices.MobileNotification.Interfaces;
using UnityEngine;
using VContainer.Unity;

namespace DracoRuan.PrebuildServices.MobileNotification.Core
{
    /// <summary>
    /// The notification service: validates and logs every call, then hands it to the
    /// <see cref="INotificationPlatform"/> for the device it runs on.
    /// </summary>
    /// <remarks>
    /// <para><b>Synchronous startup.</b> Registering channels and categories is a handful of
    /// synchronous native calls, so the constructor finishes ready and
    /// <see cref="IAsyncInitializable.IsInitialized"/> is true immediately. The permission request is
    /// deliberately not part of startup: it may put a system dialog on screen, and the boot pipeline
    /// must not wait on the player.</para>
    ///
    /// <para><b>Opened notifications are polled, not pushed.</b> Neither platform reports a tap
    /// through an event: Android keeps it in the activity intent and iOS in its last response, and
    /// both keep returning the same one until another is opened. The service checks on start, when
    /// the app regains focus and every <see cref="MobileNotificationConfig.openedCheckInterval"/>
    /// seconds, and raises <see cref="NotificationOpened"/> only when the answer changes. Tapping the
    /// same repeating notification twice in one session is therefore reported once.</para>
    /// </remarks>
    public sealed class MobileNotificationService : IMobileNotificationService, IAsyncInitializable, IStartable,
        ITickable, IDisposable
    {
        /// <summary>
        /// Frames to keep checking for the launch notification. iOS may need up to ten after a cold
        /// start before it can say whether the app was opened through one.
        /// </summary>
        private const int LaunchCheckFrames = 15;

        private readonly MobileNotificationConfig _config;
        private readonly NotificationLogger _logger;
        private readonly INotificationPlatform _platform;
        private readonly Queue<ReceivedNotification> _pendingOpened = new();
        private readonly CancellationTokenSource _lifetimeCancellation = new();
        private readonly System.Random _idRandom = new();

        private Action<ReceivedNotification> _notificationOpened;
        private Func<ReceivedNotification, NotificationData> _remoteNotificationHandler;
        private NotificationPermissionStatus _lastPermissionStatus;
        private string _lastOpenedKey;
        private float _nextOpenedCheckTime;
        private bool _isRequestingPermission;
        private bool _isDisposed;

        public MobileNotificationService(MobileNotificationConfig config)
        {
            this._config = ResolveConfig(config);
            this._logger = new NotificationLogger(this._config.enableDebugLogs);
            this._platform = CreatePlatform(this._config, this._logger);

            this._platform.NotificationDelivered += this.HandleNotificationDelivered;
            this._platform.RemoteNotificationReceived += this.HandleRemoteNotificationReceived;

            this.ApplyConfig();
            this._lastPermissionStatus = this.PermissionStatus;
            this.IsReady = true;

            this._logger.Info($"Ready ({(this.IsSupported ? "native backend" : "no backend on this platform")}, " +
                              $"permission {this._lastPermissionStatus}).");
        }

        /// <inheritdoc />
        public bool IsReady { get; private set; }

        /// <inheritdoc />
        public bool IsSupported => this._platform.IsSupported;

        /// <inheritdoc />
        public NotificationPermissionStatus PermissionStatus
        {
            get
            {
                try
                {
                    return this._platform.PermissionStatus;
                }
                catch (Exception exception)
                {
                    this._logger.Error($"Reading the permission status failed: {exception}");
                    return this._lastPermissionStatus;
                }
            }
        }

        /// <inheritdoc />
        public string DeviceToken => this._platform.DeviceToken;

        /// <inheritdoc />
        public int ApplicationBadge
        {
            get => this._platform.ApplicationBadge;
            set => this.Run(nameof(this.ApplicationBadge), () => this._platform.ApplicationBadge = Mathf.Max(0, value));
        }

        /// <inheritdoc />
        public event Action<NotificationPermissionStatus> PermissionStatusChanged;

        /// <inheritdoc />
        public event Action<string> DeviceTokenReceived;

        /// <inheritdoc />
        public event Action<ReceivedNotification> NotificationDelivered;

        /// <inheritdoc />
        public event Action<ReceivedNotification> NotificationOpened
        {
            add
            {
                this._notificationOpened += value;
                this.FlushPendingOpened();
            }
            remove => this._notificationOpened -= value;
        }

        bool IAsyncInitializable.IsInitialized() => this.IsReady;

        public void Start()
        {
            if (this._isDisposed)
                return;

            Application.focusChanged += this.HandleFocusChanged;
            this.CheckLaunchNotificationAsync(this._lifetimeCancellation.Token).Forget();

            // Once permission is granted a request shows nothing, yet on iOS it is still what
            // refreshes the APNs device token, so it runs every session in that case too.
            if (this._config.autoRequestPermission || this._lastPermissionStatus == NotificationPermissionStatus.Granted)
                this.RequestPermissionAsync(this._lifetimeCancellation.Token).Forget();
        }

        public void Tick()
        {
            if (this._isDisposed || this._config.openedCheckInterval <= 0f || Time.unscaledTime < this._nextOpenedCheckTime)
                return;

            this._nextOpenedCheckTime = Time.unscaledTime + this._config.openedCheckInterval;
            this.CheckOpenedNotification();
        }

        public void Dispose()
        {
            if (this._isDisposed)
                return;

            this._isDisposed = true;
            this.IsReady = false;

            Application.focusChanged -= this.HandleFocusChanged;
            this._lifetimeCancellation.Cancel();
            this._lifetimeCancellation.Dispose();

            this._platform.NotificationDelivered -= this.HandleNotificationDelivered;
            this._platform.RemoteNotificationReceived -= this.HandleRemoteNotificationReceived;
            this._platform.Dispose();

            this._pendingOpened.Clear();
        }

        #region Permission

        /// <inheritdoc />
        public async UniTask<NotificationPermissionStatus> RequestPermissionAsync(
            CancellationToken cancellationToken = default)
        {
            if (!this.EnsureUsable(nameof(this.RequestPermissionAsync)))
                return this._lastPermissionStatus;

            if (!this.IsSupported)
                return NotificationPermissionStatus.Unsupported;

            // A second caller waits for the prompt already on screen instead of stacking another.
            if (this._isRequestingPermission)
            {
                await UniTask.WaitWhile(() => this._isRequestingPermission, cancellationToken: cancellationToken);
                return this._lastPermissionStatus;
            }

            this._isRequestingPermission = true;
            string previousToken = this._platform.DeviceToken;

            try
            {
                using var linkedCancellation =
                    CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, this._lifetimeCancellation.Token);

                this._logger.Info("Requesting permission.");
                NotificationPermissionStatus status = await this._platform.RequestPermissionAsync(linkedCancellation.Token);
                this._logger.Info($"Permission request finished: {status}.");

                this.UpdatePermissionStatus(status);

                string token = this._platform.DeviceToken;
                if (!string.IsNullOrEmpty(token) && token != previousToken)
                {
                    this._logger.Info("Received an APNs device token.");
                    this.InvokeSafely(this.DeviceTokenReceived, token, nameof(this.DeviceTokenReceived));
                }

                return status;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                this._logger.Error($"Permission request failed: {exception}");
                return this.PermissionStatus;
            }
            finally
            {
                this._isRequestingPermission = false;
            }
        }

        /// <inheritdoc />
        public void OpenNotificationSettings() =>
            this.Run(nameof(this.OpenNotificationSettings), this._platform.OpenNotificationSettings);

        #endregion

        #region Scheduling

        /// <inheritdoc />
        public int Schedule(NotificationData notification)
        {
            if (!this.EnsureUsable(nameof(this.Schedule)))
                return NotificationData.InvalidId;

            if (notification == null)
            {
                this._logger.Error("Schedule was given no notification.");
                return NotificationData.InvalidId;
            }

            if (!notification.TryValidate(out string error))
            {
                this._logger.Error($"Refused to schedule '{notification.title}': {error}.");
                return NotificationData.InvalidId;
            }

            int id = notification.identifier != NotificationData.AutoId ? notification.identifier : this.NextId();

            try
            {
                this._platform.Schedule(id, notification);
            }
            catch (Exception exception)
            {
                this._logger.Error($"Failed to schedule #{id} '{notification.title}': {exception}");
                return NotificationData.InvalidId;
            }

            if (this._logger.IsVerbose)
            {
                string repeat = notification.repeats ? $", repeating every {notification.repeatInterval}s" : string.Empty;
                this._logger.Info($"Scheduled #{id} '{notification.title}' in {notification.fireTimeInSeconds}s{repeat}.");
            }

            return id;
        }

        /// <inheritdoc />
        public IReadOnlyList<int> ScheduleScenario(NotificationScenario scenario, DateTime? anchorUtc = null)
        {
            var scheduledIds = new List<int>();
            if (!this.EnsureUsable(nameof(this.ScheduleScenario)) || !this.IsScenarioUsable(scenario))
                return scheduledIds;

            long elapsedSeconds = 0;
            if (anchorUtc.HasValue)
            {
                // The parameter is documented as UTC, so an unspecified kind is read as UTC too.
                DateTime anchor = anchorUtc.Value.Kind == DateTimeKind.Local
                    ? anchorUtc.Value.ToUniversalTime()
                    : anchorUtc.Value;
                elapsedSeconds = (long)(DateTime.UtcNow - anchor).TotalSeconds;
            }

            foreach (NotificationData template in scenario.notifications)
            {
                // Cancel first, so an entry skipped below does not survive from an earlier run.
                this.Run(nameof(this.ScheduleScenario), () => this._platform.CancelScheduled(template.identifier));

                if (!TryResolveDelay(template.fireTimeInSeconds, template.repeats, template.repeatInterval,
                        elapsedSeconds, out long delaySeconds))
                {
                    this._logger.Info($"Skipped #{template.identifier} of scenario '{scenario.name}': its time has passed.");
                    continue;
                }

                NotificationData notification = template.Clone();
                notification.fireTimeInSeconds = delaySeconds;
                if (string.IsNullOrWhiteSpace(notification.groupKey))
                    notification.groupKey = scenario.groupKey;

                int id = this.Schedule(notification);
                if (id != NotificationData.InvalidId)
                    scheduledIds.Add(id);
            }

            this._logger.Info($"Scenario '{scenario.name}': scheduled {scheduledIds.Count}/{scenario.notifications.Count}.");
            return scheduledIds;
        }

        /// <inheritdoc />
        public NotificationStatus GetStatus(int notificationId)
        {
            if (!this.EnsureUsable(nameof(this.GetStatus)))
                return NotificationStatus.Unknown;

            try
            {
                return this._platform.GetStatus(notificationId);
            }
            catch (Exception exception)
            {
                this._logger.Error($"Reading the status of #{notificationId} failed: {exception}");
                return NotificationStatus.Unknown;
            }
        }

        /// <summary>
        /// Moves a delay authored relative to an anchor so it counts from now instead.
        /// </summary>
        /// <param name="fireDelaySeconds">Delay from the anchor.</param>
        /// <param name="repeats">Whether the notification repeats.</param>
        /// <param name="repeatIntervalSeconds">Period of a repeating notification.</param>
        /// <param name="elapsedSeconds">Seconds from the anchor to now; negative for a future anchor.</param>
        /// <param name="delaySeconds">Delay from now.</param>
        /// <returns>
        /// False for a one-time notification whose time has passed. A repeating one moves to its next
        /// future occurrence instead.
        /// </returns>
        internal static bool TryResolveDelay(long fireDelaySeconds, bool repeats, long repeatIntervalSeconds,
            long elapsedSeconds, out long delaySeconds)
        {
            delaySeconds = fireDelaySeconds - elapsedSeconds;
            if (delaySeconds >= 0)
                return true;

            if (!repeats || repeatIntervalSeconds <= 0)
                return false;

            long overdueSeconds = -delaySeconds;
            delaySeconds = repeatIntervalSeconds - overdueSeconds % repeatIntervalSeconds;
            return true;
        }

        #endregion

        #region Cancelling

        /// <inheritdoc />
        public void Cancel(int notificationId) =>
            this.Run(nameof(this.Cancel), () =>
            {
                this._platform.CancelScheduled(notificationId);
                this._platform.CancelDisplayed(notificationId);
                this._logger.Info($"Cancelled #{notificationId}.");
            });

        /// <inheritdoc />
        public void CancelScheduled(int notificationId) =>
            this.Run(nameof(this.CancelScheduled), () => this._platform.CancelScheduled(notificationId));

        /// <inheritdoc />
        public void CancelDisplayed(int notificationId) =>
            this.Run(nameof(this.CancelDisplayed), () => this._platform.CancelDisplayed(notificationId));

        /// <inheritdoc />
        public void CancelScenario(NotificationScenario scenario)
        {
            if (!this.EnsureUsable(nameof(this.CancelScenario)) || !scenario || scenario.notifications == null)
                return;

            foreach (NotificationData notification in scenario.notifications)
            {
                if (notification != null && notification.identifier != NotificationData.AutoId)
                    this.Cancel(notification.identifier);
            }
        }

        /// <inheritdoc />
        public void CancelAllScheduled() =>
            this.Run(nameof(this.CancelAllScheduled), this._platform.CancelAllScheduled);

        /// <inheritdoc />
        public void CancelAllDisplayed() =>
            this.Run(nameof(this.CancelAllDisplayed), this._platform.CancelAllDisplayed);

        /// <inheritdoc />
        public void CancelAll()
        {
            this.CancelAllScheduled();
            this.CancelAllDisplayed();
            this._logger.Info("Cancelled every notification.");
        }

        #endregion

        #region Channels and categories

        /// <inheritdoc />
        public bool RegisterChannel(NotificationChannelData channel)
        {
            if (!this.EnsureUsable(nameof(this.RegisterChannel)))
                return false;

            if (channel == null || !channel.IsValid())
            {
                this._logger.Error($"Refused channel '{channel?.channelId}': it needs an id and a name.");
                return false;
            }

            try
            {
                this._platform.RegisterChannel(channel);
            }
            catch (Exception exception)
            {
                this._logger.Error($"Registering channel '{channel.channelId}' failed: {exception}");
                return false;
            }

            this._logger.Info($"Registered channel '{channel.channelId}'.");
            return true;
        }

        /// <inheritdoc />
        public void DeleteChannel(string channelId)
        {
            if (string.IsNullOrWhiteSpace(channelId))
            {
                this._logger.Error("DeleteChannel was given no channel id.");
                return;
            }

            this.Run(nameof(this.DeleteChannel), () => this._platform.DeleteChannel(channelId));
        }

        /// <inheritdoc />
        public void SetCategories(IReadOnlyList<NotificationCategoryData> categories)
        {
            if (!this.EnsureUsable(nameof(this.SetCategories)))
                return;

            var validCategories = new List<NotificationCategoryData>();
            if (categories != null)
            {
                foreach (NotificationCategoryData category in categories)
                {
                    if (category == null || !category.IsValid())
                    {
                        this._logger.Error($"Skipped category '{category?.categoryId}': it needs an id.");
                        continue;
                    }

                    if (category.actions != null && category.actions.Exists(action => action == null || !action.IsValid()))
                    {
                        this._logger.Error($"Skipped category '{category.categoryId}': every action needs an id and a title.");
                        continue;
                    }

                    validCategories.Add(category);
                }
            }

            this.Run(nameof(this.SetCategories), () => this._platform.SetCategories(validCategories));
        }

        /// <inheritdoc />
        public void SetRemoteNotificationHandler(Func<ReceivedNotification, NotificationData> handler)
        {
            if (!this.EnsureUsable(nameof(this.SetRemoteNotificationHandler)))
                return;

            this._remoteNotificationHandler = handler;
            if (handler != null)
                this.Run(nameof(this.SetRemoteNotificationHandler), () => this._platform.InterceptRemoteNotifications = true);
        }

        #endregion

        #region Receiving

        private async UniTaskVoid CheckLaunchNotificationAsync(CancellationToken cancellationToken)
        {
            for (int frame = 0; frame < LaunchCheckFrames; frame++)
            {
                if (this.CheckOpenedNotification())
                    return;

                await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
            }
        }

        /// <summary>Returns true once the platform has answered and the answer is a notification.</summary>
        private bool CheckOpenedNotification()
        {
            ReceivedNotification notification;
            try
            {
                if (!this._platform.TryGetLastOpened(out notification))
                    return false;
            }
            catch (Exception exception)
            {
                this._logger.Error($"Checking for an opened notification failed: {exception}");
                return false;
            }

            // Both platforms keep returning the last opened notification, so only a new one counts.
            string key = $"{notification.Identifier}|{notification.ActionId}";
            if (key == this._lastOpenedKey)
                return true;

            this._lastOpenedKey = key;
            this._logger.Info(notification.ActionId == null
                ? $"Opened through #{notification.Identifier} '{notification.Title}'."
                : $"Opened through #{notification.Identifier} '{notification.Title}', action '{notification.ActionId}'.");

            this._pendingOpened.Enqueue(notification);
            this.FlushPendingOpened();
            return true;
        }

        private void FlushPendingOpened()
        {
            while (this._notificationOpened != null && this._pendingOpened.Count > 0)
                this.InvokeSafely(this._notificationOpened, this._pendingOpened.Dequeue(), nameof(this.NotificationOpened));
        }

        private void HandleNotificationDelivered(ReceivedNotification notification)
        {
            this._logger.Info($"Delivered #{notification.Identifier} '{notification.Title}' while running.");
            this.InvokeSafely(this.NotificationDelivered, notification, nameof(this.NotificationDelivered));
        }

        private void HandleRemoteNotificationReceived(ReceivedNotification notification)
        {
            NotificationData replacement = notification.ToNotificationData();
            if (this._remoteNotificationHandler != null)
            {
                try
                {
                    replacement = this._remoteNotificationHandler(notification);
                }
                catch (Exception exception)
                {
                    // Showing the original beats losing it: iOS no longer shows it on its own.
                    this._logger.Error($"The remote notification handler threw; showing the original: {exception}");
                }
            }

            if (replacement == null)
            {
                this._logger.Info($"Remote notification '{notification.Title}' was suppressed by the handler.");
                return;
            }

            this.Schedule(replacement);
        }

        private void HandleFocusChanged(bool hasFocus)
        {
            if (!hasFocus || this._isDisposed)
                return;

            // Returning from the settings screen is the only way a denial turns into a grant.
            this.UpdatePermissionStatus(this.PermissionStatus);
            this.CheckOpenedNotification();
        }

        #endregion

        #region Helpers

        private static MobileNotificationConfig ResolveConfig(MobileNotificationConfig config)
        {
            if (!config)
            {
                Debug.LogError("[MobileNotification] No MobileNotificationConfig was supplied; using defaults.");
                return ScriptableObject.CreateInstance<MobileNotificationConfig>();
            }

            if (!config.TryValidate(out string error))
            {
                Debug.LogError($"[MobileNotification] '{config.name}' is invalid ({error}); using defaults.");
                return ScriptableObject.CreateInstance<MobileNotificationConfig>();
            }

            return config;
        }

        private static INotificationPlatform CreatePlatform(MobileNotificationConfig config, NotificationLogger logger)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                return new AndroidNotificationPlatform(config);
            }
            catch (Exception exception)
            {
                logger.Error($"The Android backend failed to start; notifications are disabled: {exception}");
            }
#elif UNITY_IOS && !UNITY_EDITOR
            try
            {
                return new IOSNotificationPlatform(config, logger);
            }
            catch (Exception exception)
            {
                logger.Error($"The iOS backend failed to start; notifications are disabled: {exception}");
            }
#endif
            return new UnsupportedNotificationPlatform(logger);
        }

        private void ApplyConfig()
        {
            this.RegisterChannel(this._config.androidDefaultChannel);
            if (this._config.androidChannels != null)
            {
                foreach (NotificationChannelData channel in this._config.androidChannels)
                    this.RegisterChannel(channel);
            }

            if (this._config.iosCategories is { Count: > 0 })
                this.SetCategories(this._config.iosCategories);

            if (this._config.clearDisplayedOnStart && this.IsSupported)
            {
                this.CancelAllDisplayed();
                this.ApplicationBadge = 0;
            }
        }

        private void UpdatePermissionStatus(NotificationPermissionStatus status)
        {
            if (status == this._lastPermissionStatus)
                return;

            this._logger.Info($"Permission changed: {this._lastPermissionStatus} -> {status}.");
            this._lastPermissionStatus = status;
            this.InvokeSafely(this.PermissionStatusChanged, status, nameof(this.PermissionStatusChanged));
        }

        private bool IsScenarioUsable(NotificationScenario scenario)
        {
            if (!scenario)
            {
                this._logger.Error("ScheduleScenario was given no scenario.");
                return false;
            }

            if (!scenario.TryValidate(out string error))
            {
                this._logger.Error($"Refused scenario '{scenario.name}': {error}.");
                return false;
            }

            return true;
        }

        /// <summary>A random positive id; collisions across 2^31 values are negligible.</summary>
        private int NextId() => this._idRandom.Next(1, int.MaxValue);

        private bool EnsureUsable(string operation)
        {
            if (!this._isDisposed)
                return true;

            this._logger.Warning($"{operation} was called after the service was disposed.");
            return false;
        }

        private void Run(string operation, Action action)
        {
            if (!this.EnsureUsable(operation))
                return;

            try
            {
                action();
            }
            catch (Exception exception)
            {
                this._logger.Error($"{operation} failed: {exception}");
            }
        }

        /// <summary>Calls every subscriber, so one that throws does not starve the rest.</summary>
        private void InvokeSafely<T>(Action<T> handlers, T argument, string eventName)
        {
            if (handlers == null)
                return;

            foreach (Delegate handler in handlers.GetInvocationList())
            {
                try
                {
                    ((Action<T>)handler).Invoke(argument);
                }
                catch (Exception exception)
                {
                    this._logger.Error($"A {eventName} handler threw: {exception}");
                }
            }
        }

        #endregion
    }
}
