using System.Collections.Generic;
using UnityEngine;

namespace DracoRuan.PrebuildServices.MobileNotification.Data
{
    /// <summary>
    /// A set of notifications authored as an asset and scheduled together, such as a series of
    /// return reminders.
    /// </summary>
    /// <remarks>
    /// <para>Every notification needs its own non-zero <see cref="NotificationData.identifier"/>.
    /// Fixed ids are what let <c>ScheduleScenario</c> replace the previous run of the same scenario,
    /// and <c>CancelScenario</c> remove it, without touching notifications scheduled elsewhere.</para>
    ///
    /// <para>Delays are measured from an anchor time the caller passes in, "now" when none is given.
    /// Passing, say, the time the player last claimed a reward keeps the series aligned with that
    /// moment across sessions.</para>
    /// </remarks>
    [CreateAssetMenu(fileName = "NotificationScenario", menuName = "DracoRuan/MobileNotifications/NotificationScenario")]
    public class NotificationScenario : ScriptableObject
    {
        [Tooltip("Notifications in the scenario. Each needs a unique, non-zero identifier.")]
        public List<NotificationData> notifications = new();

        [Tooltip("Group/thread key applied to every notification that does not set its own. Optional.")]
        public string groupKey = string.Empty;

        /// <summary>Returns false with a reason when the scenario cannot be scheduled.</summary>
        public bool TryValidate(out string error)
        {
            if (this.notifications == null || this.notifications.Count == 0)
            {
                error = "it has no notifications";
                return false;
            }

            var seenIds = new HashSet<int>();
            for (int i = 0; i < this.notifications.Count; i++)
            {
                NotificationData notification = this.notifications[i];
                if (notification == null)
                {
                    error = $"notification #{i} is empty";
                    return false;
                }

                if (notification.identifier == NotificationData.AutoId)
                {
                    error = $"notification #{i} ('{notification.title}') needs a fixed, non-zero identifier";
                    return false;
                }

                if (!seenIds.Add(notification.identifier))
                {
                    error = $"identifier {notification.identifier} is used more than once";
                    return false;
                }

                if (!notification.TryValidate(out string notificationError))
                {
                    error = $"notification #{i} ('{notification.title}'): {notificationError}";
                    return false;
                }
            }

            error = null;
            return true;
        }
    }
}
