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
            ProtoData item1 = new ProtoData(1, "didier", "dupont");
            MemoryStream stream = new MemoryStream();
            Serializer.Serialize(stream, item1);
        }

        [Test]
        public void SerializationProto()
        {
            ProtoData item1 = new ProtoData(1, "didier", "dupont");
            byte[] b1 = SerializationHelper.ObjectToBytes(item1, SerializationMode.ProtocolBuffers, null);
            ProtoData item1Reloaded =
                SerializationHelper.ObjectFromBytes<ProtoData>(b1, SerializationMode.ProtocolBuffers, false);
            Assert.IsNotNull(item1Reloaded);

            using (MemoryStream ms = new MemoryStream())
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
            CacheableTypeOk item1 = new CacheableTypeOk(1, 1003, "AHA", new DateTime(2010, 10, 02), 8);
            var desc = ClientSideTypeDescription.RegisterType<CacheableTypeOk>().AsTypeDescription;
            desc.UseCompression = true;
            byte[] b1 = SerializationHelper.ObjectToBytes(item1, SerializationMode.Json, desc);
            CacheableTypeOk item1Reloaded =
                SerializationHelper.ObjectFromBytes<CacheableTypeOk>(b1, SerializationMode.Json, true);
            Assert.IsNotNull(item1Reloaded);

            using (MemoryStream ms = new MemoryStream())
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
            var desc = ClientSideTypeDescription.RegisterType<CacheableTypeOk>().AsTypeDescription;

            CacheableTypeOk item1 = new CacheableTypeOk(1, 1003, "AHA", new DateTime(2010, 10, 02), 8);
            byte[] b1 = SerializationHelper.ObjectToBytes(item1, SerializationMode.Json, desc);
            CacheableTypeOk item1Reloaded =
                SerializationHelper.ObjectFromBytes<CacheableTypeOk>(b1, SerializationMode.Json, false);
            Assert.IsNotNull(item1Reloaded);

            using (MemoryStream ms = new MemoryStream())
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
            ClientSideTypeDescription.RegisterType(typeof(TradeLike));
            Random randGen = new Random();


            //to byte array
            for (int i = 0; i < 5000; i++)
            {
                TradeLike obj = new TradeLike(1, 1001, "aaa", new DateTime(2009, 10, 10), 1);
                CachedObject packed = CachedObject.Pack(obj).Metadata;

                byte[] data = SerializationHelper.ObjectToBytes(packed, SerializationMode.ProtocolBuffers, null);
                CachedObject reloaded =
                    SerializationHelper.ObjectFromBytes<CachedObject>(data, SerializationMode.ProtocolBuffers, false);


                Assert.AreEqual(reloaded.IndexKeys[2], packed.IndexKeys[2]);

                Console.WriteLine(reloaded);
            }


            //to stream
            MemoryStream stream = new MemoryStream();
            List<CachedObject> items = new List<CachedObject>();
            for (int i = 0; i < 1000; i++)
            {
                TradeLike obj = new TradeLike(1, 1001, "aaa", new DateTime(2009, 10, 10), randGen.Next(1000));
                CachedObject packed = CachedObject.Pack(obj).Metadata;
                items.Add(packed);
            }

            List<CachedObject> itemsReloaded = new List<CachedObject>();
            Streamer.ToStreamGeneric(stream, items);
            stream.Seek(0, SeekOrigin.Begin);
            ManualResetEvent evt = new ManualResetEvent(false);
            Streamer.FromStream(stream,
                delegate(CachedObject item, int i, int totalItems)
                {
                    itemsReloaded.Add(item);
                    if (i == totalItems)
                    {
                        evt.Set();
                    }
                },
                delegate
                {
                    /* ignore exceptions */
                });

            evt.WaitOne();


            for (int i = 0; i < 1000; i++)
            {
                Assert.AreEqual(itemsReloaded[i].IndexKeys[2], items[i].IndexKeys[2]);
            }
        }

        [Test]
        public void StreamManyUnstreamMany()
        {
            List<CachedObject> items = new List<CachedObject>(3);

            CacheableTypeOk item1 = new CacheableTypeOk(1, 1003, "AHA", new DateTime(2010, 10, 02), 8);
            items.Add(CachedObject.Pack(item1));

            CacheableTypeOk item2 = new CacheableTypeOk(2, 1003, "AHA", new DateTime(2010, 10, 02), 8);
            items.Add(CachedObject.Pack(item2));

            CacheableTypeOk item3 = new CacheableTypeOk(3, 1003, "AHA", new DateTime(2010, 10, 02), 8);
            items.Add(CachedObject.Pack(item3));

            using (MemoryStream stream = new MemoryStream())
            {
                Streamer.ToStreamMany(stream, items);

                stream.Seek(0, SeekOrigin.Begin);

                int itemsReceived = 0;
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
            CacheableTypeOk item = new CacheableTypeOk(3, 1003, "AHA", new DateTime(2010, 10, 02), 8);
            List<CacheableTypeOk> oneItemList = new List<CacheableTypeOk>();
            oneItemList.Add(item);

            var desc = ClientSideTypeDescription.RegisterType<CacheableTypeOk>().AsTypeDescription;

            using (MemoryStream stream = new MemoryStream())
            {
                Streamer.ToStream(stream, item, desc);
                stream.Seek(0, SeekOrigin.Begin);

                CacheableTypeOk itemReloaded = Streamer.FromStream<CacheableTypeOk>(stream);
                Assert.IsNotNull(itemReloaded);
                Assert.AreEqual(itemReloaded, item);
            }
        }

        [Test]
        public void StreamManyUnstreamOneCacheable()
        {
            CacheableTypeOk item = new CacheableTypeOk(3, 1003, "AHA", new DateTime(2010, 10, 02), 8);
            CachedObject it = CachedObject.Pack(item);
            List<CachedObject> oneItemList = new List<CachedObject>();
            oneItemList.Add(it);

            using (MemoryStream stream = new MemoryStream())
            {
                Streamer.ToStreamMany(stream, oneItemList);
                stream.Seek(0, SeekOrigin.Begin);

                CacheableTypeOk itemReloaded = Streamer.FromStream<CacheableTypeOk>(stream);
                Assert.IsNotNull(itemReloaded);
                Assert.AreEqual(itemReloaded, item);
            }
        }

        [Test]
        public void StreamOneUnstreamMany()
        {
            CacheableTypeOk item = new CacheableTypeOk(3, 1003, "AHA", new DateTime(2010, 10, 02), 8);
            var desc = ClientSideTypeDescription.RegisterType<CacheableTypeOk>().AsTypeDescription;
            using (MemoryStream stream = new MemoryStream())
            {
                Streamer.ToStream(stream, item, desc);
                stream.Seek(0, SeekOrigin.Begin);

                int itemsReceived = 0;
                Streamer.FromStream<CacheableTypeOk>(stream,
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
        /// Test the special vershion for CacheableObject which are already serialized
        /// </summary>
        [Test]
        public void StreamUnstreamManyCacheable()
        {
            List<CachedObject> items = new List<CachedObject>(3);

            CacheableTypeOk item1 = new CacheableTypeOk(1, 1003, "AHA", new DateTime(2010, 10, 02), 8);
            CachedObject it1 = CachedObject.Pack(item1);
            items.Add(it1);

            CacheableTypeOk item2 = new CacheableTypeOk(2, 1003, "AHA", new DateTime(2010, 10, 02), 8);
            CachedObject it2 = CachedObject.Pack(item2);
            items.Add(it2);

            CacheableTypeOk item3 = new CacheableTypeOk(3, 1003, "AHA", new DateTime(2010, 10, 02), 8);
            CachedObject it3 = CachedObject.Pack(item3);
            items.Add(it3);

            using (MemoryStream stream = new MemoryStream())
            {
                Streamer.ToStreamMany(stream, items);

                stream.Seek(0, SeekOrigin.Begin);

                int itemsReceived = 0;
                Streamer.FromStream<CacheableTypeOk>(stream,
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
        /// Test streaming and unstreaming of all requests and response types one at a time
        /// </summary>
        [Test]
        public void StreamUnstreamMessagesOneByOne()
        {
            QueryBuilder qbuilder = new QueryBuilder(typeof(CacheableTypeOk));

            PutRequest put = new PutRequest(typeof(CacheableTypeOk));
            CacheableTypeOk item = new CacheableTypeOk(3, 1003, "AHA", new DateTime(2010, 10, 02), 8);

            TypeDescription typeDescription =
                ClientSideTypeDescription.RegisterType(typeof(CacheableTypeOk)).AsTypeDescription;
            put.Items.Add(CachedObject.Pack(item));

            RemoveRequest remove = new RemoveRequest(typeof(CacheableTypeOk), typeDescription.MakePrimaryKeyValue(1));

            RegisterTypeRequest register = new RegisterTypeRequest(typeDescription);

            using (MemoryStream stream = new MemoryStream())
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
            CacheableTypeOk item1 = new CacheableTypeOk(1, 1003, "AHA", new DateTime(2010, 10, 02), 8);

            CachedObject it1 = CachedObject.Pack(item1);

            using (MemoryStream stream = new MemoryStream())
            {
                Streamer.ToStream(stream, it1);

                stream.Seek(0, SeekOrigin.Begin);

                object reloaded = Streamer.FromStream<CacheableTypeOk>(stream);
                Assert.IsTrue(reloaded is CacheableTypeOk);
            }
        }


        [Test]
        public void TestProtobufEncoding()
        {
            QueryBuilder builder = new QueryBuilder(typeof(TradeLike));
            KeyValue kval = builder.MakeKeyValue("Nominal", 0);
            MemoryStream stream = new MemoryStream();
            Serializer.Serialize(stream, kval);
            stream.Seek(0, SeekOrigin.Begin);
            KeyValue reloaded = Serializer.Deserialize<KeyValue>(stream);
            Assert.AreEqual(kval, reloaded);

            stream.Seek(0, SeekOrigin.Begin);
            TradeLike obj = new TradeLike(1, 1001, "aaa", new DateTime(2009, 10, 10), 0);
            CachedObject packed = CachedObject.Pack(obj).Metadata;

            Serializer.SerializeWithLengthPrefix(stream, packed, PrefixStyle.Fixed32);
            Serializer.SerializeWithLengthPrefix(stream, packed, PrefixStyle.Fixed32);
            stream.Seek(0, SeekOrigin.Begin);
            CachedObject t1 = Serializer.DeserializeWithLengthPrefix<CachedObject>(stream, PrefixStyle.Fixed32);
            Assert.AreEqual(t1.IndexKeys[2].ToString(), "#0");
            CachedObject t2 = Serializer.DeserializeWithLengthPrefix<CachedObject>(stream, PrefixStyle.Fixed32);
            Assert.AreEqual(t2.IndexKeys[2].ToString(), "#0");
        }
    }
}