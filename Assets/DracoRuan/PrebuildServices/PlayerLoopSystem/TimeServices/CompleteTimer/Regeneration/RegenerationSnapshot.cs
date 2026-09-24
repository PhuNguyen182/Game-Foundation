namespace DracoRuan.PrebuildServices.PlayerLoopSystem.TimeServices.CompleteTimer.Regeneration
{
    /// <summary>Persistence-facing snapshot of one <see cref="RegenerationCounter"/>.</summary>
    public sealed class RegenerationSnapshot
    {
        public string Key;
        public long Stored;
        public long Max;
        public long IntervalMs;
        public long AnchorMs;
    }
}
