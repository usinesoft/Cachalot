using AdminConsole.Commands;
using Channel;
using Client.Core;
using Client.Interface;
using Client.Messages;
using NUnit.Framework;
using UnitTests.TestData;
using ServerConfig = Server.ServerConfig;

namespace UnitTests
{
    [TestFixture]
    public class TestFixtureCommandLine
    {
        /// <summary>
        /// fake server description used for tests
        /// </summary>
        private ClusterInformation _serverDescription;

        [OneTimeSetUp]
        public void FixtureSetUp()
        {

            ClientSideTypeDescription clientTypeDescription = ClientSideTypeDescription.RegisterType(typeof(TradeLike));

            var sd = new ServerDescriptionResponse {KnownTypesByFullName = {{clientTypeDescription.FullTypeName, clientTypeDescription.AsTypeDescription } }};
            
            _serverDescription = new ClusterInformation(new[]{sd});
        }

        [Test]
        public void ExecuteCommands()
        {
            var client = new CacheClient();
            InProcessChannel channel = new InProcessChannel();
            client.Channel = channel;
            Server.Server server = new Server.Server(new ServerConfig()) {Channel = channel};
            server.Start();


            client.RegisterTypeIfNeeded(typeof(TradeLike));


            var serverDesc = client.GetClusterInformation();
            Assert.AreEqual(serverDesc.Schema.Length, 1);
            CommandLineParser parser = new CommandLineParser(serverDesc);

            CommandBase cmd = parser.Parse("desc");
             cmd.TryExecute(client);
            

            cmd = parser.Parse("desc TRADELIKE");
            cmd.TryExecute(client);
            

            cmd = parser.Parse("count from TRADELIKE where folder=aaa ");
            cmd.TryExecute(client);
           

           
        }

        [Test]
        public void ParseAndCount()
        {
            
            CommandLineParser parser = new CommandLineParser(_serverDescription);
            
            var cmd = parser.Parse("count from TRADELIKE where folder=aaa ");
            Assert.IsTrue(cmd.Success);
            Assert.AreEqual(cmd.CmdType, CommandType.Count);
            Assert.AreEqual(cmd.Params.Count, 2);
            Assert.AreEqual(cmd.Params[0], "TRADELIKE");
            Assert.IsNotNull(cmd.Query);
        }

        [Test]
        public void ParseSimpleCommands()
        {
            CommandLineParser parser = new CommandLineParser(_serverDescription);

            //this one should fail
            CommandBase cmd = parser.Parse("unknown lkjljk kjlkj ");
            Assert.IsFalse(cmd.Success);
            Assert.AreEqual(cmd.CmdType, CommandType.Unknown);

            //////////////////////////////////////////////////////
            // DUMP: valid syntax DUMP or DUMP file_name

            cmd = parser.Parse("DUMP c:/temp");
            Assert.IsTrue(cmd.Success);
            Assert.AreEqual(cmd.CmdType, CommandType.Dump);
            Assert.AreEqual(cmd.Params.Count, 1);
            Assert.AreEqual(cmd.Params[0], "c:/temp");

         
            ///////////////////////////////////////////////////////////
            // LAST / LAST n
            cmd = parser.Parse("last ");
            Assert.IsTrue(cmd.Success);
            Assert.AreEqual(cmd.CmdType, CommandType.Log);
            Assert.AreEqual(cmd.Params.Count, 1);
            Assert.AreEqual(cmd.Params[0], "1"); //last <==> last 1

            cmd = parser.Parse("last 15");
            Assert.IsTrue(cmd.Success);
            Assert.AreEqual(cmd.CmdType, CommandType.Log);
            Assert.AreEqual(cmd.Params.Count, 1);
            Assert.AreEqual(cmd.Params[0], "15");

            ///////////////////////////////////////////////////////////
            // DESC / DESC table
            cmd = parser.Parse("desc");
            Assert.IsTrue(cmd.Success);
            Assert.AreEqual(cmd.CmdType, CommandType.Desc);
            Assert.AreEqual(cmd.Params.Count, 0);


            cmd = parser.Parse("desc TABLE");
            Assert.IsTrue(cmd.Success);
            Assert.AreEqual(cmd.CmdType, CommandType.Desc);
            Assert.AreEqual(cmd.Params.Count, 1);
            Assert.AreEqual(cmd.Params[0], "TABLE");
        }
    }
}