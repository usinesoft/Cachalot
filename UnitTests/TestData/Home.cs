using System;
using System.Collections.Generic;
using Client.Core;
using Client.Interface;

namespace Tests.TestData
{
    public class Home
    {
        [ServerSideValue(IndexType.Primary)] public int Id { get; set; }

        [FullTextIndexation]
        [ServerSideValue(IndexType.Dictionary)]
        public string CountryCode { get; set; }

        [FullTextIndexation]
        [ServerSideValue(IndexType.Dictionary)]
        public string Town { get; set; }

        [FullTextIndexation] public string Address { get; set; }
        [FullTextIndexation] public IList<string> Contacts { get; set; } = new List<string>();

        public string Owner { get; set; }

        public string OwnerEmail { get; set; }

        public string OwnerPhone { get; set; }

        [ServerSideValue(IndexType.Ordered)] public int Rooms { get; set; }

        [ServerSideValue(IndexType.Dictionary)]
        public int Bathrooms { get; set; }

        [ServerSideValue(IndexType.Ordered)] public decimal PriceInEuros { get; set; }

        [ServerSideValue(IndexType.Dictionary)]
        public List<DateTime> AvailableDates { get; set; } = new List<DateTime>();


        [FullTextIndexation] public List<Comment> Comments { get; set; } = new List<Comment>();
    }
}