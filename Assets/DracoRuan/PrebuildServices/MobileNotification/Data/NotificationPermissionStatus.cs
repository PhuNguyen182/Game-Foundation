namespace DracoRuan.PrebuildServices.MobileNotification.Data
{
    /// <summary>
    /// Whether the app may show notifications, merged across Android and iOS.
    /// </summary>
    public enum NotificationPermissionStatus
    {
        /// <summary>The player has not been asked yet.</summary>
        NotRequested = 0,

        /// <summary>A request is on screen and the player has not answered.</summary>
        Pending = 1,

        /// <summary>
        /// Notifications may be shown. On iOS this includes provisional and ephemeral authorization.
        /// </summary>
        Granted = 2,

        /// <summary>
        /// The player refused, or turned notifications off in the system settings. Only the settings
        /// screen (<c>IMobileNotificationService.OpenNotificationSettings</c>) can change this.
        /// </summary>
        Denied = 3,

        /// <summary>The current platform has no notification backend, for example the Editor.</summary>
        Unsupported = 4,
    }
}
