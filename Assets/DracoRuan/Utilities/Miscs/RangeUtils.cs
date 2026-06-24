using System;
using System.Collections.Generic;

namespace DracoRuan.Utilities.Miscs
{
    [Serializable]
    public struct Range<TValue> where TValue : IComparable<TValue>
    {
        public TValue minValue;
        public TValue maxValue;

        public bool IsValueInRangeEdgesIncluded(TValue value)
        {
            int minCompare = Comparer<TValue>.Default.Compare(value, minValue);
            int maxCompare = Comparer<TValue>.Default.Compare(value, maxValue);
            return minCompare >= 0 && maxCompare <= 0;
        }
        
        public bool IsValueInRangeMinIncluded(TValue value)
        {
            int minCompare = Comparer<TValue>.Default.Compare(value, minValue);
            int maxCompare = Comparer<TValue>.Default.Compare(value, maxValue);
            return minCompare >= 0 && maxCompare < 0;
        }
        
        public bool IsValueInRangeEdgesExcluded(TValue value)
        {
            int minCompare = Comparer<TValue>.Default.Compare(value, minValue);
            int maxCompare = Comparer<TValue>.Default.Compare(value, maxValue);
            return minCompare > 0 && maxCompare < 0;
        }
        
        public bool IsValueInRangeMaxIncluded(TValue value)
        {
            int minCompare = Comparer<TValue>.Default.Compare(value, minValue);
            int maxCompare = Comparer<TValue>.Default.Compare(value, maxValue);
            return minCompare > 0 && maxCompare <= 0;
        }
    }
}
