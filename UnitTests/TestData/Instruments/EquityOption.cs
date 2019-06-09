using System;

namespace UnitTests.TestData.Instruments
{
    public class EquityOption : IProduct
    {
        public enum ExerciseType
        {
            American,
            European
        }

        public enum OptionType
        {
            Call,
            Put
        }

        public enum SettlementType
        {
            Cash,
            Physical
        }

        public string Underlying { get; set; }

        public decimal Strike { get; set; }

        public OptionType Type { get; set; }

        public ExerciseType Exercise { get; set; }

        public SettlementType Settlement { get; set; }


        public string Name => "EquityOption";
        public AssetClass AssetClass => AssetClass.Equity;
        public decimal UnitPrice { get; set; }

        public DateTime? MaturityDate { get; set; }

        public decimal Quantity { get; set; }

        public decimal Notional => Quantity * UnitPrice;
    }
}