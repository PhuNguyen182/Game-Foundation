namespace DracoRuan.PrebuildServices.MobileNotification.Data
{
    /// <summary>
    /// Where a notification is in its lifecycle, as reported by the operating system.
    /// </summary>
    public enum NotificationStatus
    {
        /// <summary>
        /// The OS cannot tell, for example Android below 6.0, or a platform without a backend.
        /// </summary>
        Unknown = 0,

        /// <summary>Waiting to fire.</summary>
        Scheduled = 1,

        /// <summary>Fired and still shown in the notification center.</summary>
        Delivered = 2,

        /// <summary>No notification with this id is scheduled or shown.</summary>
        NotFound = 3,
    }
}
