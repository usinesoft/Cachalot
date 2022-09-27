using System.Collections.Generic;
using System.Threading;
using Client.Tools;

namespace Client.Core;

public static class KeyValuePool
{
    private const int PoolSize = 10_000_000;

    // shared null value
    private static readonly KeyValue Null = new KeyValue(null);
    
    private static readonly KeyValue True = new KeyValue(true);

    private static readonly KeyValue False = new KeyValue(false);

    private static int _hits;
    
    private static int _requests;
    
    private static int _complexValues;

    public static double HitRatio=> _hits/(double)_requests;
    public static double ComplexRatio=> _complexValues/(double)_requests;

    public static void Reset()
    {
        for (int i = 0; i < PoolSize; i++)
        {
            _stringPool[i] = null;
            _datePool[i] = null;
            _intPool[i] = null;
            _floatPool[i] = null;
        }
    }
        
    private static readonly KeyValue[] _stringPool = new KeyValue[PoolSize];
    private static readonly KeyValue[] _datePool = new KeyValue[PoolSize];
    private static readonly KeyValue[] _intPool = new KeyValue[PoolSize];
    private static readonly KeyValue[] _floatPool = new KeyValue[PoolSize];

    public static KeyValue Pool(KeyValue kv)
    {
        if (kv.IsNull)
        {
            return Null;
        }

        if(kv.Type == KeyValue.OriginalType.Boolean)
        {
            if(kv.IntValue == 0)
            {
                return False;
            }

            return True;
        }

        var poolToUse = kv.Type switch
        {
            KeyValue.OriginalType.SomeFloat => _floatPool,
            KeyValue.OriginalType.SomeInteger => _intPool,
            KeyValue.OriginalType.Date => _datePool,
            KeyValue.OriginalType.String => _stringPool,
            
            _ => throw new System.Exception("Shoud not get here")

        }; ;

        Interlocked.Increment(ref _requests);
        if(kv.ExtraBytes > 0)
        {
            Interlocked.Increment(ref _complexValues);
        }

        var position = (uint)kv.GetHashCode() % PoolSize;

        var poolValue = poolToUse[position];
        
        if (poolValue != null)
        {
            if (poolValue.Equals(kv))
            {
                Interlocked.Increment(ref _hits);
                return poolValue;
            }    
        }
        else
        {
            poolToUse[position] = kv;
        }

        return kv;
    }

    public static void ProcessPackedObject(PackedObject po)
    {
        for (int i = 0; i < po.Values.Length; i++)
        {
            po.Values[i] = Pool(po.Values[i]);
        }
    }

    public static void ProcessPackedObjects(IEnumerable<PackedObject> objects)
    {
        foreach (var o in objects)
        {
            ProcessPackedObject(o);
        }
    }
}

public static class KeyValueParsingPool
{
    private static readonly Dictionary<string, KeyValue> Cache = new ();

    public static KeyValue FromString(string value)
    {
        lock (Cache)
        {
            if (Cache.TryGetValue(value, out var kv))
            {
                return kv;
            }

            kv = CsvHelper.GetTypedValue(value);
            Cache[value] = kv;
            return kv;
        }
    }

    public static void Clear()
    {
        Cache.Clear();
    }


}