using System;
using System.Collections.Generic;
using System.Linq;
using Cachalot.Linq;
using Client.Interface;
using NUnit.Framework;
using UnitTests.TestData;
using UnitTests.TestData.Events;

namespace UnitTests
{
    [TestFixture]
    [Category("Performance")]
    public class TestFixturePersistenceWithExternalServer
    {
        readonly ClientConfig _config = new ClientConfig
        {
            IsPersistent = true,
            Servers =
            {
                new ServerConfig
                {
                    Host = "localhost",
                    Port = 6666
                }
            }
        };





        [Test]
        public void Both_IPV6_and_IPV4_addresses_are_accepted()
        {


            // localhost
            var config = new ClientConfig
            {
                IsPersistent = true,
                Servers =
                {
                    new ServerConfig
                    {
                        Host = "localhost",
                        Port = 6666
                    }
                }
            };


            using (var connector = new Connector(config))
            {
                connector.GenerateUniqueIds("event", 1);
            }


            // IPV4
            config = new ClientConfig
            {
                IsPersistent = true,
                Servers =
                {
                    new ServerConfig
                    {
                        Host = "127.0.0.1",
                        Port = 6666
                    }
                }
            };


            using (var connector = new Connector(config))
            {
                connector.GenerateUniqueIds("event", 1);
            }


            // IPV6
            config = new ClientConfig
            {
                IsPersistent = true,
                Servers =
                {
                    new ServerConfig
                    {
                        Host = "::1",
                        Port = 6666
                    }
                }
            };


            using (var connector = new Connector(config))
            {
                connector.GenerateUniqueIds("event", 1);
            }
        }


        [Test]

        public void Take_and_skip_extension_methods()
        {

            const int items = 10000;
            using (var connector = new Connector(_config))
            {
                connector.AdminInterface().DropDatabase();

                var dataSource = connector.DataSource<ProductEvent>();

                var ids = connector.GenerateUniqueIds("event", items);

                var eventDate = DateTime.Today.AddYears(-10);
                var events = new List<ProductEvent>();
                for (var i = 0; i < items; i++)
                {
                    switch (i % 3)
                    {
                        case 0:
                            events.Add(new FixingEvent(ids[i], "AXA", 150, "EQ-256") { EventDate = eventDate, ValueDate = eventDate.AddDays(2) });
                            break;
                        case 1:
                            events.Add(new FixingEvent(ids[i], "TOTAL", 180, "IRD-400") { EventDate = eventDate, ValueDate = eventDate.AddDays(2) });
                            break;
                        case 2:
                            events.Add(new Increase(ids[i], 180, "EQ-256") { EventDate = eventDate, ValueDate = eventDate.AddDays(2) });
                            break;
                    }

                    eventDate = eventDate.AddDays(1);
                }

                dataSource.PutMany(events);


                var list = dataSource.Where(e => e.EventType == "FIXING").Take(10).ToList();
                Assert.AreEqual(10, list.Count);

            }
        }


        [Test]
        public void Create_trades_and_apply_events()
        {
            using (var connector = new Connector(_config))
            {
                connector.AdminInterface().DropDatabase();

                var events = connector.DataSource<ProductEvent>();
                var trades = connector.DataSource<TestData.Instruments.Trade>();

                var factory = new ProductFactory(connector);

                (var trade, var evt ) =
                    factory.CreateOption(10, 100, "GOLDMAN.LDN", "OPTEUR", "AXA", 100, false, true, false, 6);

                events.Put(evt);
                trades.Put(trade);

                var tradeReloaded = trades.Single(t => t.ContractId == trade.ContractId);
                var eventReloaded = events.Single(e => e.DealId == trade.ContractId);

                Assert.AreEqual(tradeReloaded.Id, trade.Id);
                Assert.AreEqual(eventReloaded.EventId, evt.EventId);

                // apply an increase event
                (var newVersion, var increase) =
                    factory.IncreaseOption(trade, 50);

                trades.Put(trade);
                trades.Put(newVersion);
                events.Put(increase);

                var allVersions = trades.Where(t => t.ContractId == trade.ContractId).ToList()
                    .OrderBy(t=>t.Version).ToList();

                Assert.AreEqual(2, allVersions.Count);
                Assert.AreEqual(1, allVersions[0].Version);
                Assert.AreEqual(2, allVersions[1].Version);
                Assert.IsTrue(allVersions[1].IsLastVersion);
                Assert.IsFalse(allVersions[0].IsLastVersion);


            }


        }
    }
}