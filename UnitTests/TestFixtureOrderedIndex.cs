#region

using System;
using System.Collections.Generic;
using System.Linq;
using Client.Core;
using Client.Interface;
using Client.Messages;
using Client.Queries;
using NUnit.Framework;
using Server;
using UnitTests.TestData;

#endregion

namespace UnitTests
{
    [TestFixture]
    public class TestFixtureOrderedIndex
    {
        private static IList<KeyValue> MakeIntValue(int value, KeyInfo type)
        {
            return new List<KeyValue> {new KeyValue(value, type)};
        }

        private static IList<KeyValue> MakeStringValue(string value, KeyInfo type)
        {
            return new List<KeyValue> {new KeyValue(value, type)};
        }

        private static void checkLE(IndexBase indexByValue)
        {
            var keyType = new KeyInfo(KeyDataType.IntKey, KeyType.ScalarIndex, "test", true);

            //non existent value in the middle
            {
                var result1 = indexByValue.GetMany(MakeIntValue(12, keyType), QueryOperator.Le);

                Assert.AreEqual(result1.Count, 3);

                var count = indexByValue.GetCount(MakeIntValue(12, keyType), QueryOperator.Le);

                Assert.AreEqual(count, 3);
            }

            //existent value in the middle
            {
                var result1 = indexByValue.GetMany(MakeIntValue(13, keyType), QueryOperator.Le);
                Assert.AreEqual(result1.Count, 4);

                var count = indexByValue.GetCount(MakeIntValue(13, keyType), QueryOperator.Le);
                Assert.AreEqual(count, 4);
            }

            //value < all
            {
                var result1 = indexByValue.GetMany(MakeIntValue(0, keyType), QueryOperator.Le);
                Assert.AreEqual(result1.Count, 0);

                var count = indexByValue.GetCount(MakeIntValue(0, keyType), QueryOperator.Le);
                Assert.AreEqual(count, 0);
            }

            //value > all
            {
                var result1 = indexByValue.GetMany(MakeIntValue(99, keyType), QueryOperator.Le);
                Assert.AreEqual(result1.Count, 6);

                var count = indexByValue.GetCount(MakeIntValue(99, keyType), QueryOperator.Le);
                Assert.AreEqual(count, 6);
            }

            //first value
            {
                var result1 =
                    indexByValue.GetMany(MakeIntValue(1, keyType), QueryOperator.Le).OrderBy(o => o.PrimaryKey)
                        .ToList();
                Assert.AreEqual(result1.Count, 1);
                Assert.AreEqual(result1[0].PrimaryKey.ToString(), "#1");


                var count = indexByValue.GetCount(MakeIntValue(1, keyType), QueryOperator.Le);
                Assert.AreEqual(count, 1);
            }
        }

        private static void checkLS(IndexBase indexByValue)
        {
            var keyType = new KeyInfo(KeyDataType.IntKey, KeyType.ScalarIndex, "IndexKeyValue", true);

            //non existent value in the middle
            {
                IList<CachedObject> result1 =
                    indexByValue.GetMany(MakeIntValue(12, keyType), QueryOperator.Lt)
                        .OrderBy(o => o.PrimaryKey)
                        .ToList();
                Assert.AreEqual(result1.Count, 3);
                Assert.AreEqual(result1[0].PrimaryKey.ToString(), "#1");
                Assert.AreEqual(result1[1].PrimaryKey.ToString(), "#2");
                Assert.AreEqual(result1[2].PrimaryKey.ToString(), "#3");

                var count = indexByValue.GetCount(MakeIntValue(12, keyType), QueryOperator.Lt);
                Assert.AreEqual(count, 3);
            }

            //existent value in the middle
            {
                IList<CachedObject> result1 =
                    indexByValue.GetMany(MakeIntValue(13, keyType), QueryOperator.Lt)
                        .OrderBy(o => o.PrimaryKey)
                        .ToList();
                Assert.AreEqual(result1.Count, 3);
                Assert.AreEqual(result1[2].PrimaryKey.ToString(), "#3");

                var count = indexByValue.GetCount(MakeIntValue(12, keyType), QueryOperator.Lt);
                Assert.AreEqual(count, 3);
            }

            //value < all
            {
                IList<CachedObject> result1 =
                    indexByValue.GetMany(MakeIntValue(0, keyType), QueryOperator.Lt).OrderBy(o => o.PrimaryKey)
                        .ToList();
                Assert.AreEqual(result1.Count, 0);

                var count = indexByValue.GetCount(MakeIntValue(0, keyType), QueryOperator.Lt);
                Assert.AreEqual(count, 0);
            }

            //value > all
            {
                IList<CachedObject> result1 =
                    indexByValue.GetMany(MakeIntValue(99, keyType), QueryOperator.Lt)
                        .OrderBy(o => o.PrimaryKey)
                        .ToList();
                Assert.AreEqual(result1.Count, 6);

                var count = indexByValue.GetCount(MakeIntValue(99, keyType), QueryOperator.Lt);
                Assert.AreEqual(count, 6);
            }

            //first value
            {
                IList<CachedObject> result1 =
                    indexByValue.GetMany(MakeIntValue(1, keyType), QueryOperator.Lt).OrderBy(o => o.PrimaryKey)
                        .ToList();
                Assert.AreEqual(result1.Count, 0);

                var count = indexByValue.GetCount(MakeIntValue(1, keyType), QueryOperator.Lt);
                Assert.AreEqual(count, 0);
            }
        }

        private static OrderedIndex populate(params int[] valueKeys)
        {
            //register the type to get a valid TypeDescription
            //the type description is used to create CachedObjects from objects of the registered type
            var typeDescription = ClientSideTypeDescription.RegisterType(typeof(CacheableTypeOk));

            KeyInfo valueKey = null;

            foreach (var keyInfo in typeDescription.IndexFields)
                if (keyInfo.Name == "IndexKeyValue")
                    valueKey = keyInfo.AsKeyInfo;

            Assert.IsNotNull(valueKey);

            var index = new OrderedIndex(valueKey);
            for (var i = 0; i < valueKeys.Length; i++)
                index.Put(CachedObject.Pack(new CacheableTypeOk(i, 106, "A", DateTime.Now, valueKeys[i])));

            return index;
        }


        private bool IsOrdered(List<int> list, Comparison<int> compare)
        {
            for (var i = 0; i < list.Count - 1; i++)
                if (compare(list[i], list[i + 1]) > 0)
                    return false;

            return true;
        }

        [Test]
        public void Between()
        {
            var keyType = new KeyInfo(KeyDataType.IntKey, KeyType.ScalarIndex, "test", true);

            var idx1 = populate(1, 2, 3, 3, 3, 4, 5);

            {
                var count = idx1.GetCount(new List<KeyValue> {new KeyValue(3, keyType), new KeyValue(3, keyType)},
                    QueryOperator.Btw);

                Assert.AreEqual(3, count);

                var items =
                    idx1.GetMany(new List<KeyValue> {new KeyValue(3, keyType), new KeyValue(3, keyType)},
                        QueryOperator.Btw);

                Assert.AreEqual(3, items.Count);
            }


            {
                var count = idx1.GetCount(new List<KeyValue> {new KeyValue(8, keyType), new KeyValue(9, keyType)},
                    QueryOperator.Btw);

                Assert.AreEqual(0, count);

                var items =
                    idx1.GetMany(new List<KeyValue> {new KeyValue(8, keyType), new KeyValue(9, keyType)},
                        QueryOperator.Btw);

                Assert.AreEqual(0, items.Count);
            }

            {
                var count = idx1.GetCount(new List<KeyValue> {new KeyValue(1, keyType), new KeyValue(3, keyType)},
                    QueryOperator.Btw);

                Assert.AreEqual(5, count);

                var items =
                    idx1.GetMany(new List<KeyValue> {new KeyValue(1, keyType), new KeyValue(3, keyType)},
                        QueryOperator.Btw);

                Assert.AreEqual(5, items.Count);
            }
        }

        [Test]
        public void EQ()
        {
            var keyType = new KeyInfo(KeyDataType.IntKey, KeyType.ScalarIndex, "test", true);

            //many in the middle
            var idx1 = populate(1, 2, 3, 3, 3, 4, 5);

            IList<CachedObject> result1 =
                idx1.GetMany(MakeIntValue(3, keyType)).OrderBy(o => o.PrimaryKey).ToList();
            Assert.AreEqual(result1.Count, 3);
            Assert.AreEqual(result1[0].PrimaryKey, 2);
            Assert.AreEqual(result1[1].PrimaryKey, 3);
            Assert.AreEqual(result1[2].PrimaryKey, 4);

            var count = idx1.GetCount(MakeIntValue(3, keyType));
            Assert.AreEqual(count, 3);


            //many at the end
            var idx2 = populate(1, 2, 3, 3, 3);

            result1 = idx2.GetMany(MakeIntValue(3, keyType)).OrderBy(o => o.PrimaryKey).ToList();
            Assert.AreEqual(result1.Count, 3);
            Assert.AreEqual(result1[0].PrimaryKey, 2);
            Assert.AreEqual(result1[1].PrimaryKey, 3);
            Assert.AreEqual(result1[2].PrimaryKey, 4);

            count = idx1.GetCount(MakeIntValue(3, keyType));
            Assert.AreEqual(count, 3);

            //many at the beginning
            var idx3 = populate(3, 3, 3, 4, 4, 80);

            result1 = idx3.GetMany(MakeIntValue(3, keyType)).OrderBy(o => o.PrimaryKey).ToList();
            Assert.AreEqual(result1.Count, 3);
            Assert.AreEqual(result1[0].PrimaryKey, 0);
            Assert.AreEqual(result1[1].PrimaryKey, 1);
            Assert.AreEqual(result1[2].PrimaryKey, 2);

            count = idx1.GetCount(MakeIntValue(3, keyType));
            Assert.AreEqual(count, 3);

            //all equal
            var idx4 = populate(3, 3, 3);

            result1 = idx4.GetMany(MakeIntValue(3, keyType)).OrderBy(o => o.PrimaryKey).ToList();
            Assert.AreEqual(result1.Count, 3);
            Assert.AreEqual(result1[0].PrimaryKey, 0);
            Assert.AreEqual(result1[1].PrimaryKey, 1);
            Assert.AreEqual(result1[2].PrimaryKey, 2);

            count = idx1.GetCount(MakeIntValue(3, keyType));
            Assert.AreEqual(count, 3);

            //one in the middle
            var idx5 = populate(1, 3, 5, 7, 9, 111);
            result1 = idx5.GetMany(MakeIntValue(7, keyType)).OrderBy(o => o.PrimaryKey).ToList();
            Assert.AreEqual(result1.Count, 1);
            Assert.AreEqual(result1[0].PrimaryKey, 3);

            count = idx5.GetCount(MakeIntValue(7, keyType));
            Assert.AreEqual(count, 1);

            //value not found
            result1 = idx5.GetMany(MakeIntValue(8, keyType)).OrderBy(o => o.PrimaryKey).ToList();
            Assert.AreEqual(result1.Count, 0);

            count = idx5.GetCount(MakeIntValue(8, keyType));
            Assert.AreEqual(count, 0);
        }

        [Test]
        public void ExtremeCases()
        {
            //void index

            var keyType = new KeyInfo(KeyDataType.IntKey, KeyType.ScalarIndex, "test", true);

            IndexBase index = populate();
            var result = index.GetMany(MakeIntValue(12, keyType), QueryOperator.Le);
            Assert.AreEqual(result.Count, 0);
            result = index.GetMany(MakeIntValue(12, keyType), QueryOperator.Lt);
            Assert.AreEqual(result.Count, 0);
            result = index.GetMany(MakeIntValue(12, keyType));
            Assert.AreEqual(result.Count, 0);
            result = index.GetMany(MakeIntValue(12, keyType), QueryOperator.Gt);
            Assert.AreEqual(result.Count, 0);
            result = index.GetMany(MakeIntValue(12, keyType), QueryOperator.Ge);
            Assert.AreEqual(result.Count, 0);


            //one element index, value not found
            index = populate(15);
            result = index.GetMany(MakeIntValue(12, keyType), QueryOperator.Le);
            Assert.AreEqual(result.Count, 0);
            result = index.GetMany(MakeIntValue(12, keyType), QueryOperator.Lt);
            Assert.AreEqual(result.Count, 0);
            result = index.GetMany(MakeIntValue(12, keyType));
            Assert.AreEqual(result.Count, 0);
            result = index.GetMany(MakeIntValue(12, keyType), QueryOperator.Gt);
            Assert.AreEqual(result.Count, 1);
            result = index.GetMany(MakeIntValue(12, keyType), QueryOperator.Ge);
            Assert.AreEqual(result.Count, 1);


            //one element index, value found
            index = populate(12);
            result = index.GetMany(MakeIntValue(12, keyType), QueryOperator.Le);
            Assert.AreEqual(result.Count, 1);
            result = index.GetMany(MakeIntValue(12, keyType), QueryOperator.Lt);
            Assert.AreEqual(result.Count, 0);
            result = index.GetMany(MakeIntValue(12, keyType));
            Assert.AreEqual(result.Count, 1);
            result = index.GetMany(MakeIntValue(12, keyType), QueryOperator.Gt);
            Assert.AreEqual(result.Count, 0);
            result = index.GetMany(MakeIntValue(12, keyType), QueryOperator.Ge);
            Assert.AreEqual(result.Count, 1);

            //two element index (different values)
            index = populate(12, 15);
            result = index.GetMany(MakeIntValue(12, keyType), QueryOperator.Le);
            Assert.AreEqual(result.Count, 1);
            result = index.GetMany(MakeIntValue(12, keyType), QueryOperator.Lt);
            Assert.AreEqual(result.Count, 0);
            result = index.GetMany(MakeIntValue(12, keyType));
            Assert.AreEqual(result.Count, 1);
            result = index.GetMany(MakeIntValue(12, keyType), QueryOperator.Gt);
            Assert.AreEqual(result.Count, 1);
            result = index.GetMany(MakeIntValue(12, keyType), QueryOperator.Ge);
            Assert.AreEqual(result.Count, 2);


            //two element index (same value)
            index = populate(12, 12);
            result = index.GetMany(MakeIntValue(12, keyType), QueryOperator.Le);
            Assert.AreEqual(result.Count, 2);
            result = index.GetMany(MakeIntValue(12, keyType), QueryOperator.Lt);
            Assert.AreEqual(result.Count, 0);
            result = index.GetMany(MakeIntValue(12, keyType));
            Assert.AreEqual(result.Count, 2);
            result = index.GetMany(MakeIntValue(12, keyType), QueryOperator.Gt);
            Assert.AreEqual(result.Count, 0);
            result = index.GetMany(MakeIntValue(12, keyType), QueryOperator.Ge);
            Assert.AreEqual(result.Count, 2);


            //three element index (same value ==)
            index = populate(12, 12, 12);
            result = index.GetMany(MakeIntValue(12, keyType), QueryOperator.Le);
            Assert.AreEqual(result.Count, 3);
            result = index.GetMany(MakeIntValue(12, keyType), QueryOperator.Lt);
            Assert.AreEqual(result.Count, 0);
            result = index.GetMany(MakeIntValue(12, keyType));
            Assert.AreEqual(result.Count, 3);
            result = index.GetMany(MakeIntValue(12, keyType), QueryOperator.Gt);
            Assert.AreEqual(result.Count, 0);
            result = index.GetMany(MakeIntValue(12, keyType), QueryOperator.Ge);
            Assert.AreEqual(result.Count, 3);

            //three element index (same value !=)
            index = populate(15, 15, 15);
            result = index.GetMany(MakeIntValue(12, keyType), QueryOperator.Le);
            Assert.AreEqual(result.Count, 0);
            result = index.GetMany(MakeIntValue(12, keyType), QueryOperator.Lt);
            Assert.AreEqual(result.Count, 0);
            result = index.GetMany(MakeIntValue(12, keyType));
            Assert.AreEqual(result.Count, 0);
            result = index.GetMany(MakeIntValue(12, keyType), QueryOperator.Gt);
            Assert.AreEqual(result.Count, 3);
            result = index.GetMany(MakeIntValue(12, keyType), QueryOperator.Ge);
            Assert.AreEqual(result.Count, 3);
        }


        [Test]
        public void GE()
        {
            var keyType = new KeyInfo(KeyDataType.IntKey, KeyType.ScalarIndex, "test", true);

            //many in the middle
            var idx1 = populate(1, 2, 3, 3, 3, 4, 5);

            IList<CachedObject> result1 =
                idx1.GetMany(MakeIntValue(3, keyType), QueryOperator.Ge).OrderBy(o => o.PrimaryKey).ToList();
            Assert.AreEqual(result1.Count, 5);
            Assert.AreEqual(result1[0].PrimaryKey, 2);
            Assert.AreEqual(result1[1].PrimaryKey, 3);
            Assert.AreEqual(result1[2].PrimaryKey, 4);

            var count = idx1.GetCount(MakeIntValue(3, keyType), QueryOperator.Ge);
            Assert.AreEqual(count, 5);

            //many at the end
            var idx2 = populate(1, 2, 3, 3, 3);

            result1 = idx2.GetMany(MakeIntValue(3, keyType), QueryOperator.Ge).OrderBy(o => o.PrimaryKey).ToList();
            Assert.AreEqual(result1.Count, 3);
            Assert.AreEqual(result1[0].PrimaryKey, 2);
            Assert.AreEqual(result1[1].PrimaryKey, 3);
            Assert.AreEqual(result1[2].PrimaryKey, 4);

            count = idx2.GetCount(MakeIntValue(3, keyType), QueryOperator.Ge);
            Assert.AreEqual(count, 3);

            //many at the beginning
            var idx3 = populate(3, 3, 3, 4, 4, 80);

            result1 = idx3.GetMany(MakeIntValue(3, keyType), QueryOperator.Ge).OrderBy(o => o.PrimaryKey).ToList();
            Assert.AreEqual(result1.Count, 6);
            Assert.AreEqual(result1[0].PrimaryKey, 0);
            Assert.AreEqual(result1[1].PrimaryKey, 1);
            Assert.AreEqual(result1[2].PrimaryKey, 2);
            Assert.AreEqual(result1[3].PrimaryKey, 3);
            Assert.AreEqual(result1[4].PrimaryKey, 4);
            Assert.AreEqual(result1[5].PrimaryKey, 5);

            count = idx3.GetCount(MakeIntValue(3, keyType), QueryOperator.Ge);
            Assert.AreEqual(count, 6);

            //all equal
            var idx4 = populate(3, 3, 3);

            result1 = idx4.GetMany(MakeIntValue(3, keyType), QueryOperator.Ge).OrderBy(o => o.PrimaryKey).ToList();
            Assert.AreEqual(result1.Count, 3);
            Assert.AreEqual(result1[0].PrimaryKey, 0);
            Assert.AreEqual(result1[1].PrimaryKey, 1);
            Assert.AreEqual(result1[2].PrimaryKey, 2);

            count = idx4.GetCount(MakeIntValue(3, keyType), QueryOperator.Ge);
            Assert.AreEqual(count, 3);


            //one in the middle
            var idx5 = populate(1, 3, 5, 7, 9, 111);
            result1 = idx5.GetMany(MakeIntValue(7, keyType), QueryOperator.Ge).OrderBy(o => o.PrimaryKey).ToList();
            Assert.AreEqual(result1.Count, 3);
            Assert.AreEqual(result1[0].PrimaryKey, 3);
            Assert.AreEqual(result1[1].PrimaryKey, 4);
            Assert.AreEqual(result1[2].PrimaryKey, 5);

            count = idx5.GetCount(MakeIntValue(7, keyType), QueryOperator.Ge);
            Assert.AreEqual(count, 3);


            //value not found
            result1 = idx5.GetMany(MakeIntValue(8, keyType), QueryOperator.Ge).OrderBy(o => o.PrimaryKey).ToList();
            Assert.AreEqual(result1.Count, 2);
            Assert.AreEqual(result1[0].PrimaryKey, 4);
            Assert.AreEqual(result1[1].PrimaryKey, 5);

            count = idx5.GetCount(MakeIntValue(8, keyType), QueryOperator.Ge);
            Assert.AreEqual(count, 2);
        }


        [Test]
        public void GT()
        {
            var keyType = new KeyInfo(KeyDataType.IntKey, KeyType.ScalarIndex, "test", true);

            //many in the middle
            var idx1 = populate(1, 2, 3, 3, 3, 4, 5);

            IList<CachedObject> result1 =
                idx1.GetMany(MakeIntValue(3, keyType), QueryOperator.Gt).OrderBy(o => o.PrimaryKey).ToList();
            Assert.AreEqual(result1.Count, 2);
            Assert.AreEqual(result1[0].PrimaryKey, 5);
            Assert.AreEqual(result1[1].PrimaryKey, 6);

            var count = idx1.GetCount(MakeIntValue(3, keyType), QueryOperator.Gt);
            Assert.AreEqual(count, 2);


            //many at the end
            var idx2 = populate(1, 2, 3, 3, 3);

            result1 = idx2.GetMany(MakeIntValue(3, keyType), QueryOperator.Gt).OrderBy(o => o.PrimaryKey).ToList();
            Assert.AreEqual(result1.Count, 0);

            count = idx2.GetCount(MakeIntValue(3, keyType), QueryOperator.Gt);
            Assert.AreEqual(count, 0);

            //many at the beginning
            var idx3 = populate(3, 3, 3, 4, 4, 80);

            result1 = idx3.GetMany(MakeIntValue(3, keyType), QueryOperator.Gt).OrderBy(o => o.PrimaryKey).ToList();
            Assert.AreEqual(result1.Count, 3);
            Assert.AreEqual(result1[0].PrimaryKey, 3);
            Assert.AreEqual(result1[1].PrimaryKey, 4);
            Assert.AreEqual(result1[2].PrimaryKey, 5);
            count = idx3.GetCount(MakeIntValue(3, keyType), QueryOperator.Gt);
            Assert.AreEqual(count, 3);

            //all equal
            var idx4 = populate(3, 3, 3);

            result1 = idx4.GetMany(MakeIntValue(3, keyType), QueryOperator.Gt).OrderBy(o => o.PrimaryKey).ToList();
            Assert.AreEqual(result1.Count, 0);
            count = idx4.GetCount(MakeIntValue(3, keyType), QueryOperator.Gt);
            Assert.AreEqual(count, 0);


            //one in the middle
            var idx5 = populate(1, 3, 5, 7, 9, 111);
            result1 = idx5.GetMany(MakeIntValue(7, keyType), QueryOperator.Gt).OrderBy(o => o.PrimaryKey).ToList();
            Assert.AreEqual(result1.Count, 2);
            Assert.AreEqual(result1[0].PrimaryKey, 4);
            Assert.AreEqual(result1[1].PrimaryKey, 5);

            count = idx5.GetCount(MakeIntValue(7, keyType), QueryOperator.Gt);
            Assert.AreEqual(count, 2);

            //value not found
            result1 = idx5.GetMany(MakeIntValue(8, keyType), QueryOperator.Gt).OrderBy(o => o.PrimaryKey).ToList();
            Assert.AreEqual(result1.Count, 2);
            Assert.AreEqual(result1[0].PrimaryKey, 4);
            Assert.AreEqual(result1[1].PrimaryKey, 5);

            count = idx5.GetCount(MakeIntValue(8, keyType), QueryOperator.Gt);
            Assert.AreEqual(count, 2);
        }

        /// <summary>
        ///     check for Le and Lt operators on an ordered index
        /// </summary>
        [Test]
        public void Lesser()
        {
            //register the type to get a valid TypeDescription
            //the type description is used to create CachedObjects from objects of the registered type
            var typeDescription = ClientSideTypeDescription.RegisterType(typeof(CacheableTypeOk));

            KeyInfo valueKey = null;

            foreach (var keyInfo in typeDescription.IndexFields)
                if (keyInfo.Name == "IndexKeyValue")
                    valueKey = keyInfo.AsKeyInfo;

            Assert.IsNotNull(valueKey);


            //fill in order
            {
                var indexByValue = new OrderedIndex(valueKey);

                indexByValue.Put(CachedObject.Pack(new CacheableTypeOk(1, 101, "A", DateTime.Now, 1)));
                indexByValue.Put(CachedObject.Pack(new CacheableTypeOk(2, 102, "A", DateTime.Now, 4)));
                indexByValue.Put(CachedObject.Pack(new CacheableTypeOk(3, 103, "A", DateTime.Now, 6)));
                indexByValue.Put(CachedObject.Pack(new CacheableTypeOk(4, 104, "A", DateTime.Now, 13)));
                indexByValue.Put(CachedObject.Pack(new CacheableTypeOk(5, 105, "A", DateTime.Now, 14)));
                indexByValue.Put(CachedObject.Pack(new CacheableTypeOk(6, 106, "A", DateTime.Now, 80)));

                checkLE(indexByValue);
                checkLS(indexByValue);
            }

            //fill out of order
            {
                var indexByValue = new OrderedIndex(valueKey);

                indexByValue.Put(CachedObject.Pack(new CacheableTypeOk(6, 106, "A", DateTime.Now, 80)));
                indexByValue.Put(CachedObject.Pack(new CacheableTypeOk(1, 101, "A", DateTime.Now, 1)));
                indexByValue.Put(CachedObject.Pack(new CacheableTypeOk(4, 104, "A", DateTime.Now, 13)));
                indexByValue.Put(CachedObject.Pack(new CacheableTypeOk(2, 102, "A", DateTime.Now, 4)));
                indexByValue.Put(CachedObject.Pack(new CacheableTypeOk(5, 105, "A", DateTime.Now, 14)));
                indexByValue.Put(CachedObject.Pack(new CacheableTypeOk(3, 103, "A", DateTime.Now, 6)));


                checkLE(indexByValue);
                checkLS(indexByValue);
            }

            //fill out of order transactional
            {
                var indexByValue = new OrderedIndex(valueKey);

                indexByValue.BeginFill();

                indexByValue.Put(CachedObject.Pack(new CacheableTypeOk(6, 106, "A", DateTime.Now, 80)));
                indexByValue.Put(CachedObject.Pack(new CacheableTypeOk(1, 101, "A", DateTime.Now, 1)));
                indexByValue.Put(CachedObject.Pack(new CacheableTypeOk(4, 104, "A", DateTime.Now, 13)));
                indexByValue.Put(CachedObject.Pack(new CacheableTypeOk(2, 102, "A", DateTime.Now, 4)));
                indexByValue.Put(CachedObject.Pack(new CacheableTypeOk(5, 105, "A", DateTime.Now, 14)));
                indexByValue.Put(CachedObject.Pack(new CacheableTypeOk(3, 103, "A", DateTime.Now, 6)));
                indexByValue.EndFill();

                checkLE(indexByValue);
                checkLS(indexByValue);
            }


            //all equals 
            {
                var indexByValue = new OrderedIndex(valueKey);

                var keyType = new KeyInfo(KeyDataType.IntKey, KeyType.ScalarIndex, "test", true);

                indexByValue.Put(CachedObject.Pack(new CacheableTypeOk(6, 106, "A", DateTime.Now, 45)));
                indexByValue.Put(CachedObject.Pack(new CacheableTypeOk(1, 101, "A", DateTime.Now, 45)));
                indexByValue.Put(CachedObject.Pack(new CacheableTypeOk(4, 104, "A", DateTime.Now, 45)));
                indexByValue.Put(CachedObject.Pack(new CacheableTypeOk(2, 102, "A", DateTime.Now, 45)));
                indexByValue.Put(CachedObject.Pack(new CacheableTypeOk(5, 105, "A", DateTime.Now, 45)));
                indexByValue.Put(CachedObject.Pack(new CacheableTypeOk(3, 103, "A", DateTime.Now, 45)));

                //value not fount (too small)
                var result1 = indexByValue.GetMany(MakeIntValue(12, keyType), QueryOperator.Le);
                Assert.AreEqual(result1.Count, 0);

                result1 = indexByValue.GetMany(MakeIntValue(12, keyType), QueryOperator.Lt);
                Assert.AreEqual(result1.Count, 0);

                //value not found (too big)
                result1 = indexByValue.GetMany(MakeIntValue(50, keyType), QueryOperator.Le);
                Assert.AreEqual(result1.Count, 6);

                result1 = indexByValue.GetMany(MakeIntValue(50, keyType), QueryOperator.Lt);
                Assert.AreEqual(result1.Count, 6);

                //value found (all match the index key )
                result1 = indexByValue.GetMany(MakeIntValue(45, keyType), QueryOperator.Le);
                Assert.AreEqual(result1.Count, 6);

                //not found (Lt)
                result1 = indexByValue.GetMany(MakeIntValue(45, keyType), QueryOperator.Lt);
                Assert.AreEqual(result1.Count, 0);
            }
        }


        [Test]
        public void Remove()
        {
            var keyType = new KeyInfo(KeyDataType.IntKey, KeyType.ScalarIndex, "test", true);

            var idx1 = populate(1, 2, 3, 3, 3, 4, 5);

            IList<CachedObject> result1 =
                idx1.GetMany(MakeIntValue(3, keyType), QueryOperator.Gt).OrderBy(o => o.PrimaryKey).ToList();
            Assert.AreEqual(result1.Count, 2);
            Assert.AreEqual(result1[0].PrimaryKey, 5);
            Assert.AreEqual(result1[1].PrimaryKey, 6);

            idx1.RemoveOne(result1[0]);
            result1 = idx1.GetMany(MakeIntValue(3, keyType), QueryOperator.Gt).OrderBy(o => o.PrimaryKey).ToList();
            Assert.AreEqual(result1.Count, 1);
            Assert.AreEqual(result1[0].PrimaryKey, 6); //now 1, 2, 3, 3, 3, 5


            result1 = idx1.GetMany(MakeIntValue(3, keyType)).OrderBy(o => o.PrimaryKey).ToList();
            Assert.AreEqual(result1.Count, 3);
            idx1.RemoveOne(result1[0]); //now 1, 2, 3, 3, 5
            result1 = idx1.GetMany(MakeIntValue(3, keyType)).OrderBy(o => o.PrimaryKey).ToList();
            Assert.AreEqual(result1.Count, 2);
        }
    }
}