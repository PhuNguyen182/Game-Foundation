using System;
using UnityEngine;

namespace DracoRuan.PrebuildServices.MobileNotification.Data
{
    /// <summary>
    /// An Android notification channel (Android 8.0 / API 26 and above).
    /// </summary>
    /// <remarks>
    /// Registering a channel whose id already exists updates it, but Android only honors changes to
    /// the name and description. Importance, sound, vibration and lights are fixed when the channel
    /// is first created, because from then on the player owns them in the system settings. To change
    /// them, delete the channel and register it again under a new id.
    /// </remarks>
    [Serializable]
    public class NotificationChannelData
    {
        [Tooltip("Unique channel id. Notifications post to it through NotificationData.androidChannelId.")]
        public string channelId = string.Empty;

        [Tooltip("Name shown to the player in the system notification settings.")]
        public string channelName = string.Empty;

        [Tooltip("Description shown to the player in the system notification settings.")]
        public string description = string.Empty;

        [Tooltip("Fixed once the channel exists on the device.")]
        public NotificationImportance importance = NotificationImportance.Default;

        [Tooltip("Whether notifications on this channel can show a launcher badge. Fixed once the channel exists.")]
        public bool canShowBadge = true;

        [Tooltip("Whether notifications on this channel vibrate. Fixed once the channel exists.")]
        public bool enableVibration = true;

        [Tooltip("Whether notifications on this channel blink the notification light. Fixed once the channel exists.")]
        public bool enableLights;

        public NotificationChannelData()
        {
        }

        public NotificationChannelData(string channelId, string channelName, string description,
            NotificationImportance importance = NotificationImportance.Default)
        {
            this.channelId = channelId;
            this.channelName = channelName;
            this.description = description;
            this.importance = importance;
        }

        /// <summary>Whether the channel has the id and name Android requires.</summary>
        public bool IsValid() =>
            !string.IsNullOrWhiteSpace(this.channelId) && !string.IsNullOrWhiteSpace(this.channelName);
    }
}