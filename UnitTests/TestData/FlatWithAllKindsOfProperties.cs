using Client.Core;
using Client.Interface;
using System;

namespace Tests.TestData
{
    [Storage(Layout.Flat)]
    public class FlatWithAllKindsOfProperties
    {
        public enum Fuzzy
        {
            Yes,
            No,
            Maybe
        }

        [ServerSideValue(IndexType.Primary)] public int Id { get; set; }


        [ServerSideValue(IndexType.Dictionary)] public DateTime ValueDate { get; set; }

        [ServerSideValue(IndexType.Dictionary)] public DateTimeOffset? AnotherDate { get; set; }


        [ServerSideValue(IndexType.Dictionary)] public DateTime? LastUpdate { get; set; }

        [ServerSideValue(IndexType.Dictionary)] public double Nominal { get; set; }

        [ServerSideValue(IndexType.Dictionary)] public int Quantity { get; set; }

        [ServerSideValue(IndexType.Dictionary)] public string InstrumentName { get; set; }

        [ServerSideValue(IndexType.Dictionary)] public Fuzzy AreYouSure { get; set; }

        [ServerSideValue(IndexType.Dictionary)] public bool IsDeleted { get; set; }

        /// <summary>
        ///     Read only property that is indexed. It should de serialized to json
        /// </summary>
        [ServerSideValue(IndexType.Dictionary)]
        public Fuzzy Again => Fuzzy.Maybe;
    }
}