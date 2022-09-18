using Client.Core;
using Client.Interface;
using System;

namespace Tests.TestData
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

        [ServerSideValue(IndexType.Primary)]
        public int PrimaryKey
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

        [FullTextIndexation]
        [ServerSideValue(IndexType.Dictionary)]
        public string IndexKeyFolder
        {
            get => _indexKeyFolder;
            set => _indexKeyFolder = value;
        }

        [ServerSideValue(IndexType.Ordered)]
        public DateTime IndexKeyDate
        {
            get => _indexKeyDate;
            set => _indexKeyDate = value;
        }


        [FullTextIndexation] public string Comment { get; set; }


        public string ObjectData
        {
            get => _objectData;
            set => _objectData = value;
        }

        [ServerSideValue(IndexType.Ordered)]
        public int IndexKeyValue
        {
            get => _indexKeyValue;
            set => _indexKeyValue = value;
        }

        [ServerSideValue(IndexType.Ordered)] public DateTime? NullableDate { get; set; }


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