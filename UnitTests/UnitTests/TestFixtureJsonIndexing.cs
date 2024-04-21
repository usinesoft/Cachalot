using System;
using System.IO;
using System.Text;
using Client.Core;
using NUnit.Framework;
using NUnit.Framework.Legacy;
using Tests.TestData;

// ReSharper disable UnusedAutoPropertyAccessor.Local

namespace Tests.UnitTests
{
    [TestFixture]
    public class TestFixtureJsonIndexing
    {
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
                AreYouSure = AllKindsOfProperties.Fuzzy.Maybe,
                IsDeleted = true,
                Tags = { "news", "science", "space", "διξ" },
                Languages = { "en", "de", "fr" }
            };


            var packed1 = PackedObject.Pack(testObj, schema);
            var jsonIn1 = Encoding.UTF8.GetString(packed1.ObjectData);

            var json = SerializationHelper.ObjectToCompactJson(testObj);

            var packed2 = PackedObject.PackJson(json, schema);
            var jsonIn2 = Encoding.UTF8.GetString(packed1.ObjectData);

            Assert.That(jsonIn1, Is.EqualTo(jsonIn2));

            Console.WriteLine(packed1);
            Console.WriteLine(packed2);



            ClassicAssert.AreEqual (packed1.PrimaryKey, packed2.PrimaryKey); // only checks the primary key

            ClassicAssert.AreEqual(packed1.CollectionName, packed2.CollectionName);

            ClassicAssert.AreEqual(packed1.Values, packed2.Values);
            ClassicAssert.AreEqual(packed1.CollectionValues.Length, packed2.CollectionValues.Length);

            for (var i = 0; i < packed2.CollectionValues.Length; i++)
                ClassicAssert.AreEqual(packed1.CollectionValues[i].Values, packed2.CollectionValues[i].Values);


            ClassicAssert.AreEqual(packed1.ObjectData, packed2.ObjectData);
        }


       

        [Test]
        public void Serialize_a_subset_of_properties_as_json()
        {
            var date = DateTimeOffset.Now;
            if (date.DayOfWeek == default) // avoid default values that are ignored in the full json
                date = date.AddDays(1);

            var testObj = new Order
            {
                Amount = 66.5,
                Date = date,
                Category = "student",
                ClientId = 101,
                ProductId = 405,
                Id = Guid.NewGuid(),
                Quantity = 1,
                IsDelivered = true
            };


            var schema = TypedSchemaFactory.FromType<Order>();

            var packed = PackedObject.Pack(testObj, schema);

            var jsonFull = packed.GetJson(schema);

            var data1 = packed.GetData(schema.IndexesOfNames("Amount", "Category"), new[] { "Amount", "Category" });

            var json1 = new StreamReader(new MemoryStream(data1)).ReadToEnd();

            var partial1 = SerializationHelper.ObjectFromBytes<Order>(data1, SerializationMode.Json,
                schema.StorageLayout == Layout.Compressed);

            ClassicAssert.AreEqual(testObj.Amount, partial1.Amount);
            ClassicAssert.AreEqual(testObj.Category, partial1.Category);

            var data2 = packed.ObjectData;
            var json = new StreamReader(new MemoryStream(data2)).ReadToEnd();

            Assert.That(json, Is.EqualTo(jsonFull));

        }


        [Test]
        public void Packing_a_binary_object_and_its_json_should_give_identical_results_with_default_index_type()
        {
            var schema = TypedSchemaFactory.FromType(typeof(Order));

            var testObj = new Order
            {
                Amount = 66.5,
                Date = DateTimeOffset.Now,
                Category = "student",
                ClientId = 101,
                ProductId = 405,
                Id = Guid.NewGuid(),
                Quantity = 1,
                IsDelivered = true
            };

            var description = TypedSchemaFactory.FromType<Order>();

            var typeDescription = description;

            var packed1 = PackedObject.Pack(testObj, schema);

            var json = SerializationHelper.ObjectToCompactJson(testObj);

            var packed2 = PackedObject.PackJson(json, typeDescription);

            Console.WriteLine(packed1);
            Console.WriteLine(packed2);

            ClassicAssert.AreEqual(packed1.ToString(), packed2.ToString()); // only checks the primary key

            ClassicAssert.AreEqual(packed1.CollectionName, packed2.CollectionName);

            ClassicAssert.AreEqual(packed1.Values, packed2.Values);
            ClassicAssert.AreEqual(packed1.CollectionValues, packed2.CollectionValues);


            var json1 = Encoding.UTF8.GetString(packed1.ObjectData);
            var json2 = Encoding.UTF8.GetString(packed2.ObjectData);
            Assert.That(json1, Is.EqualTo(json2));

            ClassicAssert.AreEqual(packed1.ObjectData, packed2.ObjectData);
        }


        [Test]
        public void Pack_json_with_missing_properties()
        {
            var json = @"{
                    ""Id"": 123
                    }";

            var description = TypedSchemaFactory.FromType<AllKindsOfProperties>();

            var packed = PackedObject.PackJson(json, description);

            ClassicAssert.AreEqual(123, packed.PrimaryKey.IntValue);
        }

        [Test]
        public void Null_comparison_and_equality()
        {
            var val1 = new KeyValue(null);
            var val2 = new KeyValue(0);


            // null is equal only to null
            ClassicAssert.IsFalse(val1 == val2);

            // and is less than any other value
            ClassicAssert.IsTrue(val1.CompareTo(val2) < 0);
            ClassicAssert.IsTrue(val2.CompareTo(val1) > 0);
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

            //var packed = PackedObject.PackJson(json, description);

            //// properties that where not found should not be indexed
            //ClassicAssert.AreEqual(2, packed.IndexKeys.Length);

            //json = packed.Json;

            //var repacked = PackedObject.PackJson(json, description);

            //// check than repacking the object gives tha same result (except for the auto generated primary key)
            //repacked.PrimaryKey = packed.PrimaryKey;
            //ClassicAssert.AreEqual(packed.ToString(), repacked.ToString());
        }
    }
}