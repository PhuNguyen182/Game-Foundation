using System.Collections.Generic;

namespace DracoRuan.PrebuildServices.MobileNotification.Data
{
    /// <summary>
    /// A notification handed back by the OS, either because it was shown while the app was running
    /// or because the player opened the app through it.
    /// </summary>
    public sealed class ReceivedNotification
    {
        /// <summary>
        /// The id it was scheduled under, or <see cref="NotificationData.InvalidId"/> when the
        /// platform id is not numeric, which is the case for iOS remote notifications.
        /// </summary>
        public int Id { get; internal set; } = NotificationData.InvalidId;

        /// <summary>The raw platform id. For local notifications this is <see cref="Id"/> as a string.</summary>
        public string Identifier { get; internal set; }

        public string Title { get; internal set; }

        public string Body { get; internal set; }

        /// <summary>iOS only.</summary>
        public string Subtitle { get; internal set; }

        /// <summary>The <see cref="NotificationData.customData"/> it was scheduled with.</summary>
        public string Data { get; internal set; }

        /// <summary>Android only: the channel it was posted to.</summary>
        public string ChannelId { get; internal set; }

        /// <summary>iOS only: the category it was scheduled with.</summary>
        public string CategoryId { get; internal set; }

        /// <summary>The Android group or iOS thread key.</summary>
        public string GroupKey { get; internal set; }

        /// <summary>
        /// iOS only: the <see cref="NotificationActionData.actionId"/> of the button the player
        /// tapped, or null when the notification itself was tapped.
        /// </summary>
        public string ActionId { get; internal set; }

        /// <summary>iOS only: the text entered through a text input action, or null.</summary>
        public string UserText { get; internal set; }

        /// <summary>iOS only: whether it came from APNs rather than being scheduled locally.</summary>
        public bool IsRemote { get; internal set; }

        /// <summary>
        /// iOS only: every key of the payload. Remote notifications carry their custom keys here.
        /// Null on Android.
        /// </summary>
        public IReadOnlyDictionary<string, string> UserInfo { get; internal set; }

        /// <summary>
        /// Copies the content into a new notification that fires immediately, as a starting point
        /// for showing a modified version of it. The copy gets a fresh id.
        /// </summary>
        public NotificationData ToNotificationData() => new()
        {
            title = this.Title ?? string.Empty,
            body = this.Body ?? string.Empty,
            subtitle = this.Subtitle ?? string.Empty,
            customData = this.Data ?? string.Empty,
            androidChannelId = this.ChannelId ?? string.Empty,
            iosCategoryId = this.CategoryId ?? string.Empty,
            groupKey = this.GroupKey ?? string.Empty,
        };
    }
}
