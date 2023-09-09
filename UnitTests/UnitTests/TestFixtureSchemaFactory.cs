using System.Linq;
using Client.Core;
using NUnit.Framework;
using Tests.TestData;

namespace Tests.UnitTests
{
    [TestFixture]
    public class TestFixtureSchemaFactory
    {
        [Test]
        public void Test_typed_schema_factory()
        {
            var schema = TypedSchemaFactory.FromType<AllKindsOfProperties>();
            Assert.AreEqual(12, schema.ServerSide.Count);
            Assert.AreEqual(IndexType.Primary, schema.ServerSide[0].IndexType);

            // order is preserved for scalars
            Assert.Less(schema.OrderOf("ValueDate"), schema.OrderOf("AnotherDate"));

            // order is preserved for collections 
            Assert.Less(schema.OrderOf("Tags"), schema.OrderOf("Languages"));


            var scalars = schema.ServerSide.Where(x => x.IsCollection = false).ToList();
            // check that orders are a continuous range (for scalar properties)
            for (var i = 1; i < scalars.Count; i++) Assert.IsTrue(scalars[i].Order - scalars[i - 1].Order == 1);
        }

        [Test]
        public void Test_manual_schema_factory()
        {
            var schema = SchemaFactory.New("heroes")
                .PrimaryKey("id")
                .WithServerSideCollection("tags")
                .WithServerSideValue("name", IndexType.Dictionary)
                .WithServerSideValue("age", IndexType.Ordered)
                .EnableFullTextSearch("tags", "name")
                .Build();

            // order is preserved for scalars
            Assert.Less(schema.OrderOf("name"), schema.OrderOf("age"));
        }
    }
}