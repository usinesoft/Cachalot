#region

using System;
using System.Collections.Generic;
using System.Linq;
using Client.Core;
using Client.Interface;
using Newtonsoft.Json;
using ProtoBuf;

// ReSharper disable AutoPropertyCanBeMadeGetOnly.Global
// ReSharper disable UnusedMember.Global

// ReSharper disable FieldCanBeMadeReadOnly.Local
// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable NonReadonlyMemberInGetHashCode

#endregion

namespace Client.Messages
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

        /// <summary>
        ///     The one and only primary key
        /// </summary>
        [ProtoMember(1)]public KeyInfo PrimaryKeyField { get; set; }

        [ProtoMember(2)] private List<KeyInfo> _uniqueKeyFields;

        [ProtoMember(3)] private List<KeyInfo> _indexFields;


        [ProtoMember(4)] private List<KeyInfo> _listFields;

        
        [ProtoMember(5)] private List<KeyInfo> _serverSideVisible;

        /// <summary>
        ///     Name of the collection (unique for a cache instance)
        /// </summary>
        [ProtoMember(6)]
        public string CollectionName { get; set; }


        /// <summary>
        ///     Short type name
        /// </summary>
        [ProtoMember(7)]
        public string TypeName { get; set; }

        [ProtoMember(8)] public bool UseCompression { get; set; }

        /// <summary>
        ///     Fields that will be indexed for full text search
        /// </summary>
        [field: ProtoMember(9)] public List<KeyInfo> FullText { get; }

        /// <summary>
        ///     This one is used only for protobuf serialization
        /// </summary>
        public CollectionSchema()
        {
            _uniqueKeyFields = new List<KeyInfo>();
            _indexFields = new List<KeyInfo>();
            _listFields = new List<KeyInfo>();
            _serverSideVisible = new List<KeyInfo>();

            FullText = new List<KeyInfo>();
        }

        /// <summary>
        ///     The only constructor is internal to prevent explicit instantiation
        /// </summary>
        /// <param name="description"> </param>
        internal  CollectionSchema(ClientSideTypeDescription description)
        {
            PrimaryKeyField = description.PrimaryKeyField.AsKeyInfo;
            _uniqueKeyFields = new List<KeyInfo>();
            _indexFields = new List<KeyInfo>();
            _listFields = new List<KeyInfo>();

            _serverSideVisible = new List<KeyInfo>();

            foreach (var uniqueKeyField in description.UniqueKeyFields) _uniqueKeyFields.Add(uniqueKeyField.AsKeyInfo);

            foreach (var indexField in description.IndexFields) _indexFields.Add(indexField.AsKeyInfo);

            foreach (var indexField in description.ListFields) _listFields.Add(indexField.AsKeyInfo);

            foreach (var indexField in description.ServerSideValues) _serverSideVisible.Add(indexField.AsKeyInfo);

            CollectionName = description.FullTypeName;
            TypeName = description.TypeName;

            UseCompression = description.UseCompression;

            FullText = new List<KeyInfo>();
            foreach (var fullTextIndex in description.FullTextIndexed) FullText.Add(fullTextIndex.AsKeyInfo);
        }

       

        /// <summary>
        ///     The unique keys
        /// </summary>
        public IList<KeyInfo> UniqueKeyFields => _uniqueKeyFields;

        /// <summary>
        ///     The index fields
        /// </summary>
        public IList<KeyInfo> IndexFields => _indexFields;

        /// <summary>
        ///     The list fields
        /// </summary>
        public IList<KeyInfo> ListFields => _listFields;

        public IList<KeyInfo> ServerSideValues => _serverSideVisible;


       


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

            //check all the unique keys
            if (_uniqueKeyFields.Count != collectionSchema._uniqueKeyFields.Count)
                return false;

            for (var i = 0; i < _uniqueKeyFields.Count; i++)
                if (!_uniqueKeyFields[i].Equals(collectionSchema._uniqueKeyFields[i]))
                    return false;

            //check all the index keys
            if (_indexFields.Count != collectionSchema._indexFields.Count)
                return false;

            for (var i = 0; i < _indexFields.Count; i++)
                if (!_indexFields[i].Equals(collectionSchema._indexFields[i]))
                    return false;

            if (_listFields.Count != collectionSchema._listFields.Count)
                return false;

            for (var i = 0; i < _listFields.Count; i++)
                if (!_listFields[i].Equals(collectionSchema._listFields[i]))
                    return false;


            return true;
        }

        public KeyInfo KeyByName(string name)
        {
            name = name.ToLower();

            if (PrimaryKeyField.Name.ToLower() == name) return PrimaryKeyField;

            return _indexFields.FirstOrDefault(k => k.Name.ToLower() == name) ??
                   _listFields.FirstOrDefault(k => k.Name.ToLower() == name) ??
                   _uniqueKeyFields.FirstOrDefault(k => k.Name.ToLower() == name) ?? 
                   _serverSideVisible.FirstOrDefault(k => k.Name.ToLower() == name);
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

        /// <summary>
        ///     Check if a property is indexed
        /// </summary>
        /// <param name="propertyName"></param>
        /// <returns></returns>
        public bool IsIndexed(string propertyName)
        {
            if (propertyName == PrimaryKeyField.Name) return true;

            if (IndexFields.Any(f => f.Name == propertyName)) return true;

            if (UniqueKeyFields.Any(f => f.Name == propertyName)) return true;

            if (ListFields.Any(f => f.Name == propertyName)) return true;


            return false;
        }


        public override string ToString()
        {
            return JsonConvert.SerializeObject(this);
        }
    }


    /// <summary>
    /// Programatically create type description using fluent syntax
    /// </summary>
    public static class Description
    {
        public static CollectionSchema New(string fullTypeName, bool useCompression = false)
        {
            var name = fullTypeName.Split('.').Last();
            return new CollectionSchema{CollectionName = fullTypeName, UseCompression = useCompression, TypeName = name};
        }

        public static CollectionSchema PrimaryKey(this CollectionSchema @this, string name, bool fullTextSearchEnabled = false)
        {

            @this.PrimaryKeyField = new KeyInfo
            {
                Name = name,
                JsonName = name, 
                KeyType = KeyType.Primary,
                KeyDataType = KeyDataType.Default,
                IsOrdered = false,
                IsFullTextIndexed = fullTextSearchEnabled
            };

            return @this;
        }

        public static CollectionSchema AutomaticPrimaryKey(this CollectionSchema @this, string name = KeyInfo.DefaultNameForPrimaryKey)
        {

            @this.PrimaryKeyField = new KeyInfo
            {
                Name = name,
                JsonName = name, 
                KeyType = KeyType.Primary,
                KeyDataType = KeyDataType.Generate,
                IsOrdered = false,
                IsFullTextIndexed = false
            };

            return @this;
        }

        public static CollectionSchema AddUniqueKey(this CollectionSchema @this, string name, bool fullTextSearchEnabled = false)
        {
            @this.UniqueKeyFields.Add(new KeyInfo{Name = name,JsonName = name,  KeyType = KeyType.Unique, KeyDataType = KeyDataType.Default, IsOrdered =  false, IsFullTextIndexed = fullTextSearchEnabled});

            return @this;
        }

        public static CollectionSchema AddIndex(this CollectionSchema @this, string name, bool ordered = false,  bool serverSideVisible = false, bool fullTextSearchEnabled = false)
        {
            @this.IndexFields.Add(new KeyInfo{Name = name, JsonName = name, KeyType = KeyType.ScalarIndex, KeyDataType = KeyDataType.Default, IsOrdered =  ordered, IsFullTextIndexed = fullTextSearchEnabled});

            if (serverSideVisible)
            {
                @this.ServerSideValues.Add(new KeyInfo{Name = name,JsonName = name,  KeyType = KeyType.ScalarIndex, KeyDataType = KeyDataType.Default, IsOrdered =  ordered, IsFullTextIndexed = false});
            }

            return @this;
        }

        public static CollectionSchema AddListIndex(this CollectionSchema @this, string name, bool fullTextSearchEnabled = false)
        {
            @this.ListFields.Add(new KeyInfo{Name = name, JsonName = name,  KeyType = KeyType.ScalarIndex, KeyDataType = KeyDataType.Default, IsOrdered =  false, IsFullTextIndexed = fullTextSearchEnabled});

            return @this;
        }

        public static CollectionSchema AddServerSideValue(this CollectionSchema @this, string name)
        {
            @this.ServerSideValues.Add(new KeyInfo{Name = name, JsonName = name,  KeyType = KeyType.None, KeyDataType = KeyDataType.Default, IsOrdered =  false, IsFullTextIndexed = false});

            return @this;
        }

    }
}