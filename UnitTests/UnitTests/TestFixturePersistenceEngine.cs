using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Client.Core;
using Client.Interface;
using Newtonsoft.Json;
using NUnit.Framework;
using Server.Persistence;
using Tests.TestData;
using Constants = Server.Persistence.Constants;

namespace Tests.UnitTests
{
    [TestFixture]
    public class TestFixturePersistenceEngine
    {
        [SetUp]
        public void SetUp()
        {
            if (Directory.Exists(Constants.DataPath)) Directory.Delete(Constants.DataPath, true);


            if (File.Exists(_backupPath)) File.Delete(_backupPath);

            _schema = TypedSchemaFactory.FromType<Trade>();
        }

        private class TestProcessor : IPersistentObjectProcessor
        {
            public IList<PackedObject> LoadedObjects { get; } = new List<PackedObject>();

            public void Process(byte[] data)
            {
                var obj = SerializationHelper.ObjectFromBytes<PackedObject>(data, SerializationMode.ProtocolBuffers,
                    false);

                LoadedObjects.Add(obj);
            }

            public void EndProcess(string dataPath)
            {
            }
        }

        private readonly string _backupPath = Path.Combine("backup", ReliableStorage.StorageFileName);
        
        private CollectionSchema _schema;

        [OneTimeSetUp]
        public void RunBeforeAnyTests()
        {
            Environment.CurrentDirectory = TestContext.CurrentContext.TestDirectory;
            Directory.SetCurrentDirectory(TestContext.CurrentContext.TestDirectory);

            var config = new ClientConfig();
            config.LoadFromFile("inprocess_config.xml");

        }


        private DurableTransaction MakeTransaction<T>(params T[] items)
        {
            var transaction = new PutDurableTransaction();

            foreach (var item in items)
            {
                var packed = PackedObject.Pack(item, _schema);
                transaction.Items.Add(packed);
            }

            return transaction;
        }

        private DurableTransaction MakeDeleteTransaction<T>(params T[] items)
        {
            var transaction = new DeleteDurableTransaction();

            foreach (var item in items)
            {
                var packed = PackedObject.Pack(item, _schema);
                transaction.ItemsToDelete.Add(packed);
            }

            return transaction;
        }

        [Test]
        public void
            Add_two_objects_then_update_one_of_them_then_check_the_persistent_storage_contains_the_updated_value()
        {
            var transaction1 = MakeTransaction(
                new Trade(1, 5465, "TATA", DateTime.Now.Date, 150),
                new Trade(2, 5467, "TATA", DateTime.Now.Date, 180)
            );

            var transaction2 = MakeTransaction(
                new Trade(2, 5467, "TOTO", DateTime.Now.Date, 190)
            );

            var deleteTransaction = MakeDeleteTransaction(
                new Trade(1, 5465, "TATA", DateTime.Now.Date, 150)
            );


            var engine = new PersistenceEngine();
            engine.Start();

            engine.NewTransaction(transaction1);
            engine.NewTransaction(transaction2);
            engine.NewTransaction(deleteTransaction);

            // wait for the transaction log to be processed
            engine.WaitForPendingTransactions();

            engine.Stop();

            var processor = new TestProcessor();

            //reload data from persistent storage
            var unused = new ReliableStorage(processor);
            unused.LoadPersistentData();
            unused.Dispose();

            Assert.AreEqual(1, processor.LoadedObjects.Count);

            var reloaded = processor.LoadedObjects.Select(PackedObject.Unpack<Trade>).First(t => t.Id == 2);

            Assert.AreEqual("TOTO", reloaded.Folder);

            //reload data from transaction log. Check no more pending transactions
            var log = new TransactionLog();
            Assert.AreEqual(0, log.PendingTransactionsCount);
            log.Dispose();
        }


        [Test]
        public void Persistence_of_simple_put_transaction_with_one_object()
        {
            var transaction1 = MakeTransaction(new Trade(1, 5465, "TATA", DateTime.Now.Date, 150));

            var engine = new PersistenceEngine();
            engine.Start();

            engine.NewTransaction(transaction1);

            // wait for the transaction log to be processed
            engine.WaitForPendingTransactions();

            engine.Stop();

            var processor = new TestProcessor();

            //reload data from persistent storage
            var unused = new ReliableStorage(processor);
            unused.LoadPersistentData();
            unused.Dispose();

            Assert.AreEqual(1, processor.LoadedObjects.Count);

            var reloaded = PackedObject.Unpack<Trade>(processor.LoadedObjects[0]);

            Assert.AreEqual("TATA", reloaded.Folder);
        }

        [Test]
        public void Simulate_failure_during_transaction_processing()
        {
            var transaction1 = MakeTransaction(
                new Trade(1, 5465, "TATA", DateTime.Now.Date, 150),
                new Trade(2, 5467, "TATA", DateTime.Now.Date, 180)
            );

            var transaction2 = MakeTransaction(
                new Trade(2, 5467, "TOTO", DateTime.Now.Date, 190)
            );

            var log = new TransactionLog();
            log.NewTransaction(
                SerializationHelper.ObjectToBytes(transaction1, SerializationMode.ProtocolBuffers, false));
            log.NewTransaction(
                SerializationHelper.ObjectToBytes(transaction2, SerializationMode.ProtocolBuffers, false));
            var dummy = log.StartProcessing(); // call StartProcessing and close before EndProcessing
            log.Dispose();

            var engine = new PersistenceEngine();
            engine.Start();

            engine.WaitForPendingTransactions();

            engine.Stop();


            var processor = new TestProcessor();

            //reload data from persistent storage
            var unused = new ReliableStorage(processor);
            unused.LoadPersistentData();
            unused.Dispose();

            Assert.AreEqual(2, processor.LoadedObjects.Count);

            var reloaded = processor.LoadedObjects.Select(PackedObject.Unpack<Trade>).First(t => t.Id == 2);

            Assert.AreEqual("TOTO", reloaded.Folder);

            //reload data from transaction log. Check no more pending transactions
            log = new TransactionLog();
            Assert.AreEqual(0, log.PendingTransactionsCount);
            log.Dispose();
        }


        [Test]
        public void Start_the_persistence_engine_with_a_non_empty_transaction_log()
        {
            var transaction1 = MakeTransaction(
                new Trade(1, 5465, "TATA", DateTime.Now.Date, 150),
                new Trade(2, 5467, "TATA", DateTime.Now.Date, 180)
            );

            var transaction2 = MakeTransaction(
                new Trade(2, 5467, "TOTO", DateTime.Now.Date, 190)
            );

            var log = new TransactionLog();
            log.NewTransaction(
                SerializationHelper.ObjectToBytes(transaction1, SerializationMode.ProtocolBuffers, false));
            log.NewTransaction(
                SerializationHelper.ObjectToBytes(transaction2, SerializationMode.ProtocolBuffers, false));
            log.Dispose();

            var engine = new PersistenceEngine();
            engine.Start();

            engine.WaitForPendingTransactions();

            engine.Stop();


            var processor = new TestProcessor();

            //reload data from persistent storage
            var unused = new ReliableStorage(processor);
            unused.LoadPersistentData();
            unused.Dispose();

            Assert.AreEqual(2, processor.LoadedObjects.Count);

            var reloaded = processor.LoadedObjects.Select(PackedObject.Unpack<Trade>).First(t => t.Id == 2);

            Assert.AreEqual("TOTO", reloaded.Folder);

            //reload data from transaction log. Check no more pending transactions
            log = new TransactionLog();
            Assert.AreEqual(0, log.PendingTransactionsCount);
            log.Dispose();
        }



        [Test]
        public void Schema_is_serializable_as_json()
        {
            var schema = TypedSchemaFactory.FromType<Trade>();

            var json = JsonConvert.SerializeObject(schema);

            var schema1 = JsonConvert.DeserializeObject<CollectionSchema>(json);

            Assert.AreEqual(schema, schema1, "The schema can not be serialized to json");
        }
    }
}