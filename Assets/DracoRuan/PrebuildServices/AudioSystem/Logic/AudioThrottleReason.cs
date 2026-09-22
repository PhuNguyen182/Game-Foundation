namespace DracoRuan.PrebuildServices.AudioSystem.Logic
{
    /// <summary>Why a play request was refused a voice.</summary>
    /// <remarks>
    /// Reported rather than logged. A throttled footstep filling the console is strictly worse than
    /// the throttle it is reporting, so the service keeps quiet unless explicitly asked to log.
    /// </remarks>
    public enum AudioThrottleReason
    {
        /// <summary>Not refused.</summary>
        None = 0,

        /// <summary>Asked again before the entry's minimum interval had elapsed.</summary>
        FireRate = 1,

        /// <summary>The entry already has as many instances playing as it allows.</summary>
        ConcurrencyLimit = 2,
    }
}
