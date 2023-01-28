#region

using Client.Core;
using Client.Messages;
using Client.Queries;
using JetBrains.Annotations;
using System;
using System.Collections.Generic;
using System.Linq;

#endregion

namespace Server
{
    /// <summary>
    ///     Base class for all indices
    ///     Al support Put, Remove and GetMany
    ///     Only ordered indices support operators others than == (Eq or In)
    /// </summary>
    public abstract class IndexBase : IReadOnlyIndex
    {
        protected IndexBase(KeyInfo keyInfo)
        {
            KeyInfo = keyInfo;
        }

        public string Name => KeyInfo.Name;

        protected KeyInfo KeyInfo { get; }

        public abstract IndexType IndexType { get; }

        public abstract void BeginFill();

        /// <summary>
        ///     Put a new item in the index
        ///     REQUIRE: no item having the same primary key exists in the index
        ///     If an item
        /// </summary>
        /// <param name="item"></param>
        public abstract void Put(PackedObject item);

        public abstract void EndFill();

        /// <summary>
        ///     value Eq (or In ) is valid far all the indices
        ///     the others (Le,Lt,Ge,Gt) only for ordered ones (<cref>IsOrdered</cref> = true
        /// </summary>
        /// <param name="values"> one value for equality operator or multiple values for In operator</param>
        /// <param name="op"></param>
        /// <returns></returns>
        public abstract ISet<PackedObject> GetMany(IList<KeyValue> values, QueryOperator op = QueryOperator.Eq);

        public virtual IEnumerable<PackedObject> GetAll(bool descendingOrder = false, int maxCount = 0)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        ///     Count the items from the index matching the criteria
        /// </summary>
        /// <param name="values">value(s) to search for</param>
        /// <param name="op">operator to apply to the value</param>
        /// <returns>number of elements matching the key values and the operator or int.MaxValue</returns>
        public abstract int GetCount(IList<KeyValue> values, QueryOperator op = QueryOperator.Eq);


        public abstract void RemoveOne(PackedObject item);

        public abstract void Clear();
        public abstract void RemoveMany(IList<PackedObject> items);
    }

    public interface IReadOnlyIndex
    {
        string Name { get; }

        IndexType IndexType { get; }

        ISet<PackedObject> GetMany(IList<KeyValue> values, QueryOperator op = QueryOperator.Eq);

        /// <summary>
        /// Used only in full scan mode
        /// </summary>
        /// <returns></returns>
        IEnumerable<PackedObject> GetAll(bool descendingOrder = false, int maxCount = 0);

        int GetCount(IList<KeyValue> values, QueryOperator op = QueryOperator.Eq);
    }


    /// <summary>
    /// Wrap a simple dictionary as a read-only index. Useful for uniform access to all index types
    /// </summary>
    internal class UniqueIndex : IReadOnlyIndex
    {
        private readonly IDictionary<KeyValue, PackedObject> _dictionary;

        public UniqueIndex(string name, IDictionary<KeyValue, PackedObject> dictionary)
        {
            _dictionary = dictionary;
            Name = name;
        }

        public string Name { get; }

        public IndexType IndexType => IndexType.Primary;

        public ISet<PackedObject> GetMany([NotNull] IList<KeyValue> values, QueryOperator op = QueryOperator.Eq)
        {
            if (values == null) throw new ArgumentNullException(nameof(values));
            if (values.Count == 0) throw new ArgumentException("Value cannot be an empty collection.", nameof(values));

            if (op != QueryOperator.Eq)
                throw new ArgumentException("Only equality operator can be applied on a unique index");

            HashSet<PackedObject> result = new HashSet<PackedObject>();


            foreach (var keyValue in values)
            {
                if (_dictionary.TryGetValue(keyValue, out var value))
                {
                    result.Add(value);
                }
            }


            return result;
        }

        public IEnumerable<PackedObject> GetAll(bool descendingOrder = false, int maxCount = 0)
        {
            if (descendingOrder)
            {
                throw new NotSupportedException("Descending order can be used only on ordered indexes");
            }
            
            return maxCount == 0? _dictionary.Values:_dictionary.Values.Take(maxCount);
        }

        public int GetCount(IList<KeyValue> values, QueryOperator op = QueryOperator.Eq)
        {
            int result = 0;

            foreach (var keyValue in values)
            {
                if (_dictionary.ContainsKey(keyValue))
                {
                    result++;
                }
            }

            return result;
        }
    }
}