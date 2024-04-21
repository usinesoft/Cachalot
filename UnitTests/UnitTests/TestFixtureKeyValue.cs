using System;
using Client.Core;
using Client.Messages;
using Client.Tools;
using NUnit.Framework;
using NUnit.Framework.Legacy;
using Tests.TestTools;
using static System.DateTime;

namespace Tests.UnitTests
{
    [TestFixture]
    public class TestFixtureKeyValue
    {
        [Test]
        public void Compare_same_type()
        {
            {
                var kv1 = new KeyValue(1);
                var kv2 = new KeyValue(2);

                ClassicAssert.IsTrue(kv1 < kv2);
                ClassicAssert.IsTrue(kv1 <= kv2);
                ClassicAssert.AreEqual(kv1, kv1);
            }

            {
                var kv1 = new KeyValue(1.9);
                var kv2 = new KeyValue(1.99);

                ClassicAssert.IsTrue(kv1 < kv2);
                ClassicAssert.IsTrue(kv1 <= kv2);
                ClassicAssert.AreEqual(kv1, kv1);
            }

            {
                var kv1 = new KeyValue(false);
                var kv2 = new KeyValue(true);

                ClassicAssert.IsTrue(kv1 < kv2);
                ClassicAssert.IsTrue(kv1 <= kv2);
                ClassicAssert.AreEqual(kv1, kv1);
            }

            {
                var kv1 = new KeyValue("aba");
                var kv2 = new KeyValue("b");

                ClassicAssert.IsTrue(kv1 < kv2);
                ClassicAssert.IsTrue(kv1 <= kv2);
                ClassicAssert.AreEqual(kv1, kv1);
            }
        }


        [Test]
        public void Compare_different_types()
        {
            {
                var kv1 = new KeyValue(1);
                var kv2 = new KeyValue(1.2);

                ClassicAssert.AreEqual(KeyValue.OriginalType.SomeInteger, kv1.Type);
                ClassicAssert.AreEqual(KeyValue.OriginalType.SomeFloat, kv2.Type);


                ClassicAssert.IsTrue(kv1 < kv2);
                ClassicAssert.IsTrue(kv1 <= kv2);
                ClassicAssert.AreEqual(kv1, kv1);
                ClassicAssert.AreEqual(kv2, kv2);
            }

            {
                var kv1 = new KeyValue(0.5);
                var kv2 = new KeyValue(1);

                ClassicAssert.AreEqual(KeyValue.OriginalType.SomeInteger, kv2.Type);
                ClassicAssert.AreEqual(KeyValue.OriginalType.SomeFloat, kv1.Type);


                ClassicAssert.IsTrue(kv1 < kv2);
                ClassicAssert.IsTrue(kv1 <= kv2);
                ClassicAssert.AreEqual(kv1, kv1);
                ClassicAssert.AreEqual(kv2, kv2);
            }
        }

        [Test]
        public void Equality_float_and_int()
        {
            var kv1 = new KeyValue(1000M);
            var kv2 = new KeyValue(1000);

            ClassicAssert.AreEqual(KeyValue.OriginalType.SomeFloat, kv1.Type);
            ClassicAssert.AreEqual(KeyValue.OriginalType.SomeInteger, kv2.Type);

            ClassicAssert.AreEqual(kv1, kv2);
        }


        [Test]
        public void Date_does_not_use_local_timezone()
        {
            KeyValue date;

            DateTime testValue;

            

            using (var tz = new FakeLocalTimeZone(TimeZoneInfo.Utc))
            {
                testValue = Today;

                date = new KeyValue(testValue);
            }

            using (var tz = new FakeLocalTimeZone(TimeZoneInfo.FindSystemTimeZoneById("Pacific Standard Time")))
            {
                var tst = date!.DateValue!.Value!.UtcDateTime;

                ClassicAssert.AreEqual(testValue, tst);


                var date2 = new KeyValue(Today);

                ClassicAssert.AreEqual(date, date2);
            }
        }

        [Test]
        public void Date_with_time_does_not_use_local_timezone()
        {
            KeyValue date = null;

            DateTime testValue;

            using (var tz = new FakeLocalTimeZone(TimeZoneInfo.Utc))
            {
                testValue = Now;

                date = new KeyValue(testValue);
            }

            using (var tz = new FakeLocalTimeZone(TimeZoneInfo.FindSystemTimeZoneById("Pacific Standard Time")))
            {
                var tst = date!.DateValue!.Value!.UtcDateTime;

                ClassicAssert.AreEqual(testValue, tst);

            }
        }

        [Test]
        public void Format_datetime_as_string_and_json()
        {
            var date1 = DateTime.UtcNow; 
            var date2 = DateTime.Today;

            var kv1 = new KeyValue(date1);
            var kv2 = new KeyValue(date2);

            var str1 = kv1.ToString();
            var str2 = kv2.ToString();

            var json1 = kv1.ToJsonValue();
            var json2 = kv2.ToJsonValue();

            var fromJson1 = json1.GetValue<DateTime>();
            var fromJson2 = json2.GetValue<DateTime>();

            ClassicAssert.AreEqual(date1, fromJson1);
            ClassicAssert.AreEqual(date2, fromJson2);

        }

        [Test]
        public void Format_datetimeoffset_as_string_and_json()
        {
            var date1 = DateTimeOffset.Now;
            

            var kv1 = new KeyValue(date1);
            

            var str1 = kv1.ToString();
            

            var json1 = kv1.ToJsonValue();
            

            var fromJson1 = json1.GetValue<DateTimeOffset>();

            

            ClassicAssert.AreEqual(date1, fromJson1);
            
            

        }
    }
}