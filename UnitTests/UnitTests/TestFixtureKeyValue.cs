using System;
using System.Globalization;
using Client.Core;
using NUnit.Framework;
using NUnit.Framework.Legacy;
using Tests.TestTools;
using static System.DateTime;
#pragma warning disable S1199

namespace Tests.UnitTests
{
    [TestFixture]
    public class TestFixtureKeyValue
    {

        /// <summary>
        /// All kinds of dates
        /// </summary>
        public class Dates
        {
            public DateTime DateOnly1 { get; set; }
            public DateTime DateTime1 { get; set; }

            public DateTimeOffset DateOnly2 { get; set; }
            public DateTimeOffset DateTime2 { get; set; }

            public DateOnly DateOnly { get; set; }
        }


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

                Assert.That(testValue, Is.EqualTo(tst));
                
                var date2 = new KeyValue(Today);

                ClassicAssert.AreEqual(date, date2);
                Assert.That(date, Is.EqualTo(date2));
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

        /// <summary>
        /// DateOnly is always formatted as yyyy-MM-dd
        /// </summary>
        /// <param name="input"></param>
        /// <param name="kind"></param>
        /// <param name="output"></param>
        [Test]
        [TestCase("2010-12-24", "2010-12-24")]
        [TestCase("24/12/2010", "2010-12-24")]
        [TestCase("24-12-2010", "2010-12-24")]
        public void Parsing_and_formatting_date_only(string input, string output)
        {
            var dt1 = DateHelper.ParseDateOnly(input);

            Assert.That(dt1, Is.Not.Null);
            
            var str1 = DateHelper.FormatDateOnly(dt1.Value);
            

            Assert.That(str1, Is.EqualTo(output));
            

        }

        /// <summary>
        /// DateTime is always parsed as UTC if no offset is specified, or converted to UTC otherwise
        /// </summary>
        /// <param name="input"></param>
        /// <param name="kind"></param>
        /// <param name="output"></param>
        [Test]
        [TestCase("2010-12-24", DateTimeKind.Utc, "2010-12-24")]
        [TestCase("24/12/2010", DateTimeKind.Utc, "2010-12-24")]
        [TestCase("24-12-2010", DateTimeKind.Utc, "2010-12-24")]
        [TestCase("2010-12-24 10:03:45", DateTimeKind.Utc, "2010-12-24T10:03:45.0000000Z")]
        [TestCase("2010-12-24 10:03", DateTimeKind.Utc, "2010-12-24T10:03:00.0000000Z")]
        [TestCase("2010-12-24T10:30:06.456+02:00", DateTimeKind.Utc, "2010-12-24T08:30:06.4560000Z")]
        public void Parsing_and_formatting_date_time(string input, DateTimeKind kind, string output)
        {
            var dt1 = DateHelper.ParseDateTime(input);

            Assert.That(dt1, Is.Not.Null);
            Assert.That(dt1.Value.Kind, Is.EqualTo(kind));

            var str1 = DateHelper.FormatDateTime(dt1.Value);
            

            Assert.That(str1, Is.EqualTo(output));
            

        }

        /// <summary>
        /// DateTimeOffset is always parsed as UTC if no offset is specified.
        /// If an offset is specified it is restored as is
        /// </summary>
        /// <param name="input"></param>
        /// <param name="offset"> offset as HH.mm</param>
        /// <param name="output"></param>
        [Test]
        [TestCase("2010-12-24", "00:00", "2010-12-24")]
        [TestCase("24/12/2010", "00:00", "2010-12-24")]
        [TestCase("24-12-2010", "00:00", "2010-12-24")]
        [TestCase("2010-12-24 10:03:45", "00:00", "2010-12-24T10:03:45.0000000+00:00")]
        [TestCase("2010-12-24 10:03", "00:00", "2010-12-24T10:03:00.0000000+00:00")]
        [TestCase("2010-12-24T10:30:06.456+02:00", "02:00", "2010-12-24T10:30:06.4560000+02:00")]
        public void Parsing_and_formatting_date_time_offset(string input, string offset, string output)
        {
            var dt1 = DateHelper.ParseDateTimeOffset(input);

            Assert.That(dt1, Is.Not.Null);
            Assert.That(dt1.Value.Offset, Is.EqualTo(TimeSpan.ParseExact(offset, "g",CultureInfo.InvariantCulture)));

            var str1 = DateHelper.FormatDateTimeOffset(dt1.Value);
            

            Assert.That(str1, Is.EqualTo(output));
            

        }

        [Test]
        public void Format_then_parse_produces_the_original_value()
        {
            var dt1 = Today;
            Assert.That(dt1, Is.EqualTo(DateHelper.ParseDateTime(DateHelper.FormatDateTime(dt1))));
            
            var dt2 = Now;
            Assert.That(dt2.ToUniversalTime(), Is.EqualTo(DateHelper.ParseDateTime(DateHelper.FormatDateTime(dt2))));

            var dt3 = UtcNow;
            Assert.That(dt3, Is.EqualTo(DateHelper.ParseDateTime(DateHelper.FormatDateTime(dt3))));

            DateTimeOffset dto1 = UtcNow.Date;
            Assert.That(dto1, Is.EqualTo(DateHelper.ParseDateTimeOffset(DateHelper.FormatDateTimeOffset(dto1))));

        }

        [Test]
        public void Format_datetime_as_string_and_json()
        {

            var dates = new Dates
            {
                DateOnly = new DateOnly(2010, 05, 04),
                DateOnly1 = Today,
                DateOnly2 = UtcNow.Date,
                DateTime1 = Now,
                DateTime2 = DateTimeOffset.UtcNow
            };

            var json = SerializationHelper.ObjectToCompactJson(dates);

            var dates1 = SerializationHelper.ObjectFromCompactJson<Dates>(json);

            var json1 = SerializationHelper.ObjectToCompactJson(dates1);

            Assert.That(dates.DateOnly, Is.EqualTo(dates1.DateOnly));
            Assert.That(dates.DateOnly1, Is.EqualTo(dates1.DateOnly1));
            Assert.That(dates.DateOnly2, Is.EqualTo(dates1.DateOnly2));
            Assert.That(dates.DateTime1.ToUniversalTime(), Is.EqualTo(dates1.DateTime1));
            Assert.That(dates.DateTime2, Is.EqualTo(dates1.DateTime2));

            Assert.That(json, Is.EqualTo(json1));

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