using Cachalot.Linq;
using Client.Interface;
using UnitTests.TestData.Instruments;

namespace UnitTests
{
    public class TradeProvider
    {
        private Connector _connector;


        public bool LastOneWasFromCache { get; set; }


        public void Startup(ClientConfig config)
        {
            _connector = new Connector(config);

            var trades = _connector.DataSource<Trade>();

            // remove 500 items every time the limit of 500_000 is reached
            trades.ConfigEviction(EvictionType.LessRecentlyUsed, 500_000, 500);
        }


        public Trade GetTrade(int id)
        {
            var trades = _connector.DataSource<Trade>();
            var fromCache = trades[id];

            if (fromCache != null)
            {
                LastOneWasFromCache = true;
                return fromCache;
            }

            var trade = GetTradeFromDatabase(id);
            trades.Put(trade);

            LastOneWasFromCache = false;
            return trade;
        }


        public void Shutdown()
        {
            _connector.Dispose();
        }


        private Trade GetTradeFromDatabase(int id)
        {
            return new Trade {Id = id, ContractId = $"TRD-{id}"};
        }
    }
}