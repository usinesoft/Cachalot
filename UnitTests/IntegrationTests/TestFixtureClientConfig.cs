﻿//#region

//using System;
//using System.IO;
//using System.Linq;
//using System.Threading;
//using Channel;
//using Client.Core;
//using Client.Interface;
//using Client.Queries;
//using NUnit.Framework;
//using Server;
//using UnitTests.TestData;

//#endregion

//namespace UnitTests
//{
//    [TestFixture]
//    public class TestFixtureClientConfig
//    {
//        [SetUp]
//        public void Init()
//        {
//            _serverChannel = new TcpServerChannel();

//            _server = new Server.Server(new NodeConfig()) {Channel = _serverChannel};
//            _serverPort = _serverChannel.Init();
//            _serverChannel.Start();
//            _server.Start();
//            Thread.Sleep(500); //be sure the server is started            
//        }

//        [TearDown]
//        public void Exit()
//        {
//            _serverChannel.Stop();
//            _server.Stop();
//        }

//        [OneTimeSetUp]
//        public void RunBeforeAnyTests()
//        {
//            Environment.CurrentDirectory = TestContext.CurrentContext.TestDirectory;
//            Directory.SetCurrentDirectory(TestContext.CurrentContext.TestDirectory);
//        }


//        private TcpServerChannel _serverChannel;
//        private Server.Server _server;


//        private int _serverPort;

//        [Test]
//        public void DataAccess()
//        {
//            using (var client = ClientFactory.InitSingleNode("CacheClientConfig.xml", "localhost", _serverPort))
//            {
//                var serverDescription = client.GetClusterInformation();
//                ClassicAssert.IsNotNull(serverDescription);


//                ClassicAssert.AreEqual(2, serverDescription.Schema.Length);
//                ClassicAssert.AreEqual(tradeDescription.AsCollectionSchema.IndexFields.Count, 3);
//                ClassicAssert.AreEqual(tradeDescription.AsCollectionSchema.ListFields.Count, 2);
//                ClassicAssert.AreEqual(quoteDescription.AsCollectionSchema.IndexFields.Count, 3);


//                ////////////////////////////////////////////:
//                // test trades

//                var trade1 = new Trade(1, 1001, "XXX", new DateTime(2009, 1, 15), (float) 10000.25);
//                var trade2 = new Trade(2, 1002, "XXX", new DateTime(2009, 1, 15), (float) 20000.25);

//                client.Put(trade1);
//                client.Put(trade2);


//                //build a query the "hard" way

//                var tradeQueryBuilder = new QueryBuilder(tradeDescription.AsCollectionSchema);
//                var q1 = tradeQueryBuilder.MakeAtomicQuery("Nominal", QueryOperator.Gt, 1000F);
//                var q2 = tradeQueryBuilder.MakeAtomicQuery("Nominal", QueryOperator.Le, 20000.25F);
//                var q12 = tradeQueryBuilder.MakeAndQuery();
//                q12.Elements.Add(q1);
//                q12.Elements.Add(q2);
//                var q = tradeQueryBuilder.MakeOrQuery(q12);

//                ClassicAssert.IsTrue(q.IsValid);
//                ClassicAssert.IsTrue(q.Match(PackedObject.Pack(trade1, tradeDescription)));
//                ClassicAssert.IsTrue(q.Match(PackedObject.Pack(trade2, tradeDescription)));


//                var trades = client.GetMany<Trade>(q).ToList();
//                ClassicAssert.IsNotNull(trades);
//                ClassicAssert.AreEqual(trades.Count, 2);

//                //////////////////////////////////////////////////////
//                // test quotes


//                //put a quote with some null index values
//                //RefSet is null
//                var quote1 = new Quote {Name = "aaa", Mid = 2.2F, Ask = 2.1F, Bid = 2.3F};


//                client.Put(quote1);

//                var quote1Reloaded = client.GetOne<Quote>("aaa");
//                ClassicAssert.AreEqual(quote1Reloaded.QuoteType, QuoteType.INVALID);


//                //get by null index value
//                //need to create the query the "hard way" ( cause null can not be specified in a query string)
//                var quoteQueryBuilder = new QueryBuilder(quoteDescription.AsCollectionSchema);
//                q = quoteQueryBuilder.MakeOrQuery(quoteQueryBuilder.MakeAtomicQuery("RefSet", null));
//                var quotes = client.GetMany<Quote>(q).ToList();
//                ClassicAssert.AreEqual(quotes.Count, 1);
//                ClassicAssert.AreEqual(quotes[0].Name, "aaa");
//            }
//        }


//        [Test]
//        public void DataAccessWithSimplifiedInterface()
//        {
//            using (var client = ClientFactory.InitSingleNode("CacheClientConfig.xml", "localhost", _serverPort))
//            {
//                var clientImplementation = (CacheClient) client;

//                var tradeDescription = clientImplementation.KnownTypes["UnitTests.TestData.Trade"];


//                ClassicAssert.IsFalse(client.IsDataFullyLoaded<Trade>());

//                ////////////////////////////////////////////:
//                // test trades

//                var trade1 = new Trade(1, 1001, "XXX", new DateTime(2009, 1, 15), (float) 10000.25);
//                var trade2 = new Trade(2, 1002, "XXX", new DateTime(2009, 1, 15), (float) 20000.25);
//                var trade3 = new Trade(3, 1003, "YYY", new DateTime(2009, 1, 15), (float) 20000.25);

//                client.Put(trade1);
//                client.Put(trade2);
//                client.Put(trade3);

//                ClassicAssert.IsFalse(client.IsDataFullyLoaded<Trade>());

//                var qb = new QueryBuilder(tradeDescription.AsCollectionSchema);


//                var tradesFromXxx = client.GetMany<Trade>(qb.GetManyWhere("Folder=XXX")).ToList();
//                ClassicAssert.IsNotNull(tradesFromXxx);
//                ClassicAssert.AreEqual(tradesFromXxx.Count, 2);


//                var q = qb.GetMany("Folder=XXX");
//                q.OnlyIfComplete = true;

//                Assert.Throws<CacheException>(() =>
//                    client.GetMany<Trade>(q).ToList());


//                client.DeclareDataFullyLoaded<Trade>(true);
//                ClassicAssert.IsTrue(client.IsDataFullyLoaded<Trade>());

//                // now it should work as data was declared "fully loaded"
//                //tradesFromXxx = client.GetMany<Trade>(t => t.Folder == "XXX", onlyIfFullyLoaded: true).ToList();
//                //ClassicAssert.IsNotNull(tradesFromXxx);
//                //ClassicAssert.AreEqual(tradesFromXxx.Count, 2);

//                // remove should not affect data completeness (completeness is relative to the underlying storage)
//                client.Remove<Trade>(1);
//                ClassicAssert.IsTrue(client.IsDataFullyLoaded<Trade>());

//                // check that truncate resets the domain description
//                client.Truncate<Trade>();
//                ClassicAssert.IsFalse(client.IsDataFullyLoaded<Trade>());
//            }
//        }

//        [Test]
//        public void LoadClientConfigFromFile()
//        {
//            var cfg = new ClientConfig();
//            cfg.LoadFromFile("CacheClientConfig.xml");


//            ClassicAssert.AreEqual(cfg.TypeDescriptions.Count, 2);
//            var desc1 = cfg.TypeDescriptions["UnitTests.TestData.Trade"];
//            ClassicAssert.IsNotNull(desc1);
//            ClassicAssert.AreEqual(desc1.Keys.Count, 7);
//            var property5 = desc1.Keys["Nominal"];
//            ClassicAssert.AreEqual(property5.Ordered, true);
//            ClassicAssert.AreEqual(property5.KeyType, KeyType.ScalarIndex);
//            ClassicAssert.AreEqual(property5.KeyDataType, KeyDataType.IntKey);
//            ClassicAssert.AreEqual(property5.Ordered, true);


//            ClassicAssert.AreEqual(cfg.Servers.Count, 3);
//            ClassicAssert.AreEqual(cfg.Servers[0].Port, 4567);
//            ClassicAssert.AreEqual(cfg.Servers[0].Host, "localhost");

//            var desc2 = cfg.TypeDescriptions["UnitTests.TestData.Quote"];
//            ClassicAssert.AreEqual(1, desc2.Keys.Values.Count(p => p.FullTextIndexed));
//        }

//        [Test]
//        public void FromConnectionString()
//        {
//            var cfg = new ClientConfig("localhost:123");

//            ClassicAssert.AreEqual(1, cfg.Servers.Count);

//            ClassicAssert.AreEqual(123, cfg.Servers[0].Port);
//            ClassicAssert.AreEqual("localhost", cfg.Servers[0].Host);

//            cfg = new ClientConfig("host1:123 + host2:456");

//            ClassicAssert.AreEqual(2, cfg.Servers.Count);

//            ClassicAssert.AreEqual(123, cfg.Servers[0].Port);
//            ClassicAssert.AreEqual("host2", cfg.Servers[1].Host);

//            cfg = new ClientConfig("host1:123 + host2:456; 10, 4");

//            ClassicAssert.AreEqual(2, cfg.Servers.Count);

//            ClassicAssert.AreEqual(123, cfg.Servers[0].Port);
//            ClassicAssert.AreEqual("host2", cfg.Servers[1].Host);

//            ClassicAssert.AreEqual(10, cfg.ConnectionPoolCapacity);
//            ClassicAssert.AreEqual(4, cfg.PreloadedConnections);
//        }
//    }
//}

