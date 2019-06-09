using System;

namespace UnitTests.TestData.Instruments
{
    public interface IProduct
    {
        string Name { get; }

        AssetClass AssetClass { get; }

        /// <summary>
        ///     Not all assets have maturity date
        /// </summary>
        DateTime? MaturityDate { get; }

        decimal Quantity { get; }

        decimal UnitPrice { get; }

        decimal Notional { get; }
    }
}