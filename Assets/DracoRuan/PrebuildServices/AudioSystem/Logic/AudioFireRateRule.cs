namespace DracoRuan.PrebuildServices.AudioSystem.Logic
{
    /// <summary>
    /// One entry's throttling rule, passed to <see cref="AudioFireRateGate"/> per request.
    /// </summary>
    /// <remarks>
    /// Passed in rather than stored, so the gate holds only mutable play state and the authored
    /// values stay where a designer edits them.
    /// </remarks>
    public readonly struct AudioFireRateRule
    {
        /// <summary>Shortest gap between two plays of the same entry. Zero disables the check.</summary>
        public float MinIntervalSeconds { get; }

        /// <summary>How many instances of this entry may sound at once. Zero or less means no limit.</summary>
        public int MaxConcurrent { get; }

        /// <summary>What to do when <see cref="MaxConcurrent"/> is reached.</summary>
        public AudioConcurrencyPolicy Policy { get; }

        public AudioFireRateRule(float minIntervalSeconds, int maxConcurrent, AudioConcurrencyPolicy policy)
        {
            this.MinIntervalSeconds = minIntervalSeconds;
            this.MaxConcurrent = maxConcurrent;
            this.Policy = policy;
        }
    }
}
