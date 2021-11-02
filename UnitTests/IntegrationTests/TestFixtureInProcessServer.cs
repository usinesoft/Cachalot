using System.IO;
using System.Linq;
using Cachalot.Linq;
using Client.Interface;
using NUnit.Framework;
using Server.Persistence;
using Tests.TestData;

namespace Tests.IntegrationTests
{
    [TestFixture]
    public class TestFixtureInProcessServer
    {
        [SetUp]
        public void ResetData()
        {
            if (Directory.Exists(Constants.DataPath)) Directory.Delete(Constants.DataPath, true);
        }


        [Test]
        public void Insert_one_item()
        {
            using (var connector = new Connector(""))
            {
                connector.DeclareCollection<Person>();

                var tids = connector.GenerateUniqueIds("id", 1);

                var myself = new Person(tids[0], "Dan", "IONESCU");

                var persons = connector.DataSource<Person>();

                persons.Put(myself);

                var me = persons[tids[0]];
            }

            using (var connector = new Connector(new ClientConfig()))
            {
                connector.DeclareCollection<Person>();

                var persons = connector.DataSource<Person>();

                var reloaded = persons.First(t => t.First == "Dan");

                Assert.AreEqual("IONESCU", reloaded.Last);
            }
        }
    }
}