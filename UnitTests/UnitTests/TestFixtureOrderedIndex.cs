#region

using System;
using System.Collections.Generic;
using System.Linq;
using Client.Core;
using Client.Messages;
using Client.Queries;
using NUnit.Framework;
using NUnit.Framework.Legacy;
using Server;
using Tests.TestData;

#endregion

namespace Tests.UnitTests
{
    [TestFixture]
    public class TestFixtureOrderedIndex
    {
        private static IList<KeyValue> MakeIntValue(int value, KeyInfo type)
        {
            return new List<KeyValue> { new KeyValue(value) };
        }

        private static void CheckLe(IndexBase indexByValue)
        {
            var keyType = new KeyInfo("test", 0, IndexType.Ordered);

            //non existent value in the middle
            {
                var result1 = indexByValue.GetMany(MakeIntValue(12, keyType), QueryOperator.Le);

                ClassicAssert.AreEqual(result1.Count, 3);

                var count = indexByValue.GetCount(MakeIntValue(12, keyType), QueryOperator.Le);

                ClassicAssert.AreEqual(count, 3);
            }

            //existent value in the middle
            {
                var result1 = indexByValue.GetMany(MakeIntValue(13, keyType), QueryOperator.Le);
                ClassicAssert.AreEqual(result1.Count, 4);

                var count = indexByValue.GetCount(MakeIntValue(13, keyType), QueryOperator.Le);
                ClassicAssert.AreEqual(count, 4);
            }

            //value < all
            {
                var result1 = indexByValue.GetMany(MakeIntValue(0, keyType), QueryOperator.Le);
                ClassicAssert.AreEqual(result1.Count, 0);

                var count = indexByValue.GetCount(MakeIntValue(0, keyType), QueryOperator.Le);
                ClassicAssert.AreEqual(count, 0);
            }

            //value > all
            {
                var result1 = indexByValue.GetMany(MakeIntValue(99, keyType), QueryOperator.Le);
                ClassicAssert.AreEqual(result1.Count, 6);

                var count = indexByValue.GetCount(MakeIntValue(99, keyType), QueryOperator.Le);
                ClassicAssert.AreEqual(count, 6);
            }

            //first value
            {
                var result1 =
                    indexByValue.GetMany(MakeIntValue(1, keyType), QueryOperator.Le).OrderBy(o => o.PrimaryKey)
                        .ToList();
                ClassicAssert.AreEqual(result1.Count, 1);
                ClassicAssert.AreEqual(result1[0].PrimaryKey.ToString(), "1");


                var count = indexByValue.GetCount(MakeIntValue(1, keyType), QueryOperator.Le);
                ClassicAssert.AreEqual(count, 1);
            }
        }

        private static void CheckLs(IndexBase indexByValue)
        {
            var keyType = new KeyInfo("IndexKeyValue", 0, IndexType.Ordered);

            //non existent value in the middle
            {
                IList<PackedObject> result1 =
                    indexByValue.GetMany(MakeIntValue(12, keyType), QueryOperator.Lt)
                        .OrderBy(o => o.PrimaryKey)
                        .ToList();
                ClassicAssert.AreEqual(result1.Count, 3);
                ClassicAssert.AreEqual(result1[0].PrimaryKey.ToString(), "1");
                ClassicAssert.AreEqual(result1[1].PrimaryKey.ToString(), "2");
                ClassicAssert.AreEqual(result1[2].PrimaryKey.ToString(), "3");

                var count = indexByValue.GetCount(MakeIntValue(12, keyType), QueryOperator.Lt);
                ClassicAssert.AreEqual(count, 3);
            }

            //existent value in the middle
            {
                IList<PackedObject> result1 =
                    indexByValue.GetMany(MakeIntValue(13, keyType), QueryOperator.Lt)
                        .OrderBy(o => o.PrimaryKey)
                        .ToList();
                ClassicAssert.AreEqual(result1.Count, 3);
                ClassicAssert.AreEqual(result1[2].PrimaryKey.ToString(), "3");

                var count = indexByValue.GetCount(MakeIntValue(12, keyType), QueryOperator.Lt);
                ClassicAssert.AreEqual(count, 3);
            }

            //value < all
            {
                IList<PackedObject> result1 =
                    indexByValue.GetMany(MakeIntValue(0, keyType), QueryOperator.Lt).OrderBy(o => o.PrimaryKey)
                        .ToList();
                ClassicAssert.AreEqual(result1.Count, 0);

                var count = indexByValue.GetCount(MakeIntValue(0, keyType), QueryOperator.Lt);
                ClassicAssert.AreEqual(count, 0);
            }

            //value > all
            {
                IList<PackedObject> result1 =
                    indexByValue.GetMany(MakeIntValue(99, keyType), QueryOperator.Lt)
                        .OrderBy(o => o.PrimaryKey)
                        .ToList();
                ClassicAssert.AreEqual(result1.Count, 6);

                var count = indexByValue.GetCount(MakeIntValue(99, keyType), QueryOperator.Lt);
                ClassicAssert.AreEqual(count, 6);
            }

            //first value
            {
                IList<PackedObject> result1 =
                    indexByValue.GetMany(MakeIntValue(1, keyType), QueryOperator.Lt).OrderBy(o => o.PrimaryKey)
                        .ToList();
                ClassicAssert.AreEqual(result1.Count, 0);

                var count = indexByValue.GetCount(MakeIntValue(1, keyType), QueryOperator.Lt);
                ClassicAssert.AreEqual(count, 0);
            }
        }

        private static OrderedIndex Populate(params int[] valueKeys)
        {
            var schema = TypedSchemaFactory.FromType<CacheableTypeOk>();

            //register the type to get a valid CollectionSchema
            //the type description is used to create CachedObjects from objects of the registered type
            var typeDescription = TypedSchemaFactory.FromType(typeof(CacheableTypeOk));

            KeyInfo valueKey = null;

            foreach (var keyInfo in typeDescription.IndexFields)
                if (keyInfo.Name == "IndexKeyValue")
                    valueKey = keyInfo;

            ClassicAssert.IsNotNull(valueKey);

            var index = new OrderedIndex(valueKey);
            for (var i = 0; i < valueKeys.Length; i++)
                index.Put(PackedObject.Pack(new CacheableTypeOk(i, 106, "A", DateTime.Now, valueKeys[i]), schema));

            return index;
        }


        [Test]
        public void Between()
        {
            var keyType = new KeyInfo("test", 0, IndexType.Ordered);

            var idx1 = Populate(1, 2, 3, 3, 3, 4, 5);

            {
                var count = idx1.GetCount(new List<KeyValue> { new KeyValue(3), new KeyValue(3) },
                    QueryOperator.GeLe);

                ClassicAssert.AreEqual(3, count);

                var items =
                    idx1.GetMany(new List<KeyValue> { new KeyValue(3), new KeyValue(3) },
                        QueryOperator.GeLe);

                ClassicAssert.AreEqual(3, items.Count);
            }


            {
                var count = idx1.GetCount(new List<KeyValue> { new KeyValue(8), new KeyValue(9) },
                    QueryOperator.GeLe);

                ClassicAssert.AreEqual(0, count);

                var items =
                    idx1.GetMany(new List<KeyValue> { new KeyValue(8), new KeyValue(9) },
                        QueryOperator.GeLe);

                ClassicAssert.AreEqual(0, items.Count);
            }

            {
                var count = idx1.GetCount(new List<KeyValue> { new KeyValue(1), new KeyValue(3) },
                    QueryOperator.GeLe);

                ClassicAssert.AreEqual(5, count);

                var items =
                    idx1.GetMany(new List<KeyValue> { new KeyValue(1), new KeyValue(3) },
                        QueryOperator.GeLe);

                ClassicAssert.AreEqual(5, items.Count);
            }
        }

        [Test]
        public void Eq()
        {
            var keyType = new KeyInfo("test", 0, IndexType.Ordered);

            //many in the middle
            var idx1 = Populate(1, 2, 3, 3, 3, 4, 5);

            IList<PackedObject> result1 =
                idx1.GetMany(MakeIntValue(3, keyType)).OrderBy(o => o.PrimaryKey).ToList();
            ClassicAssert.AreEqual(result1.Count, 3);
            ClassicAssert.AreEqual(result1[0].PrimaryKey, 2);
            ClassicAssert.AreEqual(result1[1].PrimaryKey, 3);
            ClassicAssert.AreEqual(result1[2].PrimaryKey, 4);

            var count = idx1.GetCount(MakeIntValue(3, keyType));
            ClassicAssert.AreEqual(count, 3);


            //many at the end
            var idx2 = Populate(1, 2, 3, 3, 3);

            result1 = idx2.GetMany(MakeIntValue(3, keyType)).OrderBy(o => o.PrimaryKey).ToList();
            ClassicAssert.AreEqual(result1.Count, 3);
            ClassicAssert.AreEqual(result1[0].PrimaryKey, 2);
            ClassicAssert.AreEqual(result1[1].PrimaryKey, 3);
            ClassicAssert.AreEqual(result1[2].PrimaryKey, 4);

            count = idx1.GetCount(MakeIntValue(3, keyType));
            ClassicAssert.AreEqual(count, 3);

            //many at the beginning
            var idx3 = Populate(3, 3, 3, 4, 4, 80);

            result1 = idx3.GetMany(MakeIntValue(3, keyType)).OrderBy(o => o.PrimaryKey).ToList();
            ClassicAssert.AreEqual(result1.Count, 3);
            ClassicAssert.AreEqual(result1[0].PrimaryKey, 0);
            ClassicAssert.AreEqual(result1[1].PrimaryKey, 1);
            ClassicAssert.AreEqual(result1[2].PrimaryKey, 2);

            count = idx1.GetCount(MakeIntValue(3, keyType));
            ClassicAssert.AreEqual(count, 3);

            //all equal
            var idx4 = Populate(3, 3, 3);

            result1 = idx4.GetMany(MakeIntValue(3, keyType)).OrderBy(o => o.PrimaryKey).ToList();
            ClassicAssert.AreEqual(result1.Count, 3);
            ClassicAssert.AreEqual(result1[0].PrimaryKey, 0);
            ClassicAssert.AreEqual(result1[1].PrimaryKey, 1);
            ClassicAssert.AreEqual(result1[2].PrimaryKey, 2);

            count = idx1.GetCount(MakeIntValue(3, keyType));
            ClassicAssert.AreEqual(count, 3);

            //one in the middle
            var idx5 = Populate(1, 3, 5, 7, 9, 111);
            result1 = idx5.GetMany(MakeIntValue(7, keyType)).OrderBy(o => o.PrimaryKey).ToList();
            ClassicAssert.AreEqual(result1.Count, 1);
            ClassicAssert.AreEqual(result1[0].PrimaryKey, 3);

            count = idx5.GetCount(MakeIntValue(7, keyType));
            ClassicAssert.AreEqual(count, 1);

            //value not found
            result1 = idx5.GetMany(MakeIntValue(8, keyType)).OrderBy(o => o.PrimaryKey).ToList();
            ClassicAssert.AreEqual(result1.Count, 0);

            count = idx5.GetCount(MakeIntValue(8, keyType));
            ClassicAssert.AreEqual(count, 0);
        }

        [Test]
        public void ExtremeCases()
        {
            //void index

            var keyType = new KeyInfo("test", 0, IndexType.Ordered);

            IndexBase index = Populate();
            var result = index.GetMany(MakeIntValue(12, keyType), QueryOperator.Le);
            ClassicAssert.AreEqual(result.Count, 0);
            result = index.GetMany(MakeIntValue(12, keyType), QueryOperator.Lt);
            ClassicAssert.AreEqual(result.Count, 0);
            result = index.GetMany(MakeIntValue(12, keyType));
            ClassicAssert.AreEqual(result.Count, 0);
            result = index.GetMany(MakeIntValue(12, keyType), QueryOperator.Gt);
            ClassicAssert.AreEqual(result.Count, 0);
            result = index.GetMany(MakeIntValue(12, keyType), QueryOperator.Ge);
            ClassicAssert.AreEqual(result.Count, 0);


            //one element index, value not found
            index = Populate(15);
            result = index.GetMany(MakeIntValue(12, keyType), QueryOperator.Le);
            ClassicAssert.AreEqual(result.Count, 0);
            result = index.GetMany(MakeIntValue(12, keyType), QueryOperator.Lt);
            ClassicAssert.AreEqual(result.Count, 0);
            result = index.GetMany(MakeIntValue(12, keyType));
            ClassicAssert.AreEqual(result.Count, 0);
            result = index.GetMany(MakeIntValue(12, keyType), QueryOperator.Gt);
            ClassicAssert.AreEqual(result.Count, 1);
            result = index.GetMany(MakeIntValue(12, keyType), QueryOperator.Ge);
            ClassicAssert.AreEqual(result.Count, 1);


            //one element index, value found
            index = Populate(12);
            result = index.GetMany(MakeIntValue(12, keyType), QueryOperator.Le);
            ClassicAssert.AreEqual(result.Count, 1);
            result = index.GetMany(MakeIntValue(12, keyType), QueryOperator.Lt);
            ClassicAssert.AreEqual(result.Count, 0);
            result = index.GetMany(MakeIntValue(12, keyType));
            ClassicAssert.AreEqual(result.Count, 1);
            result = index.GetMany(MakeIntValue(12, keyType), QueryOperator.Gt);
            ClassicAssert.AreEqual(result.Count, 0);
            result = index.GetMany(MakeIntValue(12, keyType), QueryOperator.Ge);
            ClassicAssert.AreEqual(result.Count, 1);

            //two element index (different values)
            index = Populate(12, 15);
            result = index.GetMany(MakeIntValue(12, keyType), QueryOperator.Le);
            ClassicAssert.AreEqual(result.Count, 1);
            result = index.GetMany(MakeIntValue(12, keyType), QueryOperator.Lt);
            ClassicAssert.AreEqual(result.Count, 0);
            result = index.GetMany(MakeIntValue(12, keyType));
            ClassicAssert.AreEqual(result.Count, 1);
            result = index.GetMany(MakeIntValue(12, keyType), QueryOperator.Gt);
            ClassicAssert.AreEqual(result.Count, 1);
            result = index.GetMany(MakeIntValue(12, keyType), QueryOperator.Ge);
            ClassicAssert.AreEqual(result.Count, 2);


            //two element index (same value)
            index = Populate(12, 12);
            result = index.GetMany(MakeIntValue(12, keyType), QueryOperator.Le);
            ClassicAssert.AreEqual(result.Count, 2);
            result = index.GetMany(MakeIntValue(12, keyType), QueryOperator.Lt);
            ClassicAssert.AreEqual(result.Count, 0);
            result = index.GetMany(MakeIntValue(12, keyType));
            ClassicAssert.AreEqual(result.Count, 2);
            result = index.GetMany(MakeIntValue(12, keyType), QueryOperator.Gt);
            ClassicAssert.AreEqual(result.Count, 0);
            result = index.GetMany(MakeIntValue(12, keyType), QueryOperator.Ge);
            ClassicAssert.AreEqual(result.Count, 2);


            //three element index (same value ==)
            index = Populate(12, 12, 12);
            result = index.GetMany(MakeIntValue(12, keyType), QueryOperator.Le);
            ClassicAssert.AreEqual(result.Count, 3);
            result = index.GetMany(MakeIntValue(12, keyType), QueryOperator.Lt);
            ClassicAssert.AreEqual(result.Count, 0);
            result = index.GetMany(MakeIntValue(12, keyType));
            ClassicAssert.AreEqual(result.Count, 3);
            result = index.GetMany(MakeIntValue(12, keyType), QueryOperator.Gt);
            ClassicAssert.AreEqual(result.Count, 0);
            result = index.GetMany(MakeIntValue(12, keyType), QueryOperator.Ge);
            ClassicAssert.AreEqual(result.Count, 3);

            //three element index (same value !=)
            index = Populate(15, 15, 15);
            result = index.GetMany(MakeIntValue(12, keyType), QueryOperator.Le);
            ClassicAssert.AreEqual(result.Count, 0);
            result = index.GetMany(MakeIntValue(12, keyType), QueryOperator.Lt);
            ClassicAssert.AreEqual(result.Count, 0);
            result = index.GetMany(MakeIntValue(12, keyType));
            ClassicAssert.AreEqual(result.Count, 0);
            result = index.GetMany(MakeIntValue(12, keyType), QueryOperator.Gt);
            ClassicAssert.AreEqual(result.Count, 3);
            result = index.GetMany(MakeIntValue(12, keyType), QueryOperator.Ge);
            ClassicAssert.AreEqual(result.Count, 3);
        }


        [Test]
        public void Ge()
        {
            var keyType = new KeyInfo("test", 0, IndexType.Ordered);

            //many in the middle
            var idx1 = Populate(1, 2, 3, 3, 3, 4, 5);

            IList<PackedObject> result1 =
                idx1.GetMany(MakeIntValue(3, keyType), QueryOperator.Ge).OrderBy(o => o.PrimaryKey).ToList();
            ClassicAssert.AreEqual(result1.Count, 5);
            ClassicAssert.AreEqual(result1[0].PrimaryKey, 2);
            ClassicAssert.AreEqual(result1[1].PrimaryKey, 3);
            ClassicAssert.AreEqual(result1[2].PrimaryKey, 4);

            var count = idx1.GetCount(MakeIntValue(3, keyType), QueryOperator.Ge);
            ClassicAssert.AreEqual(count, 5);

            //many at the end
            var idx2 = Populate(1, 2, 3, 3, 3);

            result1 = idx2.GetMany(MakeIntValue(3, keyType), QueryOperator.Ge).OrderBy(o => o.PrimaryKey).ToList();
            ClassicAssert.AreEqual(result1.Count, 3);
            ClassicAssert.AreEqual(result1[0].PrimaryKey, 2);
            ClassicAssert.AreEqual(result1[1].PrimaryKey, 3);
            ClassicAssert.AreEqual(result1[2].PrimaryKey, 4);

            count = idx2.GetCount(MakeIntValue(3, keyType), QueryOperator.Ge);
            ClassicAssert.AreEqual(count, 3);

            //many at the beginning
            var idx3 = Populate(3, 3, 3, 4, 4, 80);

            result1 = idx3.GetMany(MakeIntValue(3, keyType), QueryOperator.Ge).OrderBy(o => o.PrimaryKey).ToList();
            ClassicAssert.AreEqual(result1.Count, 6);
            ClassicAssert.AreEqual(result1[0].PrimaryKey, 0);
            ClassicAssert.AreEqual(result1[1].PrimaryKey, 1);
            ClassicAssert.AreEqual(result1[2].PrimaryKey, 2);
            ClassicAssert.AreEqual(result1[3].PrimaryKey, 3);
            ClassicAssert.AreEqual(result1[4].PrimaryKey, 4);
            ClassicAssert.AreEqual(result1[5].PrimaryKey, 5);

            count = idx3.GetCount(MakeIntValue(3, keyType), QueryOperator.Ge);
            ClassicAssert.AreEqual(count, 6);

            //all equal
            var idx4 = Populate(3, 3, 3);

            result1 = idx4.GetMany(MakeIntValue(3, keyType), QueryOperator.Ge).OrderBy(o => o.PrimaryKey).ToList();
            ClassicAssert.AreEqual(result1.Count, 3);
            ClassicAssert.AreEqual(result1[0].PrimaryKey, 0);
            ClassicAssert.AreEqual(result1[1].PrimaryKey, 1);
            ClassicAssert.AreEqual(result1[2].PrimaryKey, 2);

            count = idx4.GetCount(MakeIntValue(3, keyType), QueryOperator.Ge);
            ClassicAssert.AreEqual(count, 3);


            //one in the middle
            var idx5 = Populate(1, 3, 5, 7, 9, 111);
            result1 = idx5.GetMany(MakeIntValue(7, keyType), QueryOperator.Ge).OrderBy(o => o.PrimaryKey).ToList();
            ClassicAssert.AreEqual(result1.Count, 3);
            ClassicAssert.AreEqual(result1[0].PrimaryKey, 3);
            ClassicAssert.AreEqual(result1[1].PrimaryKey, 4);
            ClassicAssert.AreEqual(result1[2].PrimaryKey, 5);

            count = idx5.GetCount(MakeIntValue(7, keyType), QueryOperator.Ge);
            ClassicAssert.AreEqual(count, 3);


            //value not found
            result1 = idx5.GetMany(MakeIntValue(8, keyType), QueryOperator.Ge).OrderBy(o => o.PrimaryKey).ToList();
            ClassicAssert.AreEqual(result1.Count, 2);
            ClassicAssert.AreEqual(result1[0].PrimaryKey, 4);
            ClassicAssert.AreEqual(result1[1].PrimaryKey, 5);

            count = idx5.GetCount(MakeIntValue(8, keyType), QueryOperator.Ge);
            ClassicAssert.AreEqual(count, 2);
        }


        [Test]
        public void Gt()
        {
            var keyType = new KeyInfo("test", 0, IndexType.Ordered);

            //many in the middle
            var idx1 = Populate(1, 2, 3, 3, 3, 4, 5);

            IList<PackedObject> result1 =
                idx1.GetMany(MakeIntValue(3, keyType), QueryOperator.Gt).OrderBy(o => o.PrimaryKey).ToList();
            ClassicAssert.AreEqual(result1.Count, 2);
            ClassicAssert.AreEqual(result1[0].PrimaryKey, 5);
            ClassicAssert.AreEqual(result1[1].PrimaryKey, 6);

            var count = idx1.GetCount(MakeIntValue(3, keyType), QueryOperator.Gt);
            ClassicAssert.AreEqual(count, 2);


            //many at the end
            var idx2 = Populate(1, 2, 3, 3, 3);

            result1 = idx2.GetMany(MakeIntValue(3, keyType), QueryOperator.Gt).OrderBy(o => o.PrimaryKey).ToList();
            ClassicAssert.AreEqual(result1.Count, 0);

            count = idx2.GetCount(MakeIntValue(3, keyType), QueryOperator.Gt);
            ClassicAssert.AreEqual(count, 0);

            //many at the beginning
            var idx3 = Populate(3, 3, 3, 4, 4, 80);

            result1 = idx3.GetMany(MakeIntValue(3, keyType), QueryOperator.Gt).OrderBy(o => o.PrimaryKey).ToList();
            ClassicAssert.AreEqual(result1.Count, 3);
            ClassicAssert.AreEqual(result1[0].PrimaryKey, 3);
            ClassicAssert.AreEqual(result1[1].PrimaryKey, 4);
            ClassicAssert.AreEqual(result1[2].PrimaryKey, 5);
            count = idx3.GetCount(MakeIntValue(3, keyType), QueryOperator.Gt);
            ClassicAssert.AreEqual(count, 3);

            //all equal
            var idx4 = Populate(3, 3, 3);

            result1 = idx4.GetMany(MakeIntValue(3, keyType), QueryOperator.Gt).OrderBy(o => o.PrimaryKey).ToList();
            ClassicAssert.AreEqual(result1.Count, 0);
            count = idx4.GetCount(MakeIntValue(3, keyType), QueryOperator.Gt);
            ClassicAssert.AreEqual(count, 0);


            //one in the middle
            var idx5 = Populate(1, 3, 5, 7, 9, 111);
            result1 = idx5.GetMany(MakeIntValue(7, keyType), QueryOperator.Gt).OrderBy(o => o.PrimaryKey).ToList();
            ClassicAssert.AreEqual(result1.Count, 2);
            ClassicAssert.AreEqual(result1[0].PrimaryKey, 4);
            ClassicAssert.AreEqual(result1[1].PrimaryKey, 5);

            count = idx5.GetCount(MakeIntValue(7, keyType), QueryOperator.Gt);
            ClassicAssert.AreEqual(count, 2);

            //value not found
            result1 = idx5.GetMany(MakeIntValue(8, keyType), QueryOperator.Gt).OrderBy(o => o.PrimaryKey).ToList();
            ClassicAssert.AreEqual(result1.Count, 2);
            ClassicAssert.AreEqual(result1[0].PrimaryKey, 4);
            ClassicAssert.AreEqual(result1[1].PrimaryKey, 5);

            count = idx5.GetCount(MakeIntValue(8, keyType), QueryOperator.Gt);
            ClassicAssert.AreEqual(count, 2);
        }

        /// <summary>
        ///     check for Le and Lt operators on an ordered index
        /// </summary>
        [Test]
        public void Lesser()
        {
            var schema = TypedSchemaFactory.FromType<CacheableTypeOk>();

            //register the type to get a valid CollectionSchema
            //the type description is used to create CachedObjects from objects of the registered type
            var typeDescription = TypedSchemaFactory.FromType(typeof(CacheableTypeOk));

            KeyInfo valueKey = null;

            foreach (var keyInfo in typeDescription.IndexFields)
                if (keyInfo.Name == "IndexKeyValue")
                    valueKey = keyInfo;

            ClassicAssert.IsNotNull(valueKey);


            //fill in order
            {
                var indexByValue = new OrderedIndex(valueKey);

                indexByValue.Put(PackedObject.Pack(new CacheableTypeOk(1, 101, "A", DateTime.Now, 1), schema));
                indexByValue.Put(PackedObject.Pack(new CacheableTypeOk(2, 102, "A", DateTime.Now, 4), schema));
                indexByValue.Put(PackedObject.Pack(new CacheableTypeOk(3, 103, "A", DateTime.Now, 6), schema));
                indexByValue.Put(PackedObject.Pack(new CacheableTypeOk(4, 104, "A", DateTime.Now, 13), schema));
                indexByValue.Put(PackedObject.Pack(new CacheableTypeOk(5, 105, "A", DateTime.Now, 14), schema));
                indexByValue.Put(PackedObject.Pack(new CacheableTypeOk(6, 106, "A", DateTime.Now, 80), schema));

                CheckLe(indexByValue);
                CheckLs(indexByValue);
            }

            //fill out of order
            {
                var indexByValue = new OrderedIndex(valueKey);

                indexByValue.Put(PackedObject.Pack(new CacheableTypeOk(6, 106, "A", DateTime.Now, 80), schema));
                indexByValue.Put(PackedObject.Pack(new CacheableTypeOk(1, 101, "A", DateTime.Now, 1), schema));
                indexByValue.Put(PackedObject.Pack(new CacheableTypeOk(4, 104, "A", DateTime.Now, 13), schema));
                indexByValue.Put(PackedObject.Pack(new CacheableTypeOk(2, 102, "A", DateTime.Now, 4), schema));
                indexByValue.Put(PackedObject.Pack(new CacheableTypeOk(5, 105, "A", DateTime.Now, 14), schema));
                indexByValue.Put(PackedObject.Pack(new CacheableTypeOk(3, 103, "A", DateTime.Now, 6), schema));


                CheckLe(indexByValue);
                CheckLs(indexByValue);
            }

            //fill out of order transactional
            {
                var indexByValue = new OrderedIndex(valueKey);

                indexByValue.BeginFill();

                indexByValue.Put(PackedObject.Pack(new CacheableTypeOk(6, 106, "A", DateTime.Now, 80), schema));
                indexByValue.Put(PackedObject.Pack(new CacheableTypeOk(1, 101, "A", DateTime.Now, 1), schema));
                indexByValue.Put(PackedObject.Pack(new CacheableTypeOk(4, 104, "A", DateTime.Now, 13), schema));
                indexByValue.Put(PackedObject.Pack(new CacheableTypeOk(2, 102, "A", DateTime.Now, 4), schema));
                indexByValue.Put(PackedObject.Pack(new CacheableTypeOk(5, 105, "A", DateTime.Now, 14), schema));
                indexByValue.Put(PackedObject.Pack(new CacheableTypeOk(3, 103, "A", DateTime.Now, 6), schema));
                indexByValue.EndFill();

                CheckLe(indexByValue);
                CheckLs(indexByValue);
            }


            //all equals 
            {
                var indexByValue = new OrderedIndex(valueKey);

                var keyType = new KeyInfo("test", 0, IndexType.Ordered);

                indexByValue.Put(PackedObject.Pack(new CacheableTypeOk(6, 106, "A", DateTime.Now, 45), schema));
                indexByValue.Put(PackedObject.Pack(new CacheableTypeOk(1, 101, "A", DateTime.Now, 45), schema));
                indexByValue.Put(PackedObject.Pack(new CacheableTypeOk(4, 104, "A", DateTime.Now, 45), schema));
                indexByValue.Put(PackedObject.Pack(new CacheableTypeOk(2, 102, "A", DateTime.Now, 45), schema));
                indexByValue.Put(PackedObject.Pack(new CacheableTypeOk(5, 105, "A", DateTime.Now, 45), schema));
                indexByValue.Put(PackedObject.Pack(new CacheableTypeOk(3, 103, "A", DateTime.Now, 45), schema));

                //value not fount (too small)
                var result1 = indexByValue.GetMany(MakeIntValue(12, keyType), QueryOperator.Le);
                ClassicAssert.AreEqual(result1.Count, 0);

                result1 = indexByValue.GetMany(MakeIntValue(12, keyType), QueryOperator.Lt);
                ClassicAssert.AreEqual(result1.Count, 0);

                //value not found (too big)
                result1 = indexByValue.GetMany(MakeIntValue(50, keyType), QueryOperator.Le);
                ClassicAssert.AreEqual(result1.Count, 6);

                result1 = indexByValue.GetMany(MakeIntValue(50, keyType), QueryOperator.Lt);
                ClassicAssert.AreEqual(result1.Count, 6);

                //value found (all match the index key )
                result1 = indexByValue.GetMany(MakeIntValue(45, keyType), QueryOperator.Le);
                ClassicAssert.AreEqual(result1.Count, 6);

                //not found (Lt)
                result1 = indexByValue.GetMany(MakeIntValue(45, keyType), QueryOperator.Lt);
                ClassicAssert.AreEqual(result1.Count, 0);
            }
        }


        [Test]
        public void Remove()
        {
            var keyType = new KeyInfo("test", 0, IndexType.Ordered);

            var idx1 = Populate(1, 2, 3, 3, 3, 4, 5);

            IList<PackedObject> result1 =
                idx1.GetMany(MakeIntValue(3, keyType), QueryOperator.Gt).OrderBy(o => o.PrimaryKey).ToList();
            ClassicAssert.AreEqual(result1.Count, 2);
            ClassicAssert.AreEqual(result1[0].PrimaryKey, 5);
            ClassicAssert.AreEqual(result1[1].PrimaryKey, 6);

            idx1.RemoveOne(result1[0]);
            result1 = idx1.GetMany(MakeIntValue(3, keyType), QueryOperator.Gt).OrderBy(o => o.PrimaryKey).ToList();
            ClassicAssert.AreEqual(result1.Count, 1);
            ClassicAssert.AreEqual(result1[0].PrimaryKey, 6); //now 1, 2, 3, 3, 3, 5


            result1 = idx1.GetMany(MakeIntValue(3, keyType)).OrderBy(o => o.PrimaryKey).ToList();
            ClassicAssert.AreEqual(result1.Count, 3);
            idx1.RemoveOne(result1[0]); //now 1, 2, 3, 3, 5
            result1 = idx1.GetMany(MakeIntValue(3, keyType)).OrderBy(o => o.PrimaryKey).ToList();
            ClassicAssert.AreEqual(result1.Count, 2);
        }

        [Test]
        public void IssueWithActivityTable()
        {
            var schema = TypedSchemaFactory.FromType<LogEntry>();

            var meta = schema.KeyByName("TimeStamp");

            ClassicAssert.IsNotNull(meta);

            var timeStampIndex = new OrderedIndex(meta);


            for (var i = 0; i < 1_000_000; i++)
            {
                var item = new LogEntry { Id = Guid.NewGuid(), TimeStamp = DateTimeOffset.Now };

                var packed = PackedObject.Pack(item, schema);

                timeStampIndex.Put(packed);

                var item1 = new LogEntry { Id = Guid.NewGuid(), TimeStamp = item.TimeStamp };

                var packed1 = PackedObject.Pack(item1, schema);

                timeStampIndex.Put(packed1);
            }
        }


        [Test]
        public void AnotherAttemptToReproduceTheIssue()
        {
            var schema = TypedSchemaFactory.FromType<LogEntry>();
            schema.CollectionName = LogEntry.Table;

            var activityTable = new DataStore(schema, new LruEvictionPolicy(2000, 1000), new FullTextConfig());

            for (var i = 0; i < 10_000; i++)
            {
                var item = new LogEntry { Id = Guid.NewGuid(), TimeStamp = DateTimeOffset.Now };

                var packed = PackedObject.Pack(item, schema);

                activityTable.InternalPutMany(new List<PackedObject> { packed }, false);
            }
        }
    }
}