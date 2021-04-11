using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Client.ChannelInterface;
using Client.Core;
using Client.Messages;
using Client.Queries;
using Client.Tools;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using ProtoBuf;
using Tests.TestData;

namespace Tests.UnitTests
{
    [TestFixture]
    public class TestFixtureStreaming
    {
        [OneTimeSetUp]
        public void Init()
        {
            TypedSchemaFactory.FromType(typeof(CacheableTypeOk));
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
            var b1 = SerializationHelper.ObjectToBytes(item1, SerializationMode.ProtocolBuffers, false);
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
            var desc = TypedSchemaFactory.FromType<CacheableTypeOk>();
            
            var b1 = SerializationHelper.ObjectToBytes(item1, SerializationMode.Json, true);
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
            var desc = TypedSchemaFactory.FromType<CacheableTypeOk>();

            var item1 = new CacheableTypeOk(1, 1003, "AHA", new DateTime(2010, 10, 02), 8);
            var b1 = SerializationHelper.ObjectToBytes(item1, SerializationMode.Json, desc.UseCompression);
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
            var schema = TypedSchemaFactory.FromType(typeof(TradeLike));
            var randGen = new Random();


            //to byte array
            for (var i = 0; i < 5000; i++)
            {
                var obj = new TradeLike(1, 1001, "aaa", new DateTime(2009, 10, 10), 1);
                var packed = PackedObject.Pack(obj, schema);

                var data = SerializationHelper.ObjectToBytes(packed, SerializationMode.ProtocolBuffers, false);
                var reloaded =
                    SerializationHelper.ObjectFromBytes<PackedObject>(data, SerializationMode.ProtocolBuffers, false);


                Assert.AreEqual(reloaded.Values[2], packed.Values[2]);

                Console.WriteLine(reloaded);
            }


            //to stream
            var stream = new MemoryStream();
            var items = new List<PackedObject>();
            for (var i = 0; i < 1000; i++)
            {
                var obj = new TradeLike(1, 1001, "aaa", new DateTime(2009, 10, 10), randGen.Next(1000));
                var packed = PackedObject.Pack(obj, schema);
                items.Add(packed);
            }

            var itemsReloaded = new List<PackedObject>();
            Streamer.ToStreamGeneric(stream, items);
            stream.Seek(0, SeekOrigin.Begin);
            var evt = new ManualResetEvent(false);
            Streamer.FromStream(stream,
                delegate(PackedObject item, int i, int totalItems)
                {
                    itemsReloaded.Add(item);
                    if (i == totalItems) evt.Set();
                },
                delegate
                {
                    /* ignore exceptions */
                });

            evt.WaitOne();


            for (var i = 0; i < 1000; i++) Assert.AreEqual(itemsReloaded[i].Values[2], items[i].Values[2]);
        }

        [Test]
        public void StreamManyUnstreamMany()
        {
            var schema = TypedSchemaFactory.FromType(typeof(CacheableTypeOk));

            var items = new List<PackedObject>(3);

            var item1 = new CacheableTypeOk(1, 1003, "AHA", new DateTime(2010, 10, 02), 8);
            items.Add(PackedObject.Pack(item1, schema));

            var item2 = new CacheableTypeOk(2, 1003, "AHA", new DateTime(2010, 10, 02), 8);
            items.Add(PackedObject.Pack(item2, schema));

            var item3 = new CacheableTypeOk(3, 1003, "AHA", new DateTime(2010, 10, 02), 8);
            items.Add(PackedObject.Pack(item3, schema));

            using (var stream = new MemoryStream())
            {
                Streamer.ToStreamMany(stream, items, new int[0], null);

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
            
            var desc = TypedSchemaFactory.FromType<CacheableTypeOk>();

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
            var schema = TypedSchemaFactory.FromType(typeof(CacheableTypeOk));

            var item = new CacheableTypeOk(3, 1003, "AHA", new DateTime(2010, 10, 02), 8);
            var it = PackedObject.Pack(item, schema);
            var oneItemList = new List<PackedObject> {it};

            using (var stream = new MemoryStream())
            {
                Streamer.ToStreamMany(stream, oneItemList, new int[0], null);
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
            var desc = TypedSchemaFactory.FromType<CacheableTypeOk>();
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
        ///     Test the special version for CacheableObject which are already serialized
        /// </summary>
        [Test]
        public void StreamUnstreamManyCacheable()
        {
            var schema = TypedSchemaFactory.FromType(typeof(CacheableTypeOk));

            var items = new List<PackedObject>(3);

            var item1 = new CacheableTypeOk(1, 1003, "AHA", new DateTime(2010, 10, 02), 8);
            var it1 = PackedObject.Pack(item1, schema);
            items.Add(it1);

            var item2 = new CacheableTypeOk(2, 1003, "AHA", new DateTime(2010, 10, 02), 8);
            var it2 = PackedObject.Pack(item2, schema);
            items.Add(it2);

            var item3 = new CacheableTypeOk(3, 1003, "AHA", new DateTime(2010, 10, 02), 8);
            var it3 = PackedObject.Pack(item3, schema);
            items.Add(it3);

            using (var stream = new MemoryStream())
            {
                Streamer.ToStreamMany(stream, items, new int[0], null);

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
            var schema = TypedSchemaFactory.FromType(typeof(CacheableTypeOk));

            var qbuilder = new QueryBuilder(typeof(CacheableTypeOk));

            var put = new PutRequest(typeof(CacheableTypeOk));
            var item = new CacheableTypeOk(3, 1003, "AHA", new DateTime(2010, 10, 02), 8);

            var typeDescription =
                TypedSchemaFactory.FromType(typeof(CacheableTypeOk));
            put.Items.Add(PackedObject.Pack(item, schema));

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
            var schema = TypedSchemaFactory.FromType(typeof(CacheableTypeOk));

            var item1 = new CacheableTypeOk(1, 1003, "AHA", new DateTime(2010, 10, 02), 8);

            var it1 = PackedObject.Pack(item1, schema);

            using var stream = new MemoryStream();
            Streamer.ToStream(stream, it1);

            stream.Seek(0, SeekOrigin.Begin);

            var reloaded = Streamer.FromStream<PackedObject>(stream);
            var original = PackedObject.Unpack<CacheableTypeOk>(reloaded);


            Assert.IsTrue(original is CacheableTypeOk);
        }


        [Test]
        public void TestProtobufEncoding()
        {
            var schema = TypedSchemaFactory.FromType(typeof(TradeLike));

            var builder = new QueryBuilder(typeof(TradeLike));
            var kval = builder.MakeKeyValue("Nominal", 0);
            var stream = new MemoryStream();
            Serializer.Serialize(stream, kval);
            stream.Seek(0, SeekOrigin.Begin);
            var reloaded = Serializer.Deserialize<KeyValue>(stream);
            Assert.AreEqual(kval, reloaded);

            stream.Seek(0, SeekOrigin.Begin);
            var obj = new TradeLike(1, 1001, "aaa", new DateTime(2009, 10, 10), 0);
            var packed = PackedObject.Pack(obj, schema);

            Serializer.SerializeWithLengthPrefix(stream, packed, PrefixStyle.Fixed32);
            Serializer.SerializeWithLengthPrefix(stream, packed, PrefixStyle.Fixed32);
            stream.Seek(0, SeekOrigin.Begin);
            var t1 = Serializer.DeserializeWithLengthPrefix<PackedObject>(stream, PrefixStyle.Fixed32);
            Assert.AreEqual(t1.Values[4].ToString(), "0");
            var t2 = Serializer.DeserializeWithLengthPrefix<PackedObject>(stream, PrefixStyle.Fixed32);
            Assert.AreEqual(t2.Values[4].ToString(), "0");
        }


        private static IEnumerator<RankedItem> MakeEnumerable(params int[] values)
        {

            foreach (var value in values)
            {
                var jobj = new JObject {{"value", new JValue(value)}};

                yield return new RankedItem(0, jobj);
            }
            
        }

         [Test]
        public void TestMergingSortedEnumerableAscending()
        {
            {
                var ordered = OrderByHelper.MixOrderedEnumerators("value", false,MakeEnumerable(1, 2, 4), MakeEnumerable(1, 3, 5),
                    MakeEnumerable(1, 5, 6, 18)).ToList();

                Assert.AreEqual(10, ordered.Count);

                for (int i = 0; i < ordered.Count - 1; i++)
                {
                    Assert.LessOrEqual((int) ordered[i].Item["value"], (int)ordered[i+1].Item["value"]);
                }
            }

            {
                var ordered = OrderByHelper.MixOrderedEnumerators("value", false, MakeEnumerable(1, 1, 1), MakeEnumerable(15, 15, 15),
                    MakeEnumerable(2, 2, 2, 2)).ToList();

                Assert.AreEqual(10, ordered.Count);

                for (int i = 0; i < ordered.Count - 1; i++)
                {
                    Assert.LessOrEqual((int) ordered[i].Item["value"], (int)ordered[i+1].Item["value"]);
                }
            }

            {
                var ordered = OrderByHelper.MixOrderedEnumerators("value", false,MakeEnumerable(10, 11, 12), MakeEnumerable(1, 2, 3),
                    MakeEnumerable(21, 22, 23, 24)).ToList();

                Assert.AreEqual(10, ordered.Count);

                for (int i = 0; i < ordered.Count - 1; i++)
                {
                    Assert.LessOrEqual((int) ordered[i].Item["value"], (int)ordered[i+1].Item["value"]);
                }
            }
        }

        [Test]
        public void TestMergingSortedEnumerableDescending()
        {
            {
                var ordered = OrderByHelper.MixOrderedEnumerators("value", true,MakeEnumerable(4, 2, 1), MakeEnumerable(5, 3, 1),
                    MakeEnumerable(18, 6, 5, 1)).ToList();

                Assert.AreEqual(10, ordered.Count);

                for (int i = 0; i < ordered.Count - 1; i++)
                {
                    Assert.GreaterOrEqual((int) ordered[i].Item["value"], (int)ordered[i+1].Item["value"]);
                }
            }

            {
                var ordered = OrderByHelper.MixOrderedEnumerators("value", true, MakeEnumerable(1, 1, 1), MakeEnumerable(15, 15, 15),
                    MakeEnumerable(2, 2, 2, 2)).ToList();

                Assert.AreEqual(10, ordered.Count);

                for (int i = 0; i < ordered.Count - 1; i++)
                {
                    Assert.GreaterOrEqual((int) ordered[i].Item["value"], (int)ordered[i+1].Item["value"]);
                }
            }

            {
                var ordered = OrderByHelper.MixOrderedEnumerators("value", true,MakeEnumerable(12, 11, 10), MakeEnumerable(3, 2, 1),
                    MakeEnumerable(24, 23, 22, 21)).ToList();

                Assert.AreEqual(10, ordered.Count);

                for (int i = 0; i < ordered.Count - 1; i++)
                {
                    Assert.GreaterOrEqual((int) ordered[i].Item["value"], (int)ordered[i+1].Item["value"]);
                }
            }
        }
    }
}