#region

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Xml;
using Client.Messages;
using ProtoBuf;

// ReSharper disable AutoPropertyCanBeMadeGetOnly.Global
// ReSharper disable UnusedMember.Global

// ReSharper disable FieldCanBeMadeReadOnly.Local
// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable NonReadonlyMemberInGetHashCode

#endregion

namespace Client.Core;

/// <summary>
///     Contains all information needed to create a collection on the server:
///     All indexed properties (simple, unique, primary key) with indexing parameters
///     Server-side values
///     Usage of data compression
/// </summary>
[ProtoContract]
public sealed class CollectionSchema : IEquatable<CollectionSchema>
{
    /// <summary>
    ///     Compatibility level between two schemas
    /// </summary>
    public enum CompatibilityLevel
    {
        /// <summary>
        ///     The new one is either identical or less complex that the old one
        /// </summary>
        Ok,

        /// <summary>
        ///     New indexes are added or dictionary indexes have changed into ordered ones
        /// </summary>
        NeedsReindex,

        /// <summary>
        ///     Changes detected that require all objects to be repacked
        /// </summary>
        NeedsRepacking
    }

    public CollectionSchema()
    {
    }

    [ProtoMember(1)] public List<KeyInfo> ServerSide { get; set; } = new();


    /// <summary>
    ///     Default name of the collection that uses the schema. Multiple collections can use it (with different names)
    /// </summary>
    [ProtoMember(2)]
    public string CollectionName { get; set; }


    [ProtoMember(4)] public Layout StorageLayout { get; set; }

    /// <summary>
    ///     Fields that will be indexed for full text search
    /// </summary>
    [field: ProtoMember(5)]
    public ISet<string> FullText { get; set; } = new HashSet<string>();


    /// <summary>
    ///     The index fields
    /// </summary>
    [JsonIgnore]
    public IList<KeyInfo> IndexFields => ServerSide
        .Where(v => v.IndexType is IndexType.Dictionary or IndexType.Ordered).ToList();


    [JsonIgnore] public KeyInfo PrimaryKeyField => ServerSide.Count > 0 ? ServerSide[0] : null;

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

        for (var i = 1; i < ServerSide.Count; i++)
            if (ServerSide[i] != collectionSchema.ServerSide[i])
                return false;


        return true;
    }

    public CollectionSchema Clone()
    {
        var bytes = SerializationHelper.ObjectToBytes(this, SerializationMode.ProtocolBuffers, false);

        return SerializationHelper.ObjectFromBytes<CollectionSchema>(bytes, SerializationMode.ProtocolBuffers,
            false);
    }


    public int OrderOf(string name)
    {
        var property = ServerSide.Find(k => string.Equals(k.Name, name, StringComparison.CurrentCultureIgnoreCase));
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
    ///     Clients can declare schemas that are simpler than the server side ones. This will not trigger re-indexation
    ///     Client indexes may be a subset of server indexes.
    ///     The <see cref="Layout" /> of the two schemas must be the same
    ///     The two schemas must define the same primary key
    ///     Client index may be <see cref="IndexType.Dictionary" /> and the corresponding server index
    ///     <see cref="IndexType.Ordered" /> but NOT the other way around.
    ///     Client indexes may be <see cref="IndexType.None" /> and server index <see cref="IndexType.Dictionary" /> or
    ///     <see cref="IndexType.Ordered" /> but NOT the other way around
    /// </summary>
    /// <param name="newSchema"></param>
    /// <param name="oldSchema"></param>
    /// <returns></returns>
    public static CompatibilityLevel AreCompatible(CollectionSchema newSchema, CollectionSchema oldSchema)
    {
        if (newSchema is null) throw new ArgumentNullException(nameof(newSchema));

        if (oldSchema is null) throw new ArgumentNullException(nameof(oldSchema));


        if (!Equals(newSchema.PrimaryKeyField, oldSchema.PrimaryKeyField))
            return CompatibilityLevel.NeedsRepacking;


        if (!Equals(newSchema.StorageLayout, oldSchema.StorageLayout))
            return CompatibilityLevel.NeedsRepacking;

        //check all the fields
        if (newSchema.ServerSide.Count > oldSchema.ServerSide.Count)
            return CompatibilityLevel.NeedsRepacking;

        for (var i = 1; i < newSchema.ServerSide.Count; i++)
        {
            var clientField = newSchema.ServerSide[i];
            var serverField = oldSchema.ServerSide[i];

            if (clientField.Name != serverField.Name)
                return CompatibilityLevel.NeedsRepacking;

            if (clientField.Order != serverField.Order)
                return CompatibilityLevel.NeedsRepacking;

            if (clientField.IndexType > serverField.IndexType)
                return CompatibilityLevel.NeedsReindex;
        }


        return CompatibilityLevel.Ok;
    }

    public KeyInfo KeyByName(string name)
    {
        name = name.ToLower();

        return ServerSide.Find(k => k.Name.ToLower() == name);
    }


    /// <summary>
    /// </summary>
    /// <returns> </returns>
    public override int GetHashCode()
    {
        return CollectionName.GetHashCode();
    }


}