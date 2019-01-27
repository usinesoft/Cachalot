using System;
using Client.Interface;

namespace UnitTests.TestData
{
    [Serializable]
    public class CacheableTypeOk : IEquatable<CacheableTypeOk>
    {
        private DateTime _indexKeyDate;

        private string _indexKeyFolder;
        private int _indexKeyValue;

        private string _objectData = "xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx";
        private int _primaryKey;
        private int _uniqueKey;


        public CacheableTypeOk()
        {
        }

        public CacheableTypeOk(int primaryKey, int uniqueKey, string indexKeyFolder, DateTime indexKeyDate,
            int indexKeyValue)
        {
            _primaryKey = primaryKey;
            _uniqueKey = uniqueKey;
            _indexKeyFolder = indexKeyFolder;
            _indexKeyDate = indexKeyDate;
            _indexKeyValue = indexKeyValue;
        }

        [PrimaryKey(KeyDataType.IntKey)]
        public int PrimaryKey
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
        public string IndexKeyFolder
        {
            get { return _indexKeyFolder; }
            set { _indexKeyFolder = value; }
        }

        [Index(KeyDataType.IntKey, true)]
        public DateTime IndexKeyDate
        {
            get { return _indexKeyDate; }
            set { _indexKeyDate = value; }
        }


        public string ObjectData
        {
            get { return _objectData; }
            set { _objectData = value; }
        }

        [Index(KeyDataType.IntKey, true)]
        public int IndexKeyValue
        {
            get { return _indexKeyValue; }
            set { _indexKeyValue = value; }
        }

        [Index(KeyDataType.IntKey, true)] public DateTime? NullableDate { get; set; }


        public bool Equals(CacheableTypeOk cacheableTypeOk)
        {
            if (cacheableTypeOk == null) return false;
            if (_primaryKey != cacheableTypeOk._primaryKey) return false;
            if (_uniqueKey != cacheableTypeOk._uniqueKey) return false;
            if (!Equals(_indexKeyFolder, cacheableTypeOk._indexKeyFolder)) return false;
            if (!Equals(_indexKeyDate, cacheableTypeOk._indexKeyDate)) return false;
            if (!Equals(_indexKeyValue, cacheableTypeOk._indexKeyValue)) return false;
            if (!Equals(_objectData, cacheableTypeOk._objectData)) return false;
            return true;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(this, obj)) return true;
            return Equals(obj as CacheableTypeOk);
        }

        public override int GetHashCode()
        {
            return _primaryKey;
        }
    }
}