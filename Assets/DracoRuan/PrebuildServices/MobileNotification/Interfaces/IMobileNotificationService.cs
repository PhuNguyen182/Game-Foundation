using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using DracoRuan.PrebuildServices.MobileNotification.Data;

namespace DracoRuan.PrebuildServices.MobileNotification.Interfaces
{
    /// <summary>
    /// Schedules, cancels and receives mobile notifications on Android and iOS through the Unity
    /// Mobile Notifications package.
    /// </summary>
    /// <remarks>
    /// <para>Every call is safe on any platform. In the Editor and on desktop,
    /// <see cref="IsSupported"/> is false and calls are logged no-ops, so game code needs no
    /// platform checks.</para>
    ///
    /// <para>Failures are logged and reported through return values
    /// (<see cref="NotificationData.InvalidId"/>, false, an empty list), never thrown.</para>
    ///
    /// <para>Platform-specific members say so in their summary and do nothing on the other
    /// platform.</para>
    /// </remarks>
    public interface IMobileNotificationService
    {
        /// <summary>Whether the configured channels and categories are registered and calls are accepted.</summary>
        bool IsReady { get; }

        /// <summary>False when there is no native backend, for example in the Editor.</summary>
        bool IsSupported { get; }

        #region Permission

        /// <summary>
        /// The current permission, read from the OS, so it also reflects changes the player made in
        /// the system settings.
        /// </summary>
        NotificationPermissionStatus PermissionStatus { get; }

        /// <summary>
        /// Raised when <see cref="PermissionStatus"/> changes, whether through
        /// <see cref="RequestPermissionAsync"/> or in the system settings (noticed when the app
        /// regains focus).
        /// </summary>
        event Action<NotificationPermissionStatus> PermissionStatusChanged;

        /// <summary>
        /// Asks the player for permission. Shows the system prompt on Android 13+ and iOS the first
        /// time; afterwards it completes immediately with the stored answer. Concurrent calls share
        /// one prompt.
        /// </summary>
        UniTask<NotificationPermissionStatus> RequestPermissionAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Opens the system notification settings for this app, the only place a player can undo a
        /// denial.
        /// </summary>
        void OpenNotificationSettings();

        #endregion

        #region Scheduling

        /// <summary>
        /// Schedules a local notification, one-time or repeating. Scheduling under an id that is
        /// already in use replaces that notification.
        /// </summary>
        /// <returns>The id to cancel it with, or <see cref="NotificationData.InvalidId"/> on failure.</returns>
        int Schedule(NotificationData notification);

        /// <summary>
        /// Schedules every notification of <paramref name="scenario"/>, replacing the previous run
        /// of the same scenario. Delays count from <paramref name="anchorUtc"/>, or from now when it
        /// is null. One-time notifications whose time has already passed are skipped; repeating ones
        /// move to their next occurrence.
        /// </summary>
        /// <returns>The ids that were scheduled.</returns>
        IReadOnlyList<int> ScheduleScenario(NotificationScenario scenario, DateTime? anchorUtc = null);

        /// <summary>Asks the OS where the notification scheduled under <paramref name="notificationId"/> is.</summary>
        NotificationStatus GetStatus(int notificationId);

        #endregion

        #region Cancelling

        /// <summary>Cancels the notification whether it is still scheduled or already shown.</summary>
        void Cancel(int notificationId);

        /// <summary>Cancels a notification that has not fired yet. One that is already shown stays.</summary>
        void CancelScheduled(int notificationId);

        /// <summary>Removes a shown notification. Future repeats of a repeating one still fire.</summary>
        void CancelDisplayed(int notificationId);

        /// <summary>Cancels every notification of <paramref name="scenario"/>, scheduled or shown.</summary>
        void CancelScenario(NotificationScenario scenario);

        void CancelAllScheduled();

        void CancelAllDisplayed();

        /// <summary>Cancels everything, scheduled and shown.</summary>
        void CancelAll();

        #endregion

        #region Android

        /// <summary>
        /// Android 8.0+: creates the channel, or updates it when the id already exists. Only its
        /// name and description can change after creation.
        /// </summary>
        /// <returns>False when the channel is invalid or the OS refused it.</returns>
        bool RegisterChannel(NotificationChannelData channel);

        /// <summary>Android 8.0+: deletes the channel and every notification posted to it.</summary>
        void DeleteChannel(string channelId);

        #endregion

        #region iOS

        /// <summary>
        /// iOS: replaces every registered category, those from the config included, with
        /// <paramref name="categories"/>. Categories define the action buttons notifications show.
        /// </summary>
        void SetCategories(IReadOnlyList<NotificationCategoryData> categories);

        /// <summary>iOS: the number on the app icon badge. Always 0 on Android.</summary>
        int ApplicationBadge { get; set; }

        /// <summary>
        /// iOS: the APNs device token, once <see cref="RequestPermissionAsync"/> has run with
        /// remote registration enabled in the config. Null until then, and always on Android.
        /// </summary>
        string DeviceToken { get; }

        /// <summary>iOS: raised when a permission request yields a new APNs device token.</summary>
        event Action<string> DeviceTokenReceived;

        /// <summary>
        /// iOS: intercepts remote notifications that arrive while the app is in the foreground.
        /// Instead of iOS showing them, <paramref name="handler"/> receives each one and returns the
        /// notification to show in its place, typically a modified
        /// <see cref="ReceivedNotification.ToNotificationData"/>, or null to show nothing.
        /// </summary>
        /// <remarks>
        /// Once installed, interception lasts for the rest of the session (an iOS limitation).
        /// Passing null afterwards shows remote notifications unchanged rather than handing them
        /// back to iOS.
        /// </remarks>
        void SetRemoteNotificationHandler(Func<ReceivedNotification, NotificationData> handler);

        #endregion

        #region Receiving

        /// <summary>Raised when a notification is shown while the app is running.</summary>
        event Action<ReceivedNotification> NotificationDelivered;

        /// <summary>
        /// Raised once for each notification the player opens the app through, including the one
        /// that launched it. Notifications opened before anyone subscribes are kept and delivered to
        /// the first subscriber as it subscribes, so a handler added after boot still sees the
        /// launch notification.
        /// </summary>
        event Action<ReceivedNotification> NotificationOpened;

        #endregion
    }
}
