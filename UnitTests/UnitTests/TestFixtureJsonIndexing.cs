using System;
using System.IO;
using System.Linq;
using System.Text;
using Client.Core;
using Client.Messages;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
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
                Tags = {"news", "science", "space", "διξ"},
                Languages = {"en", "de", "fr"}
            };

            
            var packed1 = PackedObject.Pack(testObj, schema);

            var json = SerializationHelper.ObjectToJson(testObj);

            var packed2 = PackedObject.PackJson(json, schema);

            Console.WriteLine(packed1);
            Console.WriteLine(packed2);

            Assert.AreEqual(packed1, packed2); // only checks the primary key

            Assert.AreEqual(packed1.CollectionName, packed2.CollectionName);

            CollectionAssert.AreEqual(packed1.Values, packed2.Values);
            Assert.AreEqual(packed1.CollectionValues.Length, packed2.CollectionValues.Length);

            for (int i = 0; i < packed2.CollectionValues.Length; i++)
            {
                CollectionAssert.AreEqual(packed1.CollectionValues[i].Values, packed2.CollectionValues[i].Values);    
            }
            
            
            
            CollectionAssert.AreEqual(packed1.ObjectData, packed2.ObjectData);
        }


        /// <summary>
        /// Compare json: all the properties must be identical even if in different order. Ignore te special $type property
        /// </summary>
        /// <param name="json1"></param>
        /// <param name="json2"></param>
        /// <returns></returns>
        public static bool CompareJson(string json1, string json2)
        {
            JObject j1 = JObject.Parse(json1);
            JObject j2 = JObject.Parse(json2);

            var properties1 = j1.Properties().Where(p => p.Name != "$type").OrderBy(p => p.Name).ToList();
            var properties2 = j2.Properties().Where(p => p.Name != "$type").OrderBy(p => p.Name).ToList();

            if (properties2.Count != properties1.Count)
                return false;

            for (int i = 0; i < properties1.Count; i++)
            {
                var p1 = properties1[i];
                var p2 = properties2[i];

                if (p1.Name != p2.Name)
                    return false;

                if (p1.Value.ToString() != p2.Value.ToString())
                    return false;
            }


            return true;
        }

        [Test]
        public void Serialize_a_subset_of_properties_as_json()
        {

            var date = DateTimeOffset.Now;
            if (date.DayOfWeek == default) // avoid default values that are ignored in the full json
            {
                date = date.AddDays(1);
            }

            var testObj = new Order
            {
                Amount = 66.5, Date = date, Category = "student", ClientId = 101, ProductId = 405,
                Id = Guid.NewGuid(),
                Quantity = 1,
                IsDelivered = true
            };


            var schema = TypedSchemaFactory.FromType<Order>();

            var packed = PackedObject.Pack(testObj, schema);

            var jsonFull = packed.Json;

            var data1 = packed.GetData(schema.IndexesOfNames("Amount", "Category"));

            var json1 = new StreamReader(new MemoryStream(data1)).ReadToEnd();

            var partial1 = SerializationHelper.ObjectFromBytes<Order>(data1, SerializationMode.Json, schema.UseCompression);

            Assert.AreEqual(testObj.Amount, partial1.Amount);
            Assert.AreEqual(testObj.Category, partial1.Category);

            var data2 = packed.GetServerSideData();
            var json = new StreamReader(new MemoryStream(data2)).ReadToEnd();

            // they are equal except for the $type property which is present only in the full json
            Assert.IsTrue(CompareJson(json, jsonFull));

            schema = TypedSchemaFactory.FromType(typeof(AllKindsOfProperties));

            var today = DateTime.Today;
            var now = DateTime.Now;

            var testObj1 = new AllKindsOfProperties
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
                Tags = {"news", "science", "space", "διξ"},
                Languages = {"en", "de", "fr"}
            };

            packed = PackedObject.Pack(testObj1, schema);

            jsonFull = packed.Json;

            data2 = packed.GetServerSideData();
            json = new StreamReader(new MemoryStream(data2)).ReadToEnd();

            // they are equal except for the $type property which is present only in the full json
            Assert.IsTrue(CompareJson(json, jsonFull));


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

            var packed1 = PackedObject.Pack(testObj, schema);

            var json = SerializationHelper.ObjectToJson(testObj);

            var packed2 = PackedObject.PackJson(json, typeDescription);

            Console.WriteLine(packed1);
            Console.WriteLine(packed2);

            Assert.AreEqual(packed1, packed2); // only checks the primary key

            Assert.AreEqual(packed1.CollectionName, packed2.CollectionName);

            CollectionAssert.AreEqual(packed1.Values, packed2.Values);
            CollectionAssert.AreEqual(packed1.CollectionValues, packed2.CollectionValues);
            

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

            var packed = PackedObject.PackJson(json, description);

            Assert.AreEqual(123, packed.PrimaryKey.IntValue);

        }

        [Test]
        public void Null_and_zero_are_equals()
        {
            var val1 = new KeyValue(null, new KeyInfo{Name = "test"});
            var val2 = new KeyValue(0, new KeyInfo{Name = "test"});

            // for indexing only null and 0 are considered the same
            Assert.AreEqual(val1, val2);
            Assert.AreEqual(val1.IntValue, val2.IntValue);
            Assert.AreEqual(val1.StringValue, val2.StringValue);

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
            //Assert.AreEqual(2, packed.IndexKeys.Length);

            //json = packed.Json;
                    
            //var repacked = PackedObject.PackJson(json, description);

            //// check than repacking the object gives tha same result (except for the auto generated primary key)
            //repacked.PrimaryKey = packed.PrimaryKey;
            //Assert.AreEqual(packed.ToString(), repacked.ToString());
        }
    }
}