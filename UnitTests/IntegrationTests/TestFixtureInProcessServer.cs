using System;
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

        [Test]
        public void Sql_with_internal_server()
        {
            using (var connector = new Connector(""))
            {
                connector.DeclareCollection<AllKindsOfProperties>();


                var item = new AllKindsOfProperties
                {
                    Id = 13,
                    Quantity = 2,
                    AreYouSure = AllKindsOfProperties.Fuzzy.Maybe,
                    IsDeleted = false,
                    AnotherDate = DateTimeOffset.Now,
                    InstrumentName = "Swap",
                    Languages = { "fr", "en" },
                    LastUpdate = DateTime.Now,
                    Nominal = 125.23
                };

                var items = connector.DataSource<AllKindsOfProperties>();

                items.Put(item);
            }

            using (var connector = new Connector(new ClientConfig()))
            {
                connector.DeclareCollection<AllKindsOfProperties>();

                var items = connector.DataSource<AllKindsOfProperties>();

                var reloaded = items.FirstOrDefault(t => t.Nominal < 150);

                Assert.IsNotNull(reloaded);
                Assert.AreEqual(13, reloaded.Id);

                var result = connector.SqlQueryAsJson("select from AllKindsOfProperties where nominal < 150").ToList();
                Assert.AreEqual(1, result.Count);
            }
        }

        [Test]
        public void Comparison_on_ordered_indexes()
        {
            using var connector = new Connector("");

            connector.DeclareCollection<Invoice>();

            var data = new[]
            {
                new Invoice
                {
                    Id = "ab101", DiscountPercentage = 0.15M, Lines = new[]
                    {
                        new InvoiceLine { Quantity = 1, UnitaryPrice = 1002.25M }
                    }

                },
                new Invoice
                {
                    Id = "ab102", DiscountPercentage = 0.15M, Lines = new[]
                    {
                        new InvoiceLine { Quantity = 1, UnitaryPrice = 1004.25M }
                    }

                }

                ,
                new Invoice
                {
                    Id = "ab103", DiscountPercentage = 0.15M, Lines = new[]
                    {
                        new InvoiceLine { Quantity = 1, UnitaryPrice = 1005.25M }
                    }

                }


            };


            var invoices = connector.DataSource<Invoice>();

            invoices.PutMany(data);

            var result = connector.SqlQueryAsJson("select from invoice where TotalAmount < 1005").ToList();
            Assert.AreEqual(2, result.Count);
        }
    }
}