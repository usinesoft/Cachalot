#region

using System;
using System.IO;
using Client.Core;
using Client.Interface;
using Client.Messages;
using NUnit.Framework;
using UnitTests.TestData;

#endregion

namespace UnitTests
{
    [TestFixture]
    public class TestFixtureCacheableType
    {
        [Test]
        public void TestKoNoPrimaryKey()
        {
            Assert.Throws<NotSupportedException>(() =>
                ClientSideTypeDescription.RegisterType(typeof(CacheableTypeKo)));
        }


        [Test]
        public void TestTypeDescriptionIsSerializable()
        {
            var typeDescription = ClientSideTypeDescription.RegisterType(typeof(CacheableTypeOk));
            var serializableDescription = typeDescription.AsTypeDescription;

            using (var stream = new MemoryStream())
            {
                SerializationHelper.ObjectToStream(serializableDescription, stream, SerializationMode.ProtocolBuffers,
                    false);

                stream.Seek(0, SeekOrigin.Begin);

                var deserializedDescription = SerializationHelper.ObjectFromStream<TypeDescription>(stream,
                    SerializationMode
                        .ProtocolBuffers,
                    false);
                Assert.IsNotNull(deserializedDescription);
                Assert.AreEqual(serializableDescription, deserializedDescription);
                Assert.AreEqual(serializableDescription.GetHashCode(), deserializedDescription.GetHashCode());
            }
        }

        [Test]
        public void TestTypeOk()
        {
            var typeDescription = ClientSideTypeDescription.RegisterType(typeof(CacheableTypeOk));
            Assert.IsNotNull(typeDescription.PrimaryKeyField);
            Assert.AreEqual(typeDescription.FullTypeName, typeof(CacheableTypeOk).FullName);
            Assert.AreEqual(typeDescription.TypeName, typeof(CacheableTypeOk).Name);
            Assert.AreEqual(typeDescription.UniqueKeysCount, 1);

            Assert.AreEqual(typeDescription.IndexCount, 4);
            Assert.AreEqual(typeDescription.FullTextIndexed.Count, 2);


        }

        [Test]
        public void HashcodesOfKeysAreAllwaysPositive()
        {
            var hash = new KeyValue("66666", new KeyInfo(KeyDataType.StringKey, KeyType.Primary, "test")).GetHashCode();

            Assert.IsTrue(hash > 0, "hash > 0");

            hash = new KeyValue(999999999999999, new KeyInfo(KeyDataType.IntKey, KeyType.Primary, "test"))
                .GetHashCode();

            Assert.IsTrue(hash > 0, "hash > 0");
        }
    }
}