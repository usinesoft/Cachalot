using System;

namespace UnitTests.TestData
{
    [Serializable]
    public class Quote
    {
        private float _ask;
        private float _bid;
        private DateTime _date;
        private float _mid;
        private string _name;

        private QuoteType _quoteType;
        private string _refSet;

        public string Name
        {
            get { return _name; }
            set { _name = value; }
        }

        public DateTime Date
        {
            get { return _date; }
            set { _date = value; }
        }

        public string RefSet
        {
            get { return _refSet; }
            set { _refSet = value; }
        }

        public float Ask
        {
            get { return _ask; }
            set { _ask = value; }
        }

        public float Bid
        {
            get { return _bid; }
            set { _bid = value; }
        }

        public float Mid
        {
            get { return _mid; }
            set { _mid = value; }
        }

        public QuoteType QuoteType
        {
            get { return _quoteType; }
            set { _quoteType = value; }
        }
    }
}