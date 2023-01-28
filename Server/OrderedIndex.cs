#region

using Client.Core;
using Client.Messages;
using Client.Queries;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;

#endregion

namespace Server
{
    /// <summary>
    ///     An index that supports comparison operators and sorting
    ///     On the other hand it only supports scalar keys (list keys are ignored)
    /// </summary>
    public class OrderedIndex : IndexBase
    {
        private readonly List<PackedObject> _tempData;
        private List<PackedObject> _data;

        private bool _insideFeedSession;

        /// <summary>
        ///     Index of the key associated to this index inside the <see cref="PackedObject.Values" />
        /// </summary>
        private int _keyIndex = -1; // not initialized

        public OrderedIndex(KeyInfo keyInfo) : base(keyInfo)
        {
            if (keyInfo.IsCollection)
                throw new ArgumentException("An ordered index can not index collection properties");

            _data = new List<PackedObject>();
            _tempData = new List<PackedObject>();
        }

        public override IndexType IndexType => IndexType.Ordered;


        public override void Clear()
        {
            _data.Clear();
        }


        public override IEnumerable<PackedObject> GetAll(bool descendingOrder = false, int maxCount = 0)
        {
            int count = 0;

            if (!descendingOrder)
            {
                foreach (var packedObject in _data)
                {
                    yield return packedObject;
                    count++;

                    if(maxCount > 0 && count > maxCount)
                        yield break;
                }
            }
            else
            {
                for (int i = _data.Count - 1; i >= 0; i--)
                {
                    yield return _data[i];
                    count++;

                    if(maxCount > 0 && count > maxCount)
                        yield break;
                }
            }

        }

        /// <summary>
        ///     Sort by specific key then by primary key
        /// </summary>
        /// <param name="left"></param>
        /// <param name="right"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int Compare(PackedObject left, PackedObject right)
        {
            var key1 = left.Values[_keyIndex];
            var key2 = right.Values[_keyIndex];


            var result = key1.CompareTo(key2);

            if (result != 0)
                return result;

            var key1Primary = left.PrimaryKey;
            var key2Primary = right.PrimaryKey;


            return key1Primary.CompareTo(key2Primary);
        }

        #region IndexBase

        public override void Put(PackedObject item)
        {
            // first time get the index of the indexation key(this value is fixed for a cacheable data type)
            if (_keyIndex == -1) _keyIndex = KeyInfo.Order;


            if (!_insideFeedSession)
            {
                if (_data.Count == 0)
                {
                    _data.Add(item);
                    return;
                }

                var indexInsertAfter = FindInsertAfterIndex(item, 0, _data.Count - 1);
                if (indexInsertAfter != -1)
                    if (indexInsertAfter == _data.Count - 1)
                        _data.Add(item);
                    else
                        _data.Insert(indexInsertAfter + 1, item);
                else
                    _data.Insert(0, item);
            }
            else
            {
                _tempData.Add(item);
            }
        }


        public override void BeginFill()
        {
            if (_insideFeedSession)
                throw new InvalidOperationException("Already in transaction");

            _insideFeedSession = true;
        }

        public override void EndFill()
        {
            if (!_insideFeedSession)
                throw new InvalidOperationException("Not inside a transaction");

            _data.AddRange(_tempData);
            _data.Sort(Compare);

            _tempData.Clear();

            _insideFeedSession = false;
        }


        public override void RemoveOne(PackedObject item)
        {
            // a feed session should not contain a primary key more than once. Can not remove inside a feed session because ordered
            // indexes are not sorted until the end of the session 
            if (_insideFeedSession)
                throw new InvalidOperationException(
                    "Illegal operation during a feed session (RemoveOne was called). Probably due duplicate primary key ");

            var index = FindIndexEq(item.Values[_keyIndex], 0, _data.Count);

            if (index == -1)
                throw new InvalidOperationException(
                    $"Can not find item {item} in the index for the Key {KeyInfo.Name}");

            while (index > 0 && _data[index - 1].Values[_keyIndex].CompareTo(item.Values[_keyIndex]) == 0
            ) index--;

            if (index != -1)
                for (var i = index; i < _data.Count; i++)
                    if (_data[i].PrimaryKey == item.PrimaryKey)
                    {
                        _data.RemoveAt(i);
                        break;
                    }
        }

        public override void RemoveMany(IList<PackedObject> items)
        {
            var primaryKeysToRemove = new HashSet<KeyValue>(items.Select(i => i.PrimaryKey));

            var newList = new List<PackedObject>(_data.Count);

            foreach (var cachedObject in _data)
                if (!primaryKeysToRemove.Contains(cachedObject.PrimaryKey))
                    newList.Add(cachedObject);

            // no need to reorder
            _data = newList;
        }

        public override int GetCount(IList<KeyValue> values, QueryOperator op = QueryOperator.Eq)
        {
            if (values.Count == 0)
                throw new ArgumentException("Empty list of keys passed to GetCount on index " + Name);


            if (values.Count > 2)
                throw new ArgumentException("More than two keys passed to GetCount on ordered index  " + Name);

            if (op == QueryOperator.In)
                return int.MaxValue; // can not count efficiently so do not use me as index

            if (values.Count == 2 && !op.IsRangeOperator())
                throw new ArgumentException("Two keys passed to GetCount on ordered index " + Name + " for operator " +
                                            op);


            


            var result = 0;
            switch (op)
            {
                case QueryOperator.Le:
                    result = CountAllLe(values[0]);
                    break;

                case QueryOperator.Lt:
                    result = CountAllLs(values[0]);
                    break;

                case QueryOperator.Eq:
                    result = CountAllEq(values[0]);
                    break;

                case QueryOperator.Ge:
                    result = CountAllGe(values[0]);
                    break;

                case QueryOperator.Gt:
                    result = CountAllGt(values[0]);
                    break;

                case QueryOperator.GeLe:
                    result = CountAllGeLe(values[0], values[1]);
                    break;

                case QueryOperator.GeLt:
                    result = CountAllGeLt(values[0], values[1]);
                    break;

                case QueryOperator.GtLe:
                    result = CountAllGtLe(values[0], values[1]);
                    break;

                case QueryOperator.GtLt:
                    result = CountAllGtLt(values[0], values[1]);
                    break;
            }

            return result;
        }

        public override ISet<PackedObject> GetMany(IList<KeyValue> values, QueryOperator op = QueryOperator.Eq)
        {
            if (_insideFeedSession)
                throw new InvalidOperationException(
                    "Illegal operation during a feed session (GetMany was called).");

            if (values.Count == 0)
                throw new ArgumentException("Empty list of keys passed to GetMany on index " + Name);

            if (values.Count > 1) 
            {
                if (op != QueryOperator.In)
                {
                    if (values.Count != 2 || !op.IsRangeOperator())
                    {
                        throw new ArgumentException($"{values.Count} with operator {op} are not supported");
                    }
                }
            }

            
            var result = new HashSet<PackedObject>();

            if (op == QueryOperator.In)
            {
                foreach (var value in values)
                {
                    FindAllEq(value, result);
                }

                return result;
            }

            switch (op)
            {
                case QueryOperator.Le:
                    FindAllLe(values[0], result);
                    break;

                case QueryOperator.Lt:
                    FindAllLs(values[0], result);
                    break;

                case QueryOperator.Eq:
                    FindAllEq(values[0], result);
                    break;

                case QueryOperator.Ge:
                    FindAllGe(values[0], result);
                    break;

                case QueryOperator.Gt:
                    FindAllGt(values[0], result);
                    break;

                //range operators
                case QueryOperator.GeLe:
                    FindAllGeLe(values[0], values[1], result);
                    break;

                case QueryOperator.GeLt:
                    FindAllGeLt(values[0], values[1], result);
                    break;

                case QueryOperator.GtLt:
                    FindAllGtLt(values[0], values[1], result);
                    break;

                case QueryOperator.GtLe:
                    FindAllGtLe(values[0], values[1], result);
                    break;
            }

            return result;
        }

        #endregion

        #region Le (Less or Equal)

        /// <summary>
        ///     Call <cref>findIndexLE</cref> then iterates through the equal items
        /// </summary>
        /// <param name="key"></param>
        /// <param name="result"></param>
        private void FindAllLe(KeyValue key, ICollection<PackedObject> result)
        {
            if (_data.Count == 0)
                return;


            var index = FindIndexLe(key, 0, _data.Count - 1);

            if (index != -1 && index < _data.Count - 1)
                while (_data[index].Values[_keyIndex].CompareTo(_data[index + 1].Values[_keyIndex]) == 0)
                {
                    index++;

                    if (index == _data.Count - 1)
                        break;
                }

            if (index != -1)
                for (var i = 0; i <= index; i++)
                    result.Add(_data[i]);
        }

        /// <summary>
        ///     count values Less than or equal to the specified value
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        private int CountAllLe(KeyValue key)
        {
            if (_data.Count == 0)
                return 0;


            var index = FindIndexLe(key, 0, _data.Count - 1);

            if (index != -1 && index < _data.Count - 1)
                while (_data[index].Values[_keyIndex].CompareTo(_data[index + 1].Values[_keyIndex]) == 0)
                {
                    index++;

                    if (index == _data.Count - 1)
                        break;
                }

            if (index != -1) return index + 1;

            return 0;
        }

        /// <summary>
        ///     Do a binary search on the indexed key searching for an element Less or Equal
        ///     By its nature the binary search will not return the first item matching the key but one of them
        /// </summary>
        /// <param name="value"></param>
        /// <param name="startIndex"></param>
        /// <param name="endIndex"></param>
        /// <returns> the index of an element having the exact key value or a value inferior to</returns>
        private int FindIndexLe(KeyValue value, int startIndex, int endIndex)
        {
            var mid = startIndex + (endIndex - startIndex) / 2;
            var midValue = _data[mid].Values[_keyIndex];
            if (midValue == value)
                return mid;

            var newStart = startIndex;
            var newEnd = endIndex;

            if (midValue.CompareTo(value) > 0)
                newEnd = mid;
            else
                newStart = mid;

            if (newStart == newEnd) //so also equal to mid
            {
                if (midValue.CompareTo(value) <= 0)
                    return mid;
                return -1;
            }

            if (newStart == newEnd - 1)
            {
                if (_data[newEnd].Values[_keyIndex].CompareTo(value) <= 0)
                    return newEnd;
                if (_data[newStart].Values[_keyIndex].CompareTo(value) <= 0)
                    return newStart;
                return -1;
            }

            return FindIndexLe(value, newStart, newEnd);
        }

        #endregion

        #region Lt (Less)

        /// <summary>
        ///     returns one equal value or the biggest less value
        /// </summary>
        /// <param name="value"></param>
        /// <param name="startIndex"></param>
        /// <param name="endIndex"></param>
        /// <returns></returns>
        private int FindIndexLt(KeyValue value, int startIndex, int endIndex)
        {
            var mid = startIndex + (endIndex - startIndex) / 2;
            var midValue = _data[mid].Values[_keyIndex];

            var newStart = startIndex;
            var newEnd = endIndex;

            if (midValue == value)
                return mid;

            if (midValue.CompareTo(value) > 0)
                newEnd = mid;
            else
                newStart = mid;

            if (newStart == newEnd) //so also equal to mid
            {
                if (midValue.CompareTo(value) < 0)
                    return mid;

                return -1;
            }

            if (newStart == newEnd - 1)
            {
                if (_data[newEnd].Values[_keyIndex].CompareTo(value) < 0)
                    return newEnd;
                if (_data[newStart].Values[_keyIndex].CompareTo(value) < 0)
                    return newStart;

                return -1;
            }

            return FindIndexLt(value, newStart, newEnd);
        }

        /// <summary>
        ///     Get the index of the biggest item less than the value using fully ordered comparison
        ///     Compare on index key first then on primary key
        /// </summary>
        /// <param name="value"></param>
        /// <param name="startIndex"></param>
        /// <param name="endIndex"></param>
        /// <returns></returns>
        private int FindInsertAfterIndex(PackedObject value, int startIndex, int endIndex)
        {
            var mid = startIndex + (endIndex - startIndex) / 2;
            var midValue = _data[mid];

            var newStart = startIndex;
            var newEnd = endIndex;

            if (midValue == value)
                return mid;

            if (Compare(midValue, value) > 0)
                newEnd = mid;
            else
                newStart = mid;

            if (newStart == newEnd) //so also equal to mid
            {
                if (Compare(midValue, value) < 0)
                    return mid;
                return -1;
            }

            if (newStart == newEnd - 1)
            {
                if (Compare(_data[newEnd], value) < 0)
                    return newEnd;
                if (Compare(_data[newStart], value) < 0)
                    return newStart;
                return -1;
            }

            return FindInsertAfterIndex(value, newStart, newEnd);
        }

        private void FindAllLs(KeyValue key, ICollection<PackedObject> result)
        {
            if (_data.Count == 0)
                return;

            var index = FindIndexLt(key, 0, _data.Count - 1);

            if (index != -1)
            {
                while (index > 0 && _data[index].Values[_keyIndex].CompareTo(key) == 0) index--;

                if (index == 0 && _data[index].Values[_keyIndex].CompareTo(key) == 0)
                    return;


                for (var i = 0; i <= index; i++) result.Add(_data[i]);
            }
        }

        private int CountAllLs(KeyValue key)
        {
            if (_data.Count == 0)
                return 0;

            var index = FindIndexLt(key, 0, _data.Count - 1);

            if (index != -1)
            {
                while (index > 0 && _data[index].Values[_keyIndex].CompareTo(key) == 0) index--;

                if (index == 0 && _data[index].Values[_keyIndex].CompareTo(key) == 0)
                    return 0;


                return index + 1;
            }

            return 0;
        }

        #endregion

        #region Eq (Equal)

        private void FindAllEq(KeyValue key, ICollection<PackedObject> result)
        {
            if (_data.Count == 0)
                return;

            var index = FindIndexEq(key, 0, _data.Count - 1);

            while (index > 0 && _data[index - 1].Values[_keyIndex].CompareTo(key) == 0) index--;


            if (index != -1)
                for (var i = index; i < _data.Count; i++)
                {
                    if (_data[i].Values[_keyIndex].CompareTo(key) != 0)
                        break;
                    result.Add(_data[i]);
                }
        }

        private int CountAllEq(KeyValue key)
        {
            if (_data.Count == 0)
                return 0;

            var index = FindIndexEq(key, 0, _data.Count - 1);

            while (index > 0 && _data[index - 1].Values[_keyIndex].CompareTo(key) == 0) index--;


            var items = 0;
            if (index != -1)
                for (var i = index; i < _data.Count; i++)
                {
                    if (_data[i].Values[_keyIndex].CompareTo(key) != 0)
                        break;
                    items++;
                }

            return items;
        }


        private int FindIndexEq(KeyValue value, int startIndex, int endIndex)
        {
            var mid = startIndex + (endIndex - startIndex) / 2;
            var midValue = _data[mid].Values[_keyIndex];

            var newStart = startIndex;
            var newEnd = endIndex;

            if (midValue == value)
                return mid;

            if (midValue.CompareTo(value) > 0)
                newEnd = mid;
            else
                newStart = mid;

            if (newStart == newEnd) //so also equal to mid
            {
                if (midValue.CompareTo(value) == 0)
                    return mid;
                return -1;
            }

            if (newStart == newEnd - 1)
            {
                if (_data[newEnd].Values[_keyIndex].CompareTo(value) == 0)
                    return newEnd;
                if (_data[newStart].Values[_keyIndex].CompareTo(value) == 0)
                    return newStart;
                return -1;
            }

            return FindIndexEq(value, newStart, newEnd);
        }

        #endregion

        #region Ge (Greater or Equal

        private int FindIndexGe(KeyValue value, int startIndex, int endIndex)
        {
            var mid = startIndex + (endIndex - startIndex) / 2;
            var midValue = _data[mid].Values[_keyIndex];
            if (midValue == value)
                return mid;

            var newStart = startIndex;
            var newEnd = endIndex;

            if (midValue.CompareTo(value) > 0)
                newEnd = mid;
            else
                newStart = mid;

            if (newStart == newEnd) //so also equal to mid
            {
                if (midValue.CompareTo(value) >= 0)
                    return mid;
                return -1;
            }

            if (newStart == newEnd - 1)
            {
                if (_data[newStart].Values[_keyIndex].CompareTo(value) >= 0)
                    return newStart;
                if (_data[newEnd].Values[_keyIndex].CompareTo(value) >= 0)
                    return newEnd;

                return -1;
            }

            return FindIndexGe(value, newStart, newEnd);
        }

        private void FindAllGe(KeyValue key, ICollection<PackedObject> result)
        {
            if (_data.Count == 0)
                return;

            var index = FindIndexGe(key, 0, _data.Count - 1);

            if (index > 0)
                while (_data[index].Values[_keyIndex].CompareTo(_data[index - 1].Values[_keyIndex]) == 0)
                {
                    index--;

                    if (index == 0)
                        break;
                }

            if (index != -1)
                for (var i = index; i <= _data.Count - 1; i++)
                    result.Add(_data[i]);
        }

        private int CountAllGe(KeyValue key)
        {
            if (_data.Count == 0)
                return 0;

            var index = FindIndexGe(key, 0, _data.Count - 1);

            if (index > 0)
                while (_data[index].Values[_keyIndex].CompareTo(_data[index - 1].Values[_keyIndex]) == 0)
                {
                    index--;

                    if (index == 0)
                        break;
                }

            if (index != -1) return _data.Count - index;

            return 0;
        }

        #endregion

        #region Gt (Greater)

        /// <summary>
        ///     Returns the index of one of the exact values or the index of the first greater value
        /// </summary>
        /// <param name="value"></param>
        /// <param name="startIndex"></param>
        /// <param name="endIndex"></param>
        /// <returns></returns>
        private int FindIndexGt(KeyValue value, int startIndex, int endIndex)
        {
            var mid = startIndex + (endIndex - startIndex) / 2;
            var midValue = _data[mid].Values[_keyIndex];

            var newStart = startIndex;
            var newEnd = endIndex;

            if (midValue == value)
                return mid;

            if (midValue.CompareTo(value) > 0)
                newEnd = mid;
            else
                newStart = mid;

            if (newStart == newEnd) //so also equal to mid
            {
                if (midValue.CompareTo(value) > 0)
                    return mid;
                return -1;
            }

            if (newStart == newEnd - 1)
            {
                if (_data[newStart].Values[_keyIndex].CompareTo(value) > 0)
                    return newStart;

                if (_data[newEnd].Values[_keyIndex].CompareTo(value) > 0)
                    return newEnd;

                return -1;
            }

            return FindIndexGt(value, newStart, newEnd);
        }

        private void FindAllGt(KeyValue key, ICollection<PackedObject> result)
        {
            if (_data.Count == 0)
                return;


            var index = FindIndexGt(key, 0, _data.Count - 1);

            if (index != -1)
            {
                // this is useful if the found index is the index of the exact value
                while (index < _data.Count - 1 && _data[index].Values[_keyIndex].CompareTo(key) == 0) index++;

                // manage the case of the exact value being present but no bigger one
                if (index == _data.Count - 1 && _data[index].Values[_keyIndex].CompareTo(key) == 0)
                    return;


                for (var i = index; i < _data.Count; i++) result.Add(_data[i]);
            }
        }


        private int CountAllGt(KeyValue key)
        {
            if (_data.Count == 0)
                return 0;


            var index = FindIndexGt(key, 0, _data.Count - 1);

            if (index == -1)
                return 0;

            //this is useful if the found index is the index of the exact value
            while (index < _data.Count - 1 && _data[index].Values[_keyIndex].CompareTo(key) == 0) index++;

            // manage the case of the exact value being present but no bigger one
            if (index == _data.Count - 1 && _data[index].Values[_keyIndex].CompareTo(key) == 0)
                return 0;

            if (index != -1) return _data.Count - index;

            return 0;
        }

        #endregion

        #region GeLe (Between)

        private void FindAllGeLe(KeyValue start, KeyValue end, ICollection<PackedObject> result)
        {
            // don't work too hard if no data available
            if (_data.Count == 0)
                return;

            // binary search for a suitable index
            var index = FindIndexGe(start, 0, _data.Count - 1);

            // move to the left if the index is at the end of a region of equal values
            if (index > 0)
                while (_data[index].Values[_keyIndex].CompareTo(_data[index - 1].Values[_keyIndex]) == 0)
                {
                    index--;

                    if (index == 0)
                        break;
                }


            if (index != -1)
                for (var i = index; i <= _data.Count - 1; i++)
                    if (_data[i].Values[_keyIndex] <= end)
                        result.Add(_data[i]);
                    else
                        break;
        }

        private void FindAllGeLt(KeyValue start, KeyValue end, ICollection<PackedObject> result)
        {
            // don't work too hard if no data available
            if (_data.Count == 0)
                return;

            // binary search for a suitable index
            var index = FindIndexGe(start, 0, _data.Count - 1);

            // move to the left if the index is at the end of a region of equal values
            if (index > 0)
                while (_data[index].Values[_keyIndex].CompareTo(_data[index - 1].Values[_keyIndex]) == 0)
                {
                    index--;

                    if (index == 0)
                        break;
                }


            if (index != -1)
                for (var i = index; i <= _data.Count - 1; i++)
                    if (_data[i].Values[_keyIndex] < end)
                        result.Add(_data[i]);
                    else
                        break;
        }

        private void FindAllGtLe(KeyValue start, KeyValue end, ICollection<PackedObject> result)
        {
            // don't work too hard if no data available
            if (_data.Count == 0)
                return;

            // binary search for a suitable index
            var index = FindIndexGt(start, 0, _data.Count - 1);


            if (index != -1)
                for (var i = index; i <= _data.Count - 1; i++)
                    if (_data[i].Values[_keyIndex] <= end)
                        result.Add(_data[i]);
                    else
                        break;
        }

        private void FindAllGtLt(KeyValue start, KeyValue end, ICollection<PackedObject> result)
        {
            // don't work too hard if no data available
            if (_data.Count == 0)
                return;

            // binary search for a suitable index
            var index = FindIndexGt(start, 0, _data.Count - 1);


            if (index != -1)
                for (var i = index; i <= _data.Count - 1; i++)
                    if (_data[i].Values[_keyIndex] < end)
                        result.Add(_data[i]);
                    else
                        break;
        }

        private int CountAllGeLe(KeyValue start, KeyValue end)
        {
            if (_data.Count == 0)
                return 0;

            // find the first index greater than or equal to start
            var indexStart = FindIndexGe(start, 0, _data.Count - 1);

            if (indexStart == -1) return 0;

            if (indexStart > 0)
                while (_data[indexStart].Values[_keyIndex].CompareTo(_data[indexStart - 1].Values[_keyIndex]) ==
                       0)
                {
                    indexStart--;

                    if (indexStart == 0)
                        break;
                }

            // find the last index lesser than or equal to end
            var indexEnd = FindIndexLe(end, 0, _data.Count - 1);

            if (indexEnd == -1) return 0;

            if (indexEnd < _data.Count - 1)
                while (_data[indexEnd].Values[_keyIndex].CompareTo(_data[indexEnd + 1].Values[_keyIndex]) == 0)
                {
                    indexEnd++;

                    if (indexEnd == _data.Count - 1)
                        break;
                }

            return indexEnd - indexStart + 1;
        }

        private int CountAllGtLe(KeyValue start, KeyValue end)
        {
            if (_data.Count == 0)
                return 0;

            // find the first index greater than start
            var indexStart = FindIndexGt(start, 0, _data.Count - 1);

            if (indexStart == -1) return 0;


            // find the last index lesser than or equal to end
            var indexEnd = FindIndexLe(end, 0, _data.Count - 1);

            if (indexEnd == -1) return 0;

            if (indexEnd < _data.Count - 1)
                while (_data[indexEnd].Values[_keyIndex].CompareTo(_data[indexEnd + 1].Values[_keyIndex]) == 0)
                {
                    indexEnd++;

                    if (indexEnd == _data.Count - 1)
                        break;
                }

            return indexEnd - indexStart + 1;
        }

        private int CountAllGeLt(KeyValue start, KeyValue end)
        {
            if (_data.Count == 0)
                return 0;

            // find the first index greater than or equal to start
            var indexStart = FindIndexGe(start, 0, _data.Count - 1);

            if (indexStart == -1) return 0;

            if (indexStart > 0)
                while (_data[indexStart].Values[_keyIndex].CompareTo(_data[indexStart - 1].Values[_keyIndex]) ==
                       0)
                {
                    indexStart--;

                    if (indexStart == 0)
                        break;
                }


            // find the last index lesser than or equal to end
            var indexEnd = FindIndexLt(end, 0, _data.Count - 1);

            if (indexEnd == -1) return 0;


            return indexEnd - indexStart + 1;
        }

        private int CountAllGtLt(KeyValue start, KeyValue end)
        {
            if (_data.Count == 0)
                return 0;

            // find the first index greater than start
            var indexStart = FindIndexGt(start, 0, _data.Count - 1);

            if (indexStart == -1) return 0;


            // find the last index lesser than or equal to end
            var indexEnd = FindIndexLt(end, 0, _data.Count - 1);

            if (indexEnd == -1) return 0;


            return indexEnd - indexStart + 1;
        }

        #endregion
    }
}