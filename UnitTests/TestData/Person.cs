using System.Runtime.Serialization;
using Client.Interface;

namespace UnitTests.TestData
{
    [DataContract]    
    public class Person
    {
        [DataMember(Order = 2)] private string _first;
        [DataMember(Order = 1)] private int _id;
        [DataMember(Order = 3)] private string _last;

        public Person(int id, string first, string last)
        {
            _id = id;
            _first = first;
            _last = last;
        }

        public Person()
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