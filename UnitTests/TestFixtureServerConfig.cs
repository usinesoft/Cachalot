//using System;
//using System.IO;
//using Client.Interface;
//using NUnit.Framework;
//using ServerConfig = Server.ServerConfig;

//namespace UnitTests
//{
//    [TestFixture]
//    public class TestFixtureServerConfig
//    {
//        [OneTimeSetUp]
//        public void RunBeforeAnyTests()
//        {
//            Environment.CurrentDirectory = TestContext.CurrentContext.TestDirectory;
//            Directory.SetCurrentDirectory(TestContext.CurrentContext.TestDirectory);
//        }

//        [Test]
//        public void LoadConfig()
//        {
//            ServerConfig config = ServerConfig.LoadFromFile("ServerConfig.xml");
//            Assert.IsNotNull(config);
//            Assert.AreEqual(config.TcpPort, 4848);
//            Assert.AreEqual(config.ConfigByType.Count, 1);
//            Assert.AreEqual(config.ConfigByType["CacheTest.TestData.Tradelike"].Threads, 6);
//            Assert.AreEqual(config.ConfigByType["CacheTest.TestData.Tradelike"].Eviction.Type, EvictionType.LessRecentlyUsed);
//            Assert.AreEqual(config.ConfigByType["CacheTest.TestData.Tradelike"].Eviction.LruEvictionCount, 1000);
//            Assert.AreEqual(config.ConfigByType["CacheTest.TestData.Tradelike"].Eviction.LruMaxItems, 10000);
//        }
//    }
//}