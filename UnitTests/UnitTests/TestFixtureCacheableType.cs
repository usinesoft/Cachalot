#region

using Client.Core;
using NUnit.Framework;
using System;
using System.IO;
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

            Assert.IsTrue(hash > 0, "hash > 0");

            hash = new KeyValue(999999999999999)
                .GetHashCode();

            Assert.IsTrue(hash > 0, "hash > 0");
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
            Assert.IsNotNull(deserializedDescription);
            Assert.AreEqual(serializableDescription, deserializedDescription);
            Assert.AreEqual(serializableDescription.GetHashCode(), deserializedDescription.GetHashCode());
        }

        [Test]
        public void TestTypeOk()
        {
            var schema = TypedSchemaFactory.FromType(typeof(CacheableTypeOk));
            Assert.IsNotNull(schema.PrimaryKeyField);
            Assert.AreEqual(schema.CollectionName, nameof(CacheableTypeOk));


            Assert.AreEqual(schema.IndexFields.Count, 5);
            Assert.AreEqual(schema.FullText.Count, 2);
        }
    }
}