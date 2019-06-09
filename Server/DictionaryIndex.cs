#region

using System;
using System.Collections.Generic;
using System.Linq;
using Client.Core;
using Client.Messages;
using Client.Queries;

#endregion

namespace Server
{
    public class DictionaryIndex : IndexBase
    {
        /// <summary>
        ///     Double indexation: first by key value then by primary key
        /// </summary>
        private readonly Dictionary<KeyValue, Dictionary<KeyValue, CachedObject>> _data;

        /// <summary>
        ///     -1      non initialized
        ///     -2      list index
        ///     >= 0    the index of the property
        /// </summary>
        private int _keyIndex = -1;

        public DictionaryIndex(KeyInfo keyInfo) : base(keyInfo)
        {
            _data = new Dictionary<KeyValue, Dictionary<KeyValue, CachedObject>>();
        }

        public override bool IsOrdered => false;

        public override void BeginFill()
        {
            //nothing to do
        }

        /// <summary>
        ///     Put a new item in the index
        ///     REQUIRE: no item having the same primary key exists in the index
        /// </summary>
        /// <param name="item"> </param>
        public override void Put(CachedObject item)
        {
            // first time get the index of the indexation key(this value is fixed for a cacheable data type)            
            if (_keyIndex == -1) // non initialized 
                for (var i = 0; i < item.IndexKeys.Length; i++)
                    if (item.IndexKeys[i].KeyName == KeyInfo.Name)
                    {
                        _keyIndex = i;
                        break;
                    }


            if (_keyIndex >= 0)
            {
                var keyValue = item.IndexKeys[_keyIndex];
                AddKeyValue(item, keyValue);
                return;
            }


            if (item.ListIndexKeys != null)
                foreach (var keyValue in item.ListIndexKeys.Where(t => t.KeyName == KeyInfo.Name))
                {
                    AddKeyValue(item, keyValue);
                    _keyIndex = -2; // list index so no need to lookup in normal indexes
                }
        }

        private void AddKeyValue(CachedObject item, KeyValue key)
        {
            if (!_data.TryGetValue(key, out var byPrimaryKey))
            {
                byPrimaryKey = new Dictionary<KeyValue, CachedObject>();
                _data[key] = byPrimaryKey;
            }

            byPrimaryKey.Add(item.PrimaryKey, item);
        }


        public override void EndFill()
        {
        }

        public override ISet<CachedObject> GetMany(IList<KeyValue> values, QueryOperator op = QueryOperator.Eq)
        {
            if (op != QueryOperator.Eq && op != QueryOperator.In)
                throw new NotSupportedException("Applying comparison operator on non ordered index:" + Name);

            var result = new HashSet<CachedObject>();

            foreach (var keyValue in values)
                if (_data.TryGetValue(keyValue, out var valuesByPrimaryKey))
                    result.UnionWith(valuesByPrimaryKey.Values);

            return result;
        }

        public override int GetCount(IList<KeyValue> values, QueryOperator op = QueryOperator.Eq)
        {
            if (op != QueryOperator.Eq && op != QueryOperator.In)
                return int.MaxValue;

            return values.Where(keyValue => _data.ContainsKey(keyValue)).Sum(keyValue => _data[keyValue].Count);
        }

        public override void RemoveOne(CachedObject item)
        {
            if (_data.Count == 0)
                return;


            if (_keyIndex >= 0)
            {
                var keyValue = item.IndexKeys[_keyIndex];
                if (_data.ContainsKey(keyValue))
                {
                    _data[keyValue].Remove(item.PrimaryKey);
                    return; //if it is a scalar index key it is unique (its name identifies the index)
                }
            }

            // if list values are present then the same object may be present multiple times in the same index
            if (item.ListIndexKeys != null)
                foreach (var listValue in item.ListIndexKeys)
                    if (listValue.KeyName == KeyInfo.Name)
                        if (_data.ContainsKey(listValue))
                            _data[listValue].Remove(item.PrimaryKey);
        }

        public override void Clear()
        {
            _data.Clear();
        }

        public override void RemoveMany(IList<CachedObject> items)
        {
            // can not do much better for this index type
            foreach (var item in items) RemoveOne(item);
        }
    }
}