using Client;
using Client.Core;
using Client.Interface;
using Server.FullTextSearch;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Server.Persistence
{
    public class PersistenceEngine : ITransactionLog
    {
        private readonly Services _serviceContainer;
        private readonly object _schemaSync = new object();


        private volatile bool _shouldContinue = true;

        private Task _singleConsumer;
        private ReliableStorage _storage;

        public int PendingTransactions => TransactionLog.PendingTransactionsCount;

        public PersistenceEngine(DataContainer dataContainer = null, string workingDirectory = null,
            Services serviceContainer = null)
        {
            _serviceContainer = serviceContainer;
            Container = dataContainer;
            WorkingDirectory = workingDirectory;

            SchemaFilePath = Path.Combine(DataPath, Constants.SchemaFileName);
            SequenceFilePath = Path.Combine(DataPath, Constants.SequenceFileName);
        }

        private TransactionLog TransactionLog { get; set; }


        private string DataPath => WorkingDirectory != null
            ? Path.Combine(WorkingDirectory, Constants.DataPath)
            : Constants.DataPath;

        private string RollbackDataPath => WorkingDirectory != null
            ? Path.Combine(WorkingDirectory, Constants.RollbackDataPath)
            : Constants.RollbackDataPath;

        private string SchemaFilePath { get; }
        private string SequenceFilePath { get; }

        public DataContainer Container { get; set; }
        private string WorkingDirectory { get; }


        public void WaitForPendingTransactions()
        {
            Dbg.Trace($"{TransactionLog.PendingTransactionsCount} pending transactions");

            while (TransactionLog.PendingTransactionsCount > 0) Thread.Sleep(10);
        }


        public void Start()
        {
            _storage = Container != null
                ? new ReliableStorage(new ObjectProcessor(Container), WorkingDirectory)
                : new ReliableStorage(new NullObjectProcessor(), WorkingDirectory);


            ServerLog.LogInfo("start processing schema");

            // Stage 1  load the schema and
            // register types (this will create an in-memory data store for each type)

            lock (_schemaSync)
            {
                Container?.LoadSchema(SchemaFilePath);
                Container?.LoadSequence(SequenceFilePath);
            }

            ServerLog.LogInfo("end processing schema");

            ServerLog.LogInfo("start loading transaction log");
            TransactionLog = new TransactionLog(WorkingDirectory);
            ServerLog.LogInfo("end loading transaction log");

            // if there are pending transactions data needs to be loaded twice
            // first without processing the persistent objects; simply initializing offset tables
            // second after processing pending transactions reload and store objects in memory
            if (TransactionLog.PendingTransactionsCount > 0)
            {
                ServerLog.LogInfo("start loading data before transaction processing");
                // load once without processing the objects
                _storage.LoadPersistentData(false);
                ServerLog.LogInfo("end loading data before transaction processing");
            }

            // start the thread that will process pending transactions
            StartProcessingTransactions();

            ServerLog.LogInfo("start waiting for pending transactions to be processed");
            // Stage 2 wait for the persistence engine to commit all the pending transactions in the log
            // in the storage


            WaitForPendingTransactions();

            ServerLog.LogInfo("end waiting for pending transactions");

            // Stage 3 house keeping

            ServerLog.LogInfo("start compacting the transaction log");
            TransactionLog.ClearLog();
            ServerLog.LogInfo("end compacting the transaction log");

            ServerLog.LogInfo("start compacting the persistence storage");
            _storage.CleanStorage();
            ServerLog.LogInfo("end compacting the persistence storage");

            ServerLog.LogInfo("begin load data");
            // Stage 4 load data from persistent storage to memory
            _storage.LoadPersistentData();
            ServerLog.LogInfo("end load data");
        }


        /// <summary>
        ///     Restart after some administrative tasks that do not need data to be loaded from persistent storage
        /// </summary>
        public void LightStart(bool resetStorage = false)
        {

            if (resetStorage)
            {
                _storage = new ReliableStorage(new NullObjectProcessor(), WorkingDirectory);
            }
            else
            {
                _storage.LightRestart();
            }


            TransactionLog = new TransactionLog(WorkingDirectory);

            StartProcessingTransactions();
        }


        /// <summary>
        /// When items are stored in the transaction log they are not yet indexed in memory so if they contain full-text data
        /// it is not tokenized yet. While storing an item from the transaction log in the persistent storage, get the tokenized full-text
        /// if available or tokenize it otherwise. It is important to avoid tokenization while reloading the database
        /// </summary>
        /// <param name="item"></param>
        /// <returns></returns>
        PackedObject GetItemWithTokenizedFullText(PackedObject item)
        {

            if (item.FullText is { Length: > 0 })
            {
                var dataStore = Container.TryGetByName(item.CollectionName);

                PackedObject result = item;
                if (dataStore != null)
                {
                    var lockMgr = _serviceContainer.LockManager;

                    lockMgr.DoWithReadLock(() =>
                    {
                        if (dataStore.DataByPrimaryKey.TryGetValue(item.PrimaryKey, out var found))
                        {
                            if (found.TokenizedFullText != null && found.TokenizedFullText.Count > 0
                            ) // tokenized full-text available
                            {

                                result = found;
                            }
                        }

                    }, dataStore.CollectionSchema.CollectionName);

                    return result;
                }

            }
            else // no full-text data
            {
                return item;
            }


            // It may reach this point when commiting to persistent storage a pending transactions from the transaction log
            // This happens in the early stages of the startup before loading items into memory
            item.TokenizedFullText = Tokenizer.Tokenize(item.FullText);
            return item;
        }

        private void StartProcessingTransactions()
        {
            _shouldContinue = true;
            _singleConsumer = Task.Run(() =>
            {
                while (_shouldContinue)
                    try
                    {
                        // this call is blocking if nothing to process
                        var persistentTransaction = TransactionLog.StartProcessing();

                        if (persistentTransaction != null &&
                            persistentTransaction.TransactionStatus != TransactionStatus.Canceled)
                        {
                            var data = persistentTransaction.Data;
                            var transaction =
                                SerializationHelper.ObjectFromBytes<DurableTransaction>(data,
                                    SerializationMode.ProtocolBuffers,
                                    false);

                            if (transaction is MixedDurableTransaction mixedTransaction)
                            {
                                foreach (var item in mixedTransaction.ItemsToPut)
                                {
                                    var itemFromMemory = GetItemWithTokenizedFullText(item);

                                    var itemData =
                                        SerializationHelper.ObjectToBytes(itemFromMemory, SerializationMode.ProtocolBuffers,
                                            false);

                                    Dbg.Trace(
                                        $"storing persistent block for object {item} transaction={persistentTransaction.Id}");
                                    _storage.StoreBlock(itemData, item.GlobalKey,
                                        unchecked((int)persistentTransaction.Id));
                                }

                                foreach (var item in mixedTransaction.GlobalKeysToDelete)
                                {
                                    Dbg.Trace($"deleting persistent block {persistentTransaction.Id}");
                                    _storage.DeleteBlock(item, unchecked((int)persistentTransaction.Id));
                                }
                            }

                            if (transaction is PutDurableTransaction putTransaction)
                            {

                                Dbg.Trace($"START storing put transaction containing {putTransaction.Items.Count} items");
                                foreach (var item in putTransaction.Items)
                                {
                                    var itemFromMemory = GetItemWithTokenizedFullText(item);

                                    var itemData =
                                        SerializationHelper.ObjectToBytes(itemFromMemory,
                                            SerializationMode.ProtocolBuffers,
                                            false);

                                    Dbg.Trace(
                                        $"storing persistent block for object {item} transaction={persistentTransaction.Id}");
                                    _storage.StoreBlock(itemData, item.GlobalKey,
                                        unchecked((int)persistentTransaction.Id));
                                }
                                Dbg.Trace($"END storing put transaction containing {putTransaction.Items.Count} items");
                            }

                            if (transaction is DeleteDurableTransaction deleteTransaction)
                                foreach (var item in deleteTransaction.GlobalKeysToDelete)
                                {
                                    Dbg.Trace($"deleting persistent block {persistentTransaction.Id}");
                                    _storage.DeleteBlock(item, unchecked((int)persistentTransaction.Id));
                                }

                            TransactionLog.EndProcessing(persistentTransaction);
                            persistentTransaction = null;
                            GC.Collect();
                        }
                        else
                        {
                            return;
                        }
                    }
                    catch (Exception e)
                    {
                        Dbg.Trace(e.Message);
                        //TODO add proper logging
                    }
            });

        }

        public void NewTransaction(DurableTransaction durableTransaction, bool isDelayed = false)
        {
            var delayInMilliseconds = isDelayed ? Constants.DelayForTwoStageTransactionsInMilliseconds : 0;

            try
            {
                TransactionLog.NewTransaction(
                    SerializationHelper.ObjectToBytes(durableTransaction, SerializationMode.ProtocolBuffers, false),
                    delayInMilliseconds);
            }
            catch (Exception e)
            {
                throw new CacheException(e.Message, ExceptionType.ErrorWritingDataInTransactionLog);
            }
        }


        public void Stop()
        {
            _shouldContinue = false;

            _singleConsumer.Wait(500);

            TransactionLog.Dispose();

            _storage.Dispose();
        }


        public void StoreDataForRollback()
        {
            if (Directory.Exists(RollbackDataPath)) Directory.Delete(RollbackDataPath, true);

            Directory.Move(DataPath, RollbackDataPath);
        }

        public void RollbackData()
        {
            if (Directory.Exists(DataPath)) Directory.Delete(DataPath, true);

            Directory.Move(RollbackDataPath, DataPath);
        }

        public void DeleteRollbackData()
        {
            if (Directory.Exists(RollbackDataPath)) Directory.Delete(RollbackDataPath, true);
        }



        public void CancelDelayedTransaction()
        {
            TransactionLog.CancelTransaction();
        }
    }
}