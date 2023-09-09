using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace Client.Tools;

/// <summary>
///     Thread safe dictionary. I know there is <see cref="ConcurrentDictionary{TKey,TValue}" /> but it is slower>
/// </summary>
/// <typeparam name="TKey"></typeparam>
/// <typeparam name="TValue"></typeparam>
public class SafeDictionary<TKey, TValue>
{
    private readonly Func<TValue> _factory;

    private readonly Dictionary<TKey, TValue> _innerDictionary = new();

    public SafeDictionary(Func<TValue> factory)
    {
        _factory = factory;
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


    public IList<KeyValuePair<TKey, TValue>> Pairs
    {
        get
        {
            lock (_innerDictionary)
            {
                return new List<KeyValuePair<TKey, TValue>>(_innerDictionary);
            }
        }
    }


    public void Clear()
    {
        lock (_innerDictionary)
        {
            _innerDictionary.Clear();
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

    public TValue TryRemove(TKey key)
    {
        lock (_innerDictionary)
        {
            if (_innerDictionary.TryGetValue(key, out var value))
            {
                _innerDictionary.Remove(key);
                return value;
            }

            return default;
        }
    }


    public TValue TryGetValue(TKey key)
    {
        lock (_innerDictionary)
        {
            if (_innerDictionary.TryGetValue(key, out var value)) return value;

            return default;
        }
    }

    public TValue GetOrCreate(TKey key)
    {
        if (_factory == null)
            throw new NotSupportedException(
                "Get or create called on a SafeDictionary instance that does not have a factory defined");

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
}