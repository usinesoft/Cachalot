#region

using System;
using System.Linq;
using Client.Core;
using Client.Interface;
using Client.Messages.Pivot;
using NUnit.Framework;
using Tests.TestData;

#endregion

namespace Tests.UnitTests
{
    [TestFixture]
    public class TestFixtureCacheableObject
    {
        private class TestData
        {
            [ServerSideValue(IndexType.Primary)]
            public Guid Id { get; set; }

            [ServerSideValue(IndexType.Dictionary)]
            public string Name { get; set; }
            
        }


        private static CacheableTypeOk GetObject1()
        {
            var object1 = new CacheableTypeOk
            {
                UniqueKey = 1,
                PrimaryKey = 11,
                IndexKeyValue = 15,
                IndexKeyDate = new DateTime(2009, 10, 25),
                IndexKeyFolder = "FOL"
            };
            return object1;
        }




        [Test]
        public void TestPackedObjectIsSerializable()
        {
            TypedSchemaFactory.FromType(typeof(CacheableTypeOk));
        }

        [Test]
        public void TestPackObject()
        {
            var object1 = GetObject1();
            var description = TypedSchemaFactory.FromType(typeof(CacheableTypeOk));

            var cached = CachedObject.Pack(object1, description);

            Assert.IsNotNull(cached);
            Assert.IsNotNull(cached.PrimaryKey);
            Assert.AreEqual(cached.PrimaryKey, 11);

            Assert.IsNotNull(cached.UniqueKeys);
            Assert.AreEqual(cached.UniqueKeys.Length, 1);
            Assert.AreEqual(cached.UniqueKeys[0], 1);

            Assert.IsNotNull(cached.IndexKeys);
            Assert.AreEqual(cached.IndexKeys.Length, 4);

            foreach (var key in cached.IndexKeys)
            {
                if (key.KeyName == "IndexKeyDate")
                {
                    Assert.AreEqual(key, new DateTime(2009, 10, 25).Ticks);
                    Assert.AreEqual(key.Type, KeyValue.OriginalType.Date);
                }

                if (key.KeyName == "IndexKeyValue")
                {
                    Assert.AreEqual(key, 15);
                    Assert.AreEqual(key.Type, KeyValue.OriginalType.SomeInteger);
                }

                if (key.Type == KeyValue.OriginalType.String)
                {
                    Assert.AreEqual(key, "FOL");
                    Assert.AreEqual(key.KeyName, "IndexKeyFolder");
                }
            }

            var fromCache = CachedObject.Unpack<CacheableTypeOk>(cached);
            Assert.AreEqual(object1, fromCache);
        }

        [Test]
        public void PackedObjectSerialization()
        {
            
            var schema = TypedSchemaFactory.FromType(typeof(Person));

            var packed = CachedObject.Pack(new Person{Id = 13, First = "Dan", Last = "IONESCU"}, schema);

            var data = SerializationHelper.ObjectToBytes(packed, SerializationMode.ProtocolBuffers, schema.UseCompression);

            var reloaded = SerializationHelper.ObjectFromBytes<CachedObject>(data, SerializationMode.ProtocolBuffers, false);

           
            Assert.AreEqual(13, reloaded.PrimaryKey.IntValue);
            Assert.AreEqual("Dan", reloaded.IndexKeys.First(k=>k.KeyName == "First").StringValue);


        }


        [Test]
        public void TestObjectWithServerSideValues()
        {
           
         
            // now try attributes on type
            //var description = TypedSchemaFactory.FromType(typeof(Order));

            //Assert.AreEqual(2, description.ServerSideValues.Count());
            //Assert.AreEqual("Amount", description.ServerSideValues.Single(v=>v.Name == "Amount").Name);

            //var desc1 = description;
            //Assert.AreEqual(2, desc1.ServerSideValues.Count);
            //Assert.AreEqual("Amount", desc1.ServerSideValues.Single(v=>v.Name == "Amount").Name);
            //Assert.AreEqual(IndexType.Ordered, desc1.ServerSideValues.Single(v=>v.Name == "Amount").IndexType);
            //Assert.AreEqual(IndexType.None, desc1.ServerSideValues.Single(v=>v.Name == "Quantity").IndexType);

         
           

            //// pack an object using different kinds of type description
            //var order = new Order
            //{
            //    Amount = 123.45, Date = DateTimeOffset.Now, Category = "geek", ClientId = 101, ProductId = 401,
            //    Id = Guid.NewGuid(),
            //    Quantity = 2
            //};

            //var packed = CachedObject.Pack(order, description);
            //Assert.AreEqual(2, packed.Values.Length);
            //Assert.AreEqual("Amount", packed.Values[0].KeyName);
            //Assert.AreEqual(order.Amount, packed.Values[0].NumericValue);
            //Assert.AreEqual("Quantity", packed.Values[1].KeyName);
            //Assert.AreEqual(order.Quantity, packed.Values[1].NumericValue);

            // TODO review after refactoring

            //var packed1 = CachedObject.Pack(order, desc);
            //Assert.AreEqual(2, packed1.Values.Length);
            //Assert.AreEqual("Amount", packed1.Values[0].Name);
            //Assert.AreEqual(order.Amount, packed1.Values[0].Value);
            //Assert.AreEqual("Quantity", packed1.Values[1].Name);
            //Assert.AreEqual(order.Quantity, packed1.Values[1].Value);

            //var json = JsonConvert.SerializeObject(order);
            //var packed2 = CachedObject.PackJson(json, desc);
            //Assert.AreEqual(2, packed2.Values.Length);
            //Assert.AreEqual("Amount", packed1.Values[0].Name);
            //Assert.AreEqual(order.Amount, packed2.Values[0].Value);

            //var packed3 = CachedObject.Pack(order);
            //Assert.AreEqual(2, packed3.Values.Length);
            //Assert.AreEqual("Amount", packed3.Values[0].Name);
            //Assert.AreEqual(order.Amount, packed3.Values[0].Value);

        }


        [Test]
        public void FluentDescriptionIsEquivalentToTheOldOne()
        {
            var description = SchemaFactory.New("Tests.TestData.Order")
                .PrimaryKey("Id")
                .WithServerSideValue("Amount", IndexType.Ordered)
                .WithServerSideValue("Quantity")
                .WithServerSideValue("Category", IndexType.Dictionary)
                .WithServerSideValue("ProductId", IndexType.Dictionary)
                .WithServerSideValue("ClientId", IndexType.Dictionary)
                .WithServerSideValue("Date", IndexType.Dictionary)
                .WithServerSideValue("DayOfWeek", IndexType.Dictionary)
                .WithServerSideValue("Month", IndexType.Dictionary)
                .WithServerSideValue("Year", IndexType.Dictionary)
                .WithServerSideValue("IsDelivered", IndexType.Dictionary)
                .Build();

            var description1 = TypedSchemaFactory.FromType<Order>();

            Assert.AreEqual(description, description1);
        }

        [Test]
        public void ComputePivotWithServerValues()
        {

            var description = TypedSchemaFactory.FromType(typeof(Order));

            var order1 = new Order
            {
                Amount = 123.45, Date = DateTimeOffset.Now, Category = "geek", ClientId = 101, ProductId = 401,
                Id = Guid.NewGuid(),
                Quantity = 2
            };

            var order2 = new Order
            {
                Amount = 123.45, Date = DateTimeOffset.Now, Category = "sf", ClientId = 101, ProductId = 401,
                Id = Guid.NewGuid(),
                Quantity = 2
            };

            var packed1 = CachedObject.Pack(order1, description);
            var packed2 = CachedObject.Pack(order2, description);

            var pivot = new PivotLevel();

            pivot.AggregateOneObject(packed1);
            pivot.AggregateOneObject(packed2);


            // Amount and Quantity should be aggregated
            Assert.AreEqual(2, pivot.AggregatedValues.Count);

            var agg = pivot.AggregatedValues.First(v => v.ColumnName == "Amount");

            Assert.AreEqual(2, agg.Count);
            Assert.AreEqual(order1.Amount + order2.Amount, agg.Sum);


            Console.WriteLine(pivot.ToString());
        }

        [Test]
        public void ComputePivotWithMultipleAxis()
        {
            var schema = TypedSchemaFactory.FromType(typeof(Order));

            var order1 = new Order
            {
                Amount = 123.45, Date = DateTimeOffset.Now, Category = "geek", ClientId = 101, ProductId = 401,
                Id = Guid.NewGuid(),
                Quantity = 2
            };

            var order2 = new Order
            {
                Amount = 123.45, Date = DateTimeOffset.Now, Category = "sf", ClientId = 101, ProductId = 401,
                Id = Guid.NewGuid(),
                Quantity = 2
            };

            var order3 = new Order
            {
                Amount = 14.5, Date = DateTimeOffset.Now, Category = "geek", ClientId = 101, ProductId = 402,
                Id = Guid.NewGuid(),
                Quantity = 2
            };

            var packed1 = CachedObject.Pack(order1, schema);
            var packed2 = CachedObject.Pack(order2, schema);
            var packed3 = CachedObject.Pack(order3, schema);


            // first test with one single axis
            var pivot = new PivotLevel();

            pivot.AggregateOneObject(packed1, "Category");
            pivot.AggregateOneObject(packed2, "Category");
            pivot.AggregateOneObject(packed3, "Category");


            // Amount and Quantity should be aggregated
            Assert.AreEqual(2, pivot.AggregatedValues.Count);

            var agg = pivot.AggregatedValues.First(v => v.ColumnName == "Amount");

            Assert.AreEqual(3, agg.Count);
            Assert.AreEqual(order1.Amount + order2.Amount + order3.Amount, agg.Sum);

            Assert.IsTrue(pivot.Children.Keys.All(k=>k.KeyName == "Category"));
            Assert.IsTrue(pivot.Children.Values.All(v=>v.AxisValue.KeyName == "Category"));

            var geek = pivot.Children.Values.First(p => p.AxisValue.StringValue == "geek");

            Assert.AreEqual(2, geek.AggregatedValues.Count);

            // then with two axis

            pivot = new PivotLevel();

            pivot.AggregateOneObject(packed1, "Category", "ProductId");
            pivot.AggregateOneObject(packed2, "Category", "ProductId");
            pivot.AggregateOneObject(packed3, "Category", "ProductId");
            
            Console.WriteLine(pivot.ToString());

            var geek1 = pivot.Children.Values.First(p => p.AxisValue.StringValue == "geek");

            Assert.AreEqual(2, geek1.AggregatedValues.Count);
            Assert.AreEqual(2, geek1.Children.Count);


            // check pivot merging

            // a new category
            var order4 = new Order
            {
                Amount = 66.5, Date = DateTimeOffset.Now, Category = "student", ClientId = 101, ProductId = 405,
                Id = Guid.NewGuid(),
                Quantity = 1
            };

            var packed4 = CachedObject.Pack(order4, schema);

            var pivot1 = new PivotLevel();

            pivot1.AggregateOneObject(packed1, "Category", "ProductId");
            pivot1.AggregateOneObject(packed2, "Category", "ProductId");
            pivot1.AggregateOneObject(packed3, "Category", "ProductId");

            var pivot2 = new PivotLevel();

            pivot2.AggregateOneObject(packed1, "Category", "ProductId");
            pivot2.AggregateOneObject(packed3, "Category", "ProductId");
            pivot2.AggregateOneObject(packed4, "Category", "ProductId");

            pivot1.MergeWith(pivot2);

            Console.WriteLine(pivot1);

            // check that an aggregate is equal to the sum of the children
            var sum1 = pivot1.AggregatedValues.First(v => v.ColumnName == "Amount").Sum;
            var sum2 = pivot1.Children.Sum(c=> c.Value.AggregatedValues.First(v => v.ColumnName == "Amount").Sum) ;

            Assert.AreEqual(sum1, sum2);

        }

        [Test]
        public void PackWithAutomaticPrimaryKey()
        {
            var description = TypedSchemaFactory.FromType(typeof(TestData));

            var obj = new TestData {Name = "toto"};
            var packed = CachedObject.Pack(obj, description);

            var pk = Guid.Parse(packed.PrimaryKey.ToString());

            Assert.AreNotEqual(Guid.Empty, pk);

        }
    }
}