#region

using System;
using System.Reflection;
using Client.Core;
using Client.Interface;
using JetBrains.Annotations;
using Newtonsoft.Json;
using ProtoBuf;

// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable AutoPropertyCanBeMadeGetOnly.Global

#endregion

namespace Client.Messages
{
    /// <summary>
    ///     Serializable version of <see cref="ClientSideKeyInfo" />
    ///     As <see cref="ClientSideKeyInfo" /> is attached to a <see cref="PropertyInfo" /> it can not be deserialized
    ///     in a context where the concrete type is not available
    ///     This class is immutable
    /// </summary>
    [ProtoContract]
    public class KeyInfo : IEquatable<KeyInfo>
    {

        public const string DefaultNameForPrimaryKey = "id";

        [UsedImplicitly]
        public KeyInfo()
        {
        }


        /// <summary>
        ///     Public constructor for non ordered keys
        /// </summary>
        /// <param name="keyDataType"> </param>
        /// <param name="keyType"> </param>
        /// <param name="name"> </param>
        /// <param name="isOrdered"></param>
        /// <param name="isFullText"></param>
        /// <param name="serverSide"></param>
        /// <param name="jsonName"></param>
        public KeyInfo(KeyDataType keyDataType, KeyType keyType, string name = DefaultNameForPrimaryKey, bool isOrdered = false,
            bool isFullText = false, bool serverSide = false, string jsonName = null)
        {
            if (keyDataType == KeyDataType.Generate && keyType != KeyType.Primary)
            {
                throw new NotSupportedException("Only the primary key can be automatically generated");
            }

            KeyDataType = keyDataType;
            KeyType = keyType;
            Name = name;
            IsOrdered = isOrdered;
            IsFullTextIndexed = isFullText;
            IsServerSideVisible = serverSide;
            JsonName = jsonName??name;
        }


        

        /// <summary>
        ///     long or string as specified by <see cref="KeyDataType" />
        /// </summary>
        [ProtoMember(1)]
        public KeyDataType KeyDataType { get; set; }

        /// <summary>
        ///     Uniqueness of the key as specified by <see cref="KeyType" />
        /// </summary>
        [ProtoMember(2)]
        public KeyType KeyType { get; set; }

        /// <summary>
        ///     Key name. Unique inside a cacheable type
        /// </summary>
        [ProtoMember(3)]
        public string Name { get; set; }

        /// <summary>
        ///     Used only for index values. If the index is ordered, order operators can be applied
        /// </summary>
        [ProtoMember(4)]
        public bool IsOrdered { get; set; }

        [ProtoMember(5)] public bool IsFullTextIndexed { get; set; }

        [ProtoMember(6)] public bool IsServerSideVisible { get; set; }
        
        [ProtoMember(7)]  public string JsonName { get; set; }

        public bool Equals(KeyInfo keyInfo)
        {
            if (keyInfo == null) return false;
            if (!Equals(KeyDataType, keyInfo.KeyDataType)) return false;
            if (!Equals(KeyType, keyInfo.KeyType)) return false;
            if (!Equals(Name, keyInfo.Name)) return false;
            if (!Equals(IsOrdered, keyInfo.IsOrdered)) return false;
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
            var result = KeyDataType.GetHashCode();
            result = 29 * result + KeyType.GetHashCode();
            result = 29 * result + Name.GetHashCode();
            result = 29 * result + IsOrdered.GetHashCode();
            return result;
        }


        public override string ToString()
        {
            return
                $"| {Name,25} | {KeyType,13} | {KeyDataType,9} | {IsOrdered,8} |{IsFullTextIndexed,8} |{IsServerSideVisible,11} |";
        }


       
    }
}