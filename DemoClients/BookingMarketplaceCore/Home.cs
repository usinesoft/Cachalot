using System;
using System.Collections.Generic;
using Client.Interface;


namespace BookingMarketplace
{
    public class Home
    {
        [PrimaryKey(KeyDataType.IntKey)]
        public int Id { get; set; }

        [Index(KeyDataType.StringKey)]
        public string CountryCode { get; set; }

        [Index(KeyDataType.StringKey)]
        public string Town { get; set; }
        
        public string Adress { get; set; }

        public string Owner { get; set; }

        public string OwnerEmail { get; set; }

        public string OwnerPhone { get; set; }

        [Index(KeyDataType.IntKey, ordered:true)]
        public int Rooms { get; set; }

        [Index(KeyDataType.IntKey)]
        public int Bathrooms { get; set; }

        [Index(KeyDataType.IntKey, ordered:true)]
        public decimal PriceInEuros { get; set; }

        [Index(KeyDataType.IntKey)]
        public List<DateTime> AvailableDates { get; set; } = new List<DateTime>(); 
    }
}