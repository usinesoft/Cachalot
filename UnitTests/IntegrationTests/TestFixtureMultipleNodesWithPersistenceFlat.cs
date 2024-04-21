using System;
using System.Collections.Generic;
using System.Linq;
using Cachalot.Extensions;
using Cachalot.Linq;
using Client.Core;
using Client.Interface;
using NUnit.Framework;
using NUnit.Framework.Legacy;
using Tests.TestData;

namespace Tests.IntegrationTests
{
    [TestFixture]
    public class TestFixtureMultipleNodesWithPersistenceFlat : MultiServerTestFixtureBase
    {
        [TearDown]
        public void Exit()
        {
            TearDown();
        }

        [SetUp]
        public void Init()
        {
            SetUp();
        }

        [OneTimeSetUp]
        public void RunBeforeAnyTests()
        {
            ServerCount = 10;
            OneTimeSetUp();
        }


        private IEnumerable<FlatWithAllKindsOfProperties> GenerateTestData(int count)
        {
            for (var i = 0; i < count; i++)
                yield return new FlatWithAllKindsOfProperties
                {
                    Id = i + 1,
                    Nominal = i * 0.5,
                    Quantity = i % 5 + 1
                };
        }

        private IEnumerable<FlatWithAllKindsOfProperties> GenerateTestDataForPivot(int count)
        {
            for (var i = 0; i < count; i++)
                yield return new FlatWithAllKindsOfProperties
                {
                    Id = i + 1,
                    InstrumentName = i % 2 == 0 ? "a" : "b",
                    ValueDate = i % 3 == 1 ? DateTime.Today : DateTime.Today.AddDays(1),
                    Nominal = i * 0.5,
                    Quantity = i % 5 + 1
                };
        }

        [Test]
        public void Check_feed_and_reload()
        {
            using (var connector = new Connector(_clientConfig))
            {
                connector.DeclareCollection<FlatWithAllKindsOfProperties>();

                var dataSource = connector.DataSource<FlatWithAllKindsOfProperties>();


                dataSource.PutMany(GenerateTestData(100));

                var reloaded = dataSource[13];

                ClassicAssert.IsNotNull(reloaded);

                var q2 = dataSource.Where(x => x.Quantity == 2).ToList();

                ClassicAssert.AreEqual(20, q2.Count);

                var projection1 = dataSource.Where(x => x.Quantity == 2).Select(x => x.Id).ToList();

                ClassicAssert.AreEqual(20, projection1.Count);

                var projection2 = dataSource.Where(x => x.Quantity == 2).Select(x => new { x.Id, x.Quantity }).ToList();

                ClassicAssert.AreEqual(20, projection2.Count);
            }

            StopServers();
            StartServers();

            // the same but without PutMany 
            using (var connector = new Connector(_clientConfig))
            {
                connector.DeclareCollection<FlatWithAllKindsOfProperties>();

                var dataSource = connector.DataSource<FlatWithAllKindsOfProperties>();


                var reloaded = dataSource[13];

                ClassicAssert.IsNotNull(reloaded);

                var q2 = dataSource.Where(x => x.Quantity == 2).ToList();

                ClassicAssert.AreEqual(20, q2.Count);

                var projection1 = dataSource.Where(x => x.Quantity == 2).Select(x => x.Id).ToList();

                ClassicAssert.AreEqual(20, projection1.Count);

                var projection2 = dataSource.Where(x => x.Quantity == 2).Select(x => new { x.Id, x.Quantity }).ToList();

                ClassicAssert.AreEqual(20, projection2.Count);
            }
        }

        [Test]
        public void Compute_pivot_with_flat_layout()
        {
            using (var connector = new Connector(_clientConfig))
            {
                connector.DeclareCollection<FlatWithAllKindsOfProperties>();

                var dataSource = connector.DataSource<FlatWithAllKindsOfProperties>();


                dataSource.PutMany(GenerateTestDataForPivot(100));
            }

            using (var connector = new Connector(_clientConfig))
            {
                connector.DeclareCollection<FlatWithAllKindsOfProperties>();

                var dataSource = connector.DataSource<FlatWithAllKindsOfProperties>();

                var request = dataSource.PreparePivotRequest().OnAxis(x => x.InstrumentName, x => x.ValueDate)
                    .AggregateValues(x => x.Quantity, x => x.Nominal);

                var pivot = request.Execute();

                Console.Write(pivot);

                pivot.CheckPivot();
            }
        }

        [Test]
        public void Reindex_existing_collection()
        {
            using (var connector = new Connector(_clientConfig))
            {
                connector.DeclareCollection<FlatWithAllKindsOfProperties>();

                var dataSource = connector.DataSource<FlatWithAllKindsOfProperties>();

                dataSource.PutMany(GenerateTestDataForPivot(100));


                Assert.Throws<CacheException>(() =>
                {
                    var _ = dataSource.OrderBy(x => x.Nominal).ToList();
                });
            }

            using (var connector = new Connector(_clientConfig))
            {
                var schema = TypedSchemaFactory.FromType(typeof(FlatWithAllKindsOfProperties));
                var property = schema.ServerSide.Where(x => x.Name == "Nominal").First();
                property.IndexType = IndexType.Ordered;

                connector.DeclareCollection(nameof(FlatWithAllKindsOfProperties), schema);

                var dataSource = connector.DataSource<FlatWithAllKindsOfProperties>();


                var allOrdered = dataSource.OrderBy(x => x.Nominal).ToList();

                ClassicAssert.AreEqual(100, allOrdered.Count);
            }
        }

        [Test]
        public void Import_csv_file_with_automatic_schema()
        {
            using (var connector = new Connector(_clientConfig))
            {
                connector.FeedCsvWithAutomaticSchema("TestData/csv/20klines.csv", "20k");

                var result = connector.SqlQueryAsJson("select from 20k where dealid=25958469").ToList();

                ClassicAssert.AreEqual(12, result.Count);
            }

            // check they are found after reload
            using (var connector = new Connector(_clientConfig))
            {
                var result = connector.SqlQueryAsJson("select from 20k where dealid=25958469").ToList();

                ClassicAssert.AreEqual(12, result.Count);

                //MonthBucket
                result = connector.SqlQueryAsJson("select  distinct ClientName from 20k").ToList();
                ClassicAssert.AreEqual(61, result.Count);

                var count = connector.SqlQueryAsJson("count from 20k where dealid=25958469").ToList();

                // should return a single object with the count property
                ClassicAssert.AreEqual(1, count.Count);
                ClassicAssert.AreEqual(12, count[0].RootElement.GetProperty("count").GetInt32());
            }
        }
    }
}