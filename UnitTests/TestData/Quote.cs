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
            get => _name;
            set => _name = value;
        }

        public DateTime Date
        {
            get => _date;
            set => _date = value;
        }

        public string RefSet
        {
            get => _refSet;
            set => _refSet = value;
        }

        public float Ask
        {
            get => _ask;
            set => _ask = value;
        }

        public float Bid
        {
            get => _bid;
            set => _bid = value;
        }

        public float Mid
        {
            get => _mid;
            set => _mid = value;
        }

        public QuoteType QuoteType
        {
            get => _quoteType;
            set => _quoteType = value;
        }
    }
}