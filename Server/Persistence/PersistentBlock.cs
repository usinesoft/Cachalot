using System;
using System.IO;

namespace Server.Persistence
{
    /// <summary>
    ///     Object stored in a persistent store
    ///     Limits are explicitly marked and can also be retrieved from Offset, Offset + ReservedSpace
    ///     to allow for data recovery in case of disaster
    /// </summary>
    public class PersistentBlock
    {
        public const int BeginMarkerValue = 0xABCD;

        public const int EndMarkerValue = 0xDCBA;
        public static readonly long MinSize = 35;

        private byte[] _rawData;


        public int BeginMarker { get; set; } = BeginMarkerValue;

        public int EndMarker { get; set; } = EndMarkerValue;

        public string PrimaryKey { get; set; }

        /// <summary>
        ///     The id of the last transaction that modified the block
        /// </summary>
        public int LastTransactionId { get; set; }

        public BlockStatus BlockStatus { get; set; }

        public int UsedDataSize { get; set; }
        public long Offset { get; set; }



        /// <summary>
        ///     We reserve more space than really used in oder to allow for in-place updates in most cases
        /// </summary>
        public int ReservedDataSize { get; set; }

        public bool HashOk { get; private set; } = true;

        public byte[] RawData
        {
            get => _rawData;
            set
            {
                _rawData = value;

                Hash = FastHash(value);
            }
        }

        internal int Hash { get; set; }

        /// <summary>
        ///     Create a valid but dirty block that fills the specified size
        ///     Used in the recovery procedure for corrupted data files
        /// </summary>
        /// <param name="size"></param>
        /// <returns></returns>
        public static PersistentBlock MakeDirtyBlock(long size)
        {
            if (size < MinSize) throw new NotSupportedException("A block can not be smaller than the minimum size");

            return new PersistentBlock
            {
                PrimaryKey = "#",
                RawData = new[] {(byte) 0},
                UsedDataSize = 1,
                LastTransactionId = 0,
                BlockStatus = BlockStatus.Dirty,
                ReservedDataSize = (int) (size - MinSize + 1)
            };
        }


        public bool Read(BinaryReader reader, bool silent = false)
        {
            var insideBlock = false;

            var offset = reader.BaseStream.Position;

            Offset = offset;

            try
            {
                BeginMarker = reader.ReadInt32();

                if (BeginMarker != BeginMarkerValue && !silent)
                    throw new InvalidBlockException(offset) {BeginMarkerKo = true};

                insideBlock = true;

                PrimaryKey = reader.ReadString();

                //if (PrimaryKey == "StressTests.AbstractEntity4b612432-118f-4fe9-ad95-10947e686e4d")
                //{

                //}
                LastTransactionId = reader.ReadInt32();

                BlockStatus = (BlockStatus) reader.ReadInt32();

                LastTransactionId = reader.ReadInt32();
                UsedDataSize = reader.ReadInt32();
                ReservedDataSize = reader.ReadInt32();

                RawData = reader.ReadBytes(UsedDataSize);

                // can arrive only if data was really corrupted
                if (ReservedDataSize < UsedDataSize)
                {
                    if (silent)
                        return true;

                    throw new InvalidBlockException(offset) {CorruptedBlock = true};

                }

                reader.ReadBytes(ReservedDataSize - UsedDataSize); // discard the padding bytes

                Hash = reader.ReadInt32();

                EndMarker = reader.ReadInt32();

                if (Hash != FastHash(_rawData))
                {
                    HashOk = false;
                    if (!silent) throw new InvalidBlockException(offset) {HashKo = true};
                }


                if (EndMarker != EndMarkerValue && !silent)
                    throw new InvalidBlockException(offset) {EndMarkerKo = true};


                return true;
            }
            catch (EndOfStreamException)
            {
                if (insideBlock) throw new InvalidBlockException(offset) {IncompleteBlock = true};

                // ignore otherwise: end of stream
                return false;
            }
            finally
            {
                offset = reader.BaseStream.Position;
                StorageSize = offset - Offset;
            }
        }

        public long StorageSize { get; set; }

        public void Write(BinaryWriter writer)
        {
            writer.Write(BeginMarker);

            writer.Write(PrimaryKey);
            writer.Write(LastTransactionId);

            writer.Write((int) BlockStatus);

            writer.Write(LastTransactionId);

            writer.Write(UsedDataSize);
            writer.Write(ReservedDataSize);

            writer.Write(RawData);

            var padding = new byte[ReservedDataSize - UsedDataSize];
            writer.Write(padding);

            writer.Write(Hash);

            writer.Write(EndMarker);
        }

        private static int FastHash(byte[] val)
        {
            unchecked
            {
                var h = 1;

                var len = val.Length;

                for (var i = 0; i + 3 < len; i += 4)
                    h = 31 * 31 * 31 * 31 * h
                        + 31 * 31 * 31 * val[i]
                        + 31 * 31 * val[i + 1]
                        + 31 * val[i + 2]
                        + val[i + 3];

                return h;
            }
        }

        public override string ToString()
        {
            return $"{nameof(LastTransactionId)}: {LastTransactionId}, {nameof(BlockStatus)}: {BlockStatus}, {nameof(UsedDataSize)}: {UsedDataSize}, {nameof(ReservedDataSize)}: {ReservedDataSize}, {nameof(HashOk)}: {HashOk}";
        }
    }
}