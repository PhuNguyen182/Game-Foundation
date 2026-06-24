using System;
using System.Collections.Generic;
using PracticalUtilities.CalculationExtensions.Comparers;
using UnityEngine;
using UnityEngine.Pool;

namespace DracoRuan.Utilities.CalculationExtensions
{
    public static class BoundsExtension
    {
        public enum SortOrder
        {
            Ascending = 1,
            Descending = 2
        }

        public enum AxisOrder
        {
            XY = 1,
            YX = 2
        }

        public static Vector3IntComparer Vector3IntComparer { get; } = new();

        public static BoundsInt GetBounds2D(this Vector3Int position, Vector3Int size)
        {
            return new BoundsInt(position - size / 2, size);
        }

        public static BoundsInt GetBounds2D(this Vector3Int position, int range = 0) =>
            new BoundsInt(position + new Vector3Int(-1, -1) * range, new(2 * range + 1, 2 * range + 1));

        public static BoundsInt Expand2D(this BoundsInt boundsInt, int range)
        {
            BoundsInt bounds = new BoundsInt
            {
                xMin = boundsInt.xMin - range / 2,
                xMax = boundsInt.xMax + range / 2,
                yMin = boundsInt.yMin - range / 2,
                yMax = boundsInt.yMax + range / 2,
            };

            return bounds;
        }

        public static BoundsInt EncapsulateExpand(IEnumerable<Vector3Int> positions)
        {
            int minX = int.MaxValue, maxX = int.MinValue;
            int minY = int.MaxValue, maxY = int.MinValue;

            foreach (Vector3Int position in positions)
            {
                if (position.x < minX)
                    minX = position.x;

                if (position.x > maxX)
                    maxX = position.x;

                if (position.y < minY)
                    minY = position.y;

                if (position.y > maxY)
                    maxY = position.y;
            }

            return new BoundsInt
            {
                min = new(minX, minY),
                max = new(maxX + 1, maxY + 1)
            };
        }

        public static BoundsInt Encapsulate(IEnumerable<Vector3Int> positions)
        {
            using (ListPool<Vector3Int>.Get(out List<Vector3Int> sortedPosition))
            {
                sortedPosition.AddRange(positions);
                sortedPosition.Sort(Vector3IntComparer);

                int count = sortedPosition.Count;
                Vector3Int firstPosition = sortedPosition[0];
                Vector3Int lastPosition = sortedPosition[count - 1];
                BoundsInt bounds = new BoundsInt
                {
                    xMin = firstPosition.x,
                    xMax = lastPosition.x + 1,
                    yMin = firstPosition.y,
                    yMax = lastPosition.y + 1
                };

                return bounds;
            }
        }

        public static IEnumerable<Vector3Int> GetRow(this BoundsInt boundsInt, Vector3Int checkPosition)
        {
            for (int x = boundsInt.xMin; x < boundsInt.xMax; x++)
                yield return new Vector3Int(x, checkPosition.y);
        }

        public static IEnumerable<Vector3Int> GetColumn(this BoundsInt boundsInt, Vector3Int checkPosition)
        {
            for (int y = boundsInt.yMin; y < boundsInt.yMax; y++)
                yield return new Vector3Int(checkPosition.x, y);
        }

        public static void ForEach2D(this BoundsInt boundsInt, Action<Vector3Int> callback,
            SortOrder sortOrder = SortOrder.Ascending)
        {
            if (sortOrder == SortOrder.Ascending)
            {
                for (int x = boundsInt.xMin; x < boundsInt.xMax; x++)
                for (int y = boundsInt.yMin; y < boundsInt.yMax; y++)
                    callback.Invoke(new Vector3Int(x, y));
            }

            else if (sortOrder == SortOrder.Descending)
            {
                for (int x = boundsInt.xMax - 1; x >= boundsInt.xMin; x--)
                for (int y = boundsInt.yMax - 1; y >= boundsInt.yMin; y--)
                    callback.Invoke(new Vector3Int(x, y));
            }
        }

        public static void ForEach3D(this BoundsInt boundsInt, Action<Vector3Int> callback)
        {
            for (int x = boundsInt.xMin; x < boundsInt.xMax; x++)
            for (int y = boundsInt.yMin; y < boundsInt.yMax; y++)
            for (int z = boundsInt.zMin; z < boundsInt.zMax; z++)
                callback.Invoke(new Vector3Int(x, y, z));
        }

        public static IEnumerable<Vector3Int> Iterator2D(this BoundsInt boundsInt
            , SortOrder sortOrder = SortOrder.Ascending, AxisOrder axisOrder = AxisOrder.XY)
        {
            if (axisOrder == AxisOrder.XY)
            {
                if (sortOrder == SortOrder.Ascending)
                {
                    for (int x = boundsInt.xMin; x < boundsInt.xMax; x++)
                    for (int y = boundsInt.yMin; y < boundsInt.yMax; y++)
                        yield return new Vector3Int(x, y);
                }

                else if (sortOrder == SortOrder.Descending)
                {
                    for (int x = boundsInt.xMax - 1; x >= boundsInt.xMin; x--)
                    for (int y = boundsInt.yMax - 1; y >= boundsInt.yMin; y--)
                        yield return new Vector3Int(x, y);
                }
            }

            else if (axisOrder == AxisOrder.YX)
            {
                if (sortOrder == SortOrder.Ascending)
                {
                    for (int y = boundsInt.yMin; y < boundsInt.yMax; y++)
                    for (int x = boundsInt.xMin; x < boundsInt.xMax; x++)
                        yield return new Vector3Int(x, y);
                }

                else if (sortOrder == SortOrder.Descending)
                {
                    for (int y = boundsInt.yMax - 1; y >= boundsInt.yMin; y--)
                    for (int x = boundsInt.xMax - 1; x >= boundsInt.xMin; x--)
                        yield return new Vector3Int(x, y);
                }
            }
        }

        public static IEnumerable<Vector3Int> Iterator3D(this BoundsInt boundsInt,
            SortOrder sortOrder = SortOrder.Ascending)
        {
            if (sortOrder == SortOrder.Ascending)
            {
                for (int x = boundsInt.xMin; x <= boundsInt.xMax; x++)
                for (int y = boundsInt.yMin; y <= boundsInt.yMax; y++)
                for (int z = boundsInt.zMin; z <= boundsInt.zMax; z++)
                    yield return new Vector3Int(x, y, z);
            }

            else if (sortOrder == SortOrder.Descending)
            {
                for (int x = boundsInt.xMax - 1; x >= boundsInt.xMin; x--)
                for (int y = boundsInt.yMax - 1; y >= boundsInt.yMin; y--)
                for (int z = boundsInt.zMax - 1; z >= boundsInt.zMin; z--)
                    yield return new Vector3Int(x, y, z);
            }
        }

        public static IEnumerable<Vector3Int> IteratorIgnoreCorner2D(this BoundsInt boundsInt)
        {
            for (int x = boundsInt.xMin; x <= boundsInt.xMax; x++)
            {
                for (int y = boundsInt.yMin; y <= boundsInt.yMax; y++)
                {
                    if (x == boundsInt.xMin && y == boundsInt.yMin)
                        continue;

                    if (x == boundsInt.xMin && y == boundsInt.yMax)
                        continue;

                    if (x == boundsInt.xMax && y == boundsInt.yMin)
                        continue;

                    if (x == boundsInt.xMax && y == boundsInt.yMax)
                        continue;

                    yield return new Vector3Int(x, y);
                }
            }
        }

        public static IEnumerable<Vector3Int> GetBorder2D(this BoundsInt bounds)
        {
            for (int x = bounds.xMin; x < bounds.xMax; x++)
            {
                for (int y = bounds.yMin; y < bounds.yMax; y++)
                {
                    if (x > bounds.xMin && x < bounds.xMax - 2)
                        continue;

                    if (y > bounds.yMin && y < bounds.yMax - 2)
                        continue;

                    yield return new Vector3Int(x, y);
                }
            }
        }
    }
}
