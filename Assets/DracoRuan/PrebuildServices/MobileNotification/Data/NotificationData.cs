using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace DracoRuan.PrebuildServices.MobileNotification.Data
{
    /// <summary>
    /// Everything needed to schedule one local notification on Android and iOS.
    /// </summary>
    /// <remarks>
    /// Fields that only one platform understands are grouped under their platform header and are
    /// ignored on the other. Empty platform fields fall back to the defaults in
    /// <see cref="MobileNotificationConfig"/>.
    /// </remarks>
    [Serializable]
    public class NotificationData
    {
        /// <summary>Pass as <see cref="identifier"/> to let the service generate an id.</summary>
        public const int AutoId = 0;

        /// <summary>Returned by the service when a notification could not be scheduled.</summary>
        public const int InvalidId = -1;

        /// <summary>
        /// The shortest repeat interval accepted. iOS refuses repeating notifications under a minute.
        /// </summary>
        public const long MinRepeatIntervalSeconds = 60;

        [Header("Content")]
        [Tooltip(
            "Stable id. 0 lets the service generate one. Scheduling again under the same id replaces the earlier notification.")]
        public int identifier;

        [Tooltip("Notification title.")] public string title = string.Empty;

        [Tooltip("Notification text.")] public string body = string.Empty;

        [Tooltip("Second line under the title. iOS only.")]
        public string subtitle = string.Empty;

        [Header("Timing")] [Tooltip("Delay, in seconds, from the moment the notification is scheduled.")]
        public long fireTimeInSeconds;

        [Tooltip("Keep firing every repeatInterval seconds after the first fire.")]
        public bool repeats;

        [Tooltip(
            "Seconds between repeats, at least 60. On iOS the first fire also happens after this interval, except for exact hourly and daily intervals.")]
        public long repeatInterval;

        [Header("Presentation")] [Tooltip("Badge count shown on the app icon. 0 uses the config default.")]
        public int badge;

        [Tooltip("Key that stacks related notifications: an Android group, or an iOS 12+ thread.")]
        public string groupKey = string.Empty;

        [Header("Android")]
        [Tooltip("Channel to post to. Empty uses the default channel from the config.")]
        [FormerlySerializedAs("category")]
        public string androidChannelId = string.Empty;

        [Tooltip("Small icon id registered in Project Settings > Mobile Notifications. Empty uses the config default.")]
        public string smallIcon = string.Empty;

        [Tooltip("Large icon id registered in Project Settings > Mobile Notifications. Empty uses the config default.")]
        public string largeIcon = string.Empty;

        [Header("iOS")]
        [Tooltip("Category whose action buttons are shown. Empty uses the default category from the config.")]
        public string iosCategoryId = string.Empty;

        [Tooltip(
            "URLs of local image, audio or video files (for example file:///.../image.png) attached to the notification.")]
        public List<string> attachmentUrls = new();

        [Header("Payload")]
        [Tooltip(
            "Free-form string, such as JSON, handed back in ReceivedNotification.Data when the notification is received or opened.")]
        public string customData = string.Empty;

        public NotificationData()
        {
        }

        public NotificationData(string title, string body, long fireTimeInSeconds)
        {
            this.title = title;
            this.body = body;
            this.fireTimeInSeconds = fireTimeInSeconds;
        }

        /// <summary>Returns a deep copy, so the copy's attachment list can change independently.</summary>
        public NotificationData Clone()
        {
            var copy = (NotificationData)this.MemberwiseClone();
            copy.attachmentUrls = this.attachmentUrls != null
                ? new List<string>(this.attachmentUrls)
                : new List<string>();
            return copy;
        }

        /// <summary>
        /// Checks the rules both platforms enforce. Returns false with a reason when one is broken.
        /// </summary>
        public bool TryValidate(out string error)
        {
            if (this.identifier < 0)
            {
                error = $"identifier must not be negative, got {this.identifier}";
                return false;
            }

            if (string.IsNullOrWhiteSpace(this.title) && string.IsNullOrWhiteSpace(this.body))
            {
                error = "title and body are both empty";
                return false;
            }

            if (this.fireTimeInSeconds < 0)
            {
                error = $"fireTimeInSeconds must not be negative, got {this.fireTimeInSeconds}";
                return false;
            }

            if (this.repeats && this.repeatInterval < MinRepeatIntervalSeconds)
            {
                error = $"repeatInterval must be at least {MinRepeatIntervalSeconds}s, got {this.repeatInterval}s";
                return false;
            }

            error = null;
            return true;
        }
    }
}