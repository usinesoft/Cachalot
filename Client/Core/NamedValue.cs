using System;
using System.Collections.Generic;
using ProtoBuf;

namespace Client.Core;

/// <summary>
///     KeyValue with name (to minimize memory usage the name is not stored any more in the KeyValue)
/// </summary>
public class NamedValue
{
    public NamedValue(KeyValue value, string name)
    {
        Value = value;
        Name = name;
    }

    [field: ProtoMember(1)] public KeyValue Value { get; }
    [field: ProtoMember(2)] public string Name { get; }

    public override bool Equals(object obj)
    {
        return obj is NamedValue value &&
               EqualityComparer<KeyValue>.Default.Equals(Value, value.Value) &&
               Name == value.Name;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Value, Name);
    }
}