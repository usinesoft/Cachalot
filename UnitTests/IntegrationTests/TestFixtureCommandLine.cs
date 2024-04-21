//using AdminConsole.Commands;
//using Channel;
//using Client.Core;
//using Client.Interface;
//using Client.Messages;
//using NUnit.Framework;
//using Server;
//using UnitTests.TestData;

//namespace UnitTests
//{
//    [TestFixture]
//    public class TestFixtureCommandLine
//    {
//        /// <summary>
//        ///     fake server description used for tests
//        /// </summary>
//        private ClusterInformation _serverDescription;

//        [OneTimeSetUp]
//        public void FixtureSetUp()
//        {
//            var clientTypeDescription = TypedSchemaFactory.FromType(typeof(TradeLike));

//            var sd = new ServerDescriptionResponse
//            {
//                KnownTypesByFullName = {{clientTypeDescription.FullTypeName, clientTypeDescription.AsCollectionSchema}}
//            };

//            _serverDescription = new ClusterInformation(new[] {sd});
//        }

//        [Test]
//        public void ExecuteCommands()
//        {
//            var client = new CacheClient();
//            var channel = new InProcessChannel();
//            client.Channel = channel;
//            var server = new Server.Server(new NodeConfig()) {Channel = channel};
//            server.Start();


//            client.RegisterTypeIfNeeded(typeof(TradeLike));


//            var serverDesc = client.GetClusterInformation();
//            ClassicAssert.AreEqual(serverDesc.Schema.Length, 1);
//            var parser = new CommandLineParser(serverDesc);

//            var cmd = parser.Parse("desc");
//            cmd.TryExecute(client);


//            cmd = parser.Parse("desc TRADELIKE");
//            cmd.TryExecute(client);


//            cmd = parser.Parse("count from TRADELIKE where folder=aaa ");
//            cmd.TryExecute(client);
//        }

//        [Test]
//        public void ParseAndCount()
//        {
//            var parser = new CommandLineParser(_serverDescription);

//            var cmd = parser.Parse("count from TRADELIKE where folder=aaa ");
//            ClassicAssert.IsTrue(cmd.Success);
//            ClassicAssert.AreEqual(cmd.CmdType, CommandType.Count);
//            ClassicAssert.AreEqual(cmd.Params.Count, 2);
//            ClassicAssert.AreEqual(cmd.Params[0], "TRADELIKE");
//            ClassicAssert.IsNotNull(cmd.Query);
//        }

//        [Test]
//        public void ParseSimpleCommands()
//        {
//            var parser = new CommandLineParser(_serverDescription);

//            //this one should fail
//            var cmd = parser.Parse("unknown lkjljk kjlkj ");
//            ClassicAssert.IsFalse(cmd.Success);
//            ClassicAssert.AreEqual(cmd.CmdType, CommandType.Unknown);

//            //////////////////////////////////////////////////////
//            // DUMP: valid syntax DUMP or DUMP file_name

//            cmd = parser.Parse("DUMP c:/temp");
//            ClassicAssert.IsTrue(cmd.Success);
//            ClassicAssert.AreEqual(cmd.CmdType, CommandType.Dump);
//            ClassicAssert.AreEqual(cmd.Params.Count, 1);
//            ClassicAssert.AreEqual(cmd.Params[0], "c:/temp");


//            ///////////////////////////////////////////////////////////
//            // LAST / LAST n
//            cmd = parser.Parse("last ");
//            ClassicAssert.IsTrue(cmd.Success);
//            ClassicAssert.AreEqual(cmd.CmdType, CommandType.Log);
//            ClassicAssert.AreEqual(cmd.Params.Count, 1);
//            ClassicAssert.AreEqual(cmd.Params[0], "1"); //last <==> last 1

//            cmd = parser.Parse("last 15");
//            ClassicAssert.IsTrue(cmd.Success);
//            ClassicAssert.AreEqual(cmd.CmdType, CommandType.Log);
//            ClassicAssert.AreEqual(cmd.Params.Count, 1);
//            ClassicAssert.AreEqual(cmd.Params[0], "15");

//            ///////////////////////////////////////////////////////////
//            // DESC / DESC table
//            cmd = parser.Parse("desc");
//            ClassicAssert.IsTrue(cmd.Success);
//            ClassicAssert.AreEqual(cmd.CmdType, CommandType.Desc);
//            ClassicAssert.AreEqual(cmd.Params.Count, 0);


//            cmd = parser.Parse("desc TABLE");
//            ClassicAssert.IsTrue(cmd.Success);
//            ClassicAssert.AreEqual(cmd.CmdType, CommandType.Desc);
//            ClassicAssert.AreEqual(cmd.Params.Count, 1);
//            ClassicAssert.AreEqual(cmd.Params[0], "TABLE");
//        }
//    }
//}

