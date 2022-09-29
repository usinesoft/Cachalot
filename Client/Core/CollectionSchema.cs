#region

using Client.Messages;
using Newtonsoft.Json;
using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Linq;

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


        [ProtoMember(4)] public Layout StorageLayout { get; set; }

        /// <summary>
        ///     Fields that will be indexed for full text search
        /// </summary>
        [field: ProtoMember(5)] public ISet<string> FullText { get; } = new HashSet<string>();



        /// <summary>
        ///     The index fields
        /// </summary>
        [JsonIgnore] public IList<KeyInfo> IndexFields => ServerSide.Where(v => v.IndexType == IndexType.Dictionary || v.IndexType == IndexType.Ordered).ToList();


        [JsonIgnore] public KeyInfo PrimaryKeyField => ServerSide.Count > 0 ? ServerSide[0] : null;


        public int OrderOf(string name)
        {
            var property = ServerSide.FirstOrDefault(k => k.Name.ToLower() == name.ToLower());
            if (property != null)
                return property.Order;

            return -1;
        }

        public int[] IndexesOfNames(params string[] names)
        {
            var byName = ServerSide.ToDictionary(v => v.Name.ToLower(), v => v.Order);

            return names.Select(n => byName[n.ToLower()]).ToArray();
        }

        public string[] NamesOfScalarFields(params int[] indexes)
        {
            var byIndex = ServerSide.Where(x => !x.IsCollection).ToDictionary(x => x.Order, x => x.Name);

            return indexes.Select(x => byIndex[x]).ToArray();
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


            if (!Equals(StorageLayout, collectionSchema.StorageLayout))
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

        /// <summary>
        /// Clients can declare schemas that are simpler than the server side ones. This will not trigger reindexation 
        /// Client indexes may be a subset of server indexes. 
        /// The <see cref="Layout"/> of the two schemas must be the same
        /// The two schemas must define the same primary key
        /// Client index may be <see cref="IndexType.Dictionary"/> and the corresponding server index <see cref="IndexType.Ordered"/> but NOT the other way around.
        /// Client indexes may be <see cref="IndexType.None"/> and server index <see cref="IndexType.Dictionary"/> or <see cref="IndexType.Ordered"/> but NOT the other way around
        /// </summary>
        /// <param name="clientSchema"></param>
        /// <param name="serverSchema"></param>
        /// <returns></returns>
        public static bool AreCompatible(CollectionSchema clientSchema, CollectionSchema serverSchema)
        {
            if (clientSchema is null)
            {
                throw new ArgumentNullException(nameof(clientSchema));
            }

            if (serverSchema is null)
            {
                throw new ArgumentNullException(nameof(serverSchema));
            }

            
            if (!Equals(clientSchema.PrimaryKeyField, serverSchema.PrimaryKeyField))
                return false;

            
            if (!Equals(clientSchema.StorageLayout, serverSchema.StorageLayout))
                return false;

            //check all the fields
            if (clientSchema.ServerSide.Count > serverSchema.ServerSide.Count)
                return false;

            for (int i = 1; i < clientSchema.ServerSide.Count; i++)
            {
                var clientField = clientSchema.ServerSide[i];
                var serverField = serverSchema.ServerSide[i];

                if (clientField.Name != serverField.Name)
                    return false;

                if (clientField.Order != serverField.Order)
                    return false;

                if (clientField.IndexType > serverField.IndexType)
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