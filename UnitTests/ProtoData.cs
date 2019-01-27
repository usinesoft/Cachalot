#region

using System.Runtime.Serialization;

#endregion

namespace UnitTests
{
    [DataContract]
    public class ProtoData
    {
        public ProtoData(long id, string firstName, string lastName)
        {
            Id = id;
            FirstName = firstName;
            LastName = lastName;
        }

        public ProtoData()
        {
        }

        [DataMember(Order = 1)] public long Id { get; set; }

        [DataMember(Order = 2)] public string FirstName { get; set; }

        [DataMember(Order = 3)] public string LastName { get; set; }
    }
}