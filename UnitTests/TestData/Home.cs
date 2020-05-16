using System;
using System.Collections.Generic;
using Client.Interface;

namespace UnitTests.TestData
{
    public class Home
    {
        [PrimaryKey(KeyDataType.IntKey)] public int Id { get; set; }

        [FullTextIndexation]
        [Index(KeyDataType.StringKey)]
        public string CountryCode { get; set; }

        [FullTextIndexation]
        [Index(KeyDataType.StringKey)]
        public string Town { get; set; }

        [FullTextIndexation] public string Address { get; set; }
        [FullTextIndexation] public IList<string> Contacts { get; set; } = new List<string>();

        public string Owner { get; set; }

        public string OwnerEmail { get; set; }

        public string OwnerPhone { get; set; }

        [Index(KeyDataType.IntKey, true)] public int Rooms { get; set; }

        [Index(KeyDataType.IntKey)] public int Bathrooms { get; set; }

        [Index(KeyDataType.IntKey, true)] public decimal PriceInEuros { get; set; }

        [Index(KeyDataType.IntKey)] public List<DateTime> AvailableDates { get; set; } = new List<DateTime>();


        [FullTextIndexation] public List<Comment> Comments { get; set; } = new List<Comment>();
    }
}