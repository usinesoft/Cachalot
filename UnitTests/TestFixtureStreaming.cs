using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Client.ChannelInterface;
using Client.Core;
using Client.Messages;
using Client.Queries;
using NUnit.Framework;
using ProtoBuf;
using UnitTests.TestData;

namespace UnitTests
{
    [TestFixture]
    public class TestFixtureStreaming
    {
        [OneTimeSetUp]
        public void Init()
        {
            ClientSideTypeDescription.RegisterType(typeof(CacheableTypeOk));
        }

        [Test]
        public void Proto()
        {
            var item1 = new ProtoData(1, "didier", "dupont");
            var stream = new MemoryStream();
            Serializer.Serialize(stream, item1);
        }

        [Test]
        public void SerializationProto()
        {
            var item1 = new ProtoData(1, "didier", "dupont");
            var b1 = SerializationHelper.ObjectToBytes(item1, SerializationMode.ProtocolBuffers, null);
            var item1Reloaded =
                SerializationHelper.ObjectFromBytes<ProtoData>(b1, SerializationMode.ProtocolBuffers, false);
            Assert.IsNotNull(item1Reloaded);

            using (var ms = new MemoryStream())
            {
                SerializationHelper.ObjectToStream(item1, ms, SerializationMode.ProtocolBuffers, false);
                ms.Seek(0, SeekOrigin.Begin);
                item1Reloaded =
                    SerializationHelper.ObjectFromStream<ProtoData>(ms, SerializationMode.ProtocolBuffers, false);
                Assert.IsNotNull(item1Reloaded);
            }
        }


        [Test]
        public void SerializationWithCompression()
        {
            var item1 = new CacheableTypeOk(1, 1003, "AHA", new DateTime(2010, 10, 02), 8);
            var desc = ClientSideTypeDescription.RegisterType<CacheableTypeOk>().AsCollectionSchema;
            desc.UseCompression = true;
            var b1 = SerializationHelper.ObjectToBytes(item1, SerializationMode.Json, desc);
            var item1Reloaded =
                SerializationHelper.ObjectFromBytes<CacheableTypeOk>(b1, SerializationMode.Json, true);
            Assert.IsNotNull(item1Reloaded);

            using (var ms = new MemoryStream())
            {
                SerializationHelper.ObjectToStream(item1, ms, SerializationMode.Json, true);
                ms.Seek(0, SeekOrigin.Begin);
                item1Reloaded =
                    SerializationHelper.ObjectFromStream<CacheableTypeOk>(ms, SerializationMode.Json, true);
                Assert.IsNotNull(item1Reloaded);
                ms.Seek(0, SeekOrigin.Begin);
                item1Reloaded =
                    SerializationHelper.ObjectFromStream<CacheableTypeOk>(ms, SerializationMode.Json, true);
                Assert.IsNotNull(item1Reloaded);
            }
        }

        [Test]
        public void SerializationWithOutCompression()
        {
            var desc = ClientSideTypeDescription.RegisterType<CacheableTypeOk>().AsCollectionSchema;

            var item1 = new CacheableTypeOk(1, 1003, "AHA", new DateTime(2010, 10, 02), 8);
            var b1 = SerializationHelper.ObjectToBytes(item1, SerializationMode.Json, desc);
            var item1Reloaded =
                SerializationHelper.ObjectFromBytes<CacheableTypeOk>(b1, SerializationMode.Json, false);
            Assert.IsNotNull(item1Reloaded);

            using (var ms = new MemoryStream())
            {
                SerializationHelper.ObjectToStream(item1, ms, SerializationMode.Json, false);
                ms.Seek(0, SeekOrigin.Begin);
                item1Reloaded =
                    SerializationHelper.ObjectFromStream<CacheableTypeOk>(ms, SerializationMode.Json, false);
                Assert.IsNotNull(item1Reloaded);
            }
        }


        [Test]
        public void SerializeCachedObjectUsingProtocolBuffers()
        {
            var schema = ClientSideTypeDescription.RegisterType(typeof(TradeLike)).AsCollectionSchema;
            var randGen = new Random();


            //to byte array
            for (var i = 0; i < 5000; i++)
            {
                var obj = new TradeLike(1, 1001, "aaa", new DateTime(2009, 10, 10), 1);
                var packed = CachedObject.Pack(obj, schema);

                var data = SerializationHelper.ObjectToBytes(packed, SerializationMode.ProtocolBuffers, null);
                var reloaded =
                    SerializationHelper.ObjectFromBytes<CachedObject>(data, SerializationMode.ProtocolBuffers, false);


                Assert.AreEqual(reloaded.IndexKeys[2], packed.IndexKeys[2]);

                Console.WriteLine(reloaded);
            }


            //to stream
            var stream = new MemoryStream();
            var items = new List<CachedObject>();
            for (var i = 0; i < 1000; i++)
            {
                var obj = new TradeLike(1, 1001, "aaa", new DateTime(2009, 10, 10), randGen.Next(1000));
                var packed = CachedObject.Pack(obj, schema);
                items.Add(packed);
            }

            var itemsReloaded = new List<CachedObject>();
            Streamer.ToStreamGeneric(stream, items);
            stream.Seek(0, SeekOrigin.Begin);
            var evt = new ManualResetEvent(false);
            Streamer.FromStream(stream,
                delegate(CachedObject item, int i, int totalItems)
                {
                    itemsReloaded.Add(item);
                    if (i == totalItems) evt.Set();
                },
                delegate
                {
                    /* ignore exceptions */
                });

            evt.WaitOne();


            for (var i = 0; i < 1000; i++) Assert.AreEqual(itemsReloaded[i].IndexKeys[2], items[i].IndexKeys[2]);
        }

        [Test]
        public void StreamManyUnstreamMany()
        {
            var schema = ClientSideTypeDescription.RegisterType(typeof(CacheableTypeOk)).AsCollectionSchema;

            var items = new List<CachedObject>(3);

            var item1 = new CacheableTypeOk(1, 1003, "AHA", new DateTime(2010, 10, 02), 8);
            items.Add(CachedObject.Pack(item1, schema));

            var item2 = new CacheableTypeOk(2, 1003, "AHA", new DateTime(2010, 10, 02), 8);
            items.Add(CachedObject.Pack(item2, schema));

            var item3 = new CacheableTypeOk(3, 1003, "AHA", new DateTime(2010, 10, 02), 8);
            items.Add(CachedObject.Pack(item3, schema));

            using (var stream = new MemoryStream())
            {
                Streamer.ToStreamMany(stream, items);

                stream.Seek(0, SeekOrigin.Begin);

                var itemsReceived = 0;
                Streamer.FromStream(stream,
                    delegate(CacheableTypeOk data, int currentItem, int totalItems)
                    {
                        Assert.IsTrue(currentItem > 0);
                        Assert.IsTrue(currentItem <= totalItems);

                        itemsReceived++;

                        Assert.AreEqual(itemsReceived, data.PrimaryKey);
                    },
                    delegate { Assert.Fail(); });

                Assert.AreEqual(itemsReceived, 3);
            }
        }

        [Test]
        public void StreamManyUnstreamOne()
        {
            var item = new CacheableTypeOk(3, 1003, "AHA", new DateTime(2010, 10, 02), 8);
            
            var desc = ClientSideTypeDescription.RegisterType<CacheableTypeOk>().AsCollectionSchema;

            using (var stream = new MemoryStream())
            {
                Streamer.ToStream(stream, item, desc);
                stream.Seek(0, SeekOrigin.Begin);

                var itemReloaded = Streamer.FromStream<CacheableTypeOk>(stream);
                Assert.IsNotNull(itemReloaded);
                Assert.AreEqual(itemReloaded, item);
            }
        }

        [Test]
        public void StreamManyUnstreamOneCacheable()
        {
            var schema = ClientSideTypeDescription.RegisterType(typeof(CacheableTypeOk)).AsCollectionSchema;

            var item = new CacheableTypeOk(3, 1003, "AHA", new DateTime(2010, 10, 02), 8);
            var it = CachedObject.Pack(item, schema);
            var oneItemList = new List<CachedObject> {it};

            using (var stream = new MemoryStream())
            {
                Streamer.ToStreamMany(stream, oneItemList);
                stream.Seek(0, SeekOrigin.Begin);

                var itemReloaded = Streamer.FromStream<CacheableTypeOk>(stream);
                Assert.IsNotNull(itemReloaded);
                Assert.AreEqual(itemReloaded, item);
            }
        }

        [Test]
        public void StreamOneUnstreamMany()
        {
            var item = new CacheableTypeOk(3, 1003, "AHA", new DateTime(2010, 10, 02), 8);
            var desc = ClientSideTypeDescription.RegisterType<CacheableTypeOk>().AsCollectionSchema;
            using (var stream = new MemoryStream())
            {
                Streamer.ToStream(stream, item, desc);
                stream.Seek(0, SeekOrigin.Begin);

                var itemsReceived = 0;
                Streamer.FromStream(stream,
                    delegate(CacheableTypeOk data, int currentItem, int totalItems)
                    {
                        Assert.IsTrue(currentItem > 0);
                        Assert.IsTrue(currentItem <= totalItems);

                        itemsReceived++;
                    },
                    delegate { Assert.Fail(); });

                Assert.AreEqual(itemsReceived, 1);
            }
        }

        /// <summary>
        ///     Test the special vershion for CacheableObject which are already serialized
        /// </summary>
        [Test]
        public void StreamUnstreamManyCacheable()
        {
            var schema = ClientSideTypeDescription.RegisterType(typeof(CacheableTypeOk)).AsCollectionSchema;

            var items = new List<CachedObject>(3);

            var item1 = new CacheableTypeOk(1, 1003, "AHA", new DateTime(2010, 10, 02), 8);
            var it1 = CachedObject.Pack(item1, schema);
            items.Add(it1);

            var item2 = new CacheableTypeOk(2, 1003, "AHA", new DateTime(2010, 10, 02), 8);
            var it2 = CachedObject.Pack(item2, schema);
            items.Add(it2);

            var item3 = new CacheableTypeOk(3, 1003, "AHA", new DateTime(2010, 10, 02), 8);
            var it3 = CachedObject.Pack(item3, schema);
            items.Add(it3);

            using (var stream = new MemoryStream())
            {
                Streamer.ToStreamMany(stream, items);

                stream.Seek(0, SeekOrigin.Begin);

                var itemsReceived = 0;
                Streamer.FromStream(stream,
                    delegate(CacheableTypeOk data, int currentItem, int totalItems)
                    {
                        Assert.IsTrue(currentItem > 0);
                        Assert.IsTrue(currentItem <= totalItems);

                        itemsReceived++;

                        Assert.AreEqual(itemsReceived, data.PrimaryKey);
                    },
                    delegate { Assert.Fail(); });

                Assert.AreEqual(itemsReceived, 3);
            }
        }

        /// <summary>
        ///     Test streaming and un streaming of all requests and response types one at a time
        /// </summary>
        [Test]
        public void StreamUnstreamMessagesOneByOne()
        {
            var schema = ClientSideTypeDescription.RegisterType(typeof(CacheableTypeOk)).AsCollectionSchema;

            var qbuilder = new QueryBuilder(typeof(CacheableTypeOk));

            var put = new PutRequest(typeof(CacheableTypeOk));
            var item = new CacheableTypeOk(3, 1003, "AHA", new DateTime(2010, 10, 02), 8);

            var typeDescription =
                ClientSideTypeDescription.RegisterType(typeof(CacheableTypeOk)).AsCollectionSchema;
            put.Items.Add(CachedObject.Pack(item, schema));

            var remove = new RemoveRequest(typeof(CacheableTypeOk), new KeyValue(1, schema.PrimaryKeyField));

            var register = new RegisterTypeRequest(typeDescription);

            using (var stream = new MemoryStream())
            {
                //request
                Streamer.ToStream(stream, new GetRequest(qbuilder.GetManyWhere("IndexKeyValue > 1000")));
                Streamer.ToStream(stream, put);
                Streamer.ToStream(stream, remove);
                Streamer.ToStream(stream, register);

                //response
                Streamer.ToStream(stream, new NullResponse());
                Streamer.ToStream(stream, new ExceptionResponse(new Exception("fake exception")));
                Streamer.ToStream(stream, new ServerDescriptionResponse());

                stream.Seek(0, SeekOrigin.Begin);
                object reloaded = Streamer.FromStream<Request>(stream);
                Assert.IsTrue(reloaded is GetRequest);

                //request
                reloaded = Streamer.FromStream<Request>(stream);
                Assert.IsTrue(reloaded is PutRequest);

                reloaded = Streamer.FromStream<Request>(stream);
                Assert.IsTrue(reloaded is RemoveRequest);

                reloaded = Streamer.FromStream<Request>(stream);
                Assert.IsTrue(reloaded is RegisterTypeRequest);

                ////response
                reloaded = Streamer.FromStream<Response>(stream);
                Assert.IsTrue(reloaded is NullResponse);

                reloaded = Streamer.FromStream<Response>(stream);
                Assert.IsTrue(reloaded is ExceptionResponse);

                reloaded = Streamer.FromStream<Response>(stream);
                Assert.IsTrue(reloaded is ServerDescriptionResponse);
            }
        }

        [Test]
        public void StreamUnstreamOneCacheable()
        {
            var schema = ClientSideTypeDescription.RegisterType(typeof(CacheableTypeOk)).AsCollectionSchema;

            var item1 = new CacheableTypeOk(1, 1003, "AHA", new DateTime(2010, 10, 02), 8);

            var it1 = CachedObject.Pack(item1, schema);

            using var stream = new MemoryStream();
            Streamer.ToStream(stream, it1);

            stream.Seek(0, SeekOrigin.Begin);

            var reloaded = Streamer.FromStream<CachedObject>(stream);
            var original = CachedObject.Unpack<CacheableTypeOk>(reloaded);


            Assert.IsTrue(original is CacheableTypeOk);
        }


        [Test]
        public void TestProtobufEncoding()
        {
            var schema = ClientSideTypeDescription.RegisterType(typeof(TradeLike)).AsCollectionSchema;

            var builder = new QueryBuilder(typeof(TradeLike));
            var kval = builder.MakeKeyValue("Nominal", 0);
            var stream = new MemoryStream();
            Serializer.Serialize(stream, kval);
            stream.Seek(0, SeekOrigin.Begin);
            var reloaded = Serializer.Deserialize<KeyValue>(stream);
            Assert.AreEqual(kval, reloaded);

            stream.Seek(0, SeekOrigin.Begin);
            var obj = new TradeLike(1, 1001, "aaa", new DateTime(2009, 10, 10), 0);
            var packed = CachedObject.Pack(obj, schema);

            Serializer.SerializeWithLengthPrefix(stream, packed, PrefixStyle.Fixed32);
            Serializer.SerializeWithLengthPrefix(stream, packed, PrefixStyle.Fixed32);
            stream.Seek(0, SeekOrigin.Begin);
            var t1 = Serializer.DeserializeWithLengthPrefix<CachedObject>(stream, PrefixStyle.Fixed32);
            Assert.AreEqual(t1.IndexKeys[2].ToString(), "0");
            var t2 = Serializer.DeserializeWithLengthPrefix<CachedObject>(stream, PrefixStyle.Fixed32);
            Assert.AreEqual(t2.IndexKeys[2].ToString(), "0");
        }
    }
}