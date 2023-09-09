using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using ProtoBuf;

namespace Client.Core;

/// <summary>
///     A collection of <see cref="KeyValue" /> for a collection property (they all have the same key name)
/// </summary>
[ProtoContract]
public class KeyValues
{
    /// <summary>
    ///     For serialization only
    /// </summary>
    [UsedImplicitly]
    private KeyValues()
    {
    }

    public KeyValues(string name, IEnumerable<KeyValue> values)
    {
        Name = name;
        Values = values.ToArray();
    }

    [field: ProtoMember(1)] public KeyValue[] Values { get; } = new KeyValue[0];

    [field: ProtoMember(2)] public string Name { get; }
}