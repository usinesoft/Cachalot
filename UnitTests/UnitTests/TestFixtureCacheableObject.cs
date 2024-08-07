﻿#region

using System;
using System.Collections.Generic;
using System.Linq;
using Client.Core;
using Client.Interface;
using Client.Messages.Pivot;
using NUnit.Framework;
using NUnit.Framework.Legacy;
using Tests.TestData;

#endregion

namespace Tests.UnitTests
{
    [TestFixture]
    public class TestFixtureCacheableObject
    {
        private class TestData
        {
            [ServerSideValue(IndexType.Primary)] public Guid Id { get; set; }

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

            var cached = PackedObject.Pack(object1, description);

            ClassicAssert.IsNotNull(cached);
            ClassicAssert.IsNotNull(cached.PrimaryKey);
            ClassicAssert.AreEqual(cached.PrimaryKey, 11);


            var fromCache = PackedObject.Unpack<CacheableTypeOk>(cached, description);
            ClassicAssert.AreEqual(object1, fromCache);
        }

        [Test]
        public void PackedObjectSerialization()
        {
            var schema = TypedSchemaFactory.FromType(typeof(Person));

            var packed = PackedObject.Pack(new Person { Id = 13, First = "Dan", Last = "IONESCU" }, schema);

            var data = SerializationHelper.ObjectToBytes(packed, SerializationMode.ProtocolBuffers,
                schema.StorageLayout == Layout.Compressed);

            var reloaded =
                SerializationHelper.ObjectFromBytes<PackedObject>(data, SerializationMode.ProtocolBuffers, false);


            ClassicAssert.AreEqual(13, reloaded.PrimaryKey.IntValue);
        }


        [Test]
        public void TestObjectWithServerSideValues()
        {
            // now try attributes on type
            //var description = TypedSchemaFactory.FromType(typeof(Order));

            //ClassicAssert.AreEqual(2, description.ServerSideValues.Count());
            //ClassicAssert.AreEqual("Amount", description.ServerSideValues.Single(v=>v.Name == "Amount").Name);

            //var desc1 = description;
            //ClassicAssert.AreEqual(2, desc1.ServerSideValues.Count);
            //ClassicAssert.AreEqual("Amount", desc1.ServerSideValues.Single(v=>v.Name == "Amount").Name);
            //ClassicAssert.AreEqual(IndexType.Ordered, desc1.ServerSideValues.Single(v=>v.Name == "Amount").IndexType);
            //ClassicAssert.AreEqual(IndexType.None, desc1.ServerSideValues.Single(v=>v.Name == "Quantity").IndexType);


            //// pack an object using different kinds of type description
            //var order = new Order
            //{
            //    Amount = 123.45, Date = DateTimeOffset.Now, Category = "geek", ClientId = 101, ProductId = 401,
            //    Id = Guid.NewGuid(),
            //    Quantity = 2
            //};

            //var packed = PackedObject.Pack(order, description);
            //ClassicAssert.AreEqual(2, packed.Values.Length);
            //ClassicAssert.AreEqual("Amount", packed.Values[0].KeyName);
            //ClassicAssert.AreEqual(order.Amount, packed.Values[0].NumericValue);
            //ClassicAssert.AreEqual("Quantity", packed.Values[1].KeyName);
            //ClassicAssert.AreEqual(order.Quantity, packed.Values[1].NumericValue);

            // TODO review after refactoring

            //var packed1 = PackedObject.Pack(order, desc);
            //ClassicAssert.AreEqual(2, packed1.Values.Length);
            //ClassicAssert.AreEqual("Amount", packed1.Values[0].Name);
            //ClassicAssert.AreEqual(order.Amount, packed1.Values[0].Value);
            //ClassicAssert.AreEqual("Quantity", packed1.Values[1].Name);
            //ClassicAssert.AreEqual(order.Quantity, packed1.Values[1].Value);

            //var json = JsonConvert.SerializeObject(order);
            //var packed2 = PackedObject.PackJson(json, desc);
            //ClassicAssert.AreEqual(2, packed2.Values.Length);
            //ClassicAssert.AreEqual("Amount", packed1.Values[0].Name);
            //ClassicAssert.AreEqual(order.Amount, packed2.Values[0].Value);

            //var packed3 = PackedObject.Pack(order);
            //ClassicAssert.AreEqual(2, packed3.Values.Length);
            //ClassicAssert.AreEqual("Amount", packed3.Values[0].Name);
            //ClassicAssert.AreEqual(order.Amount, packed3.Values[0].Value);
        }


        [Test]
        public void FluentDescriptionIsEquivalentToTheOldOne()
        {
            var description = SchemaFactory.New("Order")
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

            ClassicAssert.AreEqual(description, description1);
        }

        [Test]
        public void ComputePivotWithServerValues()
        {
            var description = TypedSchemaFactory.FromType(typeof(Order));

            var order1 = new Order
            {
                Amount = 123.45,
                Date = DateTimeOffset.Now,
                Category = "geek",
                ClientId = 101,
                ProductId = 401,
                Id = Guid.NewGuid(),
                Quantity = 2
            };

            var order2 = new Order
            {
                Amount = 123.45,
                Date = DateTimeOffset.Now,
                Category = "sf",
                ClientId = 101,
                ProductId = 401,
                Id = Guid.NewGuid(),
                Quantity = 2
            };

            var packed1 = PackedObject.Pack(order1, description);
            var packed2 = PackedObject.Pack(order2, description);

            var pivot = new PivotLevel(description, new List<int>(), new List<int> { 1, 2 });

            // Amount and Quantity to be aggregated (index 1 and 2) in the schema
            pivot.AggregateOneObject(packed1);
            pivot.AggregateOneObject(packed2);


            // Amount and Quantity should be aggregated
            ClassicAssert.AreEqual(2, pivot.AggregatedValues.Count);

            var agg = pivot.AggregatedValues.First(v => v.ColumnName == "Amount");

            ClassicAssert.AreEqual(2, agg.Count);
            ClassicAssert.AreEqual(order1.Amount + order2.Amount, agg.Sum);


            Console.WriteLine(pivot.ToString());
        }

        [Test]
        public void ComputePivotWithMultipleAxis()
        {
            var schema = TypedSchemaFactory.FromType(typeof(Order));

            var order1 = new Order
            {
                Amount = 123.45,
                Date = DateTimeOffset.Now,
                Category = "geek",
                ClientId = 101,
                ProductId = 401,
                Id = Guid.NewGuid(),
                Quantity = 2
            };

            var order2 = new Order
            {
                Amount = 123.45,
                Date = DateTimeOffset.Now,
                Category = "sf",
                ClientId = 101,
                ProductId = 401,
                Id = Guid.NewGuid(),
                Quantity = 2
            };

            var order3 = new Order
            {
                Amount = 14.5,
                Date = DateTimeOffset.Now,
                Category = "geek",
                ClientId = 101,
                ProductId = 402,
                Id = Guid.NewGuid(),
                Quantity = 2
            };

            var packed1 = PackedObject.Pack(order1, schema);
            var packed2 = PackedObject.Pack(order2, schema);
            var packed3 = PackedObject.Pack(order3, schema);


            // first test with one single axis (Category index = 3)
            var pivot = new PivotLevel(schema, new List<int> { 3 }, new List<int> { 1, 2 });
            ;

            pivot.AggregateOneObject(packed1);
            pivot.AggregateOneObject(packed2);
            pivot.AggregateOneObject(packed3);


            // Amount and Quantity should be aggregated
            ClassicAssert.AreEqual(2, pivot.AggregatedValues.Count);

            var agg = pivot.AggregatedValues.First(v => v.ColumnName == "Amount");

            ClassicAssert.AreEqual(3, agg.Count);
            ClassicAssert.AreEqual(order1.Amount + order2.Amount + order3.Amount, agg.Sum);


            ClassicAssert.IsTrue(pivot.Children.Values.All(v => v.AxisValue.Name == "Category"));

            var geek = pivot.Children.Values.First(p => p.AxisValue.Value.StringValue == "geek");

            ClassicAssert.AreEqual(2, geek.AggregatedValues.Count);

            // then with two axis

            pivot = new PivotLevel(schema, new List<int> { 3, 4 }, new List<int> { 1, 2 });

            pivot.AggregateOneObject(packed1);
            pivot.AggregateOneObject(packed2);
            pivot.AggregateOneObject(packed3);

            Console.WriteLine(pivot.ToString());

            var geek1 = pivot.Children.Values.First(p => p.AxisValue.Value.StringValue == "geek");

            ClassicAssert.AreEqual(2, geek1.AggregatedValues.Count);
            ClassicAssert.AreEqual(2, geek1.Children.Count);


            // check pivot merging

            // a new category
            var order4 = new Order
            {
                Amount = 66.5,
                Date = DateTimeOffset.Now,
                Category = "student",
                ClientId = 101,
                ProductId = 405,
                Id = Guid.NewGuid(),
                Quantity = 1
            };

            var packed4 = PackedObject.Pack(order4, schema);

            var pivot1 = new PivotLevel(schema, new List<int> { 3, 4 }, new List<int> { 1, 2 });

            pivot1.AggregateOneObject(packed1);
            pivot1.AggregateOneObject(packed2);
            pivot1.AggregateOneObject(packed3);

            var pivot2 = new PivotLevel(schema, new List<int> { 3, 4 }, new List<int> { 1, 2 });

            pivot2.AggregateOneObject(packed1);
            pivot2.AggregateOneObject(packed3);
            pivot2.AggregateOneObject(packed4);

            pivot1.MergeWith(pivot2);

            Console.WriteLine(pivot1);

            // check that an aggregate is equal to the sum of the children
            var sum1 = pivot1.AggregatedValues.First(v => v.ColumnName == "Amount").Sum;
            var sum2 = pivot1.Children.Sum(c => c.Value.AggregatedValues.First(v => v.ColumnName == "Amount").Sum);

            ClassicAssert.AreEqual(sum1, sum2);
        }

        [Test]
        public void PackWithAutomaticPrimaryKey()
        {
            var description = TypedSchemaFactory.FromType(typeof(TestData));

            var obj = new TestData { Name = "toto" };
            var packed = PackedObject.Pack(obj, description);

            var pk = Guid.Parse(packed.PrimaryKey.ToString());

            ClassicAssert.AreNotEqual(Guid.Empty, pk);
        }
    }
}