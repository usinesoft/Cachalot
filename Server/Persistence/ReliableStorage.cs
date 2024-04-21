#define DEBUG_VERBOSE
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Client;

namespace Server.Persistence;

public class ReliableStorage : IDisposable
{
    public const string StorageFileName = "datastore.bin";

    /// <summary>
    ///     Temporary copy o the data file used for recovery (after drop or if restore fails)
    /// </summary>
    public const string TempFileName = "datastore.tmp";

    private readonly string _backupPath;


    private Thread _backupInitThread;

    private BackupStorage _backupStorage;

    /// <summary>
    /// </summary>
    /// <param name="objectProcessor">an object processor is responsible to transform raw data blocks into useful objects</param>
    /// <param name="workingDirectory"></param>
    /// <param name="backupPath"></param>
    /// <param name="isBackup"></param>
    public ReliableStorage(IPersistentObjectProcessor objectProcessor, string workingDirectory = null,
                           string backupPath = null, bool isBackup = false)
    {
        if (isBackup && backupPath == null)
            throw new ArgumentException("For a backup storage, backup path must be specified");

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

    /// <summary>
    ///     Internally used for statistics
    /// </summary>
    public static int StoredInPlace { get; set; }

    /// <summary>
    ///     Internally used for statistics
    /// </summary>
    public static int Relocated { get; set; }

    protected string DataPath { get; }

    public int CorruptedBlocks { get; private set; }

    private IPersistentObjectProcessor ObjectProcessor { get; }
    public bool IsBackup { get; }

    public int InactiveBlockCount { get; private set; }

    public long StorageSize { get; private set; }

    public int BlockCount => BlockInfoByPrimaryKey.Count;

    public ISet<string> Keys => new HashSet<string>(BlockInfoByPrimaryKey.Keys);

    protected Dictionary<string, BlockInfo> BlockInfoByPrimaryKey { get; } = new();

    protected Stream StorageStream { get; private set; }

    public void Dispose()
    {
        StorageStream.Dispose();

        _backupStorage?.Dispose();
    }

    public void LightRestart()
    {
        var fullPath = Path.Combine(DataPath, StorageFileName);

        StorageStream = new FileStream(fullPath, FileMode.OpenOrCreate);
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
            _backupStorage = new(_backupPath);

            _backupInitThread = new(() => { _backupStorage.LoadPersistentData(); });

            _backupInitThread.Start();
        }

        var success = false;

        var needsRecovery = false;

        StorageStream.Seek(0, SeekOrigin.Begin);

        while (!success)
            try
            {
                LoadAll(useObjectProcessor);

                GC.Collect();

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
        var nextOffset = FindNextBeginMarker(e.Offset + PersistentBlock.MinSize, StorageStream);

        var size = nextOffset - e.Offset;

        var dirtyBlock = PersistentBlock.MakeDirtyBlock(size);

        StorageStream.Seek(e.Offset, SeekOrigin.Begin);

        var writer = new BinaryWriter(StorageStream);

        dirtyBlock.Write(writer);

        InactiveBlockCount++;

        CorruptedBlocks++;
    }


    /// <summary>
    ///     For recovery tests only. Creates a corrupted block on-purpose
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

    public static long FindNextBeginMarker(long offset, Stream stream)
    {
        var reader = new BinaryReader(stream);
        for (var curOffset = offset;; curOffset++)
            try
            {
                stream.Seek(curOffset, SeekOrigin.Begin);
                var marker = reader.ReadInt32();
                if (marker == PersistentBlock.BeginMarkerValue)
                {
                    stream.Seek(curOffset, SeekOrigin.Begin); // rewind the stream
                    return curOffset;
                }
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

        // read all the blocks
        while (block.Read(reader))
        {
            // get information for deleted blocks too as there may be an incomplete delete transaction to 
            // reprocess
            if (block.BlockStatus is BlockStatus.Active or BlockStatus.Deleted)
            {
                BlockInfoByPrimaryKey[block.PrimaryKey] =
                    new(block.Offset, block.LastTransactionId);

                Dbg.Trace($"read block {block.PrimaryKey} offset {block.Offset}");

                // only active blocks contain objects 
                if (useObjectProcessor && block.BlockStatus == BlockStatus.Active)
                    ObjectProcessor.Process(block.RawData);
            }

            if (block.BlockStatus != BlockStatus.Active) InactiveBlockCount++;
        }

        if (useObjectProcessor) ObjectProcessor.EndProcess(DataPath);

        StorageSize = StorageStream.Position;
    }


    public static void CompactAndRepair(string dataPath, string dataFile, string tempFile)
    {
        if (File.Exists(TempFileName)) File.Delete(TempFileName);

        var fullPath = Path.Combine(dataPath, StorageFileName);
        var tempPath = Path.Combine(dataPath, TempFileName);


        using (var tempStream = new FileStream(tempPath, FileMode.Create))
        {
            var writer = new BinaryWriter(tempStream);

            var readStream = new FileStream(fullPath, FileMode.OpenOrCreate);

            var reader = new BinaryReader(readStream);

            var block = new PersistentBlock();


            // read all the blocks
            while (block.Read(reader, true))
            {
                // if a corrupted block was found try to find the begin-marker of the next block
                if (!block.IsValidBlock())
                {
                    ServerLog.LogWarning($"Corrupted block was found: primary-key={block.PrimaryKey}");

                    FindNextBeginMarker(readStream.Position, readStream);
                }
                else if (block.BlockStatus == BlockStatus.Active)
                {
                    block.Write(writer);
                }

                block = new();
            }


            writer.Flush();
            readStream.Dispose();
        }

        File.Delete(fullPath);

        File.Move(tempPath, fullPath);
    }

    /// <summary>
    ///     Remove dirty blocks thus compacting the storage
    /// </summary>
    public void CleanStorage()
    {
        StorageStream.Dispose();

        CompactAndRepair(DataPath, StorageFileName, TempFileName);

        var fullPath = Path.Combine(DataPath, StorageFileName);

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

        

        if (BlockInfoByPrimaryKey.TryGetValue(primaryKey, out var blockInfo)) // updating an object
        {
            // load the old version of the block
            StorageStream.Seek(blockInfo.Offset, SeekOrigin.Begin);

            var reader = new BinaryReader(StorageStream);

            var oldBlock = new PersistentBlock();

            bool validBlock;
            try
            {
                oldBlock.Read(reader);

                validBlock = oldBlock.IsValidBlock();
            }
            catch (Exception)
            {
                validBlock = false;
            }

            
            switch (validBlock)
            {
                // If enough space is available do in-place update
                case true when oldBlock.ReservedDataSize > block.UsedDataSize:
                    block.ReservedDataSize = oldBlock.ReservedDataSize;

                    WriteBlock(block, blockInfo.Offset);
                    Dbg.Trace($"Store block in place {primaryKey} offset {blockInfo.Offset}");
                    
                    StoredInPlace++;
                    break;
                // Not enough space to store the new version in-place
                // Add it at the end as a new block and mark the old block as dirty
                case true:
                    // mark old block as dirty, so it will be removed during the next cleanup
                    oldBlock.BlockStatus = BlockStatus.Dirty;

                    InactiveBlockCount++;

                    Dbg.Trace($"Mark old block as dirty {primaryKey} offset {blockInfo.Offset}");

                    WriteBlock(oldBlock, blockInfo.Offset);

                    var offset1 = StorageSize; // will be stored at the end

                    // store the object in a new block at the end of the storage
                    block.ReservedDataSize = (int)(block.UsedDataSize * 1.5);
                    StorageSize = WriteBlock(block, StorageSize);

                    BlockInfoByPrimaryKey[primaryKey] = new(offset1, block.LastTransactionId);

                    Dbg.Trace($"Write new block at the end  {primaryKey} at offset {StorageSize}");

                    
                    Relocated++;
                    break;
                // The old block is not valid, this means the storage media has issues
                // We write the new block at the end of the storage
                case false:
                    
                    block.ReservedDataSize = (int)(block.UsedDataSize * 1.5);
                    Dbg.Trace($"Old block not valid. Write new block at the end {primaryKey} size in storage = {block.ReservedDataSize}  at offset {StorageSize}");

                    var offset = StorageSize; // will be stored at the end
                    
                    StorageSize = WriteBlock(block, StorageSize);

                    BlockInfoByPrimaryKey[primaryKey] = new(offset, block.LastTransactionId);
                    
                    break;
            }
            
        }
        else // a new object not already in the persistent storage
        {
            // reserve some more space to allow for in-place updating
            block.ReservedDataSize = (int)(block.UsedDataSize * 1.5);

            var offset = StorageSize;
            Dbg.Trace($"New object. Write new block at the end  {primaryKey} size in storage = {block.ReservedDataSize} at offset {StorageSize}");

            StorageSize = WriteBlock(block, StorageSize);

            
            BlockInfoByPrimaryKey[primaryKey] = new(offset, block.LastTransactionId);
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