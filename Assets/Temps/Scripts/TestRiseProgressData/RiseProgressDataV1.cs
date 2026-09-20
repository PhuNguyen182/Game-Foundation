using System;
using System.Collections.Generic;
using DracoRuan.Foundation.DataFlow.LocalData;
using MessagePack;

namespace Temps.Scripts.TestRiseProgressData
{
    /// <summary>
    /// Rise progression save data, schema version 1.
    /// </summary>
    /// <remarks>
    /// <para><b>One class per schema version, and old ones are never edited.</b> When the shape
    /// changes, add <c>RiseProgressDataV2</c> and a migrator from this class to it — do not modify
    /// this one. Its only remaining job is to describe exactly what a v1 payload on a player's
    /// device looks like, and editing it would rewrite history: the migrator would deserialize old
    /// bytes into the edited shape, which MessagePack accepts without complaint while quietly
    /// producing wrong values.</para>
    ///
    /// <para><c>[Key]</c> numbers are part of the format. Never renumber or reuse one; give new
    /// fields the next free index.</para>
    /// </remarks>
    [Serializable]
    [MessagePackObject]
    public sealed class RiseProgressDataV1 : IGameData
    {
        [Key(0)] public int Number { get; set; }

        [Key(1)] public List<int> Streaks { get; set; } = new() { 1, 2, 3, 4 };

        [Key(2)]
        public Dictionary<int, int> StreakCollection { get; set; } = new()
        {
            { 0, 0 },
            { 1, 1 },
            { 2, 2 },
            { 3, 3 }
        };
    }
}
