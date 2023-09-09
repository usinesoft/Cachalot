using Client.Core;
using Client.Interface;

namespace Tests.TestData
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

        [ServerSideValue(IndexType.Primary)] public int Id { get; set; }

        [ServerSideValue(IndexType.Dictionary)]
        public string First { get; set; }

        [ServerSideValue(IndexType.Dictionary)]
        public string Last { get; set; }
    }
}