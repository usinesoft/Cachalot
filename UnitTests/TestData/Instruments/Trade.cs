using System;
using Client.Interface;

namespace UnitTests.TestData.Instruments
{
    public class Trade
    {
        [PrimaryKey(KeyDataType.IntKey)] public int Id { get; set; }

        [Index(KeyDataType.IntKey)] public int Version { get; set; }

        [Index(KeyDataType.StringKey)] public string ContractId { get; set; }

        [Index(KeyDataType.IntKey)] public bool IsDestroyed { get; set; }

        [Index(KeyDataType.IntKey)] public bool IsLastVersion { get; set; }

        [Index(KeyDataType.IntKey, true)] public DateTime TradeDate { get; set; }

        [Index(KeyDataType.IntKey, true)] public DateTime ValueDate { get; set; }

        [Index(KeyDataType.IntKey, true)] public DateTime MaturityDate { get; set; }

        [Index(KeyDataType.IntKey, true)] public DateTime Timestamp { get; set; }

        [Index(KeyDataType.StringKey)] public string Portfolio { get; set; }

        [Index(KeyDataType.StringKey)] public string Counterparty { get; set; }

        public IProduct Product { get; set; }
    }
}