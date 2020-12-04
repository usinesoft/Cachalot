using System;
using Client.Interface;
using Client.Messages;
using ProtoBuf;

namespace Client.Core
{
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

        [ProtoMember(9)] private bool _dataCompression;

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


        /// <summary>
        ///     True if all the loaders have finished loading data
        /// </summary>
        [ProtoMember(6)] private bool _ready;


        /// <summary>
        ///     Full name of the .NET <see cref="Type" />
        /// </summary>
        public string FullTypeName
        {
            get => _fullTypeName;
            set => _fullTypeName = value;
        }

        /// <summary>
        ///     Upper case short name of the data type
        /// </summary>
        public string TableName
        {
            get
            {
                var nameParts = _fullTypeName.Split('.');
                return nameParts[nameParts.Length - 1].ToUpper();
            }
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
        ///     True if all the loaders have finished loading data
        /// </summary>
        public bool Ready
        {
            get => _ready;
            set => _ready = value;
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

        public bool DataCompression
        {
            get => _dataCompression;
            set => _dataCompression = value;
        }


        public long ReadCount
        {
            get => _readCount;
            set => _readCount = value;
        }
    }
}