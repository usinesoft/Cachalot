using System;
using System.Collections.Generic;
using System.Text;
using Client.Core;
using Client.Interface;
using NUnit.Framework;
using Tests.TestData;

// ReSharper disable UnusedAutoPropertyAccessor.Local

namespace Tests.UnitTests
{
    [TestFixture]
    public class TestFixtureJsonIndexing
    {
        private enum Fuzzy
        {
            Yes,
            No,
            Maybe
        }

        private class AllKindsOfProperties
        {
            [ServerSideValue(IndexType.Primary)] public int Id { get; set; }


            [ServerSideValue(IndexType.Dictionary)]public DateTime ValueDate { get; set; }

            [ServerSideValue(IndexType.Dictionary)] public DateTimeOffset AnotherDate { get; set; }


            [ServerSideValue(IndexType.Dictionary)] public DateTime LastUpdate { get; set; }

            [ServerSideValue(IndexType.Dictionary)] public double Nominal { get; set; }

            [ServerSideValue(IndexType.Dictionary)] public int Quantity { get; set; }

            [ServerSideValue(IndexType.Dictionary)] public string InstrumentName { get; set; }

            [ServerSideValue(IndexType.Dictionary)] public Fuzzy AreYouSure { get; set; }

            [ServerSideValue(IndexType.Dictionary)] public bool IsDeleted { get; set; }

            [ServerSideValue(IndexType.Dictionary)] public IList<string> Tags { get; } = new List<string>();

            [ServerSideValue(IndexType.Dictionary)] public IList<string> Languages { get; } = new List<string>();

            /// <summary>
            ///     Read only property that is indexed. It shoul de serialized to json
            /// </summary>
            [ServerSideValue(IndexType.Dictionary)]
            public Fuzzy Again => Fuzzy.Maybe;
        }


        [Test]
        public void Packing_a_binary_object_and_its_json_should_give_identical_results()
        {
            var today = DateTime.Today;
            var now = DateTime.Now;

            var schema = TypedSchemaFactory.FromType(typeof(AllKindsOfProperties));

            var testObj = new AllKindsOfProperties
            {
                Id = 15,
                ValueDate = today,
                LastUpdate = now,
                Nominal = 156.32,
                Quantity = 35,
                InstrumentName = "IRS",
                AnotherDate = now,
                AreYouSure = Fuzzy.Maybe,
                IsDeleted = true,
                Tags = {"news", "science", "space", "διξ"},
                Languages = {"en", "de", "fr"}
            };

            
            var packed1 = CachedObject.Pack(testObj, schema);

            var json = SerializationHelper.ObjectToJson(testObj);

            var packed2 = CachedObject.PackJson(json, schema);

            Console.WriteLine(packed1);
            Console.WriteLine(packed2);

            Assert.AreEqual(packed1, packed2); // only checks the primary key

            Assert.AreEqual(packed1.CollectionName, packed2.CollectionName);

            CollectionAssert.AreEqual(packed1.UniqueKeys, packed2.UniqueKeys);
            CollectionAssert.AreEqual(packed1.IndexKeys, packed2.IndexKeys);
            CollectionAssert.AreEqual(packed1.ListIndexKeys, packed2.ListIndexKeys);

            
            CollectionAssert.AreEqual(packed1.ObjectData, packed2.ObjectData);
        }

        [Test]
        public void Packing_a_binary_object_and_its_json_should_give_identical_results_with_default_index_type()
        {
            var schema = TypedSchemaFactory.FromType(typeof(Order));

            var testObj = new Order
            {
                Amount = 66.5, Date = DateTimeOffset.Now, Category = "student", ClientId = 101, ProductId = 405,
                Id = Guid.NewGuid(),
                Quantity = 1,
                IsDelivered = true
            };

            var description = TypedSchemaFactory.FromType<Order>();

            var typeDescription = description;

            var packed1 = CachedObject.Pack(testObj, schema);

            var json = SerializationHelper.ObjectToJson(testObj);

            var packed2 = CachedObject.PackJson(json, typeDescription);

            Console.WriteLine(packed1);
            Console.WriteLine(packed2);

            Assert.AreEqual(packed1, packed2); // only checks the primary key

            Assert.AreEqual(packed1.CollectionName, packed2.CollectionName);

            CollectionAssert.AreEqual(packed1.UniqueKeys, packed2.UniqueKeys);
            CollectionAssert.AreEqual(packed1.IndexKeys, packed2.IndexKeys);
            CollectionAssert.AreEqual(packed1.ListIndexKeys, packed2.ListIndexKeys);

            var json1 = Encoding.UTF8.GetString(packed1.ObjectData);
            var json2 = Encoding.UTF8.GetString(packed2.ObjectData);

            CollectionAssert.AreEqual(packed1.ObjectData, packed2.ObjectData);
        }

        
        

        [Test]
        public void Pack_json_with_missing_properties()
        {

            var json = @"{
                    'Id': 123,
                    }";

            var description = TypedSchemaFactory.FromType<AllKindsOfProperties>();

            var packed = CachedObject.PackJson(json, description);

            Assert.AreEqual(123, packed.PrimaryKey.IntValue);

        }


        [Test]
        public void Pack_json_with_automatic_primary_key()
        {
            //TODO  review

            //var json = @"{
            //        'name':'toto',
            //        'age':16

            //        }";

            //var description = SchemaFactory
            //    .New("people")
            //    .AutomaticPrimaryKey()
            //    .WithServerSideValue("name", IndexType.Dictionary)
            //    .WithServerSideValue("age", IndexType.Dictionary)
            //    .Build();

            //var packed = CachedObject.PackJson(json, description);

            //// properties that where not found should not be indexed
            //Assert.AreEqual(2, packed.IndexKeys.Length);

            //json = packed.Json;
                    
            //var repacked = CachedObject.PackJson(json, description);

            //// check than repacking the object gives tha same result (except for the auto generated primary key)
            //repacked.PrimaryKey = packed.PrimaryKey;
            //Assert.AreEqual(packed.ToString(), repacked.ToString());
        }


    }
}