using System.Collections.Generic;

namespace vietlabs.fr2
{
    internal static class CollectionExtensions
    {
        internal static List<T> InitializeOrClear<T>(this List<T> list)
        {
            if (list == null) return new List<T>();
            list.Clear();
            return list;
        }
        
        internal static Dictionary<TKey, TValue> InitializeOrClear<TKey, TValue>(this Dictionary<TKey, TValue> dictionary)
        {
            if (dictionary == null) return new Dictionary<TKey, TValue>();
            dictionary.Clear();
            return dictionary;
        }
        
        internal static void InitializeOrClear<T>(ref HashSet<T> hashSet)
        {
            if (hashSet == null)
            {
                hashSet = new HashSet<T>();
                return;
            }
            hashSet.Clear();
        }
    }
}
