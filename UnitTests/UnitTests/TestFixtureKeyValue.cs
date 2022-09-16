using Client.Core;
using NUnit.Framework;

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

                Assert.IsTrue(kv1 < kv2);
                Assert.IsTrue(kv1 <= kv2);
                Assert.AreEqual(kv1, kv1);
            }

            {
                var kv1 = new KeyValue(1.9);
                var kv2 = new KeyValue(1.99);

                Assert.IsTrue(kv1 < kv2);
                Assert.IsTrue(kv1 <= kv2);
                Assert.AreEqual(kv1, kv1);
            }

            {
                var kv1 = new KeyValue(false);
                var kv2 = new KeyValue(true);

                Assert.IsTrue(kv1 < kv2);
                Assert.IsTrue(kv1 <= kv2);
                Assert.AreEqual(kv1, kv1);
            }

            {
                var kv1 = new KeyValue("aba");
                var kv2 = new KeyValue("b");

                Assert.IsTrue(kv1 < kv2);
                Assert.IsTrue(kv1 <= kv2);
                Assert.AreEqual(kv1, kv1);
            }


        }


        [Test]
        public void Compare_different_types()
        {
            {
                var kv1 = new KeyValue(1);
                var kv2 = new KeyValue(1.2);

                Assert.AreEqual(KeyValue.OriginalType.SomeInteger, kv1.Type);
                Assert.AreEqual(KeyValue.OriginalType.SomeFloat, kv2.Type);


                Assert.IsTrue(kv1 < kv2);
                Assert.IsTrue(kv1 <= kv2);
                Assert.AreEqual(kv1, kv1);
                Assert.AreEqual(kv2, kv2);
            }

            {
                var kv1 = new KeyValue(0.5);
                var kv2 = new KeyValue(1);

                Assert.AreEqual(KeyValue.OriginalType.SomeInteger, kv2.Type);
                Assert.AreEqual(KeyValue.OriginalType.SomeFloat, kv1.Type);


                Assert.IsTrue(kv1 < kv2);
                Assert.IsTrue(kv1 <= kv2);
                Assert.AreEqual(kv1, kv1);
                Assert.AreEqual(kv2, kv2);
            }
        }

    }
}