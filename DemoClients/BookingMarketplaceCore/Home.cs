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

        [FullTextIndexation]
        [Index(KeyDataType.StringKey)]
        public string Town { get; set; }
        
        [FullTextIndexation]
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
    
        [FullTextIndexation]
        public List<Comment> Comments { get; set; } = new List<Comment>(); 
    }


    public class Comment
    {
        public DateTime Date { get; set; }

        public string User { get; set; }

        public string Text { get; set; }


        public override string ToString()
        {
            return Text;
        }
    }
}