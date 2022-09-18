using Client.Core;
using Client.Interface;
using System;

namespace Tests.TestData.Instruments
{
    public class Trade
    {
        [ServerSideValue(IndexType.Primary)] public int Id { get; set; }

        [ServerSideValue(IndexType.Dictionary)] public int Version { get; set; }

        [ServerSideValue(IndexType.Dictionary)] public string ContractId { get; set; }

        [ServerSideValue(IndexType.Dictionary)] public bool IsDestroyed { get; set; }

        [ServerSideValue(IndexType.Dictionary)] public bool IsLastVersion { get; set; }

        [ServerSideValue(IndexType.Ordered)] public DateTime TradeDate { get; set; }

        [ServerSideValue(IndexType.Ordered)] public DateTime ValueDate { get; set; }

        [ServerSideValue(IndexType.Ordered)] public DateTime MaturityDate { get; set; }

        [ServerSideValue(IndexType.Ordered)] public DateTime Timestamp { get; set; }

        [ServerSideValue(IndexType.Dictionary)] public string Portfolio { get; set; }

        [ServerSideValue(IndexType.Dictionary)] public string Counterparty { get; set; }

        public IProduct Product { get; set; }
    }
}