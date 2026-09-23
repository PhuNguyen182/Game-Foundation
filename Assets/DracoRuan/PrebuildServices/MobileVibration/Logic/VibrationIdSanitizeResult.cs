namespace DracoRuan.PrebuildServices.MobileVibration.Logic
{
    /// <summary>The outcome of turning one vibration id into a C# member name.</summary>
    public readonly struct VibrationIdSanitizeResult
    {
        public VibrationIdStatus Status { get; }

        /// <summary>The member name to emit, or null when <see cref="Status"/> is Rejected.</summary>
        public string MemberName { get; }

        /// <summary>Why it was rejected, or what had to be adjusted. Null when nothing happened.</summary>
        public string Message { get; }

        private VibrationIdSanitizeResult(VibrationIdStatus status, string memberName, string message)
        {
            this.Status = status;
            this.MemberName = memberName;
            this.Message = message;
        }

        public static VibrationIdSanitizeResult Ok(string memberName) =>
            new VibrationIdSanitizeResult(VibrationIdStatus.Ok, memberName, null);

        public static VibrationIdSanitizeResult Adjusted(string memberName, string message) =>
            new VibrationIdSanitizeResult(VibrationIdStatus.Adjusted, memberName, message);

        public static VibrationIdSanitizeResult Rejected(string message) =>
            new VibrationIdSanitizeResult(VibrationIdStatus.Rejected, null, message);
    }
}
