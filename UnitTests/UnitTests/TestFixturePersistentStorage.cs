using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Server.Persistence;

namespace Tests.UnitTests
{
    [TestFixture]
    public class TestFixturePersistentStorage
    {
        [SetUp]
        public void SetUp()
        {
            if (File.Exists(_path)) File.Delete(_path);

            if (File.Exists(_backupPath)) File.Delete(_backupPath);
        }

        [TearDown]
        public void TearDown()
        {
            if (File.Exists(_path)) File.Delete(_path);

            if (File.Exists(_backupPath)) File.Delete(_backupPath);
        }

        private class NullProcessor : IPersistentObjectProcessor
        {
            public List<byte[]> ProcessedBlocks { get; } = new List<byte[]>();

            public void Process(byte[] data)
            {
                ProcessedBlocks.Add(data);
            }

            public void EndProcess(string dataPath)
            {
            }
        }


        private readonly string _path = Path.Combine(Constants.DataPath, ReliableStorage.StorageFileName);
        private readonly string _backupPath = Path.Combine("backup", ReliableStorage.StorageFileName);


        [Test]
        public void Create_then_delete_object()
        {
            var data = new byte[] { 1, 2, 3 };

            // add two new blocks
            using (var storage = new ReliableStorage(new NullProcessor()))
            {
                storage.LoadPersistentData();

                storage.StoreBlock(data, "a1", 150);
                storage.StoreBlock(data, "a2", 150);

                Assert.AreEqual(2, storage.BlockCount);
            }

            var processor = new NullProcessor();
            using (var storage = new ReliableStorage(processor))
            {
                storage.LoadPersistentData();

                storage.LoadPersistentData();
                Assert.AreEqual(2, storage.BlockCount);

                storage.DeleteBlock("a1", 44);
            }

            processor = new NullProcessor();
            using (var storage = new ReliableStorage(processor))
            {
                storage.LoadPersistentData();
                Assert.AreEqual(2, storage.BlockCount); // deleted blocks are counted too
            }

            Assert.AreEqual(1, processor.ProcessedBlocks.Count);
            CollectionAssert.AreEqual(data, processor.ProcessedBlocks[0]);
        }

        [Test]
        public void Performance_test()
        {
            var data = new byte[1000];
            for (var i = 0; i < 1000; i++) data[i] = (byte)(i % 255);

            var data1 = new byte[2000];
            for (var i = 0; i < 2000; i++) data1[i] = (byte)(i % 255);

            using (var storage = new ReliableStorage(new NullProcessor()))
            {
                storage.LoadPersistentData();

                const int count = 10000;
                var sw = new Stopwatch();
                sw.Start();
                for (var i = 0; i < count; i++) storage.StoreBlock(data, i.ToString(), 155);

                sw.Stop();

                Console.WriteLine(
                    $"Writing {count} blocks to persistent storage took {sw.ElapsedMilliseconds} milliseconds");
            }


            {
                var w = new Stopwatch();
                var processor = new NullProcessor();

                w.Start();
                using (var storage = new ReliableStorage(processor))
                {
                    storage.LoadPersistentData();
                }

                w.Stop();

                Console.WriteLine(
                    $"Loading {processor.ProcessedBlocks.Count} blocks from persistent storage took {w.ElapsedMilliseconds} milliseconds");
            }

            using (var storage = new ReliableStorage(new NullProcessor()))
            {
                storage.LoadPersistentData();

                const int count = 10000;
                var sw = new Stopwatch();
                sw.Start();
                for (var i = 0; i < count; i++) storage.StoreBlock(data, i.ToString(), 155);

                sw.Stop();

                Console.WriteLine(
                    $"In-place updating {count} blocks to persistent storage took {sw.ElapsedMilliseconds} milliseconds");
            }

            using (var storage = new ReliableStorage(new NullProcessor()))
            {
                storage.LoadPersistentData();

                const int count = 10000;
                var sw = new Stopwatch();
                sw.Start();
                for (var i = 0; i < count; i++) storage.StoreBlock(data1, i.ToString(), 155);

                sw.Stop();

                Console.WriteLine(
                    $"out of place updating {count} blocks to persistent storage took {sw.ElapsedMilliseconds} milliseconds");
            }

            {
                var w = new Stopwatch();
                var processor = new NullProcessor();

                w.Start();
                using (var storage = new ReliableStorage(processor))
                {
                    storage.LoadPersistentData();
                    w.Stop();

                    Assert.AreEqual(10000, storage.InactiveBlockCount);
                }


                Console.WriteLine(
                    $"Loading {processor.ProcessedBlocks.Count} blocks from persistent storage  with dirty blocks took {w.ElapsedMilliseconds} milliseconds");
            }


            var info = new FileInfo(Path.Combine(Constants.DataPath, ReliableStorage.StorageFileName));

            Console.WriteLine($"Before cleaning the file size was {info.Length}");

            // clean the storage (remove all dirty blocks)
            {
                var w = new Stopwatch();
                var processor = new NullProcessor();


                using var storage = new ReliableStorage(processor);

                storage.LoadPersistentData();
                w.Start();
                storage.CleanStorage();
                w.Stop();

                Assert.AreEqual(0, storage.InactiveBlockCount);

                Console.WriteLine(
                    $"Storage cleaning took {w.ElapsedMilliseconds} milliseconds");
            }

            info = new FileInfo(Path.Combine(Constants.DataPath, ReliableStorage.StorageFileName));

            Console.WriteLine($"After cleaning the file size was {info.Length}");

            {
                var w = new Stopwatch();
                var processor = new NullProcessor();

                w.Start();
                using (var storage = new ReliableStorage(processor))
                {
                    storage.LoadPersistentData();
                    w.Stop();

                    Assert.AreEqual(0, storage.InactiveBlockCount);
                }


                Console.WriteLine(
                    $"Loading {processor.ProcessedBlocks.Count} blocks from persistent storage after cleaning took {w.ElapsedMilliseconds} milliseconds");
            }
        }


        [Test]
        public void Size_of_smallest_dirty_block()
        {
            var block = PersistentBlock.MakeDirtyBlock(35);

            var stream = new MemoryStream();
            var w = new BinaryWriter(stream);
            block.Write(w);

            Console.WriteLine("min size is " + stream.Position);

            Assert.AreEqual(35, stream.Position,
                "smallest block size changed (was 35). Code must be updated: PersistentBlock.MinSize constant ");
        }

        [Test]
        public void Test_recovery_of_corrupted_storage()
        {
            // add two new blocks
            using (var storage = new ReliableStorage(new NullProcessor()))
            {
                storage.LoadPersistentData();

                Assert.AreEqual(0, storage.BlockCount);

                storage.StoreBlock(new byte[] { 1, 2, 3 }, "a1", 150);
                storage.StoreBlock(new byte[] { 1, 2, 3, 4, 5 }, "a2", 151);
                storage.StoreBlock(new byte[] { 21, 22, 23, 24, 25 }, "a3", 152);

                Assert.AreEqual(3, storage.BlockCount);
                Assert.AreEqual(0, storage.InactiveBlockCount);

                storage.MakeCorruptedBlock("a2");
            }

            // here we apply recovery wile loading
            using (var storage = new ReliableStorage(new NullProcessor()))
            {
                storage.LoadPersistentData();

                Assert.AreEqual(2, storage.BlockCount);

                Assert.AreEqual(1, storage.InactiveBlockCount);

                Assert.AreEqual(2, storage.Keys.Count);

                Assert.AreEqual(1, storage.CorruptedBlocks);

                Assert.IsTrue(storage.Keys.Contains("a1"));
                Assert.IsTrue(storage.Keys.Contains("a3"));
            }


            // here it is clean again (one block was lost)
            using (var storage = new ReliableStorage(new NullProcessor()))
            {
                storage.LoadPersistentData();
                Assert.AreEqual(2, storage.BlockCount);

                Assert.AreEqual(1, storage.InactiveBlockCount);

                Assert.AreEqual(0, storage.CorruptedBlocks);

                Assert.IsTrue(storage.Keys.Contains("a1"));
                Assert.IsTrue(storage.Keys.Contains("a3"));
            }
        }

        [Test]
        public void Test_recovery_of_corrupted_storage_with_backup_available()
        {
            // add two new blocks
            using (var storage = new ReliableStorage(new NullProcessor(), null, "backup"))
            {
                storage.LoadPersistentData();

                Assert.AreEqual(0, storage.BlockCount);

                storage.StoreBlock(new byte[] { 1, 2, 3 }, "a1", 150);
                storage.StoreBlock(new byte[] { 1, 2, 3, 4, 5 }, "a2", 151);
                storage.StoreBlock(new byte[] { 21, 22, 23, 24, 25 }, "a3", 152);

                Assert.AreEqual(3, storage.BlockCount);
                Assert.AreEqual(0, storage.InactiveBlockCount);

                storage.MakeCorruptedBlock("a2");
            }

            // here we apply recovery wile loading
            using (var storage = new ReliableStorage(new NullProcessor(), null, "backup"))
            {
                storage.LoadPersistentData();
                Assert.AreEqual(3, storage.BlockCount);

                Assert.AreEqual(1, storage.InactiveBlockCount);

                Assert.AreEqual(3, storage.Keys.Count);

                Assert.AreEqual(1, storage.CorruptedBlocks);

                Assert.IsTrue(storage.Keys.Contains("a1"));
                Assert.IsTrue(storage.Keys.Contains("a2"));
                Assert.IsTrue(storage.Keys.Contains("a3"));
            }


            // here it is clean again (lost block was recovered)
            using (var storage = new ReliableStorage(new NullProcessor(), null, "backup"))
            {
                storage.LoadPersistentData();

                Assert.AreEqual(3, storage.BlockCount);

                Assert.AreEqual(1, storage.InactiveBlockCount);

                Assert.AreEqual(0, storage.CorruptedBlocks);

                Assert.IsTrue(storage.Keys.Contains("a1"));
                Assert.IsTrue(storage.Keys.Contains("a2"));
                Assert.IsTrue(storage.Keys.Contains("a3"));


                // do some updates after recovery
                storage.StoreBlock(new byte[] { 121, 122, 123, 124, 125, 126 }, "a4", 152);

                storage.DeleteBlock("a1", 455);
            }

            // check the main storage
            using (var storage = new ReliableStorage(new NullProcessor()))
            {
                storage.LoadPersistentData();

                Assert.AreEqual(4, storage.BlockCount); // deleted blocks are counted too

                Assert.AreEqual(2, storage.InactiveBlockCount);

                Assert.AreEqual(0, storage.CorruptedBlocks);

                Assert.IsTrue(storage.Keys.Contains("a2"));
                Assert.IsTrue(storage.Keys.Contains("a3"));
                Assert.IsTrue(storage.Keys.Contains("a4"));
            }

            // check the backup storage
            using (var storage = new ReliableStorage(new NullProcessor(), null, "backup"))
            {
                storage.LoadPersistentData();

                Assert.AreEqual(4, storage.BlockCount); // deleted blocks are counted too

                Assert.IsTrue(storage.Keys.Contains("a2"));
                Assert.IsTrue(storage.Keys.Contains("a3"));
                Assert.IsTrue(storage.Keys.Contains("a4"));
            }
        }


        [Test]
        public void Test_with_backup_storage()
        {
            var data = new byte[1000];
            for (var i = 0; i < 1000; i++) data[i] = (byte)(i % 255);

            var data1 = new byte[2000];
            for (var i = 0; i < 2000; i++) data1[i] = (byte)(i % 255);

            using (var storage = new ReliableStorage(new NullProcessor(), null, "backup"))
            {
                storage.LoadPersistentData();
                const int count = 10000;
                var sw = new Stopwatch();
                sw.Start();
                for (var i = 0; i < count; i++) storage.StoreBlock(data, i.ToString(), 155);

                sw.Stop();

                Console.WriteLine(
                    $"Writing {count} blocks to persistent storage took {sw.ElapsedMilliseconds} milliseconds");
            }


            {
                var w = new Stopwatch();
                var processor = new NullProcessor();

                w.Start();
                using (var storage = new ReliableStorage(processor, "backup"))
                {
                    storage.LoadPersistentData();
                }

                w.Stop();

                Console.WriteLine(
                    $"Loading {processor.ProcessedBlocks.Count} blocks from persistent storage took {w.ElapsedMilliseconds} milliseconds");
            }

            using (var storage = new ReliableStorage(new NullProcessor(), "backup"))
            {
                storage.LoadPersistentData();

                const int count = 10000;
                var sw = new Stopwatch();
                sw.Start();
                for (var i = 0; i < count; i++) storage.StoreBlock(data, i.ToString(), 155);

                sw.Stop();

                Console.WriteLine(
                    $"In-place updating {count} blocks to persistent storage took {sw.ElapsedMilliseconds} milliseconds");
            }

            using (var storage = new ReliableStorage(new NullProcessor(), null, "backup"))
            {
                storage.LoadPersistentData();

                const int count = 10000;
                var sw = new Stopwatch();
                sw.Start();
                for (var i = 0; i < count; i++) storage.StoreBlock(data1, i.ToString(), 155);

                sw.Stop();

                Console.WriteLine(
                    $"out of place updating {count} blocks to persistent storage took {sw.ElapsedMilliseconds} milliseconds");
            }

            var info1 = new FileInfo(Path.Combine(Constants.DataPath, ReliableStorage.StorageFileName));
            var info2 = new FileInfo(Path.Combine("backup", ReliableStorage.StorageFileName));

            Assert.AreEqual(info1.Length, info2.Length);
        }


        [Test]
        public void Write_blocks_to_new_storage()
        {
            // add two new blocks
            using (var storage = new ReliableStorage(new NullProcessor()))
            {
                storage.LoadPersistentData();

                Assert.AreEqual(0, storage.BlockCount);

                storage.StoreBlock(new byte[] { 1, 2, 3 }, "a1", 150);
                storage.StoreBlock(new byte[] { 11, 12, 13, 14 }, "a2", 151);

                Assert.AreEqual(2, storage.BlockCount);
            }


            // load add a new one and do in-place update
            using (var storage = new ReliableStorage(new NullProcessor()))
            {
                storage.LoadPersistentData();

                Assert.AreEqual(2, storage.BlockCount);

                storage.StoreBlock(new byte[] { 21, 22, 23 }, "a3", 150); // new block

                storage.StoreBlock(new byte[] { 11, 12, 13, 14, 15 }, "a2", 155); // in place update of old block

                Assert.AreEqual(3, storage.BlockCount);
            }

            using (var storage = new ReliableStorage(new NullProcessor()))
            {
                storage.LoadPersistentData();

                Assert.AreEqual(3, storage.BlockCount);
                Assert.AreEqual(0, storage.InactiveBlockCount);

                Assert.IsTrue(storage.Keys.Contains("a1"));
                Assert.IsTrue(storage.Keys.Contains("a2"));
                Assert.IsTrue(storage.Keys.Contains("a3"));
            }

            // load again and make an update that can not be done in-place
            using (var storage = new ReliableStorage(new NullProcessor()))
            {
                storage.LoadPersistentData();

                Assert.AreEqual(3, storage.BlockCount);

                storage.StoreBlock(new byte[] { 11, 12, 13, 14, 15, 16, 17, 18 }, "a2", 155);

                Assert.AreEqual(3, storage.BlockCount);
                Assert.AreEqual(1, storage.InactiveBlockCount);
            }


            //reload and check data
            using (var storage = new ReliableStorage(new NullProcessor()))
            {
                storage.LoadPersistentData();
                Assert.AreEqual(3, storage.BlockCount);
                Assert.AreEqual(1, storage.InactiveBlockCount);

                Assert.IsTrue(storage.Keys.Contains("a1"));
                Assert.IsTrue(storage.Keys.Contains("a2"));
                Assert.IsTrue(storage.Keys.Contains("a3"));
            }
        }


        [Test]
        [Category("Performance")]
        public void Write_reload_test_with_one_million_objects()
        {
            var data = new byte[1000];
            for (var i = 0; i < 1000; i++) data[i] = (byte)(i % 255);


            using (var storage = new ReliableStorage(new NullProcessor()))
            {
                storage.LoadPersistentData();

                const int count = 1000000;
                var sw = new Stopwatch();
                sw.Start();
                for (var i = 0; i < count; i++) storage.StoreBlock(data, i.ToString(), 155);

                sw.Stop();

                Console.WriteLine(
                    $"Writing {count} blocks to persistent storage took {sw.ElapsedMilliseconds} milliseconds");
            }


            {
                var w = new Stopwatch();
                var processor = new NullProcessor();

                w.Start();
                using (var storage = new ReliableStorage(processor))
                {
                    storage.LoadPersistentData();
                }

                w.Stop();

                Console.WriteLine(
                    $"Loading {processor.ProcessedBlocks.Count} blocks from persistent storage took {w.ElapsedMilliseconds} milliseconds");
            }
        }


        [Test]
        public void Write_the_same_object_twice()
        {
            // add two new blocks
            using (var storage = new ReliableStorage(new NullProcessor()))
            {
                storage.LoadPersistentData();
                Assert.AreEqual(0, storage.BlockCount);

                storage.StoreBlock(new byte[] { 1, 2, 3 }, "a1", 150);
                storage.StoreBlock(new byte[] { 1, 2, 3 }, "a1", 150);


                Assert.AreEqual(1, storage.BlockCount);
                Assert.AreEqual(0, storage.InactiveBlockCount);
            }

            using (var storage = new ReliableStorage(new NullProcessor()))
            {
                storage.LoadPersistentData();

                Assert.AreEqual(1, storage.BlockCount);

                Assert.AreEqual(0, storage.InactiveBlockCount);
            }
        }

        private byte[] MakeByteArray(int size)
        {
            var result = new byte[size];

            Random.Shared.NextBytes(result);

            return result;
        }

        [Test]
        public void Write_resized_bigger_object()
        {
            var smallArray1 = MakeByteArray(1000);
            var smallArray2 = MakeByteArray(1000);

            var largeArray1 = MakeByteArray(10000);
            var largerArray2 = MakeByteArray(1010);

            // add two new blocks
            using (var storage = new ReliableStorage(new NullProcessor()))
            {
                storage.LoadPersistentData();
                Assert.AreEqual(0, storage.BlockCount);

                storage.StoreBlock(smallArray1, "a1", 150);
                storage.StoreBlock(smallArray2, "a2", 150);


                Assert.AreEqual(2, storage.BlockCount);
                Assert.AreEqual(0, storage.InactiveBlockCount);
            }

            var processor = new NullProcessor();

            // reload and resize the blocks 
            using (var storage = new ReliableStorage(processor))
            {
                storage.LoadPersistentData();
                Assert.AreEqual(2, storage.BlockCount);

                Assert.AreEqual(2, processor.ProcessedBlocks.Count);


                CollectionAssert.AreEqual(processor.ProcessedBlocks[0], smallArray1);
                CollectionAssert.AreEqual(processor.ProcessedBlocks[1], smallArray2);


                // We store resized blocks. The first one should be written at the end
                // and the block at his previous position should be marked as inactive
                // The second one should be stored in the same position as it is only slightly larger 
                // and we always reserve 50% of extra space

                storage.StoreBlock(largeArray1, "a1", 150);
                storage.StoreBlock(largerArray2, "a2", 150);


                Assert.AreEqual(2, storage.BlockCount);
                Assert.AreEqual(1, storage.InactiveBlockCount);
            }

            processor = new NullProcessor();
            // reload resized blocks
            using (var storage = new ReliableStorage(processor))
            {
                storage.LoadPersistentData();
                Assert.AreEqual(2, storage.BlockCount);
                Assert.AreEqual(1, storage.InactiveBlockCount);

                Assert.AreEqual(2, processor.ProcessedBlocks.Count);

                // the large one will be at the end as it was written to a new block at the end
                CollectionAssert.AreEqual(processor.ProcessedBlocks[1], largeArray1);
                CollectionAssert.AreEqual(processor.ProcessedBlocks[0], largerArray2);
            }
        }

        [Test]
        public void Write_resized_smaller_object()
        {
            var smallArray1 = MakeByteArray(1000);
            var smallArray2 = MakeByteArray(1000);

            var largeArray1 = MakeByteArray(10000);
            MakeByteArray(1010);

            // add two new blocks
            using (var storage = new ReliableStorage(new NullProcessor()))
            {
                storage.LoadPersistentData();
                Assert.AreEqual(0, storage.BlockCount);

                storage.StoreBlock(largeArray1, "a1", 150);
                storage.StoreBlock(smallArray2, "a2", 150);


                Assert.AreEqual(2, storage.BlockCount);
                Assert.AreEqual(0, storage.InactiveBlockCount);
            }

            var processor = new NullProcessor();

            // reload and resize the blocks 
            using (var storage = new ReliableStorage(processor))
            {
                storage.LoadPersistentData();
                Assert.AreEqual(2, storage.BlockCount);

                Assert.AreEqual(2, processor.ProcessedBlocks.Count);


                CollectionAssert.AreEqual(processor.ProcessedBlocks[0], largeArray1);
                CollectionAssert.AreEqual(processor.ProcessedBlocks[1], smallArray2);


                // We store one resized block and one unchanged
                // The resized block is smaller so it should be stored in the same position


                storage.StoreBlock(smallArray1, "a1", 150);
                storage.StoreBlock(smallArray2, "a2", 150);


                Assert.AreEqual(2, storage.BlockCount);
                Assert.AreEqual(0, storage.InactiveBlockCount);
            }

            processor = new NullProcessor();
            // reload resized blocks
            using (var storage = new ReliableStorage(processor))
            {
                storage.LoadPersistentData();
                Assert.AreEqual(2, storage.BlockCount);
                Assert.AreEqual(0, storage.InactiveBlockCount);

                Assert.AreEqual(2, processor.ProcessedBlocks.Count);


                CollectionAssert.AreEqual(processor.ProcessedBlocks[0], smallArray1);
                CollectionAssert.AreEqual(processor.ProcessedBlocks[1], smallArray2);
            }
        }

        [Test]
        public void Write_then_reload_object()
        {
            var data = new byte[] { 1, 2, 3 };

            // add two new blocks
            using (var storage = new ReliableStorage(new NullProcessor()))
            {
                storage.LoadPersistentData();

                storage.StoreBlock(data, "a1", 150);

                Assert.AreEqual(1, storage.BlockCount);
            }

            var processor = new NullProcessor();
            using (var storage = new ReliableStorage(processor))
            {
                storage.LoadPersistentData();

                Assert.AreEqual(1, storage.BlockCount);
            }

            Assert.AreEqual(1, processor.ProcessedBlocks.Count);
            CollectionAssert.AreEqual(data, processor.ProcessedBlocks[0]);
        }


        [Test]
        public void Random_stress_test()
        {
            // init with a seed that makes it reproducible the same day
            var rgen = new Random(DateTime.Today.DayOfYear);

            const int iterations = 100;

            ReliableStorage.Relocated = 0;
            ReliableStorage.StoredInPlace = 0;

            for (var i = 0; i < iterations; i++)
            {
                var key1 = rgen.Next(10) + 1;

                var length1 = rgen.Next(10000) + 1;

                var data1 = MakeByteArray(length1);
                data1[0] = (byte)key1;


                using (var storage = new ReliableStorage(new NullProcessor()))
                {
                    storage.LoadPersistentData();

                    storage.StoreBlock(data1, key1.ToString(), 150);
                }

                var processor = new NullProcessor();

                using (var storage = new ReliableStorage(processor))
                {
                    storage.LoadPersistentData();

                    var reloaded = processor.ProcessedBlocks.Single(x => x[0] == key1);

                    CollectionAssert.AreEqual(data1, reloaded);

                    storage.CleanStorage();
                }
            }

            var proc = new NullProcessor();
            using (var storage = new ReliableStorage(proc))
            {
                storage.LoadPersistentData();
                Console.WriteLine(
                    $"blocks={storage.BlockCount} inactive={storage.InactiveBlockCount} corrupted={storage.CorruptedBlocks} storage size={storage.StorageSize}");
                Console.WriteLine($"in-place={ReliableStorage.StoredInPlace} relocated={ReliableStorage.Relocated}");
            }
        }
    }
}