using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace Server.Persistence
{
    public class ReliableStorage : IDisposable
    {
        public const string StorageFileName = "datastore.bin";
        private const string TempFileName = "datastore.tmp";
        private readonly string _backupPath;


        private Thread _backupInitThread;
        private BackupStorage _backupStorage;

        public ReliableStorage(IPersistentObjectProcessor objectProcessor, string workingDirectory = null,
            string backupPath = null, bool isBackup = false)
        {
            _backupPath = backupPath;

            // the backup path is an absolute path. The data path is relative to the working directory
            DataPath = isBackup ? _backupPath :
                workingDirectory != null ? Path.Combine(workingDirectory, Constants.DataPath) : Constants.DataPath;

            ObjectProcessor = objectProcessor;
            IsBackup = isBackup;

            if (!Directory.Exists(DataPath)) Directory.CreateDirectory(DataPath);

            var fullPath = Path.Combine(DataPath, StorageFileName);

            StorageStream = new FileStream(fullPath, FileMode.OpenOrCreate);
        }


        protected string DataPath { get; }

        public int CorruptedBlocks { get; private set; }

        private IPersistentObjectProcessor ObjectProcessor { get; }
        public bool IsBackup { get; }

        public int InactiveBlockCount { get; private set; }

        public long StorageSize { get; private set; }

        public int BlockCount => BlockInfoByPrimaryKey.Count;

        public ISet<string> Keys => new HashSet<string>(BlockInfoByPrimaryKey.Keys);

        protected Dictionary<string, BlockInfo> BlockInfoByPrimaryKey { get; } = new Dictionary<string, BlockInfo>();

        protected Stream StorageStream { get; private set; }

        public void Dispose()
        {
            StorageStream.Dispose();

            _backupStorage?.Dispose();
        }

        public void LoadPersistentData(bool useObjectProcessor = true)
        {
            bool IsPrimaryStorage()
            {
                return !IsBackup && _backupPath != null;
            }

            // initialize the backup storage in parallel
            if (IsPrimaryStorage())
            {
                _backupStorage = new BackupStorage(_backupPath);

                _backupInitThread = new Thread(() => { _backupStorage.LoadPersistentData(); });

                _backupInitThread.Start();
            }

            var success = false;

            var needsRecovery = false;

            StorageStream.Seek(0, SeekOrigin.Begin);

            while (!success)
                try
                {
                    LoadAll(useObjectProcessor);

                    success = true;
                }
                catch (InvalidBlockException e)
                {
                    needsRecovery = true;
                    MarkInvalidBlocksAsDirty(e);
                }


            // wait for the backup to be initialized
            if (IsPrimaryStorage()) _backupInitThread.Join();

            // The storage was repaired but blocks have been lost. If a backup is available retrieve the 
            // missing block from the backup
            if (needsRecovery && _backupStorage != null)
            {
                var missingBlockKeys = new HashSet<string>(_backupStorage.Keys);

                missingBlockKeys.ExceptWith(Keys);

                foreach (var blockKey in missingBlockKeys)
                {
                    var block = _backupStorage.ReadBlock(blockKey);
                    StoreBlock(block.RawData, block.PrimaryKey, block.LastTransactionId);
                }
            }
        }

        private void MarkInvalidBlocksAsDirty(InvalidBlockException e)
        {
            var nextOffset = FindNextBeginMarker(e.Offset + PersistentBlock.MinSize);

            var size = nextOffset - e.Offset;

            var dirtyBlock = PersistentBlock.MakeDirtyBlock(size);

            StorageStream.Seek(e.Offset, SeekOrigin.Begin);

            var writer = new BinaryWriter(StorageStream);

            dirtyBlock.Write(writer);

            InactiveBlockCount++;

            CorruptedBlocks++;
        }


        /// <summary>
        ///     For recovery tests only
        /// </summary>
        /// <param name="primaryKey"></param>
        public void MakeCorruptedBlock(string primaryKey)
        {
            if (BlockInfoByPrimaryKey.TryGetValue(primaryKey, out var info))
            {
                var block = new PersistentBlock();
                StorageStream.Seek(info.Offset, SeekOrigin.Begin);
                var reader = new BinaryReader(StorageStream);
                block.Read(reader);

                block.Hash = block.Hash + 1; // the hash will not match so the block is corrupted
                StorageStream.Seek(info.Offset, SeekOrigin.Begin);

                var writer = new BinaryWriter(StorageStream);
                block.Write(writer);
            }
        }

        private long FindNextBeginMarker(long offset)
        {
            var reader = new BinaryReader(StorageStream);
            for (var curOffset = offset;; curOffset++)
                try
                {
                    StorageStream.Seek(curOffset, SeekOrigin.Begin);
                    var marker = reader.ReadInt32();
                    if (marker == PersistentBlock.BeginMarkerValue) return curOffset;
                }
                catch (EndOfStreamException)
                {
                    return curOffset;
                }
        }

        private void LoadAll(bool useObjectProcessor)
        {
            var reader = new BinaryReader(StorageStream);

            var block = new PersistentBlock();
            long offset = 0;


            // read all the blocks
            while (block.Read(reader))
            {
                offset = block.Offset + block.StorageSize;
                // get information for deleted blocks too as there may be an incomplete delete transaction to 
                // reprocess
                if (block.BlockStatus == BlockStatus.Active || block.BlockStatus == BlockStatus.Deleted)
                {
                    BlockInfoByPrimaryKey[block.PrimaryKey] =
                        new BlockInfo(block.Offset, block.LastTransactionId);

                    // only active blocks contain objects 
                    if (useObjectProcessor && block.BlockStatus == BlockStatus.Active)
                        ObjectProcessor.Process(block.RawData);
                }

                if (block.BlockStatus != BlockStatus.Active) InactiveBlockCount++;

            }

            if (useObjectProcessor) ObjectProcessor.EndProcess(DataPath);

            StorageSize = offset;
        }

        /// <summary>
        ///     Remove dirty blocks thus compacting the storage
        /// </summary>
        public void CleanStorage()
        {
            if (File.Exists(TempFileName)) File.Delete(TempFileName);

            var fullPath = Path.Combine(DataPath, StorageFileName);
            var tempPath = Path.Combine(DataPath, TempFileName);

            using (var tempStream = new FileStream(tempPath, FileMode.Create))
            {
                var writer = new BinaryWriter(tempStream);
                StorageStream.Dispose();

                StorageStream = new FileStream(fullPath, FileMode.OpenOrCreate);

                var reader = new BinaryReader(StorageStream);

                var block = new PersistentBlock();
                long offset = 0;


                // read all the blocks
                while (block.Read(reader))
                    if (block.BlockStatus == BlockStatus.Active)
                    {
                        offset = (int) StorageStream.Position;
                        block.Write(writer);
                    }

                StorageSize = offset;

                writer.Flush();
                StorageStream.Dispose();
            }

            File.Delete(fullPath);

            File.Move(tempPath, fullPath);


            StorageStream = new FileStream(fullPath, FileMode.OpenOrCreate);

            InactiveBlockCount = 0;
        }


        public void DeleteBlock(string primaryKey, int transactionId)
        {
            if (BlockInfoByPrimaryKey.TryGetValue(primaryKey, out var blockInfo))
            {
                StorageStream.Seek(blockInfo.Offset, SeekOrigin.Begin);

                var reader = new BinaryReader(StorageStream);
                var block = new PersistentBlock();
                block.Read(reader);
                if (block.BlockStatus != BlockStatus.Active)
                    // if the same id ok. We are re executing a transaction that was not marked as Processed. 
                    // It happens if the server crashes during the update of persistence blocks. The transaction is simply played
                    // again when the server is restarted
                    if (block.LastTransactionId != transactionId)
                        throw new NotSupportedException(
                            $"Trying to delete an inactive block for primary key {primaryKey}");

                block.BlockStatus = BlockStatus.Deleted;
                block.LastTransactionId = transactionId;

                StorageStream.Seek(blockInfo.Offset, SeekOrigin.Begin);
                var writer = new BinaryWriter(StorageStream);
                block.Write(writer);
                writer.Flush();

                BlockInfoByPrimaryKey.Remove(primaryKey);

                InactiveBlockCount++;

                _backupStorage?.DeleteBlock(primaryKey, transactionId);
            }
            else
            {
                throw new NotSupportedException($"Active block not found for primary key {primaryKey}");
            }
        }

        /// <summary>
        ///     Add a new bloc or update an existing one (depending if the primary key is already stored)
        /// </summary>
        /// <param name="data"></param>
        /// <param name="primaryKey"></param>
        /// <param name="transactionId"></param>
        public void StoreBlock(byte[] data, string primaryKey, int transactionId)
        {
            var block = new PersistentBlock
            {
                RawData = data,
                PrimaryKey = primaryKey,
                BlockStatus = BlockStatus.Active,
                LastTransactionId = transactionId,
                UsedDataSize = data.Length
            };

            if (BlockInfoByPrimaryKey.TryGetValue(primaryKey, out var blockInfo))
            {
                // load the old version of the block
                StorageStream.Seek(blockInfo.Offset, SeekOrigin.Begin);

                var reader = new BinaryReader(StorageStream);

                var oldBlock = new PersistentBlock();

                var validBlock = true;
                try
                {
                    oldBlock.Read(reader);
                }
                catch (Exception )
                {
                    validBlock = false;
                }

                // if enough space is available do in-place update
                if (validBlock && oldBlock.ReservedDataSize > block.UsedDataSize)
                {
                    block.ReservedDataSize = oldBlock.ReservedDataSize;

                    WriteBlock(block, blockInfo.Offset);
                }
                else // the old block is marked as deleted and the new version is added at the end of the stream
                {
                    if (validBlock)
                    {
                        oldBlock.BlockStatus = BlockStatus.Dirty;

                        InactiveBlockCount++;

                        WriteBlock(oldBlock, blockInfo.Offset);

                    }
                    
                    block.ReservedDataSize = (int) (block.UsedDataSize * 1.5);
                    StorageSize = WriteBlock(block, StorageSize);

                    BlockInfoByPrimaryKey[primaryKey] = new BlockInfo(StorageSize, block.LastTransactionId);
                }
            }
            else // a new object not already in the persistent storage
            {
                // reserve some more space to allow for in-place updating
                block.ReservedDataSize = (int) (block.UsedDataSize * 1.5);

                var offset = StorageSize;
                StorageSize = WriteBlock(block, StorageSize);

                BlockInfoByPrimaryKey[primaryKey] = new BlockInfo(offset, block.LastTransactionId);
            }

            _backupStorage?.StoreBlock(data, primaryKey, transactionId);
        }


        /// <summary>
        ///     Write a block and return the offset after the block
        /// </summary>
        /// <param name="block"></param>
        /// <param name="offset"></param>
        /// <returns></returns>
        private long WriteBlock(PersistentBlock block, long offset)
        {
            StorageStream.Seek(offset, SeekOrigin.Begin);
            var writer = new BinaryWriter(StorageStream);
            block.Write(writer);
            writer.Flush();

            return StorageStream.Position;
        }

        protected class BlockInfo
        {
            public BlockInfo(long offset, long transactionId)
            {
                Offset = offset;
                TransactionId = transactionId;
            }

            public long Offset { get; }

            /// <summary>
            ///     The id of the last transaction that modified the block
            /// </summary>
            public long TransactionId { get; }
        }
    }
}