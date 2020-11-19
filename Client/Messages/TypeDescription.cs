#region

using System;
using System.Collections;
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
    ///     Serializable version of <see cref="ClientSideTypeDescription" />
    ///     Replaces all ClientSideKeyInfo by KeyInfo
    ///     This class is immutable and can be generated only from a <see cref="ClientSideTypeDescription" />
    ///     It can be used (and deserialized) in a context where the original type is not available
    /// </summary>
    [ProtoContract]
    public class TypeDescription : IEquatable<TypeDescription>
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
        ///     Long type name (unique for a cache instance)
        /// </summary>
        [ProtoMember(6)]
        public string FullTypeName { get; set; }


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
        public TypeDescription()
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
        internal  TypeDescription(ClientSideTypeDescription description)
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

            FullTypeName = description.FullTypeName;
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
        /// <param name="typeDescription"> </param>
        /// <returns> </returns>
        public bool Equals(TypeDescription typeDescription)
        {
            if (typeDescription == null)
                return false;
            if (!Equals(PrimaryKeyField, typeDescription.PrimaryKeyField))
                return false;
            if (!Equals(FullTypeName, typeDescription.FullTypeName))
                return false;
            if (!Equals(TypeName, typeDescription.TypeName))
                return false;
            if (!Equals(UseCompression, typeDescription.UseCompression))
                return false;

            //check all the unique keys
            if (_uniqueKeyFields.Count != typeDescription._uniqueKeyFields.Count)
                return false;

            for (var i = 0; i < _uniqueKeyFields.Count; i++)
                if (!_uniqueKeyFields[i].Equals(typeDescription._uniqueKeyFields[i]))
                    return false;

            //check all the index keys
            if (_indexFields.Count != typeDescription._indexFields.Count)
                return false;

            for (var i = 0; i < _indexFields.Count; i++)
                if (!_indexFields[i].Equals(typeDescription._indexFields[i]))
                    return false;

            if (_listFields.Count != typeDescription._listFields.Count)
                return false;

            for (var i = 0; i < _listFields.Count; i++)
                if (!_listFields[i].Equals(typeDescription._listFields[i]))
                    return false;


            return true;
        }

        public KeyInfo KeyByName(string name)
        {
            if (PrimaryKeyField.Name == name) return PrimaryKeyField;

            return _indexFields.FirstOrDefault(k => k.Name == name) ??
                   _listFields.FirstOrDefault(k => k.Name == name) ??
                   _uniqueKeyFields.FirstOrDefault(k => k.Name == name);
        }


        /// <summary>
        ///     Helper function, generate a valid index <see cref="KeyValue" /> by key name
        /// </summary>
        /// <returns> </returns>
        public KeyValue MakeIndexKeyValue(string keyName, object value)
        {
            foreach (var indexField in _indexFields)
                if (indexField.Name.ToUpper() == keyName.ToUpper())
                    return KeyInfo.ValueToKeyValue(value, indexField);


            throw new ArgumentException("Not an index key", nameof(keyName));
        }

        /// <summary>
        ///     helper function. Creates a valid collection of key values for an indexed collection property
        /// </summary>
        /// <param name="keyName"></param>
        /// <param name="values"></param>
        /// <returns></returns>
        public IEnumerable<KeyValue> MakeIndexedListKeyValues(string keyName, IEnumerable values)
        {
            foreach (var value in values)
            foreach (var indexField in _listFields)
                if (indexField.Name.ToUpper() == keyName.ToUpper())
                    yield return KeyInfo.ValueToKeyValue(value, indexField);
        }

        /// <summary>
        ///     Helper function, generate a valid unique <see cref="KeyValue" /> by key name
        /// </summary>
        /// <param name="keyName"> case insensitive key name </param>
        /// <param name="value"> value to convert </param>
        /// <returns> </returns>
        public KeyValue MakeUniqueKeyValue(string keyName, object value)
        {
            foreach (var uniqueKey in _uniqueKeyFields)
                if (uniqueKey.Name.ToUpper() == keyName.ToUpper())
                    return KeyInfo.ValueToKeyValue(value, uniqueKey);


            throw new ArgumentException("Not a unique key", nameof(keyName));
        }


        /// <summary>
        ///     Helper function, generate a valid <see cref="KeyValue" /> by key name
        ///     (can be the name of a key of any type)
        /// </summary>
        /// <param name="keyName"> case insensitive key name </param>
        /// <param name="value"> value to convert </param>
        /// <returns> </returns>
        public KeyValue MakeKeyValue(string keyName, object value)
        {
            if (PrimaryKeyField.Name.ToUpper() == keyName.ToUpper())
                return KeyInfo.ValueToKeyValue(value, PrimaryKeyField);

            foreach (var uniqueKey in _uniqueKeyFields)
                if (uniqueKey.Name.ToUpper() == keyName.ToUpper())
                    return KeyInfo.ValueToKeyValue(value, uniqueKey);

            foreach (var indexKey in _indexFields)
                if (indexKey.Name.ToUpper() == keyName.ToUpper())
                    return KeyInfo.ValueToKeyValue(value, indexKey);

            foreach (var indexKey in _listFields)
                if (indexKey.Name.ToUpper() == keyName.ToUpper())
                    return KeyInfo.ValueToKeyValue(value, indexKey);

            throw new ArgumentException("Not an indexation key", nameof(keyName));
        }

        /// <summary>
        ///     Helper method. Create a valid <see cref="KeyValue" /> for the primary key of this type
        /// </summary>
        /// <param name="value"> </param>
        /// <returns> </returns>
        public KeyValue MakePrimaryKeyValue(object value)
        {
            if (value == null)
                throw new ArgumentNullException(nameof(value));

            return KeyInfo.ValueToKeyValue(value, PrimaryKeyField);
        }

        /// <summary>
        /// </summary>
        /// <param name="obj"> </param>
        /// <returns> </returns>
        public override bool Equals(object obj)
        {
            if (ReferenceEquals(this, obj))
                return true;
            return Equals(obj as TypeDescription);
        }

        /// <summary>
        /// </summary>
        /// <returns> </returns>
        public override int GetHashCode()
        {
            return FullTypeName.GetHashCode();
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
        public static TypeDescription New(string fullTypeName, bool useCompression = false)
        {
            var name = fullTypeName.Split('.').Last();
            return new TypeDescription{FullTypeName = fullTypeName, UseCompression = useCompression, TypeName = name};
        }

        public static TypeDescription PrimaryKey(this TypeDescription @this, string name, bool fullTextSearchEnabled = false)
        {

            @this.PrimaryKeyField = new KeyInfo
            {
                Name = name,
                KeyType = KeyType.Primary,
                KeyDataType = KeyDataType.Default,
                IsOrdered = false,
                IsFullTextIndexed = fullTextSearchEnabled
            };

            return @this;
        }

        public static TypeDescription AddUniqueKey(this TypeDescription @this, string name, bool fullTextSearchEnabled = false)
        {
            @this.UniqueKeyFields.Add(new KeyInfo{Name = name, KeyType = KeyType.Unique, KeyDataType = KeyDataType.Default, IsOrdered =  false, IsFullTextIndexed = fullTextSearchEnabled});

            return @this;
        }

        public static TypeDescription AddIndex(this TypeDescription @this, string name, bool ordered = false,  bool serverSideVisible = false, bool fullTextSearchEnabled = false)
        {
            @this.IndexFields.Add(new KeyInfo{Name = name, KeyType = KeyType.ScalarIndex, KeyDataType = KeyDataType.Default, IsOrdered =  ordered, IsFullTextIndexed = fullTextSearchEnabled});

            if (serverSideVisible)
            {
                @this.ServerSideValues.Add(new KeyInfo{Name = name, KeyType = KeyType.ScalarIndex, KeyDataType = KeyDataType.Default, IsOrdered =  ordered, IsFullTextIndexed = false});
            }

            return @this;
        }

        public static TypeDescription AddListIndex(this TypeDescription @this, string name, bool fullTextSearchEnabled = false)
        {
            @this.ListFields.Add(new KeyInfo{Name = name, KeyType = KeyType.ScalarIndex, KeyDataType = KeyDataType.Default, IsOrdered =  false, IsFullTextIndexed = fullTextSearchEnabled});

            return @this;
        }

        public static TypeDescription AddServerSideValue(this TypeDescription @this, string name)
        {
            @this.ServerSideValues.Add(new KeyInfo{Name = name, KeyType = KeyType.None, KeyDataType = KeyDataType.Default, IsOrdered =  false, IsFullTextIndexed = false});

            return @this;
        }

    }
}