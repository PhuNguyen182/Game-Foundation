using System;
using System.Collections.Generic;
using DracoRuan.Foundation.DataFlow.LocalData;
using MessagePack;

namespace Temps.Scripts.TestRiseProgressData
{
    [Serializable]
    [MessagePackObject]
    public class RiseProgressData : IGameData, IDisposable
    {
        [IgnoreMember]
        public int DataVersion => 1;

        [Key(0)] public int Number { get; set; }

        [Key(1)] public List<int> Streaks { get; set; } = new() { 1, 2, 3, 4 };

        [Key(2)]
        public Dictionary<int, int> StreakCollection { get; set; } = new()
        {
            { 0, 0 },
            { 1, 1 },
            { 2, 2 },
            { 3, 3 },
        };
    
        public void Dispose()
        {
        
        }
    }
}
