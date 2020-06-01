#region

using System;
using System.IO;
using System.Linq;
using System.Threading;
using Channel;
using Client.Core;
using Client.Interface;
using Client.Queries;
using NUnit.Framework;
using Server;
using UnitTests.TestData;

#endregion

namespace UnitTests
{
    [TestFixture]
    public class TestFixtureClientConfig
    {
        [SetUp]
        public void Init()
        {
            _serverChannel = new TcpServerChannel();

            _server = new Server.Server(new NodeConfig()) {Channel = _serverChannel};
            _serverPort = _serverChannel.Init();
            _serverChannel.Start();
            _server.Start();
            Thread.Sleep(500); //be sure the server is started            
        }

        [TearDown]
        public void Exit()
        {
            _serverChannel.Stop();
            _server.Stop();
        }

        [OneTimeSetUp]
        public void RunBeforeAnyTests()
        {
            Environment.CurrentDirectory = TestContext.CurrentContext.TestDirectory;
            Directory.SetCurrentDirectory(TestContext.CurrentContext.TestDirectory);
        }


        private TcpServerChannel _serverChannel;
        private Server.Server _server;


        private int _serverPort;

        [Test]
        public void DataAccess()
        {
            using (var client = ClientFactory.InitSingleNode("CacheClientConfig.xml", "localhost", _serverPort))
            {
                var clientImplementation = (
                    CacheClient) client;
                var serverDescription = clientImplementation.GetServerDescription();
                Assert.IsNotNull(serverDescription);

                var tradeDescription = clientImplementation.KnownTypes["UnitTests.TestData.Trade"];
                var quoteDescription = clientImplementation.KnownTypes["UnitTests.TestData.Quote"];


                Assert.AreEqual(serverDescription.KnownTypesByFullName.Count, 2);
                Assert.AreEqual(tradeDescription.AsTypeDescription.IndexFields.Count, 3);
                Assert.AreEqual(tradeDescription.AsTypeDescription.ListFields.Count, 2);
                Assert.AreEqual(quoteDescription.AsTypeDescription.IndexFields.Count, 3);


                ////////////////////////////////////////////:
                // test trades

                var trade1 = new Trade(1, 1001, "XXX", new DateTime(2009, 1, 15), (float) 10000.25);
                var trade2 = new Trade(2, 1002, "XXX", new DateTime(2009, 1, 15), (float) 20000.25);

                client.Put(trade1);
                client.Put(trade2);


                //build a query the "hard" way

                var tradeQueryBuilder = new QueryBuilder(tradeDescription.AsTypeDescription);
                var q1 = tradeQueryBuilder.MakeAtomicQuery("Nominal", QueryOperator.Gt, 1000F);
                var q2 = tradeQueryBuilder.MakeAtomicQuery("Nominal", QueryOperator.Le, 20000.25F);
                var q12 = tradeQueryBuilder.MakeAndQuery();
                q12.Elements.Add(q1);
                q12.Elements.Add(q2);
                var q = tradeQueryBuilder.MakeOrQuery(q12);

                Assert.IsTrue(q.IsValid);
                Assert.IsTrue(q.Match(CachedObject.Pack(trade1, tradeDescription)));
                Assert.IsTrue(q.Match(CachedObject.Pack(trade2, tradeDescription)));


                var trades = client.GetMany<Trade>(q).ToList();
                Assert.IsNotNull(trades);
                Assert.AreEqual(trades.Count, 2);

                //////////////////////////////////////////////////////
                // test quotes


                //put a quote with some null index values
                //RefSet is null
                var quote1 = new Quote {Name = "aaa", Mid = 2.2F, Ask = 2.1F, Bid = 2.3F};


                client.Put(quote1);

                var quote1Reloaded = client.GetOne<Quote>("aaa");
                Assert.AreEqual(quote1Reloaded.QuoteType, QuoteType.INVALID);


                //get by null index value
                //need to create the query the "hard way" ( cause null can not be specified in a query string)
                var quoteQueryBuilder = new QueryBuilder(quoteDescription.AsTypeDescription);
                q = quoteQueryBuilder.MakeOrQuery(quoteQueryBuilder.MakeAtomicQuery("RefSet", null));
                var quotes = client.GetMany<Quote>(q).ToList();
                Assert.AreEqual(quotes.Count, 1);
                Assert.AreEqual(quotes[0].Name, "aaa");
            }
        }


        [Test]
        public void DataAccessWithSimplifiedInterface()
        {
            using (var client = ClientFactory.InitSingleNode("CacheClientConfig.xml", "localhost", _serverPort))
            {
                var clientImplementation = (CacheClient) client;

                var tradeDescription = clientImplementation.KnownTypes["UnitTests.TestData.Trade"];


                Assert.IsFalse(client.IsDataFullyLoaded<Trade>());

                ////////////////////////////////////////////:
                // test trades

                var trade1 = new Trade(1, 1001, "XXX", new DateTime(2009, 1, 15), (float) 10000.25);
                var trade2 = new Trade(2, 1002, "XXX", new DateTime(2009, 1, 15), (float) 20000.25);
                var trade3 = new Trade(3, 1003, "YYY", new DateTime(2009, 1, 15), (float) 20000.25);

                client.Put(trade1);
                client.Put(trade2);
                client.Put(trade3);

                Assert.IsFalse(client.IsDataFullyLoaded<Trade>());

                var qb = new QueryBuilder(tradeDescription.AsTypeDescription);


                var tradesFromXxx = client.GetMany<Trade>(qb.GetManyWhere("Folder=XXX")).ToList();
                Assert.IsNotNull(tradesFromXxx);
                Assert.AreEqual(tradesFromXxx.Count, 2);


                var q = qb.GetMany("Folder=XXX");
                q.OnlyIfComplete = true;

                Assert.Throws<CacheException>(() =>
                    client.GetMany<Trade>(q).ToList());


                client.DeclareDataFullyLoaded<Trade>(true);
                Assert.IsTrue(client.IsDataFullyLoaded<Trade>());

                // now it should work as data was declared "fully loaded"
                //tradesFromXxx = client.GetMany<Trade>(t => t.Folder == "XXX", onlyIfFullyLoaded: true).ToList();
                //Assert.IsNotNull(tradesFromXxx);
                //Assert.AreEqual(tradesFromXxx.Count, 2);

                // remove should not affect data completeness (completeness is relative to the underlying storage)
                client.Remove<Trade>(1);
                Assert.IsTrue(client.IsDataFullyLoaded<Trade>());

                // check that truncate resets the domain description
                client.Truncate<Trade>();
                Assert.IsFalse(client.IsDataFullyLoaded<Trade>());
            }
        }

        [Test]
        public void LoadClientConfigFromFile()
        {
            var cfg = new ClientConfig();
            cfg.LoadFromFile("CacheClientConfig.xml");


            Assert.AreEqual(cfg.TypeDescriptions.Count, 2);
            var desc1 = cfg.TypeDescriptions["UnitTests.TestData.Trade"];
            Assert.IsNotNull(desc1);
            Assert.AreEqual(desc1.Keys.Count, 7);
            var property5 = desc1.Keys["Nominal"];
            Assert.AreEqual(property5.Ordered, true);
            Assert.AreEqual(property5.KeyType, KeyType.ScalarIndex);
            Assert.AreEqual(property5.KeyDataType, KeyDataType.IntKey);
            Assert.AreEqual(property5.Ordered, true);


            Assert.AreEqual(cfg.Servers.Count, 3);
            Assert.AreEqual(cfg.Servers[0].Port, 4567);
            Assert.AreEqual(cfg.Servers[0].Host, "localhost");

            var desc2 = cfg.TypeDescriptions["UnitTests.TestData.Quote"];
            Assert.AreEqual(1, desc2.Keys.Values.Count(p => p.FullTextIndexed));
        }

        [Test]
        public void FromConnectionString()
        {
            var cfg = new ClientConfig("localhost:123");

            Assert.AreEqual(1, cfg.Servers.Count);

            Assert.AreEqual(123, cfg.Servers[0].Port);
            Assert.AreEqual("localhost", cfg.Servers[0].Host);

            cfg = new ClientConfig("host1:123 + host2:456");

            Assert.AreEqual(2, cfg.Servers.Count);

            Assert.AreEqual(123, cfg.Servers[0].Port);
            Assert.AreEqual("host2", cfg.Servers[1].Host);

            cfg = new ClientConfig("host1:123 + host2:456; 10, 4");

            Assert.AreEqual(2, cfg.Servers.Count);

            Assert.AreEqual(123, cfg.Servers[0].Port);
            Assert.AreEqual("host2", cfg.Servers[1].Host);

            Assert.AreEqual(10, cfg.ConnectionPoolCapacity);
            Assert.AreEqual(4, cfg.PreloadedConnections);
        }
    }
}