using System.Collections.Generic;
using UnityEngine;

namespace PracticalUtilities.CalculationExtensions.Comparers
{
    public class Vector3IntComparer : IComparer<Vector3Int>
    {
        public int Compare(Vector3Int a, Vector3Int b)
        {
            int compare = 0;

            if (a.x == b.x && a.y == b.y)
                compare = 0;

            else if (a.x != b.x && a.y == b.y)
                compare = a.x.CompareTo(b.x);

            else if (a.x == b.x && a.y != b.y)
                compare = a.y.CompareTo(b.y);

            else
                compare = a.y.CompareTo(b.y);

            return compare;
        }
    }
}
