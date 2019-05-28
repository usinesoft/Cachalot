using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace Server.Persistence
{
    public class ReliableStorage : IDisposable
    {
        private readonly string _backupPath;

        protected class BlockInfo
        {
            public BlockInfo(long offset, long transactionId)
            {
                Offset = offset;
                TransactionId = transactionId;
            }

            public long Offset { get; }

            /// <summary>
            /// The id of the last transaction that modified the block
            /// </summary>
            public long TransactionId { get; }
        }

       
        protected string DataPath { get; }

        public int CorruptedBlocks { get; private set; }
        public ReliableStorage(IPersistentObjectProcessor objectProcessor,  string workingDirectory = null, string backupPath = null, bool isBackup = false)
        {
            _backupPath = backupPath;

            // the backup path is an absolute path. The data path is relative to the working directory
            DataPath = isBackup ? _backupPath: workingDirectory != null ? Path.Combine(workingDirectory, Constants.DataPath): Constants.DataPath;

            ObjectProcessor = objectProcessor;
            IsBackup = isBackup;

            if (!Directory.Exists(DataPath))
            {
                Directory.CreateDirectory(DataPath);
            }

            string fullPath = Path.Combine(DataPath, StorageFileName);

            _storageStream = new FileStream(fullPath, FileMode.OpenOrCreate);

           
        }

        public void LoadPersistentData(bool useObjectProcessor = true)
        {
            // initialize the backup storage in parallel
            if (!IsBackup && _backupPath != null)
            {
                _backupStorage = new BackupStorage(_backupPath);

                _backupInitThread = new Thread(() =>
                {
                    _backupStorage.LoadPersistentData();
                    
                });

                _backupInitThread.Start();
            }

            bool success = false;

            bool needsRecovery = false;

            _storageStream.Seek(0, SeekOrigin.Begin);

            while (!success)
            {
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
            }


            // wait for the backup to be initialized
            if (_backupPath != null)
            {
                _backupInitThread.Join();
            }

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
            long nextOffset = FindNextBeginMarker(e.Offset + PersistentBlock.MinSize);

            long size = nextOffset - e.Offset;

            var dirtyBlock = PersistentBlock.MakeDirtyBlock(size);

            _storageStream.Seek(e.Offset, SeekOrigin.Begin);

            var writer = new BinaryWriter(_storageStream);

            dirtyBlock.Write(writer);

            InactiveBlockCount++;

            CorruptedBlocks++;
        }


        /// <summary>
        /// For recovery tests only
        /// </summary>
        /// <param name="primaryKey"></param>
        public void MakeCorruptedBlock(string primaryKey)
        {
            if(_blockInfoByPrimaryKey.TryGetValue(primaryKey, out var info))
            {
                var block = new PersistentBlock();
                _storageStream.Seek(info.Offset, SeekOrigin.Begin);
                var reader = new BinaryReader(_storageStream);
                block.Read(reader);

                block.Hash = block.Hash + 1; // the hash will not match so the block is corrupted
                _storageStream.Seek(info.Offset, SeekOrigin.Begin);

                var writer = new BinaryWriter(_storageStream);
                block.Write(writer);
            }
        }

        private long FindNextBeginMarker(long offset)
        {
            BinaryReader reader = new BinaryReader(_storageStream);
            for (long curOffset = offset; ; curOffset++)
            {
                try
                {
                    _storageStream.Seek(curOffset, SeekOrigin.Begin);
                    var marker = reader.ReadInt32();
                    if (marker == PersistentBlock.BeginMarkerValue)
                    {

                        return curOffset;
                    }
                }
                catch (EndOfStreamException)
                {
                    return curOffset;
                }
            }
        }

        private void LoadAll(bool useObjectProcessor)
        {
            var reader = new BinaryReader(_storageStream);

            var block = new PersistentBlock();
            long offset = 0;


            // read all the blocks
            while (block.Read(reader))
            {

                // get information for deleted blocks too as there may be an uncomplete delete transaction to 
                // reprocess
                if (block.BlockStatus == BlockStatus.Active || block.BlockStatus == BlockStatus.Deleted)
                {
                    _blockInfoByPrimaryKey[block.PrimaryKey] =
                        new BlockInfo(offset, block.LastTransactionId);
                    
                    // only active blocks contain objects 
                    if (useObjectProcessor && block.BlockStatus == BlockStatus.Active)
                    {
                        ObjectProcessor.Process(block.RawData);
                    }
                    
                }

                if(block.BlockStatus != BlockStatus.Active)
                {
                    InactiveBlockCount++;
                }

                offset = (int)_storageStream.Position;
            }

            if (useObjectProcessor)
            {
                ObjectProcessor.EndProcess();
            }

            StorageSize = offset;
        }

        public void Dispose()
        {
            _storageStream.Dispose();

            _backupStorage?.Dispose();
        }

        /// <summary>
        /// Remove dirty blocks thus compacting the storage
        /// </summary>
        public void CleanStorage()
        {
            if (File.Exists(TempFileName))
            {
                File.Delete(TempFileName);
            }

            string fullPath = Path.Combine(DataPath, StorageFileName);
            string tempPath = Path.Combine(DataPath, TempFileName);

            using (var tempStream = new FileStream(tempPath, FileMode.Create))
            {
                var writer = new BinaryWriter(tempStream);
                _storageStream.Dispose();

                _storageStream = new FileStream(fullPath, FileMode.OpenOrCreate);

                var reader = new BinaryReader(_storageStream);

                var block = new PersistentBlock();
                long offset = 0;


                // read all the blocks
                while (block.Read(reader))
                {
                    if (block.BlockStatus == BlockStatus.Active)
                    {
                        offset = (int) _storageStream.Position;
                        block.Write(writer);
                    }
                }

                StorageSize = offset;

                writer.Flush();
                _storageStream.Dispose();
            }

            File.Delete(fullPath);

            File.Move(tempPath, fullPath);


            _storageStream = new FileStream(fullPath, FileMode.OpenOrCreate);

            InactiveBlockCount = 0;
        }


        public void DeleteBlock(string primaryKey, int transactionId)
        {
            if (_blockInfoByPrimaryKey.TryGetValue(primaryKey, out var blockInfo))
            {
                _storageStream.Seek(blockInfo.Offset, SeekOrigin.Begin);

                var reader = new BinaryReader(_storageStream);
                var block = new PersistentBlock();
                block.Read(reader);
                if (block.BlockStatus != BlockStatus.Active)
                {
                    // if the same id ok. We are re executing a transaction that was not marked as Processed. 
                    // It happens if the server crashes during the update of persistence blocks. The transaction is simply played
                    // again when the server is restarted
                    if (block.LastTransactionId != transactionId)
                    {
                        throw new NotSupportedException($"Trying to delete an inactive block for primary key {primaryKey}");
                    }
                    
                }

                block.BlockStatus = BlockStatus.Deleted;
                block.LastTransactionId = transactionId;

                _storageStream.Seek(blockInfo.Offset, SeekOrigin.Begin);
                var writer = new BinaryWriter(_storageStream);
                block.Write(writer);
                writer.Flush();

                _blockInfoByPrimaryKey.Remove(primaryKey);

                InactiveBlockCount++;

                _backupStorage?.DeleteBlock(primaryKey, transactionId);
            }
            else
            {
                throw new NotSupportedException($"Active block not found for primary key {primaryKey}");
            }
        }

        /// <summary>
        /// Add a new bloc or update an existing one (depending if the primary key is already stored)
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

            if (_blockInfoByPrimaryKey.TryGetValue(primaryKey, out var blockInfo))
            {
                // load the old version of the block
                _storageStream.Seek(blockInfo.Offset, SeekOrigin.Begin);

                var reader = new BinaryReader(_storageStream);

                var oldBlock = new PersistentBlock();
                oldBlock.Read(reader);

                // if enough space is available do in-place update
                if (oldBlock.ReservedDataSize > block.UsedDataSize)
                {
                    block.ReservedDataSize = oldBlock.ReservedDataSize;

                    WriteBlock(block, blockInfo.Offset);
                }
                else // the old block is marked as deleted and the new version is added at the end of the stream
                {
                    oldBlock.BlockStatus = BlockStatus.Dirty;

                    InactiveBlockCount++;

                    WriteBlock(oldBlock, blockInfo.Offset);

                    block.ReservedDataSize = (int) (block.UsedDataSize * 1.5);
                    StorageSize = WriteBlock(block, StorageSize);

                    _blockInfoByPrimaryKey[primaryKey] = new BlockInfo(StorageSize, block.LastTransactionId);
                }
            }
            else // a new object not already in the persistent storage
            {
                // reserve some more space to allow for in-place updating
                block.ReservedDataSize = (int) (block.UsedDataSize * 1.5);

                var offset = StorageSize;
                StorageSize = WriteBlock(block, StorageSize);

                _blockInfoByPrimaryKey[primaryKey] = new BlockInfo(offset, block.LastTransactionId);
            }

            _backupStorage?.StoreBlock(data, primaryKey, transactionId);
        }

        public const string StorageFileName = "datastore.bin";
        private const string TempFileName = "datastore.tmp";

        private IPersistentObjectProcessor ObjectProcessor { get; }
        public bool IsBackup { get; }
        private Stream _storageStream;


        private readonly Dictionary<string, BlockInfo> _blockInfoByPrimaryKey = new Dictionary<string, BlockInfo>();
        private Thread _backupInitThread;
        private BackupStorage _backupStorage;

        public int InactiveBlockCount { get; private set; }

        public long StorageSize { get; private set; }

        public int BlockCount => _blockInfoByPrimaryKey.Count;

        public ISet<string> Keys => new HashSet<string>(_blockInfoByPrimaryKey.Keys);

        protected Dictionary<string, BlockInfo> BlockInfoByPrimaryKey => _blockInfoByPrimaryKey;

        protected Stream StorageStream => _storageStream;


        /// <summary>
        /// Write a block and return the offset after the block
        /// </summary>
        /// <param name="block"></param>
        /// <param name="offset"></param>
        /// <returns></returns>
        long WriteBlock(PersistentBlock block, long offset)
        {
            _storageStream.Seek(offset, SeekOrigin.Begin);
            var writer = new BinaryWriter(_storageStream);
            block.Write(writer);
            writer.Flush();

            return _storageStream.Position;
        }
    }
}