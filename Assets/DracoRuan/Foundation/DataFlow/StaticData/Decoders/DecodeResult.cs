namespace DracoRuan.Foundation.DataFlow.StaticData.Decoders
{
    /// <summary>
    /// What a decoder made of a payload, and — when it failed — why.
    /// </summary>
    /// <remarks>
    /// <para><b>Success is explicit, not inferred from the value.</b> The predecessor returned an
    /// empty array both when a CSV parsed to zero rows and when it failed to parse at all, so the
    /// fallback chain stopped on a broken table believing it had loaded an empty one. A separate
    /// <see cref="Succeeded"/> flag makes those two outcomes impossible to confuse.</para>
    ///
    /// <para><see cref="FailureReason"/> is carried rather than logged on the spot: a source missing
    /// its value is ordinary in a fallback chain, and logging an error at every step produces a red
    /// console on a perfectly healthy run. The controller decides what to report once it knows
    /// whether any source succeeded.</para>
    /// </remarks>
    public readonly struct DecodeResult<T>
    {
        private DecodeResult(bool succeeded, T value, bool ownsValue, string failureReason)
        {
            this.Succeeded = succeeded;
            this.Value = value;
            this.OwnsValue = ownsValue;
            this.FailureReason = failureReason;
        }

        public bool Succeeded { get; }

        public T Value { get; }

        /// <summary>
        /// True when the decoder created <see cref="Value"/> itself rather than passing through
        /// something the source owns. The controller destroys what it owns and asks the source to
        /// release the rest, so an asset is never destroyed and a runtime instance never leaks.
        /// </summary>
        public bool OwnsValue { get; }

        /// <summary>Null on success. A sentence naming what went wrong, for the load report.</summary>
        public string FailureReason { get; }

        public static DecodeResult<T> Success(T value, bool ownsValue) =>
            new(true, value, ownsValue, null);

        public static DecodeResult<T> Failure(string reason) =>
            new(false, default, false, reason);
    }
}
