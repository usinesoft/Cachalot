using System;
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

        private readonly Func<TValue> _factory;

        public SafeDictionary(Func<TValue> factory)
        {
            _factory = factory;
        }

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

        public TValue TryGetValue(TKey key)
        {
            lock (_innerDictionary)
            {
                if (_innerDictionary.TryGetValue(key, out var value))
                {
                    return value;
                }
                
                return default;
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

        public TValue GetOrCreate(TKey key)
        {
            if(_factory == null)
                throw new NotSupportedException("Get or create called on a SafeDictionary instance that does not have a factory defined");
            lock (_innerDictionary)
            {
                if (!_innerDictionary.TryGetValue(key, out var value))
                {
                    value = _factory();
                    _innerDictionary[key] = value;
                }
                return value;
            }
        }

        public ICollection<TValue> Values
        {
            get
            {
                lock (_innerDictionary)
                {
                    return new List<TValue>(_innerDictionary.Values);
                }
            }
        }

        public ICollection<TKey> Keys
        {
            get
            {
                lock (_innerDictionary)
                {
                    return new List<TKey>(_innerDictionary.Keys);
                }
            }
        }

        public IList<TValue> ForKeys(ICollection<TKey> keys)
        {
            var result = new List<TValue>();

            lock (_innerDictionary)
            {
                foreach (var key in keys)
                {
                    if(_innerDictionary.TryGetValue(key, out var value))
                    {
                        result.Add(value);
                    }
                }
            }

            return result;
        }


    }
}