using System;
using System.Collections.Generic;
using UnityEngine;

namespace DracoRuan.PrebuildServices.MobileNotification.Data
{
    /// <summary>
    /// An iOS notification category: the set of action buttons a notification shows.
    /// </summary>
    [Serializable]
    public class NotificationCategoryData
    {
        [Tooltip("Unique category id. Notifications use it through NotificationData.iosCategoryId.")]
        public string categoryId = string.Empty;

        [Tooltip("Buttons shown on notifications of this category, in display order.")]
        public List<NotificationActionData> actions = new();

        /// <summary>Whether the category has an id. A category without actions is allowed.</summary>
        public bool IsValid() => !string.IsNullOrWhiteSpace(this.categoryId);
    }
}
