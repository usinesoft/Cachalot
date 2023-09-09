using System;
using System.IO;

namespace Server.Persistence;

/// <summary>
///     A specialized storage used for backup. It can load individual blocks to be used for the recovery of a main storage
/// </summary>
public class BackupStorage : ReliableStorage
{
    public BackupStorage(string backupPath) : base(new NullObjectProcessor(), null, backupPath, true)
    {
    }


    public PersistentBlock ReadBlock(string primaryKey)
    {
        if (BlockInfoByPrimaryKey.TryGetValue(primaryKey, out var info))
        {
            var block = new PersistentBlock();

            StorageStream.Seek(info.Offset, SeekOrigin.Begin);
            var reader = new BinaryReader(StorageStream);

            block.Read(reader);

            return block;
        }

        throw new NotSupportedException("primary key not found in backup storage");
    }
}