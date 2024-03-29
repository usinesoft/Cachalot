using System;
using Client.Interface;
using ProtoBuf;

namespace Client.Core;

/// <summary>
///     Contains complementary information about a data table(not contained in the <see cref="CollectionSchema" />)
/// </summary>
[ProtoContract]
[Serializable]
public class DataStoreInfo
{
    [NonSerialized] [ProtoMember(7)] private DomainDescription _availableData;

    /// <summary>
    ///     Number of items in the datastore
    /// </summary>
    [ProtoMember(3)] private long _count;

    /// <summary>
    ///     Eviction policy for this cacheable type
    /// </summary>
    [ProtoMember(4)] private EvictionType _evictionPolicy;

    /// <summary>
    ///     Policy specific description
    /// </summary>
    [ProtoMember(5)] private string _evictionPolicyDescription;

    [ProtoMember(2)] private string _fullTypeName;

    [ProtoMember(8)] private long _hitCount;

    [ProtoMember(11)] private long _readCount;

    [ProtoMember(9)] private Layout _storageLayout;


    /// <summary>
    ///     Full name of the .NET <see cref="Type" />
    /// </summary>
    public string FullTypeName
    {
        get => _fullTypeName;
        set => _fullTypeName = value;
    }


    /// <summary>
    ///     Number of items in the datastore
    /// </summary>
    public long Count
    {
        get => _count;
        set => _count = value;
    }

    /// <summary>
    ///     Eviction policy for this cacheable type
    /// </summary>
    public EvictionType EvictionPolicy
    {
        get => _evictionPolicy;
        set => _evictionPolicy = value;
    }

    /// <summary>
    ///     Policy specific description
    /// </summary>
    public string EvictionPolicyDescription
    {
        get => _evictionPolicyDescription;
        set => _evictionPolicyDescription = value;
    }


    /// <summary>
    ///     Description of the loaded data <seealso cref="DomainDescription" />
    /// </summary>
    public DomainDescription AvailableData
    {
        get => _availableData;
        set => _availableData = value;
    }


    public long HitCount
    {
        get => _hitCount;
        set => _hitCount = value;
    }

    public Layout StorageLayout
    {
        get => _storageLayout;
        set => _storageLayout = value;
    }


    public long ReadCount
    {
        get => _readCount;
        set => _readCount = value;
    }
}