using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Client.Core;
using Client.Interface;
using NUnit.Framework;
using Server.Persistence;
using UnitTests.TestData;

namespace UnitTests
{
    [TestFixture]
    public class TestFixturePersistenceEngine
    {
        private class TestProcessor : IPersistentObjectProcessor
        {
            public IList<CachedObject> LoadedObjects { get; } = new List<CachedObject>();

            public void Process(byte[] data)
            {
                var obj = SerializationHelper.ObjectFromBytes<CachedObject>(data, SerializationMode.ProtocolBuffers,
                    false);

                LoadedObjects.Add(obj);
            }

            public void EndProcess()
            {
            }
        }

        private readonly string _backupPath = Path.Combine("backup", ReliableStorage.StorageFileName);
        private ClientSideTypeDescription _typeDescription;


        [SetUp]
        public void SetUp()
        {
            if (Directory.Exists(Constants.DataPath)) Directory.Delete(Constants.DataPath, true);


            if (File.Exists(_backupPath)) File.Delete(_backupPath);
        }

        [OneTimeSetUp]
        public void RunBeforeAnyTests()
        {
            Environment.CurrentDirectory = TestContext.CurrentContext.TestDirectory;
            Directory.SetCurrentDirectory(TestContext.CurrentContext.TestDirectory);

            var config = new ClientConfig();
            config.LoadFromFile("inprocess_config.xml");

            foreach (var typeDescription in config.TypeDescriptions)
            {
                if (typeDescription.Value.FullTypeName.Contains("Trade"))
                {
                    _typeDescription = ClientSideTypeDescription.RegisterType(typeof(Trade), typeDescription.Value);
                }                    
            }
            
        }


        private  Transaction MakeTransaction<T>(params T[] items)
        {
            var transaction = new PutTransaction();

            foreach (var item in items)
            {
                var packed = CachedObject.Pack(item, _typeDescription);
                transaction.Items.Add(packed);
            }

            return transaction;
        }

        private  Transaction MakeDeleteTransaction<T>(params T[] items)
        {
            var transaction = new DeleteTransaction();

            foreach (var item in items)
            {
                var packed = CachedObject.Pack(item, _typeDescription);
                transaction.ItemsToDelete.Add(packed);
            }

            return transaction;
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

            var reloaded = CachedObject.Unpack<Trade>(processor.LoadedObjects[0]);

            Assert.AreEqual("TATA", reloaded.Folder);
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

            var reloaded = processor.LoadedObjects.Select(CachedObject.Unpack<Trade>).First(t => t.Id == 2);

            Assert.AreEqual("TOTO", reloaded.Folder);

            //reload data from transaction log. Check no more pending transactions
            var log = new TransactionLog();
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
                SerializationHelper.ObjectToBytes(transaction1, SerializationMode.ProtocolBuffers, null));
            log.NewTransaction(
                SerializationHelper.ObjectToBytes(transaction2, SerializationMode.ProtocolBuffers, null));
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

            var reloaded = processor.LoadedObjects.Select(CachedObject.Unpack<Trade>).First(t => t.Id == 2);

            Assert.AreEqual("TOTO", reloaded.Folder);

            //reload data from transaction log. Check no more pending transactions
            log = new TransactionLog();
            Assert.AreEqual(0, log.PendingTransactionsCount);
            log.Dispose();
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
                SerializationHelper.ObjectToBytes(transaction1, SerializationMode.ProtocolBuffers, null));
            log.NewTransaction(
                SerializationHelper.ObjectToBytes(transaction2, SerializationMode.ProtocolBuffers, null));
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

            var reloaded = processor.LoadedObjects.Select(CachedObject.Unpack<Trade>).First(t => t.Id == 2);

            Assert.AreEqual("TOTO", reloaded.Folder);

            //reload data from transaction log. Check no more pending transactions
            log = new TransactionLog();
            Assert.AreEqual(0, log.PendingTransactionsCount);
            log.Dispose();
        }
    }
}