using System.Collections.Concurrent;
using System.Collections.Generic;

namespace Client.Tools
{
 
    /// <summary>
    /// Thread safe dictionary. I know there is <see cref="ConcurrentDictionary{TKey,TValue}"/> but it is slower>
    /// </summary>
    /// <typeparam name="TKey"></typeparam>
    /// <typeparam name="TValue"></typeparam>
    public class SafeDictionary<TKey, TValue> 
    {
        private readonly Dictionary<TKey, TValue> _innerDictionary = new Dictionary<TKey, TValue>();
        

        public void Clear()
        {
            lock (_innerDictionary)
            {
                _innerDictionary.Clear();
            }
        }

       
        public int Count
        {
            get
            {
                lock (_innerDictionary)
                {
                    return _innerDictionary.Count;
                }
            }
        }


        public void Add(TKey key, TValue value)
        {
            lock (_innerDictionary)
            {
                _innerDictionary.Add(key, value);
            }
        }

        public bool ContainsKey(TKey key)
        {
            lock (_innerDictionary)
            {
                return _innerDictionary.ContainsKey(key);
            }
        }

        public bool Remove(TKey key)
        {
            lock (_innerDictionary)
            {
                return _innerDictionary.Remove(key);
            }
        }

        public bool TryGetValue(TKey key, out TValue value)
        {
            lock (_innerDictionary)
            {
                return _innerDictionary.TryGetValue(key, out value);
            }
        }

        public TValue this[TKey key]
        {
            get
            {
                lock (_innerDictionary)
                {
                    return _innerDictionary[key];
                }
            }
            set
            {
                lock (_innerDictionary)
                {
                    _innerDictionary[key] = value;
                }
            }
        }
    }
}