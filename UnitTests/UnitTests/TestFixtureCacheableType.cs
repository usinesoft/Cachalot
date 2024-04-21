#region

using System;
using System.IO;
using Client.Core;
using NUnit.Framework;
using NUnit.Framework.Legacy;
using Tests.TestData;

#endregion

namespace Tests.UnitTests
{
    [TestFixture]
    public class TestFixtureCacheableType
    {
        [Test]
        public void HashCodesOfKeysAreAlwaysPositive()
        {
            var hash = new KeyValue("66666").GetHashCode();

            ClassicAssert.IsTrue(hash > 0, "hash > 0");

            hash = new KeyValue(999999999999999)
                .GetHashCode();

            ClassicAssert.IsTrue(hash > 0, "hash > 0");
        }


        [Test]
        public void DifferentTypesAsKeyValue()
        {
            {
                byte bt = 12;
                var kv = new KeyValue(bt);

                ClassicAssert.AreEqual(bt, kv.IntValue);
                ClassicAssert.AreEqual(KeyValue.OriginalType.SomeInteger, kv.Type);
            }

            {
                byte? bt = 12;
                var kv = new KeyValue(bt);

                ClassicAssert.AreEqual(bt, kv.IntValue);
                ClassicAssert.AreEqual(KeyValue.OriginalType.SomeInteger, kv.Type);
            }

            {
                byte? bt = null;
                var kv = new KeyValue(bt);

                ClassicAssert.AreEqual(0, kv.IntValue);
                ClassicAssert.AreEqual(KeyValue.OriginalType.Null, kv.Type);
            }

            {
                var dt = DateTime.Now;
                var kv = new KeyValue(dt);

                ClassicAssert.AreEqual(dt.Ticks, kv.IntValue);
                ClassicAssert.AreEqual(KeyValue.OriginalType.Date, kv.Type);
            }

            {
                DateTime? dt = DateTime.Now;
                var kv = new KeyValue(dt);

                ClassicAssert.AreEqual(dt.Value.Ticks, kv.IntValue);
                ClassicAssert.AreEqual(KeyValue.OriginalType.Date, kv.Type);
            }

            {
                DateTime? dt = null;
                var kv = new KeyValue(dt);

                ClassicAssert.AreEqual(0, kv.IntValue);
                ClassicAssert.AreEqual(KeyValue.OriginalType.Null, kv.Type);
            }

            {
                DateTime dt = default;
                var kv = new KeyValue(dt);

                ClassicAssert.AreEqual(0, kv.IntValue);
                ClassicAssert.AreEqual(KeyValue.OriginalType.Date, kv.Type);
            }

            {
                var dow = DayOfWeek.Friday;
                var kv = new KeyValue(dow);

                ClassicAssert.AreEqual(5, kv.IntValue);
                ClassicAssert.AreEqual(KeyValue.OriginalType.SomeInteger, kv.Type);
            }

            {
                DayOfWeek? dow = DayOfWeek.Friday;
                var kv = new KeyValue(dow);

                ClassicAssert.AreEqual(5, kv.IntValue);
                ClassicAssert.AreEqual(KeyValue.OriginalType.SomeInteger, kv.Type);
            }

            {
                var tf = true;
                var kv = new KeyValue(tf);

                ClassicAssert.AreEqual(1, kv.IntValue);
                ClassicAssert.AreEqual(KeyValue.OriginalType.Boolean, kv.Type);
            }
        }

        [Test]
        public void TestKoNoPrimaryKey()
        {
            Assert.Throws<NotSupportedException>(() =>
                TypedSchemaFactory.FromType(typeof(CacheableTypeKo)));
        }


        [Test]
        public void TestTypeDescriptionIsSerializable()
        {
            var typeDescription = TypedSchemaFactory.FromType(typeof(CacheableTypeOk));
            var serializableDescription = typeDescription;

            using var stream = new MemoryStream();

            SerializationHelper.ObjectToStream(serializableDescription, stream, SerializationMode.ProtocolBuffers,
                false);

            stream.Seek(0, SeekOrigin.Begin);

            var deserializedDescription = SerializationHelper.ObjectFromStream<CollectionSchema>(stream,
                SerializationMode
                    .ProtocolBuffers,
                false);
            ClassicAssert.IsNotNull(deserializedDescription);
            ClassicAssert.AreEqual(serializableDescription, deserializedDescription);
            ClassicAssert.AreEqual(serializableDescription.GetHashCode(), deserializedDescription.GetHashCode());
        }

        [Test]
        public void TestTypeOk()
        {
            var schema = TypedSchemaFactory.FromType(typeof(CacheableTypeOk));
            ClassicAssert.IsNotNull(schema.PrimaryKeyField);
            ClassicAssert.AreEqual(schema.CollectionName, nameof(CacheableTypeOk));


            ClassicAssert.AreEqual(schema.IndexFields.Count, 5);
            ClassicAssert.AreEqual(schema.FullText.Count, 2);
        }
    }
}