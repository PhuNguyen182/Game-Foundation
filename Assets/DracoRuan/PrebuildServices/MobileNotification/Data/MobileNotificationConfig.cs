using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace DracoRuan.PrebuildServices.MobileNotification.Data
{
    /// <summary>
    /// Settings the notification service reads once, when it starts.
    /// </summary>
    /// <remarks>
    /// Some behavior lives in Project Settings > Mobile Notifications instead, because it has to be
    /// baked into the Android manifest or the Xcode project at build time:
    /// <list type="bullet">
    /// <item><b>Reschedule on Device Restart</b> (Android): keeps scheduled notifications across reboots.</item>
    /// <item><b>Notification Icons</b> (Android): registers the icons that <see cref="androidSmallIcon"/>
    /// and <see cref="androidLargeIcon"/> refer to by id.</item>
    /// <item><b>Enable Push Notifications</b> (iOS): adds the APNs capability that
    /// <see cref="iosRegisterForRemoteNotifications"/> needs.</item>
    /// </list>
    /// </remarks>
    [CreateAssetMenu(fileName = "MobileNotificationConfig",
        menuName = "DracoRuan/MobileNotifications/MobileNotificationConfig")]
    public class MobileNotificationConfig : ScriptableObject
    {
        [Header("General")] [Tooltip("Log every call. Warnings and errors are logged regardless.")]
        public bool enableDebugLogs;

        [Tooltip(
            "Ask for permission as soon as the service starts. Leave off to ask at a moment that makes sense to the player.")]
        public bool autoRequestPermission;

        [Tooltip("Show notifications that fire while the app is in the foreground.")]
        public bool showInForeground = true;

        [Tooltip("Remove shown notifications, and reset the iOS badge, when the service starts.")]
        public bool clearDisplayedOnStart = true;

        [Tooltip("Badge applied to notifications that do not set their own. 0 leaves the badge alone.")] [Min(0)]
        public int defaultBadge = 1;

        [Tooltip(
            "Seconds between checks for a notification the player opened while the app was already running. 0 checks only on start and when the app regains focus.")]
        [Min(0f)]
        public float openedCheckInterval = 1f;

        [Header("Android")]
        [Tooltip("Channel that notifications without an androidChannelId post to. Always registered.")]
        public NotificationChannelData androidDefaultChannel =
            new("default_channel", "Notifications", "General notifications");

        [Tooltip("Additional channels registered when the service starts.")] [FormerlySerializedAs("customChannels")]
        public List<NotificationChannelData> androidChannels = new();

        [Tooltip("Default small icon id. Empty uses the app icon.")]
        public string androidSmallIcon = string.Empty;

        [Tooltip("Default large icon id. Empty shows none.")]
        public string androidLargeIcon = string.Empty;

        [Header("iOS")] [Tooltip("Ask for permission to show alerts.")]
        public bool iosRequestAlert = true;

        [Tooltip("Ask for permission to set the app icon badge.")]
        public bool iosRequestBadge = true;

        [Tooltip("Ask for permission to play sounds.")]
        public bool iosRequestSound = true;

        [Tooltip(
            "Register with APNs while asking for permission. The device token then arrives through DeviceTokenReceived.")]
        public bool iosRegisterForRemoteNotifications;

        [Tooltip("Category used by notifications without an iosCategoryId. Empty uses none.")]
        [FormerlySerializedAs("defaultCategory")]
        public string iosDefaultCategoryId = string.Empty;

        [Tooltip("Categories, and their action buttons, registered when the service starts.")]
        public List<NotificationCategoryData> iosCategories = new();

        /// <summary>Returns false with a reason when the config cannot be used as is.</summary>
        public bool TryValidate(out string error)
        {
            if (this.androidDefaultChannel == null || !this.androidDefaultChannel.IsValid())
            {
                error = "androidDefaultChannel needs an id and a name";
                return false;
            }

            error = null;
            return true;
        }
    }
}