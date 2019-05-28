#region

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Channel;
using Client.Core;
using Client.Queries;
using NUnit.Framework;
using Server;
using UnitTests.TestData;

#endregion

namespace UnitTests
{
    [TestFixture]
    public class TestFixtureTcpMultiNode
    {
        [SetUp]
        public void Init()
        {
            _serverChannel1 = new TcpServerChannel();
            _server1 = new Server.Server(new NodeConfig()) {Channel = _serverChannel1};
            _serverPort1 = _serverChannel1.Init();
            _serverChannel1.Start();
            _server1.Start();

            _serverChannel2 = new TcpServerChannel();
            _server2 = new Server.Server(new NodeConfig()) {Channel = _serverChannel2};
            _serverPort2 = _serverChannel2.Init();
            _serverChannel2.Start();
            _server2.Start();


            Thread.Sleep(500); //be sure the server nodes are started

            _client1 = new CacheClient
            {
                Channel =
                    new TcpClientChannel(new TcpClientPool(4, 1, "localhost",
                        _serverPort1)),
                ShardIndex = 0,
                ShardsCount = 2
            };

            _client2 = new CacheClient
            {
                Channel =
                    new TcpClientChannel(new TcpClientPool(4, 1, "localhost",
                        _serverPort2)),
                ShardIndex = 1,
                ShardsCount = 2
            };

            _aggregator = new Aggregator {CacheClients = {_client1, _client2}};


            _aggregator.RegisterTypeIfNeeded(typeof(CacheableTypeOk));
        }

        [TearDown]
        public void Exit()
        {
            _serverChannel1.Stop();
            _server1.Stop();

            _serverChannel2.Stop();
            _server2.Stop();
        }

        private TcpServerChannel _serverChannel1;
        private Server.Server _server1;
        private int _serverPort1;

        private TcpServerChannel _serverChannel2;
        private Server.Server _server2;
        private int _serverPort2;

        private CacheClient _client1;
        private CacheClient _client2;
        private Aggregator _aggregator;


        [Test]
        public void FeedDataToCache()
        {
            var items = new List<CacheableTypeOk>();
            for (var i = 0; i < 113; i++)
            {
                var item1 = new CacheableTypeOk(i, 1000 + i, "aaa", new DateTime(2010, 10, 10), 1500);
                items.Add(item1);
            }


            _aggregator.FeedMany(items, true);

            //var itemsReloaded = _aggregator.GetMany<CacheableTypeOk>(x => x.IndexKeyFolder == "aaa").ToList();
            //Assert.AreEqual(113, itemsReloaded.Count);
        }


        [Test]
        public void FeedDataToCacheUsingFeedSession()
        {
            var items = new List<CacheableTypeOk>();
            for (var i = 0; i < 113; i++)
            {
                var item1 = new CacheableTypeOk(i, 1000 + i, "aaa", new DateTime(2010, 10, 10), 1500);
                items.Add(item1);
            }

            var session = _aggregator.BeginFeed<CacheableTypeOk>(10);
            foreach (var item in items) _aggregator.Add(session, item);

            _aggregator.EndFeed<CacheableTypeOk>(session);

            _aggregator.DeclareDataFullyLoaded<CacheableTypeOk>(true);

            //var itemsReloaded = _aggregator.GetMany<CacheableTypeOk>(x => x.IndexKeyFolder == "aaa", onlyIfFullyLoaded:false).ToList();
            //Assert.AreEqual(113, itemsReloaded.Count);

            var one = _aggregator.GetOne<CacheableTypeOk>(1);
            Assert.IsNotNull(one);
            Assert.AreEqual(1, one.PrimaryKey);

            var two = _aggregator.GetOne<CacheableTypeOk>(1);
            Assert.IsNotNull(two);
            Assert.AreEqual(1, two.PrimaryKey);

            var none = _aggregator.GetOne<CacheableTypeOk>(150);
            Assert.IsNull(none);

            two.IndexKeyDate = new DateTime(2011, 10, 10);
            _aggregator.Put(two);

            _aggregator.Remove<CacheableTypeOk>(5);

            // crate a complex query using IN operator and BTW operator

            // this one will use the IN as primary query
            {
                var qb = new QueryBuilder(typeof(CacheableTypeOk));
                var qIn = qb.In("IndexKeyFolder", "aaa", "bbb", "ccc");
                var qBetween = qb.MakeAtomicQuery("IndexKeyDate", new DateTime(2010, 10, 10),
                    new DateTime(2010, 11, 10));
                qIn.Elements[0].Elements.Add(qBetween);

                var elements = _aggregator.GetMany<CacheableTypeOk>(qIn).ToList();
                Assert.AreEqual(elements.Count, 111); // 113 - one removed - one outside the date range
            }

            // this one will use the BETWEEN as primary query
            {
                var qb = new QueryBuilder(typeof(CacheableTypeOk));
                var qIn = qb.In("IndexKeyFolder", "aaa", "bbb", "ccc");
                var qBetween = qb.MakeAtomicQuery("IndexKeyDate", new DateTime(2011, 10, 10),
                    new DateTime(2011, 10, 10));
                qIn.Elements[0].Elements.Add(qBetween);

                var elements = _aggregator.GetMany<CacheableTypeOk>(qIn).ToList();
                Assert.AreEqual(elements.Count, 1); // only one element whose date was updated
            }
        }
    }
}