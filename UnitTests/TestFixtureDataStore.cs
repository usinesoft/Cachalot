#region

using System;
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
    public class TestFixtureDataStore
    {
        [SetUp]
        public void SetUp()
        {
            _collectionSchema = ClientSideTypeDescription.RegisterType(typeof(CacheableTypeOk)).AsCollectionSchema;


            _dataStore = new DataStore(_collectionSchema, new NullEvictionPolicy(), new NodeConfig());
        }

        private DataStore _dataStore;

        private CollectionSchema _collectionSchema;


        [Test]
        public void GetManyOnOrderedIndexes()
        {
            var item = new CacheableTypeOk(1, 1001, "AHA", new DateTime(2010, 10, 01), 9);
            _dataStore.InternalAddNew(CachedObject.Pack(item), false);

            item = new CacheableTypeOk(2, 1002, "AHA", new DateTime(2010, 10, 01), 8);
            _dataStore.InternalAddNew(CachedObject.Pack(item), false);

            item = new CacheableTypeOk(3, 1003, "AHA", new DateTime(2010, 10, 02), 8);
            _dataStore.InternalAddNew(CachedObject.Pack(item), false);

            item = new CacheableTypeOk(4, 1004, "BBB", new DateTime(2010, 9, 01), 5);
            _dataStore.InternalAddNew(CachedObject.Pack(item), false);

            item = new CacheableTypeOk(5, 1005, "BBB", new DateTime(2010, 10, 01), 4);
            _dataStore.InternalAddNew(CachedObject.Pack(item), false);

            item = new CacheableTypeOk(6, 1006, "BBA", new DateTime(2010, 10, 01), 1);
            _dataStore.InternalAddNew(CachedObject.Pack(item), false);

            var result =
                _dataStore.InternalGetMany(
                    _collectionSchema.MakeIndexKeyValue("IndexKeyDate", new DateTime(2010, 10, 01)),
                    QueryOperator.Le);
            Assert.AreEqual(result.Count, 5);

            result = _dataStore.InternalGetMany(_collectionSchema.MakeIndexKeyValue("IndexKeyValue", 8),
                QueryOperator.Ge);
            Assert.AreEqual(result.Count, 3);

            _dataStore.RemoveByPrimaryKey(_collectionSchema.MakePrimaryKeyValue(2));

            result = _dataStore.InternalGetMany(
                _collectionSchema.MakeIndexKeyValue("IndexKeyDate", new DateTime(2010, 10, 01)),
                QueryOperator.Le);
            Assert.AreEqual(result.Count, 4);

            result = _dataStore.InternalGetMany(_collectionSchema.MakeIndexKeyValue("IndexKeyValue", 8),
                QueryOperator.Ge);
            Assert.AreEqual(result.Count, 2);
        }

        [Test]
        public void PutDifferentType()
        {
            var item1 = new Order();
            Assert.Throws<InvalidOperationException>(() => _dataStore.InternalAddNew(CachedObject.Pack(item1), false));
            
        }

        [Test]
        public void PutNewGetOne()
        {
            var item1 = new CacheableTypeOk(1, 1001, "AHA", new DateTime(2010, 10, 01), 55);
            _dataStore.InternalAddNew(CachedObject.Pack(item1), false);

            //get one by primary key
            var cachedItem1 = _dataStore.InternalGetOne(_collectionSchema.MakePrimaryKeyValue(1));
            Assert.IsTrue(cachedItem1.PrimaryKey.Equals(1));
            var item1Reloaded = CachedObject.Unpack<CacheableTypeOk>(cachedItem1);
            Assert.AreEqual(item1, item1Reloaded);

            //get one by unique key
            cachedItem1 =
                _dataStore.InternalGetOne(new KeyValue(1001,
                    new KeyInfo(KeyDataType.IntKey, KeyType.Unique, "UniqueKey")));
            Assert.IsTrue(cachedItem1.PrimaryKey.Equals(1));
            Assert.IsTrue(cachedItem1.UniqueKeys[0].Equals(1001));
            item1Reloaded = CachedObject.Unpack<CacheableTypeOk>(cachedItem1);
            Assert.AreEqual(item1, item1Reloaded);
        }

        [Test]
        public void Queries()
        {
            var item = new CacheableTypeOk(1, 1001, "AHA", new DateTime(2010, 10, 01), 9);
            _dataStore.InternalAddNew(CachedObject.Pack(item), false);

            item = new CacheableTypeOk(2, 1002, "AHA", new DateTime(2010, 10, 01), 8);
            _dataStore.InternalAddNew(CachedObject.Pack(item), false);

            item = new CacheableTypeOk(3, 1003, "AHA", new DateTime(2010, 10, 02), 8);
            _dataStore.InternalAddNew(CachedObject.Pack(item), false);

            item = new CacheableTypeOk(4, 1004, "BBB", new DateTime(2010, 9, 01), 5);
            _dataStore.InternalAddNew(CachedObject.Pack(item), false);

            item = new CacheableTypeOk(5, 1005, "BBB", new DateTime(2010, 10, 01), 4);
            _dataStore.InternalAddNew(CachedObject.Pack(item), false);

            item = new CacheableTypeOk(6, 1006, "BBA", new DateTime(2010, 10, 01), 1);
            _dataStore.InternalAddNew(CachedObject.Pack(item), false);


            var builder = new QueryBuilder(typeof(CacheableTypeOk));

            //test In query with unique key : should return items 4 and 5            
            var q1 = builder.In("uniquekey", 1004, 1005);

            var result = _dataStore.InternalGetMany(q1);
            Assert.AreEqual(result.Count, 2);
            Assert.AreEqual(result[0].PrimaryKey, 4);
            Assert.AreEqual(result[1].PrimaryKey, 5);

            //test In query with primary key : should return items 4 and 5

            var q2 = builder.In(4, 5);

            result = _dataStore.InternalGetMany(q2);
            Assert.AreEqual(result.Count, 2);
            Assert.AreEqual(result[0].PrimaryKey, 4);
            Assert.AreEqual(result[1].PrimaryKey, 5);

            //test In query with index key : should return items 4, 5, 6
            var q3 = builder.In("IndexKeyFolder", "BBB", "BBA");

            result = _dataStore.InternalGetMany(q3);
            Assert.AreEqual(result.Count, 3);

            //where IndexKeyValue <= 4 AND IndexKeyFolder = "BBB"
            var q4 = builder.GetMany("IndexKeyValue <= 4", "IndexKeyFolder = BBB");

            result = _dataStore.InternalGetMany(q4);
            Assert.AreEqual(result.Count, 1);
            Assert.AreEqual(result[0].PrimaryKey, 5);
            Assert.IsTrue(q4.Match(result[0]));

            //where IndexKeyFolder = "AHA" AND IndexKeyDate <= 20101001 
            var q5 = builder.GetMany("IndexKeyFolder = AHA", $"IndexKeyDate <= {new DateTime(2010, 10, 01).Ticks}");

            result = _dataStore.InternalGetMany(q5);
            Assert.AreEqual(result.Count, 2);
            foreach (var cachedObject in result) Assert.IsTrue(q5.Match(cachedObject));

            //where IndexKeyDate <= 20101001 
            var q6 = builder.GetMany($"IndexKeyDate <=  {new DateTime(2010, 10, 01).Ticks}");

            result = _dataStore.InternalGetMany(q6);
            Assert.AreEqual(result.Count, 5);
            foreach (var cachedObject in result) Assert.IsTrue(q6.Match(cachedObject));

            // IN alone
            var q7 = builder.In("IndexKeyFolder", "BBA", "BBB", "BBC");
            result = _dataStore.InternalGetMany(q7);
            Assert.AreEqual(result.Count, 3);

            Assert.IsFalse(_dataStore.LastExecutionPlan.IsFullScan);
            Assert.AreEqual(_dataStore.LastExecutionPlan.PrimaryIndexName, "IndexKeyFolder");

            // IN and BTW
            var q81 = builder.In("IndexKeyFolder", "BBA", "BBB", "BBC");
            var q82 = builder.MakeAtomicQuery("indexKeyValue", 4, 5);
            q81.Elements[0].Elements.Add(q82);

            var queryDescription = q81.ToString();
            Assert.IsTrue(queryDescription.Contains("AND"));

            result = _dataStore.InternalGetMany(q81);
            Assert.AreEqual(result.Count, 2);

            Assert.IsFalse(_dataStore.LastExecutionPlan.IsFullScan);
            Assert.AreEqual(_dataStore.LastExecutionPlan.PrimaryIndexName, "IndexKeyValue");
            Assert.AreEqual(_dataStore.LastExecutionPlan.ElementsInPrimarySet, 2);

            // Perform a comparison on a non ordered index. it should be solved by a full scan  
            var q9 = builder.GetMany("IndexKeyFolder >= BBA");
            result = _dataStore.InternalGetMany(q9);
            Assert.AreEqual(result.Count, 3);
            Assert.IsTrue(_dataStore.LastExecutionPlan.IsFullScan);

            // Multiple query to be solved by ful scan
            var q10 = builder.GetMany("IndexKeyFolder >= BBA", "IndexKeyFolder < BBB");
            result = _dataStore.InternalGetMany(q10);
            Assert.AreEqual(result.Count, 1);
            Assert.IsTrue(_dataStore.LastExecutionPlan.IsFullScan);
        }

        [Test]
        public void RemoveAndGetMany()
        {
            //add two items (2 and 3 as primary keys)
            var item1 = new CacheableTypeOk(2, 1002, "AHA", new DateTime(2010, 10, 01), 55);
            _dataStore.InternalAddNew(CachedObject.Pack(item1), false);

            var item2 = new CacheableTypeOk(3, 1003, "AHA", new DateTime(2010, 10, 01), 55);
            _dataStore.InternalAddNew(CachedObject.Pack(item2), false);

            //this get one should return the first item
            var cachedItem1 = _dataStore.InternalGetOne(_collectionSchema.MakePrimaryKeyValue(2));
            Assert.IsTrue(cachedItem1.PrimaryKey.Equals(2));

            //this GetMany() should return the two items 
            var result = _dataStore.InternalGetMany(_collectionSchema.MakeIndexKeyValue("indexkeyfolder", "AHA"));
            Assert.AreEqual(result.Count, 2);

            //remove the first item
            _dataStore.RemoveByPrimaryKey(_collectionSchema.MakePrimaryKeyValue(2));

            //it should not be there any more
            var shouldBeNull = _dataStore.InternalGetOne(_collectionSchema.MakePrimaryKeyValue(2));
            Assert.IsNull(shouldBeNull);

            //this GetMany() should return one item
            result = _dataStore.InternalGetMany(_collectionSchema.MakeIndexKeyValue("indexkeyfolder", "AHA"));
            Assert.AreEqual(result.Count, 1);
        }

        [Test]
        public void StillWorksAfterTruncate()
        {
            _dataStore.InternalTruncate();

            //add two items (2 and 3 as primary keys)
            var item1 = new CacheableTypeOk(2, 1002, "AHA", new DateTime(2010, 10, 01), 55);
            _dataStore.InternalAddNew(CachedObject.Pack(item1), false);

            var item2 = new CacheableTypeOk(3, 1003, "AHA", new DateTime(2010, 10, 01), 55);
            _dataStore.InternalAddNew(CachedObject.Pack(item2), false);

            _dataStore.InternalTruncate();
            //this GetMany() should return no item cause the data was truncated
            var result = _dataStore.InternalGetMany(_collectionSchema.MakeIndexKeyValue("indexkeyfolder", "AHA"));
            Assert.AreEqual(result.Count, 0);

            //put the items back
            _dataStore.InternalAddNew(CachedObject.Pack(item1), false);
            _dataStore.InternalAddNew(CachedObject.Pack(item2), false);

            //this get one should return the first item
            var cachedItem1 = _dataStore.InternalGetOne(_collectionSchema.MakePrimaryKeyValue(2));
            Assert.IsTrue(cachedItem1.PrimaryKey.Equals(2));

            //this GetMany() should return the two items 
            result = _dataStore.InternalGetMany(_collectionSchema.MakeIndexKeyValue("indexkeyfolder", "AHA"));
            Assert.AreEqual(result.Count, 2);

            //remove the first item
            _dataStore.RemoveByPrimaryKey(_collectionSchema.MakePrimaryKeyValue(2));

            //it should not be there any more
            var shouldBeNull = _dataStore.InternalGetOne(_collectionSchema.MakePrimaryKeyValue(2));
            Assert.IsNull(shouldBeNull);

            //this GetMany() should return one item
            result = _dataStore.InternalGetMany(_collectionSchema.MakeIndexKeyValue("indexkeyfolder", "AHA"));
            Assert.AreEqual(result.Count, 1);
        }
    }
}