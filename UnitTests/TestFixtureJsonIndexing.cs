using System;
using System.Collections.Generic;
using System.Text;
using Client.Core;
using Client.Interface;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

// ReSharper disable UnusedAutoPropertyAccessor.Local

namespace UnitTests
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
            [PrimaryKey(KeyDataType.IntKey)] public int Id { get; set; }


            [Index(KeyDataType.IntKey)] public DateTime ValueDate { get; set; }

            [Index(KeyDataType.IntKey)] public DateTimeOffset AnotherDate { get; set; }


            [Index(KeyDataType.IntKey)] public DateTime LastUpdate { get; set; }

            [Index(KeyDataType.IntKey)] public double Nominal { get; set; }

            [Index(KeyDataType.IntKey)] public int Quantity { get; set; }

            [Index(KeyDataType.StringKey)] public string InstrumentName { get; set; }

            [Index(KeyDataType.IntKey)] public Fuzzy AreYouSure { get; set; }

            [Index(KeyDataType.IntKey)] public bool IsDeleted { get; set; }

            [Index(KeyDataType.StringKey)] public IList<string> Tags { get; } = new List<string>();

            [Index(KeyDataType.StringKey)] public IList<string> Languages { get; } = new List<string>();

            /// <summary>
            ///     Read only property that is indexed. It shoul de serialized to json
            /// </summary>
            [Index(KeyDataType.IntKey)]
            public Fuzzy Again => Fuzzy.Maybe;
        }


        [Test]
        public void Packing_a_binary_object_and_its_json_should_give_identical_results()
        {
            var today = DateTime.Today;
            var now = DateTime.Now;


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

            var description = ClientSideTypeDescription.RegisterType<AllKindsOfProperties>();

            var typeDescription = description.AsTypeDescription;

            var packed1 = CachedObject.Pack(testObj);

            var json = SerializationHelper.ObjectToJson(testObj);

            var packed2 = CachedObject.PackJson(json, typeDescription);

            Console.WriteLine(packed1);
            Console.WriteLine(packed2);

            Assert.AreEqual(packed1, packed2); // only checks the primary key

            Assert.AreEqual(packed1.FullTypeName, packed2.FullTypeName);

            CollectionAssert.AreEqual(packed1.UniqueKeys, packed2.UniqueKeys);
            CollectionAssert.AreEqual(packed1.IndexKeys, packed2.IndexKeys);
            CollectionAssert.AreEqual(packed1.ListIndexKeys, packed2.ListIndexKeys);

            var json1 = Encoding.UTF8.GetString(packed1.ObjectData);
            var json2 = Encoding.UTF8.GetString(packed2.ObjectData);

            CollectionAssert.AreEqual(packed1.ObjectData, packed2.ObjectData);
        }

        [Test]
        public void Test_json_property_conversion_to_int()
        {
            var today = DateTime.Today;
            var now = DateTime.Now;

            var testObj = new AllKindsOfProperties
            {
                ValueDate = today,
                AnotherDate = today.AddDays(1),
                LastUpdate = now,
                Nominal = 156.32,
                Quantity = 35,
                InstrumentName = "IRS",
                AreYouSure = Fuzzy.Maybe,
                IsDeleted = true
            };

            var json = JsonConvert.SerializeObject(testObj);

            var jo = JObject.Parse(json);

            var valueDate = jo.Property("ValueDate");

            var lval = CachedObject.JTokenToLong(valueDate);

            Assert.AreEqual(today.Ticks, lval);

            var anotherDate = jo.Property("AnotherDate");

            lval = CachedObject.JTokenToLong(anotherDate);

            Assert.AreEqual(today.AddDays(1).Ticks, lval);

            lval = CachedObject.JTokenToLong(jo.Property("LastUpdate"));


            Assert.AreEqual(now.Ticks, lval);

            lval = CachedObject.JTokenToLong(jo.Property("Nominal"));
            Assert.AreEqual(1563200, lval);

            lval = CachedObject.JTokenToLong(jo.Property("Quantity"));
            Assert.AreEqual(35, lval);

            lval = CachedObject.JTokenToLong(jo.Property("AreYouSure"));
            Assert.AreEqual(2, lval);

            // check that the reasdonly property is serialized because it is an index
            lval = CachedObject.JTokenToLong(jo.Property("Again"));
            Assert.AreEqual(2, lval);

            lval = CachedObject.JTokenToLong(jo.Property("IsDeleted"));
            Assert.AreEqual(1, lval);
        }

        [Test]
        public void Test_json_property_conversion_to_string()
        {
            var today = DateTime.Today;
            var now = DateTime.Now;

            var testObj = new AllKindsOfProperties
            {
                ValueDate = today,
                LastUpdate = now,
                Nominal = 156.32,
                Quantity = 35,
                InstrumentName = "IRS"
            };

            var json = JsonConvert.SerializeObject(testObj);

            var jo = JObject.Parse(json);

            var sval = CachedObject.JTokenToString(jo.Property("Nominal"));
            Assert.AreEqual("156.32", sval);

            sval = CachedObject.JTokenToString(jo.Property("Quantity"));
            Assert.AreEqual("35", sval);

            sval = CachedObject.JTokenToString(jo.Property("InstrumentName"));
            Assert.AreEqual("IRS", sval);
        }
    }
}