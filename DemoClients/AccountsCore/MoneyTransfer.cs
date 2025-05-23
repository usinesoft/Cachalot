﻿using System;
using Client.Core;
using Client.Interface;

namespace Accounts
{
    public class MoneyTransfer
    {

        [ServerSideValue(IndexType.Primary)]
        public int Id { get; set; }

        [ServerSideValue(IndexType.Ordered)]
        public decimal Amount { get; set; }

        [ServerSideValue(IndexType.Dictionary)]
        public DateTime Date { get; set; }

        [ServerSideValue(IndexType.Dictionary)]
        public int SourceAccount { get; set; }


        [ServerSideValue(IndexType.Dictionary)]
        public int DestinationAccount { get; set; }

    }
}