using Client.Core;
using Client.Tools;
using NUnit.Framework;
using System;
using System.Linq;

namespace Tests.UnitTests
{

    [TestFixture]
    public class TestFixtureCsv
    {

        [Test]
        public void Test_schema_inference()
        {
            var csvHelper = new CsvSchemaBuilder("TestData/csv/20klines.csv");

            var schema = csvHelper.InfereSchema();

            Console.Write(schema.AnalysisReport());

            var collectionSchema = schema.ToCollectionSchema();

            var dealId = collectionSchema.ServerSide.FirstOrDefault(x => x.Name == "DealId");
            Assert.IsNotNull(dealId);
            Assert.AreEqual(dealId.IndexType, IndexType.Dictionary);
            Assert.AreEqual(1, dealId.Order);

            var currentValue = collectionSchema.ServerSide.FirstOrDefault(x => x.Name == "CurrentValue");
            Assert.IsNotNull(currentValue);
            Assert.AreEqual(currentValue.IndexType, IndexType.None, "float values should not be indexed");
        }

        [Test]
        [TestCase("-618913.3333", KeyValue.OriginalType.SomeFloat)]
        [TestCase("3/20/2017", KeyValue.OriginalType.Date)]
        [TestCase("KB-143", KeyValue.OriginalType.String)]
        [TestCase("8/2/2022 11:03", KeyValue.OriginalType.Date)]
        [TestCase("0001-01-01", KeyValue.OriginalType.Date)]
        [TestCase("2005-01-01", KeyValue.OriginalType.Date)]
        public void Type_detection(string value, KeyValue.OriginalType expectedType)
        {
            var val = JExtensions.SmartParse(value);
            var type = new KeyValue(val).Type;

            Assert.AreEqual(expectedType, type);

        }
    }
}
