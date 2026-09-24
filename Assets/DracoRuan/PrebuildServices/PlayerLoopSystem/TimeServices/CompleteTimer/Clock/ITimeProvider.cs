namespace DracoRuan.PrebuildServices.PlayerLoopSystem.TimeServices.CompleteTimer.Clock
{
    /// <summary>
    /// Source of "now" for every timer in the system, expressed as absolute UTC milliseconds.
    /// </summary>
    /// <remarks>
    /// Pluggable so tests can inject a fake clock and so a game with a server can swap in
    /// server-synced time without touching the scheduler. Every consumer reads <see cref="UtcNowMs"/>
    /// once per tick and never derives elapsed time by subtracting two live reads a frame apart -
    /// that pattern is exactly what corrupted the old implementation (see REWRITE_PLAN.md, A2).
    /// </remarks>
    public interface ITimeProvider
    {
        /// <summary>Current time as absolute UTC milliseconds since epoch.</summary>
        long UtcNowMs { get; }
    }
}
