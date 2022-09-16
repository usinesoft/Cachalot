#region

using Client.Core;
using Client.Messages;
using Client.Queries;
using System;
using System.Collections.Generic;
using System.Linq;

#endregion

namespace Server
{
    public class DictionaryIndex : IndexBase
    {
        /// <summary>
        ///     Double indexation: first by key value then by primary key
        /// </summary>
        private readonly Dictionary<KeyValue, HashSet<PackedObject>> _data;

        /// <summary>
        ///     -1      non initialized
        ///     >= 0    the index of the property (index in <see cref="PackedObject.Values" /> if scalar property or in
        ///     <see cref="PackedObject.CollectionValues" />  if collection
        /// </summary>
        private int _keyIndex = -1;

        public DictionaryIndex(KeyInfo keyInfo) : base(keyInfo)
        {
            _data = new Dictionary<KeyValue, HashSet<PackedObject>>();
        }

        public override IndexType IndexType => IndexType.Dictionary;

        public override void BeginFill()
        {
            //nothing to do
        }

        /// <summary>
        ///     Put a new item in the index
        ///     REQUIRE: no item having the same primary key exists in the index
        /// </summary>
        /// <param name="item"> </param>
        public override void Put(PackedObject item)
        {
            if (_keyIndex == -1) // not initialized
            {
                _keyIndex = KeyInfo.Order;

            }

            if (!KeyInfo.IsCollection)
            {
                var keyValue = item.Values[_keyIndex];
                AddKeyValue(item, keyValue);
                return;
            }

            // at this point the indexed property is a collection
            foreach (var collectionValue in item.CollectionValues[_keyIndex].Values) AddKeyValue(item, collectionValue);
        }

        private void AddKeyValue(PackedObject item, KeyValue key)
        {
            if (!_data.TryGetValue(key, out var byPrimaryKey))
            {
                byPrimaryKey = new HashSet<PackedObject>();
                _data[key] = byPrimaryKey;
            }


            byPrimaryKey.Add(item);
        }


        public override void EndFill()
        {
        }

        public override ISet<PackedObject> GetMany(IList<KeyValue> values, QueryOperator op = QueryOperator.Eq)
        {
            if (op != QueryOperator.Eq && op != QueryOperator.In && op != QueryOperator.Contains)
                throw new NotSupportedException($"operator {op} not valid on the non ordered index {Name}");


            if (values.Count == 1)
            {
                if (_data.TryGetValue(values[0], out var valuesByPrimaryKey))
                    return new HashSet<PackedObject>(valuesByPrimaryKey);

                return new HashSet<PackedObject>();
            }

            var result = new HashSet<PackedObject>();

            foreach (var keyValue in values)
                if (_data.TryGetValue(keyValue, out var valuesByPrimaryKey))
                    result.UnionWith(valuesByPrimaryKey);

            return result;
        }

        public override int GetCount(IList<KeyValue> values, QueryOperator op = QueryOperator.Eq)
        {
            if (op != QueryOperator.Eq && op != QueryOperator.In && op != QueryOperator.Contains)
                return int.MaxValue;// to avoid this index being used for optimization

            return values.Where(keyValue => _data.ContainsKey(keyValue)).Sum(keyValue => _data[keyValue].Count);
        }

        public override void RemoveOne(PackedObject item)
        {
            if (_data.Count == 0)
                return;

            if (!KeyInfo.IsCollection)
            {
                var keyValue = item.Values[_keyIndex];
                if (_data.ContainsKey(keyValue)) _data[keyValue].Remove(item);
            }
            else
            {
                foreach (var listValue in item.CollectionValues[_keyIndex].Values)
                    if (_data.ContainsKey(listValue))
                        _data[listValue].Remove(item);
            }
        }

        public override void Clear()
        {
            _data.Clear();
        }

        public override void RemoveMany(IList<PackedObject> items)
        {
            // can not do much better for this index type
            foreach (var item in items) RemoveOne(item);
        }
    }
}