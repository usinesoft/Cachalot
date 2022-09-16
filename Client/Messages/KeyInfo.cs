#region

using Client.Core;
using JetBrains.Annotations;
using ProtoBuf;
using System;

// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable AutoPropertyCanBeMadeGetOnly.Global

#endregion

namespace Client.Messages
{
    /// <summary>
    ///     Metadata of an property which is server-side visible
    /// </summary>
    [ProtoContract]
    public class KeyInfo : IEquatable<KeyInfo>
    {
        [UsedImplicitly]
        public KeyInfo()
        {
        }


        /// <summary>
        ///     Public constructor for non ordered keys
        /// </summary>
        /// <param name="name">property name</param>
        /// <param name="order">unique for a schema (primary key always 0)</param>
        /// <param name="indexType">type of index <see cref="IndexType" /></param>
        /// <param name="jsonName">not null only if json name is different from property name</param>
        /// <param name="isCollection">true if the property is a collection</param>
        public KeyInfo(string name, int order, IndexType indexType = IndexType.None, string jsonName = null, bool isCollection = false)
        {
            Name = name;
            Order = order;

            IndexType = indexType;
            IsCollection = isCollection;


            JsonName = jsonName ?? name;
        }


        /// <summary>
        ///     Key name. Unique inside a cacheable type
        /// </summary>

        [field: ProtoMember(1)]
        public string Name { get; set; }


        [field: ProtoMember(2)] public IndexType IndexType { get; set; }

        [field: ProtoMember(3)] public string JsonName { get; set; }

        [field: ProtoMember(4)] public int Order { get; set; }

        [field: ProtoMember(5)] public bool IsCollection { get; set; }

        public bool Equals(KeyInfo keyInfo)
        {
            if (keyInfo == null) return false;
            if (!Equals(IndexType, keyInfo.IndexType)) return false;
            if (!Equals(Name, keyInfo.Name)) return false;
            if (!Equals(JsonName, keyInfo.JsonName)) return false;
            if (!Equals(Order, keyInfo.Order)) return false;
            return true;
        }

        /// <summary>
        /// </summary>
        /// <param name="keyInfo1"> </param>
        /// <param name="keyInfo2"> </param>
        /// <returns> </returns>
        public static bool operator !=(KeyInfo keyInfo1, KeyInfo keyInfo2)
        {
            return !Equals(keyInfo1, keyInfo2);
        }

        /// <summary>
        /// </summary>
        /// <param name="keyInfo1"> </param>
        /// <param name="keyInfo2"> </param>
        /// <returns> </returns>
        public static bool operator ==(KeyInfo keyInfo1, KeyInfo keyInfo2)
        {
            return Equals(keyInfo1, keyInfo2);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(this, obj)) return true;
            return Equals(obj as KeyInfo);
        }

        public override int GetHashCode()
        {
            var result = IndexType.GetHashCode();
            result = 29 * result + Name.GetHashCode();
            // ReSharper disable once NonReadonlyMemberInGetHashCode
            result = 29 * result + Order.GetHashCode();
            return result;
        }


        public override string ToString()
        {
            return $"{nameof(Name)}: {Name}, {nameof(IndexType)}: {IndexType}, {nameof(Order)}: {Order}, {nameof(IsCollection)}: {IsCollection}";
        }
    }
}