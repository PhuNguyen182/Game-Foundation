using System;
using System.Collections.Generic;
using System.Linq;
using Random = UnityEngine.Random;

namespace DracoRuan.Utilities.CalculationExtensions.Collections
{
    public static class CollectionExtension
    {
        public static void AddRange<T>(this HashSet<T> hashSet, IEnumerable<T> collection)
        {
            foreach (T item in collection)
                hashSet.Add(item);
        }

        public static void ForEach<T>(this HashSet<T> hashSet, Action<T> callback = null)
        {
            foreach (T item in hashSet)
                callback?.Invoke(item);
        }

        public static void ForEach<T>(this IEnumerable<T> collection, Action<T> callback = null)
        {
            foreach (T item in collection)
                callback?.Invoke(item);
        }
        
        public static IEnumerable<TR> Iterator<T, TR>(this IEnumerable<T> collection, Func<T, TR> callback)
        {
            foreach (T item in collection)
                yield return callback.Invoke(item);
        }
        
        /// <summary>
        /// Get a list that removed the predicated elements.
        /// </summary>
        public static List<T> RemoveAllWhere<T>(this List<T> list, Predicate<T> predicate)
        {
            List<T> newList = new();
            foreach (T item in list)
            {
                if (!predicate(item)) 
                    newList.Add(item);
            }
            
            return newList;
        }

        /// <summary> 
        /// Get a random element in a list. 
        /// </summary>
        public static T GetRandom<T>(this IList<T> list)
        {
            if (list.Count == 0) throw new IndexOutOfRangeException("List is empty!");
            return list[Random.Range(0, list.Count)];
        }

        /// <summary>
        /// Get a random element in a list but different from the current.
        /// </summary>
        public static T GetRandomNotRepeat<T>(this IList<T> list, int current)
        {
            if (list.Count == 0) 
                throw new IndexOutOfRangeException("List is empty!");
            
            int next = Random.Range(0, list.Count);
            if (next == current) 
                return list.GetRandomNotRepeat(current);
            
            return list[next];
        }

        /// <summary>
        /// Remove random element in a list. Return that element.
        /// </summary>
        public static T RemoveRandom<T>(this IList<T> list)
        {
            if (list.Count == 0) 
                throw new IndexOutOfRangeException("List is empty!");
            
            int index = Random.Range(0, list.Count);
            T item = list[index];
            list.RemoveAt(index);
            return item;
        }
        
        public static void ForEach<T>(this List<T> list, Action<T> callback = null)
        {
            foreach (T item in list)
                callback?.Invoke(item);
        }

        public static void Shuffle<T>(this List<T> list)
        {
            int count = list.Count;

            for (int i = 0; i < count - 1; i++)
            {
                int randIndex = Random.Range(i + 1, count);
                (list[i], list[randIndex]) = (list[randIndex], list[i]);
            }
        }
        
        /// <summary>
        /// Return a list satisfies multiple predicates. Must use predicates which their set is not duplicated with others.
        /// </summary>
        public static List<T> Wheres<T>(this IList<T> list, params Func<T, bool>[] predicate)
        {
            List<T> newList = list.ToList();
            foreach (Func<T, bool> item in predicate) 
                newList = newList.Where(item).ToList();

            return newList;
        }

        /// <summary>
        /// Check if this item is the last item in the list.
        /// </summary>
        public static bool IsLast<T>(this IList<T> list, T item)
            => item.Equals(list[^1]);

        /// <summary>
        /// Check if a list is null or not.
        /// </summary>
        public static bool IsNull<T>(this IList<T> list)
            => list == null;

        /// <summary>
        /// Check if a list is empty or not.
        /// </summary>
        public static bool IsEmpty<T>(this IList<T> list)
            => !list.IsNull() && list.Count <= 0;

        /// <summary>
        /// Check if a list is null or empty or not.
        /// </summary>
        public static bool IsNullOrEmpty<T>(this IList<T> list)
            => list.IsNull() || list.IsEmpty();

        /// <summary>
        /// Add an element if the list doesn't contain it.
        /// </summary>
        public static IList<T> AddDistinct<T>(this IList<T> list, T element)
        {
            if (!list.Contains(element)) 
                list.Add(element);
            
            return list;
        }

        /// <summary>
        /// Try to get an element from a list.
        /// </summary>
        public static T TryGet<T>(this List<T> list, int index)
            => index >= list.Count ? default : list[index];

        /// <summary>
        /// Try to remove element(s) from a list.
        /// </summary>
        public static IList<T> TryRemove<T>(this IList<T> list, T element)
        {
            if (list.Contains(element))
            {
                list.Remove(element);
                return list;
            }

            throw new Exception("List does not contains element");
            
        }
        public static IList<T> TryRemove<T>(this IList<T> list, params T[] elements)
        {
            foreach (T element in elements) 
                list.TryRemove(element);
            
            return list;
        }

        /// <summary>
        /// Move an item from list1 into list2.
        /// </summary>
        public static void Move<T>(this List<T> list1, List<T> list2, T item)
        {
            list2.Add(item);
            list1.Remove(item);
        }

        /// <summary>
        /// Move an item in a list to another index.
        /// </summary>
        public static void MoveToIndex<T>(this List<T> list, int oldIndex, int newIndex)
        {
            if ((oldIndex == newIndex) || (0 > oldIndex) || (oldIndex >= list.Count) || (0 > newIndex) ||
                (newIndex >= list.Count)) return;
            
            T tmp = list[oldIndex];

            if (oldIndex < newIndex)
            {
                for (int i = oldIndex; i < newIndex; i++) 
                    list[i] = list[i + 1];
            }
            else
            {
                for (int i = oldIndex; i > newIndex; i--) 
                    list[i] = list[i - 1];
            }
            
            list[newIndex] = tmp;
        }
        
        public static void MoveToFirst<T>(this List<T> list, T item)
        {
            int index = list.IndexOf(item);
            if (index <= 0 || index >= list.Count) 
                return;
            
            list.MoveToIndex(index, 0);
        }
        
        public static void MoveToLast<T>(this List<T> list, T item)
        {
            int index = list.IndexOf(item);
            if (index < 0 || index >= list.Count) 
                return;
            
            list.MoveToIndex(index, list.Count - 1);
        }
    }
}
