using System;
using UnityEngine;

namespace DracoRuan.PrebuildServices.MobileNotification.Data
{
    /// <summary>
    /// A button shown on an iOS notification. Actions are grouped into a
    /// <see cref="NotificationCategoryData"/>, and a notification shows them by naming that category
    /// in <see cref="NotificationData.iosCategoryId"/>.
    /// </summary>
    /// <remarks>
    /// When the player taps the button, the notification arrives through
    /// <c>IMobileNotificationService.NotificationOpened</c> with <see cref="ReceivedNotification.ActionId"/>
    /// set to <see cref="actionId"/>.
    /// </remarks>
    [Serializable]
    public class NotificationActionData
    {
        [Tooltip("Id reported back in ReceivedNotification.ActionId when the player taps this action.")]
        public string actionId = string.Empty;

        [Tooltip("Button label.")]
        public string title = string.Empty;

        [Tooltip("Launch the app into the foreground when tapped. When off, the action is handled without opening the app.")]
        public bool foreground = true;

        [Tooltip("Highlight the action as destructive (shown in red).")]
        public bool destructive;

        [Tooltip("Require the device to be unlocked before the action runs.")]
        public bool authenticationRequired;

        [Tooltip("Show a text field. What the player types arrives in ReceivedNotification.UserText.")]
        public bool textInput;

        [Tooltip("Label of the send button next to the text field. Only used when textInput is on.")]
        public string textInputButtonTitle = "Send";

        [Tooltip("Placeholder shown in the empty text field. Only used when textInput is on.")]
        public string textInputPlaceholder = string.Empty;

        /// <summary>Whether the action has the id and title iOS requires.</summary>
        public bool IsValid() =>
            !string.IsNullOrWhiteSpace(this.actionId) && !string.IsNullOrWhiteSpace(this.title);
    }
}
