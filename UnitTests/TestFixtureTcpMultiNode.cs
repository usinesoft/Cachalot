#region

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Channel;
using Client.Interface;
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
            _server1 = new Server.Server(new NodeConfig()) { Channel = _serverChannel1 };
            _serverPort1 = _serverChannel1.Init();
            _serverChannel1.Start();
            _server1.Start();

            _serverChannel2 = new TcpServerChannel();
            _server2 = new Server.Server(new NodeConfig()) { Channel = _serverChannel2 };
            _serverPort2 = _serverChannel2.Init();
            _serverChannel2.Start();
            _server2.Start();


            Thread.Sleep(500); //be sure the server nodes are started

            _client1 = new DataClient
            {
                Channel = new TcpClientChannel(new TcpClientPool(4, 1, "localhost", _serverPort1)),
                ShardIndex = 0,
                ShardsCount = 2
            };

            _client2 = new DataClient
            {
                Channel = new TcpClientChannel(new TcpClientPool(4, 1, "localhost", _serverPort2)),
                ShardIndex = 1,
                ShardsCount = 2
            };

            _aggregator = new DataAggregator { CacheClients = { _client1, _client2 } };


            _aggregator.DeclareCollection<CacheableTypeOk>();
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

        private DataClient _client1;
        private DataClient _client2;
        private DataAggregator _aggregator;


        [Test]
        public void FeedDataToCache()
        {
            var items = new List<CacheableTypeOk>();
            for (var i = 0; i < 113; i++)
            {
                var item1 = new CacheableTypeOk(i, 1000 + i, "aaa", new DateTime(2010, 10, 10), 1500);
                items.Add(item1);
            }

            _aggregator.PutMany(items, true);

            var itemsReloaded = _aggregator.GetMany<CacheableTypeOk>(x => x.IndexKeyFolder == "aaa").ToList();
            
            Assert.AreEqual(113, itemsReloaded.Count);

            Assert.Throws<CacheException>(() =>
            {
                int removed = _aggregator.RemoveMany<CacheableTypeOk>(i => i.UniqueKey >= 1000 && i.UniqueKey < 1010);

            }, "can not apply comparison operator on unique keys");
            
            
        }

    }
}