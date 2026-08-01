using System;
using System.Collections.Generic;

namespace vietlabs.fr2
{
    internal static class DictionaryExtensions
    {
        internal static TValue GetValueOrDefault<TKey, TValue>(this Dictionary<TKey, TValue> dictionary, TKey key, TValue defaultValue = default(TValue))
        {
            return dictionary.TryGetValue(key, out TValue value) ? value : defaultValue;
        }
        
        internal static TValue TryGetValueOrAdd<TKey, TValue>(this Dictionary<TKey, TValue> dictionary, TKey key, TValue defaultValue)
        {
            if (dictionary.TryGetValue(key, out TValue value))
                return value;
            
            dictionary[key] = defaultValue;
            return defaultValue;
        }
    }
} 