using Client.Core;
using NUnit.Framework;
using System;
using Tests.TestData;

// ReSharper disable UnusedAutoPropertyAccessor.Local

namespace Tests.UnitTests
{
    [TestFixture]
    public class TestFixtureFlatLayout
    {
        [Test]
        public void Pack_and_unpack_an_object_with_flat_layout()
        {

            var schema1 = TypedSchemaFactory.FromType(typeof(FlatWithAllKindsOfProperties));
            Assert.AreEqual(Layout.Flat, schema1.StorageLayout);

            var today = DateTime.Today;
            var now = DateTime.Now;

            var testObj1 = new FlatWithAllKindsOfProperties
            {
                Id = 15,
                ValueDate = today,
                LastUpdate = now,
                Nominal = 156.32,
                Quantity = 35,
                InstrumentName = "IRS",
                AnotherDate = now,
                AreYouSure = FlatWithAllKindsOfProperties.Fuzzy.Maybe,
                IsDeleted = true,

            };

            var packed1 = PackedObject.Pack(testObj1, schema1);

            var data1 = SerializationHelper.ObjectToBytes(packed1, SerializationMode.ProtocolBuffers, false);

            Assert.IsNull(packed1.ObjectData);

            var json1 = packed1.GetJson(schema1);

            var packed2 = PackedObject.PackJson(json1, schema1);
            var json2 = packed2.GetJson(schema1);

            Assert.AreEqual(json1, json2);

            // alter the schema to switch to default layout
            schema1.StorageLayout = Layout.Default;
            var packed3 = PackedObject.Pack(testObj1, schema1);

            var data2 = SerializationHelper.ObjectToBytes(packed3, SerializationMode.ProtocolBuffers, false);

            Assert.Greater(data2.Length, data1.Length);

            // compare with compressed layout
            schema1.StorageLayout = Layout.Compressed;
            var packed4 = PackedObject.Pack(testObj1, schema1);

            var data3 = SerializationHelper.ObjectToBytes(packed4, SerializationMode.ProtocolBuffers, false);

            Assert.Greater(data2.Length, data3.Length);

        }

    }
}