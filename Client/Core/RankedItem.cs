using Newtonsoft.Json.Linq;

namespace Client.Core
{
    public class RankedItem
    {
        public double Rank { get; }

        public JObject Item { get; }

        public RankedItem(double rank, JObject item)
        {
            Rank = rank;
            Item = item;
        }

        private bool Equals(RankedItem other)
        {
            return _comparer.Equals(Item, other.Item);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((RankedItem)obj);
        }

        readonly JTokenEqualityComparer _comparer = new JTokenEqualityComparer();
        public override int GetHashCode()
        {

            return _comparer.GetHashCode(Item);

        }
    }


}