using System;
using System.Collections.Generic;
using System.Linq;
using Random = UnityEngine.Random;

namespace DracoRuan.Utilities.CalculationExtensions.Collections
{
    public static class ArrayExtension
    {
        /// <summary> 
        /// Get a random element in the array. 
        /// </summary>
        public static T GetRandom<T>(this T[] array)
        {
            if (array.Length == 0) throw new IndexOutOfRangeException("Array is empty.");
            return array[Random.Range(0, array.Length)];
        }

        /// <summary> 
        /// Get the number of elements that satisfy the condition in the array. 
        /// </summary>
        public static int Count<T>(this T[] list, Func<T, bool> predicate)
            => list.Where(predicate).ToArray().Length;

        /// <summary>
        /// Shuffle an array.
        /// </summary>
        public static void Shuffle<T>(this T[] list)
        {
            for (int i = list.Length - 1; i > 1; i--)
            {
                int j = Random.Range(0, i + 1);
                (list[j], list[i]) = (list[i], list[j]);
            }
        }

        /// <summary>
        /// Return a array satisfies multiple predicates.
        /// </summary>
        public static IList<T> Wheres<T>(this T[] list, IList<Func<T, bool>> predicate)
        {
            List<T> newList = new();

            foreach (Func<T, bool> item in predicate)
            {
                List<T> addList = list.Where(item).ToList();
                foreach (T element in addList) 
                    newList.Add(element);
            }

            return newList;
        }

        /// <summary>
        /// Check if an array is empty or not.
        /// </summary>
        public static bool IsEmpty<T>(this T[] array)
            => !array.IsNull() && array.Length <= 0;

        /// <summary>
        /// Swap 2 elements in an array.
        /// </summary>
        public static T[] Swap<T>(this T[] array, int indexA, int indexB)
        {
            (array[indexA], array[indexB]) = (array[indexB], array[indexA]);
            return array;
        }

        /// <summary>
        /// Add a value to an array;
        /// </summary>
        public static T[] Add<T>(this T[] array, T value)
        {
            List<T> list = array.ToList();
            list.Add(value);
            array = list.ToArray();
            return array;
        }

        /// <summary>
        /// Get index of an element in an array.
        /// </summary>
        public static int IndexOf<T>(this T[] array, T element)
            => Array.IndexOf(array, element);

        /// <summary>
        /// Try get an element from an array.
        /// </summary>
        public static T TryGet<T>(this T[] array, int index)
        {
            if (array.IsNullOrEmpty()) 
                return default;
            
            return array[index];
        }
        
        public static bool IsNull<T>(this T[] array)
            => array == null;
        
        public static bool IsNullOrEmpty<T>(this T[] array)
            => array is not { Length: > 0 };
    }
}
