using System;
using System.Collections.Generic;
using Client.Interface;

namespace UnitTests.TestData
{
    [Serializable]
    public class TradeLike : IEquatable<TradeLike>
    {
        private List<KeyValuePair<DateTime, DateTime>> _ech;
        private DateTime _indexKeyDate;

        private string _indexKeyFolder;
        private int _indexKeyValue;
        private int _primaryKey;
        private int _uniqueKey;


        public TradeLike()
        {
            initEch();
        }

        public TradeLike(int primaryKey, int uniqueKey, string indexKeyFolder, DateTime indexKeyDate, int indexKeyValue)
        {
            initEch();

            _primaryKey = primaryKey;
            _uniqueKey = uniqueKey;
            _indexKeyFolder = indexKeyFolder;
            _indexKeyDate = indexKeyDate;
            _indexKeyValue = indexKeyValue;
        }

        [PrimaryKey(KeyDataType.IntKey)]
        public int Key
        {
            get { return _primaryKey; }
            set { _primaryKey = value; }
        }

        [Key(KeyDataType.IntKey)]
        public int UniqueKey
        {
            get { return _uniqueKey; }
            set { _uniqueKey = value; }
        }

        [Index(KeyDataType.StringKey)]
        public string Folder
        {
            get { return _indexKeyFolder; }
            set { _indexKeyFolder = value; }
        }

        [Index(KeyDataType.IntKey, true)]
        public DateTime ValueDate
        {
            get { return _indexKeyDate; }
            set { _indexKeyDate = value; }
        }


        [Index(KeyDataType.IntKey, true)]
        public int Nominal
        {
            get { return _indexKeyValue; }
            set { _indexKeyValue = value; }
        }


        public bool Equals(TradeLike cacheableTypeOk)
        {
            if (_primaryKey != cacheableTypeOk?._primaryKey) return false;
            if (_uniqueKey != cacheableTypeOk._uniqueKey) return false;
            if (!Equals(_indexKeyFolder, cacheableTypeOk._indexKeyFolder)) return false;
            if (!Equals(_indexKeyDate, cacheableTypeOk._indexKeyDate)) return false;
            if (!Equals(_indexKeyValue, cacheableTypeOk._indexKeyValue)) return false;

            return true;
        }

        void initEch()
        {
            _ech = new List<KeyValuePair<DateTime, DateTime>>();
            for (int i = 0; i < 10; i++)
            {
                _ech.Add(new KeyValuePair<DateTime, DateTime>(new DateTime(2010, 9, 15), new DateTime(2010, 12, 15)));
            }
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(this, obj)) return true;
            return Equals(obj as TradeLike);
        }

        public override int GetHashCode()
        {
            return _primaryKey;
        }
    }
}