#region

using System;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using Client.Core;
using Client.Interface;
using Client.Messages;
using Newtonsoft.Json;
using NUnit.Framework;
using UnitTests.TestData;

#endregion

namespace UnitTests
{
    [TestFixture]
    public class TestFixtureCacheableObject
    {
        public static Func<TObject, object> GetPropGetter<TObject>(string propertyName)
        {
            var paramExpression = Expression.Parameter(typeof(TObject), "value");

            Expression propertyGetterExpression = Expression.Property(paramExpression, propertyName);

            var result =
                Expression.Lambda<Func<TObject, object>>(Expression.Convert(propertyGetterExpression, typeof(object)),
                    paramExpression).Compile();

            return result;
        }


        private static CacheableTypeOk GetObject1()
        {
            var object1 = new CacheableTypeOk
            {
                UniqueKey = 1,
                PrimaryKey = 11,
                IndexKeyValue = 15,
                IndexKeyDate = new DateTime(2009, 10, 25),
                IndexKeyFolder = "FOL"
            };
            return object1;
        }


        [Test]
        public void CompareTypedAndUntypedPacking()
        {
            var obj = GetObject1();
            var description = ClientSideTypeDescription.RegisterType<CacheableTypeOk>();

            var packed1 = CachedObject.Pack(obj, description);

            var packed2 = CachedObject.Pack(obj, description.AsTypeDescription);

            Assert.AreEqual(packed1, packed2);

            const int iterations = 100000;

            var watch = new Stopwatch();
            watch.Start();


            for (var i = 0; i < iterations; i++) packed1 = CachedObject.Pack(obj, description);


            Console.WriteLine($"{iterations} iterations with reflexion took {watch.ElapsedMilliseconds} ms");

            var desc = description.AsTypeDescription;

            watch.Restart();


            for (var i = 0; i < iterations; i++) packed2 = CachedObject.Pack(obj, desc);

            Console.WriteLine($"{iterations} iterations with json packing took {watch.ElapsedMilliseconds} ms");
        }

        [Test]
        public void TestCompiledGetterVsReflexion()
        {
            var obj = GetObject1();

            var prop = typeof(CacheableTypeOk).GetProperty("PrimaryKey");

            var getter = GetPropGetter<CacheableTypeOk>("PrimaryKey");


            // warm up
            var val1 = prop.GetValue(obj);

            var val2 = getter(obj);

            Assert.AreEqual(11, val1);
            Assert.AreEqual(11, val2);
            const int iterations = 1000000;

            var watch = new Stopwatch();
            watch.Start();

            var sum = 0;

            for (var i = 0; i < iterations; i++)
            {
                val1 = prop.GetValue(obj);
                sum += (int) val1;
            }

            Assert.AreEqual(iterations * 11, sum);
            Console.WriteLine($"{iterations} iterations with reflexion took {watch.ElapsedMilliseconds} ms");


            watch.Restart();

            sum = 0;
            for (var i = 0; i < iterations; i++)
            {
                val1 = getter(obj);
                sum += (int) val1;
            }

            Assert.AreEqual(iterations * 11, sum);

            Console.WriteLine($"{iterations} iterations with compiled getter took {watch.ElapsedMilliseconds} ms");
        }


        [Test]
        public void TestPackedObjectIsSerializable()
        {
            ClientSideTypeDescription.RegisterType(typeof(CacheableTypeOk));
        }

        [Test]
        public void TestPackObject()
        {
            var object1 = GetObject1();
            ClientSideTypeDescription.RegisterType(typeof(CacheableTypeOk));

            var cached = CachedObject.Pack(object1);

            Assert.IsNotNull(cached);
            Assert.IsNotNull(cached.PrimaryKey);
            Assert.AreEqual(cached.PrimaryKey, 11);

            Assert.IsNotNull(cached.UniqueKeys);
            Assert.AreEqual(cached.UniqueKeys.Length, 1);
            Assert.AreEqual(cached.UniqueKeys[0], 1);

            Assert.IsNotNull(cached.IndexKeys);
            Assert.AreEqual(cached.IndexKeys.Length, 4);

            foreach (var key in cached.IndexKeys)
            {
                if (key.KeyName == "IndexKeyDate")
                {
                    Assert.AreEqual(key, new DateTime(2009, 10, 25).Ticks);
                    Assert.AreEqual(key.KeyDataType, KeyDataType.IntKey);
                }

                if (key.KeyName == "IndexKeyValue")
                {
                    Assert.AreEqual(key, 15);
                    Assert.AreEqual(key.KeyDataType, KeyDataType.IntKey);
                }

                if (key.KeyDataType == KeyDataType.StringKey)
                {
                    Assert.AreEqual(key, "FOL");
                    Assert.AreEqual(key.KeyName, "IndexKeyFolder");
                }
            }

            var fromCache = CachedObject.Unpack<CacheableTypeOk>(cached);
            Assert.AreEqual(object1, fromCache);
        }


        [Test]
        public void TestObjectWithServerSideValues()
        {
            var config = new ClientConfig();
            config.LoadFromFile("ClientConfigOrder.xml");

            var serverSide = config.TypeDescriptions.Single().Value.Keys.Values.Where(k => k.KeyType == KeyType.ServerSideValue).ToList();

            Assert.AreEqual(2, serverSide.Count);
            Assert.AreEqual("Amount", serverSide[0].PropertyName);
            Assert.AreEqual(KeyDataType.Default, serverSide[0].KeyDataType);
            Assert.AreEqual(KeyType.ServerSideValue, serverSide[0].KeyType);

            // register with configuration file
            var description = ClientSideTypeDescription.RegisterType(typeof(Order), config.TypeDescriptions.Values.Single());

            Assert.AreEqual(2, description.ServerSideValues.Count());
            Assert.AreEqual("Amount", description.ServerSideValues.Single(v=>v.Name == "Amount").Name);

            var desc = description.AsTypeDescription;
            Assert.AreEqual(2, desc.ServerSideValues.Count);
            Assert.AreEqual("Amount", desc.ServerSideValues.Single(v=>v.Name == "Amount").Name);
            Assert.AreEqual(KeyType.ServerSideValue, desc.ServerSideValues.Single(v=>v.Name == "Amount").KeyType);

            // now try attributes on type
            var description1 = ClientSideTypeDescription.RegisterType(typeof(Order), config.TypeDescriptions.Values.Single());

            Assert.AreEqual(2, description1.ServerSideValues.Count());
            Assert.AreEqual("Amount", description1.ServerSideValues.Single(v=>v.Name == "Amount").Name);

            var desc1 = description1.AsTypeDescription;
            Assert.AreEqual(2, desc1.ServerSideValues.Count);
            Assert.AreEqual("Amount", desc1.ServerSideValues.Single(v=>v.Name == "Amount").Name);
            Assert.AreEqual(KeyType.ServerSideValue, desc1.ServerSideValues.Single(v=>v.Name == "Amount").KeyType);

            // check that we get the same description from configuration types and with tags
            Assert.AreEqual(desc, desc1);

            
            //var desc2 = Description.AddProperty("Id", KeyType.Primary)
            //    .AddProperty("Amount",KeyType.ScalarIndex, true)
            //    .AddProperty("Quantity", KeyType.ServerSideValue)
            //    .Ad 


            // pack an object using different kinds of type description
            var order = new Order
            {
                Amount = 123.45, Date = DateTimeOffset.Now, Category = "geek", ClientId = 101, ProductId = 401,
                Id = Guid.NewGuid(),
                Quantity = 2
            };

            var packed = CachedObject.Pack(order, description);
            Assert.AreEqual(2, packed.Values.Length);
            Assert.AreEqual("Amount", packed.Values[0].Name);
            Assert.AreEqual(order.Amount, packed.Values[0].Value);
            Assert.AreEqual("Quantity", packed.Values[1].Name);
            Assert.AreEqual(order.Quantity, packed.Values[1].Value);

            var packed1 = CachedObject.Pack(order, desc);
            Assert.AreEqual(2, packed1.Values.Length);
            Assert.AreEqual("Amount", packed1.Values[0].Name);
            Assert.AreEqual(order.Amount, packed1.Values[0].Value);
            Assert.AreEqual("Quantity", packed1.Values[1].Name);
            Assert.AreEqual(order.Quantity, packed1.Values[1].Value);

            var json = JsonConvert.SerializeObject(order);
            var packed2 = CachedObject.PackJson(json, desc);
            Assert.AreEqual(2, packed2.Values.Length);
            Assert.AreEqual("Amount", packed1.Values[0].Name);
            Assert.AreEqual(order.Amount, packed2.Values[0].Value);


        }


        [Test]
        public void PackObjectUsingFluentTypeDescription()
        {
            var description = Description.New("UnitTests.TestData.Order")
                .PrimaryKey("Id")
                .AddIndex("Amount", true, true)
                .AddServerSideValue("Quantity")
                .AddIndex("Category")
                .AddIndex("ProductId")
                .AddIndex("ClientId")
                .AddIndex("Date")
                .AddIndex("DayOfWeek")
                .AddIndex("Month")
                .AddIndex("Year");

            var description1 = ClientSideTypeDescription.RegisterType<Order>().AsTypeDescription;

            Assert.AreEqual(description, description1);
        }
    }
}