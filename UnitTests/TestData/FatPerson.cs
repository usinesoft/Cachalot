using System;
using Client.Interface;

namespace UnitTests.TestData
{
    /// <summary>
    ///     This persons data needs to be compressed inside the cache
    /// </summary>    
    [Serializable]
    public class FatPerson
    {
        private string _first;
        private int _id;

        private string _last;

        public FatPerson(int id, string first, string last)
        {
            _id = id;
            _first = first;
            _last = last;
        }

        public FatPerson()
        {
        }

        [PrimaryKey(KeyDataType.IntKey)]
        public int Id
        {
            get { return _id; }
            set { _id = value; }
        }

        [Index(KeyDataType.StringKey)]
        public string First
        {
            get { return _first; }
            set { _first = value; }
        }

        [Index(KeyDataType.StringKey)]
        public string Last
        {
            get { return _last; }
            set { _last = value; }
        }
    }
}