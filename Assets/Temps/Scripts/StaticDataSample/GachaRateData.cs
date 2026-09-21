using System;
using System.Collections.Generic;
using UnityEngine;

namespace Temps.Scripts.StaticDataSample
{
    /// <summary>
    /// Sample config table: drop rates per rarity.
    /// </summary>
    /// <remarks>
    /// <para>Written the normal Unity way — private fields with <c>[SerializeField]</c>, read through
    /// properties. That shape is the whole reason
    /// <c>UnitySerializationContractResolver</c> exists: Newtonsoft's defaults would populate none of
    /// these from a remote payload and hand back an object full of zeroes without complaining.</para>
    ///
    /// <para>The JSON a remote config key holds is therefore exactly the field names below:</para>
    /// <code>
    /// { "_pityPullCount": 90, "_entries": [ { "_rarity": "SSR", "_weight": 0.006 } ] }
    /// </code>
    /// </remarks>
    [CreateAssetMenu(menuName = "DracoRuan/Samples/Gacha Rate Data", fileName = "GachaRateData")]
    public sealed class GachaRateData : ScriptableObject
    {
        [SerializeField] private int _pityPullCount = 90;
        [SerializeField] private List<GachaRateEntry> _entries = new();

        /// <summary>Pulls before a guaranteed top-rarity drop.</summary>
        public int PityPullCount => this._pityPullCount;

        public IReadOnlyList<GachaRateEntry> Entries => this._entries;

        /// <summary>
        /// The checks this table cannot be wrong about. Run by the controller against every source, so
        /// a remote payload that fails them loses to the asset shipped in the build rather than
        /// replacing it.
        /// </summary>
        public bool IsWellFormed(out string failureReason)
        {
            if (this._pityPullCount <= 0)
            {
                failureReason = $"{nameof(this.PityPullCount)} is {this._pityPullCount}, must be positive.";
                return false;
            }

            if (this._entries == null || this._entries.Count == 0)
            {
                failureReason = "No rate entries.";
                return false;
            }

            float total = 0f;
            foreach (GachaRateEntry entry in this._entries)
            {
                if (entry.Weight < 0f)
                {
                    failureReason = $"Rarity '{entry.Rarity}' has a negative weight.";
                    return false;
                }

                total += entry.Weight;
            }

            if (total <= 0f)
            {
                failureReason = "All weights are zero, so nothing could ever drop.";
                return false;
            }

            failureReason = null;
            return true;
        }
    }

    [Serializable]
    public sealed class GachaRateEntry
    {
        [SerializeField] private string _rarity;
        [SerializeField] private float _weight;

        public string Rarity => this._rarity;
        public float Weight => this._weight;
    }
}
