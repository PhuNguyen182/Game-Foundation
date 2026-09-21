using Temps.Scripts.StaticDataSample;
using Temps.Scripts.TestRiseProgressData;
using UnityEngine;
using VContainer.Unity;

namespace Temps.Scripts
{
    /// <summary>
    /// Sample consumer of both halves of DataFlow: a save repository and a config table.
    /// </summary>
    /// <remarks>
    /// <para>Shows the normal usage shape for saves: read <c>Data</c>, mutate it, call
    /// <c>MarkDirty()</c>. The repository is not saved here — the scheduler batches writes and
    /// flushes them on a timer and when the app is backgrounded.</para>
    ///
    /// <para><b>An <c>IStartable</c> taking a repository in its constructor is exactly the case the
    /// gate exists for.</b> VContainer constructs every <c>IStartable</c> well before any
    /// <c>IAsyncStartable</c> runs, so this class comes into existence before migration has even
    /// started. <c>Start()</c> therefore subscribes rather than assuming the data is loaded.</para>
    ///
    /// <para>The config table is the mirror image: it finishes loading <i>before</i> this runs, so its
    /// handler fires the moment it is attached. Both cases are handled by subscribing and nothing
    /// else, which is the point of the controller invoking a late subscriber immediately.</para>
    /// </remarks>
    public class TestService : IStartable
    {
        private const string LogTag = "TestService";

        private readonly RiseProgressionDataController _riseProgression;
        private readonly GachaRateController _gachaRate;

        public TestService(RiseProgressionDataController riseProgression, GachaRateController gachaRate)
        {
            this._riseProgression = riseProgression;
            this._gachaRate = gachaRate;
        }

        public void Start()
        {
            // Not loaded yet: fires later, from the boot pipeline.
            this._riseProgression.OnDataLoaded += this.OnRiseProgressionLoaded;

            // Already loaded: fires inline, on this line.
            this._gachaRate.OnDataLoaded += this.OnGachaRateLoaded;
        }

        private void OnGachaRateLoaded()
        {
            GachaRateData data = this._gachaRate.Data;
            Debug.Log(
                $"[{LogTag}] Gacha rates from {this._gachaRate.LastLoadResult.WinningSource}: " +
                $"pity={data.PityPullCount}, tiers={data.Entries.Count}");
        }

        private void OnRiseProgressionLoaded()
        {
            RiseProgressDataV1 data = this._riseProgression.Data;
            Debug.Log($"[{LogTag}] Rise progression loaded: Number={data.Number}, " +
                      $"Streaks={data.Streaks.Count}");
        }

        /// <summary>Example mutation: change the data, then say so.</summary>
        public void IncrementNumber()
        {
            if (!this._riseProgression.IsInitialized)
                return;

            this._riseProgression.Data.Number++;

            // Queues the write; the scheduler decides when it actually hits disk.
            this._riseProgression.MarkDirty();
        }
    }
}
