using Client.Core;
using Client.Messages;
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
                var kv1 = new KeyValue(1, new KeyInfo());
                var kv2 = new KeyValue(2, new KeyInfo());

                Assert.IsTrue(kv1 < kv2);
                Assert.IsTrue(kv1 <= kv2);
                Assert.AreEqual(kv1, kv1);
            }

            {
                var kv1 = new KeyValue(1.9, new KeyInfo());
                var kv2 = new KeyValue(1.99, new KeyInfo());

                Assert.IsTrue(kv1 < kv2);
                Assert.IsTrue(kv1 <= kv2);
                Assert.AreEqual(kv1, kv1);
            }

            {
                var kv1 = new KeyValue(false, new KeyInfo());
                var kv2 = new KeyValue(true, new KeyInfo());

                Assert.IsTrue(kv1 < kv2);
                Assert.IsTrue(kv1 <= kv2);
                Assert.AreEqual(kv1, kv1);
            }

            {
                var kv1 = new KeyValue("aba", new KeyInfo());
                var kv2 = new KeyValue("b", new KeyInfo());

                Assert.IsTrue(kv1 < kv2);
                Assert.IsTrue(kv1 <= kv2);
                Assert.AreEqual(kv1, kv1);
            }


        }


        [Test]
        public void Compare_different_types()
        {
            {
                var kv1 = new KeyValue(1, new KeyInfo());
                var kv2 = new KeyValue(1.2, new KeyInfo());

                Assert.AreEqual(KeyValue.OriginalType.SomeInteger, kv1.Type);
                Assert.AreEqual(KeyValue.OriginalType.SomeFloat, kv2.Type);


                Assert.IsTrue(kv1 < kv2);
                Assert.IsTrue(kv1 <= kv2);
                Assert.AreEqual(kv1, kv1);
                Assert.AreEqual(kv2, kv2);
            }

            {
                var kv1 = new KeyValue(0.5, new KeyInfo());
                var kv2 = new KeyValue(1, new KeyInfo());

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