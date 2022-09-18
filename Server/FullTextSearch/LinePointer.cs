using Client.Core;

namespace Server.FullTextSearch
{
    /// <summary>
    ///     Unique identifier for a line of text
    /// </summary>
    public class LinePointer
    {
        /// <summary>
        ///     Used for deserialization
        /// </summary>
        public LinePointer()
        {
        }

        public LinePointer(int line, KeyValue primaryKey)
        {
            Line = line;
            PrimaryKey = primaryKey;
        }

        public int Line { get; }

        public KeyValue PrimaryKey { get; }


        /// <summary>
        ///     Used when updating a document. The old pointers are marked as deleted and they are not used anymore
        /// </summary>
        public bool Deleted { get; set; }

        private bool Equals(LinePointer other)
        {
            return Line == other.Line && PrimaryKey == other.PrimaryKey && Deleted == other.Deleted;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((LinePointer)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = Line;
                hashCode = (hashCode * 397) ^ PrimaryKey.GetHashCode();
                // ReSharper disable once NonReadonlyMemberInGetHashCode
                hashCode = (hashCode * 397) ^ Deleted.GetHashCode();
                return hashCode;
            }
        }

        public static bool operator ==(LinePointer left, LinePointer right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(LinePointer left, LinePointer right)
        {
            return !Equals(left, right);
        }


        public override string ToString()
        {
            return $"{Line:D3} {PrimaryKey}";
        }
    }
}