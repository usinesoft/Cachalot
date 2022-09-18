using System.Collections.Generic;
using Client.Tools;

namespace Client.Core;

public class KeyValuePool
{
    private const int PoolSize = 10_000_000;
        
    private static readonly KeyValue[] _pool = new KeyValue[PoolSize];

    public static KeyValue Pool(KeyValue kv)
    {
        var position = (uint)kv.GetHashCode() % PoolSize;

        var poolValue = _pool[position];
        
        if (poolValue != null)
        {
            if (poolValue.Equals(kv))
            {
                return poolValue;
            }    
        }
        else
        {
            _pool[position] = kv;
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


}