using System.Collections.Generic;

namespace TrackerGen
{
    internal static class DictionaryExtensions
    {
        public static V TryGetValue<K, V>(this IDictionary<K, V> dict, K key, V defaultValue = default(V))
        {
            if (dict.TryGetValue(key, out var value))
            {
                return value;
            }
            return defaultValue;
        }
    }
}
