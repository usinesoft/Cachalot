#region

using System;
using System.Diagnostics;
using System.Linq.Expressions;
using Client.Core;
using Client.Interface;
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
    }
}