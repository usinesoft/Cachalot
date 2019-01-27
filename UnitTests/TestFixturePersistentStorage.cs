using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using NUnit.Framework;
using Server.Persistence;

namespace UnitTests
{
    [TestFixture]
    public class TestFixturePersistentStorage
    {
        class NullProcessor : IPersistentObjectProcessor
        {
            public List<byte[]> ProcessedBlocks { get; } = new List<byte[]>();

            public void Process(byte[] data)
            {
                ProcessedBlocks.Add(data);
            }

            public void EndProcess()
            {
                
            }
        }


        private readonly string _path = Path.Combine(Constants.DataPath, ReliableStorage.StorageFileName);
        private readonly string _backupPath = Path.Combine("backup", ReliableStorage.StorageFileName);

        [SetUp]
        public void SetUp()
        {
            if (File.Exists(_path))
            {
                File.Delete(_path);
            }

            if (File.Exists(_backupPath))
            {
                File.Delete(_backupPath);
            }
        }

        [TearDown]
        public void TearDown()
        {
            if (File.Exists(_path))
            {
                File.Delete(_path);
            }

            if (File.Exists(_backupPath))
            {
                File.Delete(_backupPath);
            }
        }


        [Test]
        public void Write_blocks_to_new_storage()
        {
            // add two new blocks
            using (var storage = new ReliableStorage(new NullProcessor()))
            {
                storage.LoadPersistentData();

                Assert.AreEqual(0, storage.BlockCount);

                storage.StoreBlock(new byte[] {1, 2, 3}, "a1", 150);
                storage.StoreBlock(new byte[] {11, 12, 13, 14}, "a2", 151);

                Assert.AreEqual(2, storage.BlockCount);
            }


            // load add a new one and do in-place update
            using (var storage = new ReliableStorage(new NullProcessor()))
            {
                storage.LoadPersistentData();

                Assert.AreEqual(2, storage.BlockCount);

                storage.StoreBlock(new byte[] {21, 22, 23}, "a3", 150); // new block

                storage.StoreBlock(new byte[] {11, 12, 13, 14, 15}, "a2", 155); // in place update of old block

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

                storage.StoreBlock(new byte[] {11, 12, 13, 14, 15, 16, 17, 18}, "a2", 155);

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
        public void Write_then_reload_object()
        {
            var data = new byte[] {1, 2, 3};

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
        public void Write_the_same_object_twice()
        {
            // add two new blocks
            using (var storage = new ReliableStorage(new NullProcessor()))
            {
                storage.LoadPersistentData();
                Assert.AreEqual(0, storage.BlockCount);

                storage.StoreBlock(new byte[] {1, 2, 3}, "a1", 150);
                storage.StoreBlock(new byte[] {1, 2, 3}, "a1", 150);


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

        [Test]
        public void Performance_test()
        {
            var data = new byte[1000];
            for (int i = 0; i < 1000; i++)
            {
                data[i] = (byte) (i % 255);
            }

            var data1 = new byte[2000];
            for (int i = 0; i < 2000; i++)
            {
                data1[i] = (byte) (i % 255);
            }

            using (var storage = new ReliableStorage(new NullProcessor()))
            {
                storage.LoadPersistentData();

                const int count = 10000;
                Stopwatch sw = new Stopwatch();
                sw.Start();
                for (int i = 0; i < count; i++)
                {
                    storage.StoreBlock(data, i.ToString(), 155);
                }

                sw.Stop();

                Console.WriteLine(
                    $"Writing {count} blocks to persistent storage took {sw.ElapsedMilliseconds} milliseconds");
            }


            {
                Stopwatch w = new Stopwatch();
                var processor = new NullProcessor();

                w.Start();
                using ( var storage = new ReliableStorage(processor))
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
                Stopwatch sw = new Stopwatch();
                sw.Start();
                for (int i = 0; i < count; i++)
                {
                    storage.StoreBlock(data, i.ToString(), 155);
                }

                sw.Stop();

                Console.WriteLine(
                    $"In-place updating {count} blocks to persistent storage took {sw.ElapsedMilliseconds} milliseconds");
            }

            using (var storage = new ReliableStorage(new NullProcessor()))
            {
                storage.LoadPersistentData();

                const int count = 10000;
                Stopwatch sw = new Stopwatch();
                sw.Start();
                for (int i = 0; i < count; i++)
                {
                    storage.StoreBlock(data1, i.ToString(), 155);
                }

                sw.Stop();

                Console.WriteLine(
                    $"out of place updating {count} blocks to persistent storage took {sw.ElapsedMilliseconds} milliseconds");
            }

            {
                Stopwatch w = new Stopwatch();
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
                Stopwatch w = new Stopwatch();
                var processor = new NullProcessor();

                
                using (var storage = new ReliableStorage(processor))
                {
                    storage.LoadPersistentData();
                    w.Start();
                    storage.CleanStorage();
                    w.Stop();

                    Assert.AreEqual(0, storage.InactiveBlockCount);

                    Console.WriteLine(
                        $"Storage cleaning took {w.ElapsedMilliseconds} milliseconds");
                }
                
            }

            info = new FileInfo(Path.Combine(Constants.DataPath, ReliableStorage.StorageFileName));

            Console.WriteLine($"After cleaning the file size was {info.Length}");
            
            {
                Stopwatch w = new Stopwatch();
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
        public void Test_with_backup_storage()
        {
            var data = new byte[1000];
            for (int i = 0; i < 1000; i++)
            {
                data[i] = (byte)(i % 255);
            }

            var data1 = new byte[2000];
            for (int i = 0; i < 2000; i++)
            {
                data1[i] = (byte)(i % 255);
            }

            using (var storage = new ReliableStorage(new NullProcessor(), null, "backup"))
            {
                storage.LoadPersistentData();
                const int count = 10000;
                Stopwatch sw = new Stopwatch();
                sw.Start();
                for (int i = 0; i < count; i++)
                {
                    storage.StoreBlock(data, i.ToString(), 155);
                }

                sw.Stop();

                Console.WriteLine(
                    $"Writing {count} blocks to persistent storage took {sw.ElapsedMilliseconds} milliseconds");
            }


            {
                Stopwatch w = new Stopwatch();
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
                Stopwatch sw = new Stopwatch();
                sw.Start();
                for (int i = 0; i < count; i++)
                {
                    storage.StoreBlock(data, i.ToString(), 155);
                }

                sw.Stop();

                Console.WriteLine(
                    $"In-place updating {count} blocks to persistent storage took {sw.ElapsedMilliseconds} milliseconds");
            }

            using (var storage = new ReliableStorage(new NullProcessor(),  null, "backup"))
            {
                storage.LoadPersistentData();

                const int count = 10000;
                Stopwatch sw = new Stopwatch();
                sw.Start();
                for (int i = 0; i < count; i++)
                {
                    storage.StoreBlock(data1, i.ToString(), 155);
                }

                sw.Stop();

                Console.WriteLine(
                    $"out of place updating {count} blocks to persistent storage took {sw.ElapsedMilliseconds} milliseconds");
            }

            var info1 = new FileInfo(Path.Combine(Constants.DataPath, ReliableStorage.StorageFileName));
            var info2 = new FileInfo(Path.Combine("backup", ReliableStorage.StorageFileName));

            Assert.AreEqual(info1.Length, info2.Length);

        }


        [Test]
        public void Size_of_smallest_dirty_block()
        {
            var block = PersistentBlock.MakeDirtyBlock(35);

            MemoryStream stream = new MemoryStream();
            var w = new BinaryWriter(stream);
            block.Write(w);

            Console.WriteLine("min size is "  +  stream.Position);

            Assert.AreEqual(35, stream.Position, "smallest block size changed (was 35). Code must be updated: PersistentBlock.MinSize constant ");
        }


        [Test]
        [Category("Performance")]
        public void Write_reload_test_with_one_million_objects()
        {
            var data = new byte[1000];
            for (int i = 0; i < 1000; i++)
            {
                data[i] = (byte) (i % 255);
            }


            using (var storage = new ReliableStorage(new NullProcessor()))
            {
                storage.LoadPersistentData();

                const int count = 1000000;
                Stopwatch sw = new Stopwatch();
                sw.Start();
                for (int i = 0; i < count; i++)
                {
                    storage.StoreBlock(data, i.ToString(), 155);
                }

                sw.Stop();

                Console.WriteLine(
                    $"Writing {count} blocks to persistent storage took {sw.ElapsedMilliseconds} milliseconds");
            }


            {
                Stopwatch w = new Stopwatch();
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
            using (var storage = new ReliableStorage(new NullProcessor(),null, "backup"))
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


            // here it is clean again (lost block was recoveres)
            using (var storage = new ReliableStorage(new NullProcessor(),null, "backup"))
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

                storage.DeleteBlock("a1",455);

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
        }
}