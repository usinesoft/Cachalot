using System;
using System.Collections.Generic;

namespace UnitTests.TestData
{
    [Serializable]
    public class Trade : IEquatable<Trade>
    {
        private IList<int> _accounts = new List<int>();
        private int _contractId;

        private string _folder;
        private int _id;
        private float _nominal;
        private DateTime _valueDate;

        public Trade()
        {
        }

        public Trade(int id, int contractId, string folder, DateTime valueDate, float nominal)
        {
            _id = id;
            _contractId = contractId;
            _folder = folder;
            _valueDate = valueDate;
            _nominal = nominal;
        }


        //[Index(KeyDataType.IntKey)]
        public List<DateTime> FixingDates { get; set; } = new List<DateTime>();


        //[PrimaryKey(KeyDataType.IntKey)]
        public int Id
        {
            get => _id;
            set => _id = value;
        }


        //[Key(KeyDataType.IntKey)]
        public int ContractId
        {
            get => _contractId;
            set => _contractId = value;
        }


        //[Index(KeyDataType.StringKey)]
        public string Folder
        {
            get => _folder;
            set => _folder = value;
        }


        //[Index(KeyDataType.IntKey, true)]
        public DateTime ValueDate
        {
            get => _valueDate;
            set => _valueDate = value;
        }


        public float Nominal
        {
            get => _nominal;
            set => _nominal = value;
        }


        //[Index(KeyDataType.IntKey)]
        public IList<int> Accounts
        {
            get => _accounts;
            set => _accounts = value;
        }


        public bool Equals(Trade right)
        {
            if (right == null) return false;
            if (_id != right._id) return false;
            if (_contractId != right._contractId) return false;
            if (!Equals(_folder, right._folder)) return false;
            if (!Equals(_valueDate, right._valueDate)) return false;

            if (Math.Abs(_nominal - right._nominal) > float.Epsilon) return false;

            return true;
        }


        public override bool Equals(object obj)
        {
            if (!(obj is Trade))
                return false;

            if (ReferenceEquals(this, obj)) return true;

            return Equals(obj as Trade);
        }

        public override int GetHashCode()
        {
            return _id;
        }
    }
}