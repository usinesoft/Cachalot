using System;
using System.Collections.Generic;
using Client.Core;
using Client.Interface;

namespace Tests.TestData
{
    [Serializable]
    public class TradeLike
    {
        private List<KeyValuePair<DateTime, DateTime>> _ech;
        private DateTime _indexKeyDate;

        private string _indexKeyFolder;
        private int _indexKeyValue;
        private int _primaryKey;
        private int _uniqueKey;


        public TradeLike()
        {
            InitEch();
        }

        public TradeLike(int primaryKey, int uniqueKey, string indexKeyFolder, DateTime indexKeyDate, int indexKeyValue)
        {
            InitEch();

            _primaryKey = primaryKey;
            _uniqueKey = uniqueKey;
            _indexKeyFolder = indexKeyFolder;
            _indexKeyDate = indexKeyDate;
            _indexKeyValue = indexKeyValue;
        }

        [ServerSideValue(IndexType.Primary)]
        public int Key
        {
            get => _primaryKey;
            set => _primaryKey = value;
        }

        [ServerSideValue(IndexType.Dictionary)]
        public int UniqueKey
        {
            get => _uniqueKey;
            set => _uniqueKey = value;
        }

        [ServerSideValue(IndexType.Dictionary)]
        public string Folder
        {
            get => _indexKeyFolder;
            set => _indexKeyFolder = value;
        }

        [ServerSideValue(IndexType.Ordered)]
        public DateTime ValueDate
        {
            get => _indexKeyDate;
            set => _indexKeyDate = value;
        }


        [ServerSideValue(IndexType.Ordered)]
        public int Nominal
        {
            get => _indexKeyValue;
            set => _indexKeyValue = value;
        }


        private void InitEch()
        {
            _ech = new List<KeyValuePair<DateTime, DateTime>>();
            for (var i = 0; i < 10; i++)
                _ech.Add(new KeyValuePair<DateTime, DateTime>(new DateTime(2010, 9, 15), new DateTime(2010, 12, 15)));
        }
    }
}