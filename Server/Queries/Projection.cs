using Client.Core;
using System.Collections.Generic;

namespace Server.Queries
{
    /// <summary>
    /// A projection is a subset of scalar object values
    /// It has deep-equal semantics and it is used to resolve distinct clauses in queries (eliminate duplicates on a subset of properties)
    /// </summary>
    sealed class Projection
    {
        private readonly int _hashCode;

        public Projection(PackedObject source, params int[] indexes)
        {
            foreach (var index in indexes)
            {
                var value = source.Values[index];
                _hashCode ^= value.GetHashCode();

                Values.Add(value);
            }
        }

        private List<KeyValue> Values { get; } = new List<KeyValue>();

        private bool Equals(Projection other)
        {
            if (_hashCode != other._hashCode)
            {
                return false;
            }

            if (Values.Count != other.Values.Count)
                return false;

            for (int i = 0; i < Values.Count; i++)
            {
                if (Values[i] != other.Values[i])
                    return false;
            }

            return true;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((Projection)obj);
        }

        public override int GetHashCode()
        {
            return _hashCode;
        }
    }
}