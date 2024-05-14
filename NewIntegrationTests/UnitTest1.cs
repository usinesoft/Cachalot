using System.Security.Cryptography.X509Certificates;
using Cachalot.Linq;
using Channel;
using DatasetGenerator.DataModel;
using Server;

namespace NewIntegrationTests
{
    public class Tests
    {
        private Server.Server? _server;

        private int _port;
        private TcpServerChannel? _channel;

        [OneTimeSetUp]
        public async Task GlobalSetup()
        {
            
            var nodeConfig = new NodeConfig { IsPersistent = false};
            _channel = new TcpServerChannel();
            _port = _channel.Init();
            _channel.Start();
            _server = new(nodeConfig)
            {
                Channel = _channel
            };

            _server.Start();

            // wait for the server to start
            await Task.Delay(1000);

            var connector = new Connector($"localhost:{_port}");

            connector.DeclareCollection<Product>();
            var products = connector.DataSource<Product>();

            var loader = new ProductLoader();

            products.PutMany(loader.Load("csv/products.csv"));


        }

        [OneTimeTearDown]
        public void GlobalTearDown()
        {
            _channel?.Stop();
            _server?.Stop();
        }

        [Test]
        public void Get_products_with_linq()
        {
            var connector = new Connector($"localhost:{_port}");

            connector.DeclareCollection<Product>();
            var products = connector.DataSource<Product>();

            var count = products.Count();

            Assert.That(count, Is.AtLeast(10_000));

            var menProducts = products.Where(x => x.Category == "men").ToList();

            var model = menProducts.Select(x=> x.Model).First();

            var byModel = products.First(x => x.Model == model);

            Assert.NotNull(byModel);

            var cheap = products.Where(x => x.Category == "kids" && x.CurrentPrice < 20).ToList();

            CollectionAssert.IsNotEmpty(cheap);

            var result =
                connector.SqlQueryAsJson(
                    "SELECT FROM Product WHERE Category = 'kids' AND CurrentPrice < 20").ToList();

            CollectionAssert.IsNotEmpty(result);

            Assert.That(cheap.Count, Is.EqualTo(result.Count), "Same number od items with SQL and linq");
        }
    }
}