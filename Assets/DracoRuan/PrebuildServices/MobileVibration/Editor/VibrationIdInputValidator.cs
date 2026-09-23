using DracoRuan.PrebuildServices.MobileVibration.Logic;
using UnityEditor;

namespace DracoRuan.PrebuildServices.MobileVibration.Editor
{
    /// <summary>One validation pass over a candidate vibration id.</summary>
    public readonly struct VibrationIdCheck
    {
        public VibrationIdCheck(string message, MessageType messageType, bool canSubmit, string suggestion)
        {
            this.Message = message;
            this.MessageType = messageType;
            this.CanSubmit = canSubmit;
            this.Suggestion = suggestion;
        }

        public string Message { get; }
        public MessageType MessageType { get; }
        public bool CanSubmit { get; }

        /// <summary>A sanitized alternative, offered only when the candidate was rejected.</summary>
        public string Suggestion { get; }
    }

    /// <summary>
    /// The one id-validation pass shared by every form that asks for a vibration id: New Entry,
    /// Duplicate and Rename.
    /// </summary>
    /// <remarks>
    /// Extracted from <c>VibrationManagerWindow.RecheckNewId</c> so the three forms cannot drift into
    /// checking slightly different things.
    /// </remarks>
    public static class VibrationIdInputValidator
    {
        /// <param name="candidate">The id text as currently typed.</param>
        /// <param name="ignoreOwnerPath">
        /// The asset path to exclude from the collision check — the entry being renamed, so it does
        /// not collide with itself.
        /// </param>
        public static VibrationIdCheck Check(string candidate, string ignoreOwnerPath)
        {
            if (string.IsNullOrEmpty(candidate))
                return new VibrationIdCheck(null, MessageType.None, false, null);

            VibrationIdSanitizeResult sanitized = VibrationIdSanitizer.ToMemberName(candidate);

            if (sanitized.Status == VibrationIdStatus.Rejected)
            {
                string suggestion = VibrationIdSanitizer.Suggest(candidate);
                bool hasUsableSuggestion = !string.IsNullOrEmpty(suggestion)
                                           && suggestion != candidate
                                           && VibrationIdFormat.IsValid(suggestion, out _);

                return new VibrationIdCheck(sanitized.Message, MessageType.Error, false,
                    hasUsableSuggestion ? suggestion : null);
            }

            VibrationIdConflict? conflict = VibrationIdCollisionDetector.CheckAgainst(
                candidate, VibrationIdIndex.Entries, ignoreOwnerPath);

            if (conflict.HasValue)
                return new VibrationIdCheck(conflict.Value.Message, MessageType.Error, false, null);

            return sanitized.Status == VibrationIdStatus.Adjusted
                ? new VibrationIdCheck(sanitized.Message, MessageType.Warning, true, null)
                : new VibrationIdCheck($"Available. Generates as VibrationId.{sanitized.MemberName}",
                    MessageType.Info, true, null);
        }
    }
}
