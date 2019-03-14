using System;
using Client.Interface;

namespace Accounts
{
    public class MoneyTransfer
    {

        [PrimaryKey(KeyDataType.IntKey)]
        public int Id { get; set; }

        [Index(KeyDataType.IntKey, true)]
        public decimal Amount { get; set; }

        [Index(KeyDataType.IntKey, true)]
        public DateTime Date { get; set; }

        [Index(KeyDataType.IntKey, true)]
        public int SourceAccount { get; set; }


        [Index(KeyDataType.IntKey, true)]
        public int DestinationAccount { get; set; }

    }
}