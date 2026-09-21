using System.Collections.Generic;
using System.Text;
using DracoRuan.Foundation.DataFlow.StaticData.Sources;

namespace DracoRuan.Foundation.DataFlow.StaticData.Controllers
{
    /// <summary>How one source in a fallback chain ended.</summary>
    public enum StaticDataAttemptOutcome
    {
        /// <summary>This build has no source of that type registered.</summary>
        SourceUnavailable = 0,

        /// <summary>The source had no value for the key. The ordinary case, not a fault.</summary>
        Missing = 1,

        /// <summary>A payload arrived but could not be decoded — wrong shape, bad CSV, drifted schema.</summary>
        DecodeFailed = 2,

        /// <summary>Decoded fine but the controller rejected the contents.</summary>
        ValidationFailed = 3,

        /// <summary>This source supplied the data.</summary>
        Loaded = 4,

        /// <summary>Never attempted, because an earlier source already won.</summary>
        NotAttempted = 5,
    }

    public readonly struct StaticDataSourceAttempt
    {
        public StaticDataSourceAttempt(StaticDataSourceType sourceType, string key,
            StaticDataAttemptOutcome outcome, string detail = null)
        {
            this.SourceType = sourceType;
            this.Key = key;
            this.Outcome = outcome;
            this.Detail = detail;
        }

        public StaticDataSourceType SourceType { get; }
        public string Key { get; }
        public StaticDataAttemptOutcome Outcome { get; }

        /// <summary>Why it ended that way, when there is anything to say.</summary>
        public string Detail { get; }
    }

    /// <summary>
    /// What happened the last time a controller loaded: which source won, and why each earlier one
    /// did not.
    /// </summary>
    /// <remarks>
    /// <para>Exists because "the game is running on the wrong config" is otherwise almost impossible
    /// to diagnose in the field. A boolean would say the load succeeded; what a support ticket needs
    /// is that remote config timed out, the Addressables address was missing, and the value came from
    /// the build's Resources copy.</para>
    /// </remarks>
    public sealed class StaticDataLoadResult
    {
        private static readonly IReadOnlyList<StaticDataSourceAttempt> NoAttempts =
            new StaticDataSourceAttempt[0];

        public StaticDataLoadResult(bool succeeded, StaticDataSourceType winningSource,
            IReadOnlyList<StaticDataSourceAttempt> attempts)
        {
            this.Succeeded = succeeded;
            this.WinningSource = winningSource;
            this.Attempts = attempts ?? NoAttempts;
        }

        /// <summary>A never-loaded controller's result, so this is never null.</summary>
        public static readonly StaticDataLoadResult NotLoaded =
            new(false, StaticDataSourceType.None, NoAttempts);

        public bool Succeeded { get; }

        /// <summary><see cref="StaticDataSourceType.None"/> when nothing supplied the data.</summary>
        public StaticDataSourceType WinningSource { get; }

        public IReadOnlyList<StaticDataSourceAttempt> Attempts { get; }

        /// <summary>One line per source, for the log and for a diagnostics screen.</summary>
        public string Describe()
        {
            StringBuilder builder = new();

            for (int index = 0; index < this.Attempts.Count; index++)
            {
                StaticDataSourceAttempt attempt = this.Attempts[index];
                if (index > 0)
                    builder.Append("; ");

                builder.Append(attempt.SourceType).Append('(').Append(attempt.Key).Append(")=")
                    .Append(attempt.Outcome);

                if (!string.IsNullOrEmpty(attempt.Detail))
                    builder.Append(": ").Append(attempt.Detail);
            }

            return builder.ToString();
        }
    }
}
