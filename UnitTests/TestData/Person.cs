using Client.Interface;

namespace UnitTests.TestData
{
    
    public class Person
    {
        public Person(int id, string first, string last)
        {
            Id = id;
            First = first;
            Last = last;
        }

        public Person()
        {
        }

        [PrimaryKey(KeyDataType.IntKey)]
        public int Id { get; set; }

        [Index(KeyDataType.StringKey)]
        public string First { get; set; }

        [Index(KeyDataType.StringKey)]
        public string Last { get; set; }
    }
}