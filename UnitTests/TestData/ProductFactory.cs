using System;
using Cachalot.Linq;
using Tests.TestData.Events;
using Tests.TestData.Instruments;

namespace Tests.TestData
{
    public class ProductFactory
    {
        private readonly Connector _connector;

        public ProductFactory(Connector connector)
        {
            _connector = connector;
        }

        public (Instruments.Trade trade, Event creationEvent) CreateOption(int quantity, int unitPrice,
                                                                           string counterparty, string portfolio,
                                                                           string underlying, decimal strike,
                                                                           bool isPut, bool cashSettlement,
                                                                           bool isAmerican, int monthsToMaturity)
        {
            var tid = _connector.GenerateUniqueIds("trade_id", 1);

            var cid = _connector.GenerateUniqueIds("contract_id", 1);

            var eid = _connector.GenerateUniqueIds("event_id", 1);


            var trade = new Instruments.Trade
            {
                ContractId = "EQD-" + cid[0],
                Id = tid[0],
                ValueDate = DateTime.Today.AddDays(1),
                TradeDate = DateTime.Today,
                Timestamp = DateTime.Now,
                Counterparty = counterparty,
                IsDestroyed = false,
                Portfolio = portfolio,
                Version = 1,
                IsLastVersion = true
            };

            var product = new EquityOption
            {
                Exercise = isAmerican ? EquityOption.ExerciseType.American : EquityOption.ExerciseType.European,
                Type = isPut ? EquityOption.OptionType.Put : EquityOption.OptionType.Call,
                Settlement = cashSettlement ? EquityOption.SettlementType.Cash : EquityOption.SettlementType.Physical,
                MaturityDate = DateTime.Today.AddMonths(monthsToMaturity),
                UnitPrice = unitPrice,
                Quantity = quantity,
                Underlying = underlying,
                Strike = strike
            };

            trade.Product = product;

            var evt = new Create(eid[0], trade.ContractId);

            return (trade, evt);
        }


        public (Instruments.Trade trade, Event increaseEvent) IncreaseOption(Instruments.Trade trade,
                                                                             decimal deltaQuantity)
        {
            var newVersion = trade.Clone();

            var tid = _connector.GenerateUniqueIds("trade_id", 1);
            newVersion.Version++;
            newVersion.Id = tid[0];

            newVersion.Timestamp = DateTime.Now;
            var option = (EquityOption)newVersion.Product;
            option.Quantity += deltaQuantity;

            trade.IsLastVersion = false;

            var eid = _connector.GenerateUniqueIds("event_id", 1);


            var evt = new Increase(eid[0], deltaQuantity, trade.ContractId);

            return (newVersion, evt);
        }
    }
}