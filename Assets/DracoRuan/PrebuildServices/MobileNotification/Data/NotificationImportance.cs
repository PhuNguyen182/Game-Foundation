namespace DracoRuan.PrebuildServices.MobileNotification.Data
{
    /// <summary>
    /// Importance of an Android notification channel.
    /// </summary>
    /// <remarks>
    /// Values match <c>Unity.Notifications.Android.Importance</c> one to one so they can be cast
    /// directly. Android has no level 1, which is why the numbering skips it.
    /// </remarks>
    public enum NotificationImportance
    {
        /// <summary>Not shown in the notification shade.</summary>
        None = 0,

        /// <summary>Shown everywhere, silent and unobtrusive.</summary>
        Low = 2,

        /// <summary>Shown everywhere and makes a sound.</summary>
        Default = 3,

        /// <summary>Makes a sound and pops up on screen as a heads-up notification.</summary>
        High = 4,
    }
}
