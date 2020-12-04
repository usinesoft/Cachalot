//#region

//using System;
//using System.Collections.Generic;
//using System.IO;
//using System.Linq;
//using System.Text.RegularExpressions;
//using AdminConsole.Commands;
//using Channel;
//using Client.Core;
//using Client.Interface;
//using Client.Queries;
//using Client.Tools;
//using NUnit.Framework;
//using Server;
//using UnitTests.TestData;

//#endregion

//namespace UnitTests
//{
//    [TestFixture]
//    public class TestFixtureClientServerWithCommandLine
//    {
//        [SetUp]
//        public void Init()
//        {
//            _client = new DataClient();
//            var channel = new InProcessChannel();
//            _client.Channel = channel;
//            _server = new Server.Server(new NodeConfig()) {Channel = channel};
//            _server.Start();

//            _client.RegisterTypeIfNeeded(typeof(CacheableTypeOk));
//            _client.RegisterTypeIfNeeded(typeof(TradeLike));

//            var serverDesc = _client.GetClusterInformation();
//            _parser = new CommandLineParser(serverDesc);

//            Logger.CommandLogger = _logger;

//            if (File.Exists("export_test.json")) File.Delete("export_test.json");
//        }

//        [TearDown]
//        public void Exit()
//        {
//            _client.Dispose();
//            _server.Stop();
//        }

//        private DataClient _client;

//        private Server.Server _server;

//        private readonly StringLogger _logger = new StringLogger();
//        private CommandLineParser _parser;


//        /// <summary>
//        ///     Extract one capture from a string usin regular expressions
//        /// </summary>
//        /// <param name="expr"></param>
//        /// <param name="input"></param>
//        /// <returns></returns>
//        public static string ExtractOne(string expr, string input)
//        {
//            var regex = new Regex(expr, RegexOptions.IgnoreCase);
//            var match = regex.Match(input);
//            Assert.IsTrue(match.Success);
//            return match.Groups[1].Captures[0].Value;
//        }


//        [Test]
//        public void DataAccess()
//        {
//            //add two new items
//            var item1 = new CacheableTypeOk(1, 1001, "aaa", new DateTime(2010, 10, 10), 1500);
//            _client.Put(item1);

//            var item2 = new CacheableTypeOk(2, 1002, "aaa", new DateTime(2010, 10, 10), 1600);
//            _client.Put(item2);

//            var item3 = new CacheableTypeOk(3, 1003, "bbb", new DateTime(2010, 10, 10), 1600);
//            _client.Put(item3);

//            {
//                _logger.Reset();
//                var cmd = _parser.Parse("count CacheableTypeOk where IndexKeyFolder=aaa");
//                Assert.IsTrue(cmd.CanExecute);
//                Assert.IsNotNull(cmd.TryExecute(_client));
//                var response = _logger.Buffer;

//                var items = ExtractOne(@"\s*found\s*([0-9]*?)\s*items", response);
//                Assert.AreEqual(items, "2");
//            }

//            {
//                _logger.Reset();
//                var cmd = _parser.Parse("select CacheableTypeOk where IndexKeyValue > 1000");
//                Assert.IsTrue(cmd.CanExecute);
//                Assert.NotNull(cmd.TryExecute(_client));
//                var response = _logger.Buffer;


//                // check that the response is valid json array

//                var objects = SerializationHelper.DeserializeJson<List<CacheableTypeOk>>(response);

//                Assert.AreEqual(3, objects.Count);
//            }

//            {
//                _logger.Reset();
//                var cmd = _parser.Parse("count CacheableTypeOk where IndexKeyFolder=bbb");
//                Assert.IsTrue(cmd.CanExecute);
//                Assert.IsNotNull(cmd.TryExecute(_client));
//                var response = _logger.Buffer;

//                var items = ExtractOne(@"\s*found\s*([0-9]*?)\s*items", response);
//                Assert.AreEqual(items, "1");
//            }

//            {
//                _logger.Reset();
//                var cmd = _parser.Parse("delete CacheableTypeOk where IndexKeyFolder=bbb");
//                Assert.IsTrue(cmd.CanExecute);
//                Assert.IsNotNull(cmd.TryExecute(_client));
//                var response = _logger.Buffer;

//                var items = ExtractOne(@"\s*deleted\s*([0-9]*?)\s*item", response);
//                Assert.AreEqual(items, "1");
//            }

//            {
//                _logger.Reset();
//                var cmd = _parser.Parse("count CacheableTypeOk where IndexKeyFolder=bbb");
//                Assert.IsTrue(cmd.CanExecute);
//                Assert.IsNotNull(cmd.TryExecute(_client));
//                var response = _logger.Buffer;

//                var items = ExtractOne(@"\s*found\s*([0-9]*?)\s*items", response);
//                Assert.AreEqual(items, "0");
//            }

//            {
//                _logger.Reset();
//                var cmd = _parser.Parse("delete CacheableTypeOk where IndexKeyFolder=aaa");
//                Assert.IsTrue(cmd.CanExecute);
//                Assert.IsNotNull(cmd.TryExecute(_client));
//                var response = _logger.Buffer;

//                var items = ExtractOne(@"\s*deleted\s*([0-9]*?)\s*item", response);
//                Assert.AreEqual(items, "2");
//            }

//            {
//                var item = new CacheableTypeOk(3, 1003, "bbb", new DateTime(2010, 10, 10), 1600);
//                _client.Put(item);

//                _logger.Reset();
//                var cmd = _parser.Parse("truncate CacheableTypeOk");
//                Assert.IsTrue(cmd.CanExecute);
//                Assert.IsNotNull(cmd.TryExecute(_client));
//                var response = _logger.Buffer;
//                Assert.IsTrue(response.Contains("Deleted 1 items."));
//            }


//            GetServerInfo();
//        }


//        [Test]
//        public void FeedAndGetMany()
//        {
//            //feed lots of objects to the cache
//            var session = _client.BeginFeed<TradeLike>(50, false);

//            var random = new Random();
//            for (var i = 0; i < 10000; i++)
//            {
//                var newItem = new TradeLike(i, 1000 + i, "aaa", new DateTime(2009, 10, 10), random.Next(1000));
//                _client.Add(session, newItem);
//            }

//            _client.EndFeed<TradeLike>(session);

//            //get a subset of the fed objects
//            var builder = new QueryBuilder(typeof(TradeLike));
//            var query = builder.GetMany("Nominal < 500");


//            //sync get
//            _client.GetMany<TradeLike>(query).ToList();


//            //select 
//            _logger.Reset();
//            var cmd = _parser.Parse("select TRADELIKE where Nominal < 500");
//            Assert.IsTrue(cmd.CanExecute);
//            Assert.IsNotNull(cmd.TryExecute(_client));

//            // check for empty result
//            _logger.Reset();
//            cmd = _parser.Parse("select TRADELIKE where Nominal < 500, Folder = inexistent");
//            Assert.IsTrue(cmd.CanExecute);
//            Assert.IsNotNull(cmd.TryExecute(_client));
//        }

//        [Test]
//        public void GetServerInfo()
//        {
//            _logger.Reset();
//            var cmd = _parser.Parse("desc");
//            Assert.IsTrue(cmd.CanExecute);
//            Assert.IsNotNull(cmd.TryExecute(_client));
//            var response = _logger.Buffer;
//            Assert.IsTrue(response.ToUpper().Contains("CACHEABLETYPEOK"));
//        }


//        [Test]
//        public void Import_export_data_to_json()
//        {
//            //add some data
//            var item1 = new CacheableTypeOk(1, 1001, "aaa", new DateTime(2010, 10, 10), 1500);
//            _client.Put(item1);

//            var item2 = new CacheableTypeOk(2, 1002, "aaa", new DateTime(2010, 10, 10), 1600);
//            _client.Put(item2);

//            var item3 = new CacheableTypeOk(3, 1003, "bbb", new DateTime(2010, 10, 10), 1600);
//            _client.Put(item3);


//            var parser = new CommandLineParser(_client.GetClusterInformation());
//            var selectInto = parser.Parse("select CacheableTypeOk where IndexKeyFolder=aaa into export_test.json");

//            selectInto.TryExecute(_client);

//            FileAssert.Exists("export_test.json");

//            var exported = DumpHelper.LoadObjects("export_test.json", _client).ToList();
//            Assert.AreEqual(2, exported.Count);

//            var json = File.ReadAllText("export_test.json");
//            json = json.Replace("aaa", "abc");
//            File.WriteAllText("export_test.json", json);

//            var import = parser.Parse("import export_test.json");
//            import.TryExecute(_client);

//            _logger.Reset();
//            var cmd = _parser.Parse("count CacheableTypeOk where IndexKeyFolder=abc");
//            Assert.IsTrue(cmd.CanExecute);
//            Assert.IsNotNull(cmd.TryExecute(_client));
//            var response = _logger.Buffer;

//            var items = ExtractOne(@"\s*found\s*([0-9]*?)\s*items", response);
//            Assert.AreEqual(items, "2");
//        }
//    }
//}