namespace DracoRuan.PrebuildServices.AudioSystem.Logic
{
    /// <summary>The outcome of turning one audio id into a C# member name.</summary>
    public readonly struct AudioIdSanitizeResult
    {
        public AudioIdStatus Status { get; }

        /// <summary>The member name to emit, or null when <see cref="Status"/> is Rejected.</summary>
        public string MemberName { get; }

        /// <summary>Why it was rejected, or what had to be adjusted. Null when nothing happened.</summary>
        public string Message { get; }

        private AudioIdSanitizeResult(AudioIdStatus status, string memberName, string message)
        {
            this.Status = status;
            this.MemberName = memberName;
            this.Message = message;
        }

        public static AudioIdSanitizeResult Ok(string memberName) =>
            new AudioIdSanitizeResult(AudioIdStatus.Ok, memberName, null);

        public static AudioIdSanitizeResult Adjusted(string memberName, string message) =>
            new AudioIdSanitizeResult(AudioIdStatus.Adjusted, memberName, message);

        public static AudioIdSanitizeResult Rejected(string message) =>
            new AudioIdSanitizeResult(AudioIdStatus.Rejected, null, message);
    }
}
