#region

using System;
using System.Collections.Generic;
using System.Linq;
using Client.Messages;
using Newtonsoft.Json;
using ProtoBuf;

// ReSharper disable AutoPropertyCanBeMadeGetOnly.Global
// ReSharper disable UnusedMember.Global

// ReSharper disable FieldCanBeMadeReadOnly.Local
// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable NonReadonlyMemberInGetHashCode

#endregion

namespace Client.Core
{
    /// <summary>
    ///     Contains all information needed to create a collection on the server:
    ///         All indexed properties (simple, unique, primary key) with indexing parameters
    ///         Server-side values
    ///         Usage of data compression
    /// </summary>
    [ProtoContract]
    public class CollectionSchema : IEquatable<CollectionSchema>
    {

        internal CollectionSchema()
        {

        }

        public CollectionSchema Clone()
        {
            var bytes = SerializationHelper.ObjectToBytes(this, SerializationMode.ProtocolBuffers, false);

            return SerializationHelper.ObjectFromBytes<CollectionSchema>(bytes, SerializationMode.ProtocolBuffers,
                false);
        }

        [ProtoMember(1)] public List<KeyInfo> ServerSide { get; set; } = new List<KeyInfo>();

        
        /// <summary>
        ///     Default name of the collection that uses the schema. Multiple collections can use it (with different names)
        /// </summary>
        [ProtoMember(2)]
        public string CollectionName { get; set; }


        /// <summary>
        ///     Short type name. May be the same as CollectionName. If CollectionName is a full type name with namespaces this will be the simple name
        /// </summary>
        [ProtoMember(3)]
        public string TypeName { get; set; }

        [ProtoMember(4)] public bool UseCompression { get; set; }

        /// <summary>
        ///     Fields that will be indexed for full text search
        /// </summary>
        [field: ProtoMember(5)] public ISet<string> FullText { get; } = new HashSet<string>();


        /// <summary>
        ///     The unique keys
        /// </summary>
        [JsonIgnore] public IList<KeyInfo> UniqueKeyFields => ServerSide.Where(v=>v.IndexType ==  IndexType.Unique).ToList();

        /// <summary>
        ///     The index fields
        /// </summary>
        [JsonIgnore] public IList<KeyInfo> IndexFields => ServerSide.Where(v=>v.IndexType ==  IndexType.Dictionary || v.IndexType == IndexType.Ordered).ToList();

        
        [JsonIgnore] public KeyInfo PrimaryKeyField => ServerSide.Count > 0? ServerSide[0]:null;


        public int OrderOf(string name)
        {
            var property = ServerSide.FirstOrDefault(k => k.Name == name);
            if (property != null)
                return property.Order;

            return -1;
        }

        public int[] IndexesOfNames(params string[] names)
        {
            var byName = ServerSide.ToDictionary(v => v.Name, v => v.Order);

            return names.Select(n=>byName[n]).ToArray();
        }
        /// <summary>
        /// </summary>
        /// <param name="collectionSchema"> </param>
        /// <returns> </returns>
        public bool Equals(CollectionSchema collectionSchema)
        {
            if (collectionSchema == null)
                return false;

            if (!Equals(PrimaryKeyField, collectionSchema.PrimaryKeyField))
                return false;

            if (!Equals(CollectionName, collectionSchema.CollectionName))
                return false;

            if (!Equals(TypeName, collectionSchema.TypeName))
                return false;

            if (!Equals(UseCompression, collectionSchema.UseCompression))
                return false;

            //check all the fields
            if (ServerSide.Count != collectionSchema.ServerSide.Count)
                return false;

            for (int i = 1; i < ServerSide.Count; i++)
            {
                if (ServerSide[i] != collectionSchema.ServerSide[i])
                    return false;
            }
            

            return true;
        }

        public KeyInfo KeyByName(string name)
        {
            name = name.ToLower();

            return ServerSide.FirstOrDefault(k => k.Name.ToLower() == name);
        }


       
        /// <summary>
        /// </summary>
        /// <param name="obj"> </param>
        /// <returns> </returns>
        public override bool Equals(object obj)
        {
            if (ReferenceEquals(this, obj))
                return true;
            return Equals(obj as CollectionSchema);
        }

        /// <summary>
        /// </summary>
        /// <returns> </returns>
        public override int GetHashCode()
        {
            return CollectionName.GetHashCode();
        }


        public override string ToString()
        {
            return JsonConvert.SerializeObject(this, Formatting.Indented);
        }
    }
}