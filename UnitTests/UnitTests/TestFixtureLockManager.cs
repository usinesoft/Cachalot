using FakeItEasy;
using NUnit.Framework;
using Server;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Tests.UnitTests
{
    [TestFixture]
    public class TestFixtureLockManager
    {
        private readonly Dictionary<Guid, int> _resourceA = new Dictionary<Guid, int>();
        private readonly Dictionary<Guid, int> _resourceB = new Dictionary<Guid, int>();
        private readonly Dictionary<Guid, int> _resourceC = new Dictionary<Guid, int>();


        private void ReadAbc()
        {
            var unused = _resourceA.Select(p => p.Value).Count(v => v > 0);

            unused = _resourceB.Select(p => p.Value).Count(v => v > 10);

            unused = _resourceC.Select(p => p.Value).Count(v => v > 10);
        }

        private void WriteAbc()
        {
            _resourceA.Add(Guid.NewGuid(), 1);
            _resourceA.Add(Guid.NewGuid(), 2);
            _resourceA.Add(Guid.NewGuid(), 3);

            _resourceB.Add(Guid.NewGuid(), 10);
            _resourceB.Add(Guid.NewGuid(), 20);
            _resourceB.Add(Guid.NewGuid(), 30);

            _resourceC.Add(Guid.NewGuid(), 5);
            _resourceC.Add(Guid.NewGuid(), 6);
            _resourceC.Add(Guid.NewGuid(), 7);
        }

        private void ReadA()
        {
            var unused = _resourceA.Select(p => p.Value).Count(v => v > 0);
        }

        private void WriteA()
        {
            _resourceA.Add(Guid.NewGuid(), 1);
            _resourceA.Add(Guid.NewGuid(), 2);
            _resourceA.Add(Guid.NewGuid(), 3);
        }


        [Test]
        public void No_deadlock_and_no_race_condition_on_resources()
        {
            var mgr = new LockManager();

            var watch = new Stopwatch();
            watch.Start();

            var iterations = 10_000;

            Parallel.For(0, iterations, new ParallelOptions { MaxDegreeOfParallelism = 10 }, i =>
            {
                Parallel.Invoke(
                    () => mgr.DoWithReadLock(ReadAbc, "a", "b", "c"),
                    () => mgr.DoWithReadLock(ReadA, "a"),
                    () => mgr.DoWithWriteLock(WriteAbc, "a", "b", "c"),
                    () => mgr.DoWithWriteLock(WriteA, "a")
                );
            });

            watch.Stop();

            Console.WriteLine($"{iterations} iterations took {watch.ElapsedMilliseconds} ms");
            Console.WriteLine($"retries = {mgr.Retries}");
            Console.WriteLine($"a = {_resourceA.Count} b = {_resourceB.Count} c = {_resourceC.Count}");


            var locks = mgr.GetCurrentlyHoldLocks();

            Assert.AreEqual(0, locks);
        }

        [Test]
        public void No_deadlock_if_much_more_reads_than_writes()
        {
            var mgr = new LockManager();

            var watch = new Stopwatch();
            watch.Start();

            var iterations = 10_000;


            Parallel.Invoke(() =>
                {
                    Parallel.For(0, iterations, i =>
                    {
                        mgr.DoWithReadLock(ReadAbc, "a", "b", "c");
                        mgr.DoWithReadLock(ReadA, "a");
                    });
                },
                () =>
                {
                    for (var i = 0; i < iterations; i++)
                    {
                        mgr.DoWithWriteLock(WriteAbc, "a", "b", "c");
                    }
                }
            );


            watch.Stop();

            Console.WriteLine($"{iterations} iterations took {watch.ElapsedMilliseconds} ms");
            Console.WriteLine($"retries = {mgr.Retries}");
            Console.WriteLine($"a = {_resourceA.Count} b = {_resourceB.Count} c = {_resourceC.Count}");


            var locks = mgr.GetCurrentlyHoldLocks();

            Assert.AreEqual(0, locks);
        }


        [Test]
        public void No_deadlock_and_no_race_condition_on_resources_using_sessions()
        {
            var mgr = new LockManager();

            var watch = new Stopwatch();
            watch.Start();

            var iterations = 10_000;
            Parallel.For(0, iterations, i =>
            {
                Parallel.Invoke(
                    () =>
                    {
                        var session = Guid.NewGuid();
                        mgr.AcquireLock(session, false, "a", "b", "c");
                        ReadAbc();
                        mgr.CloseSession(session);
                    },
                    () =>
                    {
                        var session = Guid.NewGuid();
                        mgr.AcquireLock(session, false, "a");
                        ReadA();
                        mgr.CloseSession(session);
                    },
                    () =>
                    {
                        var session = Guid.NewGuid();
                        mgr.AcquireLock(session, true, "a", "b", "c");
                        WriteAbc();
                        mgr.CloseSession(session);
                    },
                    () =>
                    {
                        var session = Guid.NewGuid();
                        mgr.AcquireLock(session, true, "a");
                        WriteA();
                        mgr.CloseSession(session);
                    });
            });

            watch.Stop();

            Console.WriteLine($"{iterations} iterations took {watch.ElapsedMilliseconds} ms");
            Console.WriteLine($"retries = {mgr.Retries}");
            Console.WriteLine($"a = {_resourceA.Count} b = {_resourceB.Count} c = {_resourceC.Count}");


            var locks = mgr.GetCurrentlyHoldLocks();

            Assert.AreEqual(0, locks);
        }

        [Test]
        public void
            Mixed_use_of_sessions_and_simple_accesses_simulating_both_transactional_and_non_transactional_clients()
        {
            var mgr = new LockManager();

            var watch = new Stopwatch();
            watch.Start();


            var iterations = 100;
            Parallel.For(0, iterations, i =>
            {
                Parallel.Invoke(
                    () =>
                    {
                        for (var i = 0; i < iterations; i++)
                        {
                            var session = Guid.NewGuid();
                            mgr.AcquireLock(session, false, "a", "b", "c");
                            ReadAbc();
                            mgr.CloseSession(session);
                        }
                    },
                    () =>
                    {
                        for (var i = 0; i < iterations; i++)
                        {
                            var session = Guid.NewGuid();
                            mgr.AcquireLock(session, false, "a");
                            ReadA();
                            mgr.CloseSession(session);
                        }
                    },
                    () =>
                    {
                        for (var i = 0; i < iterations; i++)
                        {
                            var session = Guid.NewGuid();
                            mgr.AcquireLock(session, true, "a", "b", "c");
                            WriteAbc();
                            mgr.CloseSession(session);
                        }
                    },
                    () =>
                    {
                        for (var i = 0; i < iterations; i++)
                        {
                            var session = Guid.NewGuid();
                            mgr.AcquireLock(session, true, "a");
                            WriteA();
                            mgr.CloseSession(session);
                        }
                    },
                    () =>
                    {
                        for (var i = 0; i < 100; i++) mgr.DoWithReadLock(ReadAbc, "a", "b", "c");
                    },
                    () =>
                    {
                        for (var i = 0; i < 100; i++) mgr.DoWithReadLock(ReadA, "a");
                    },
                    () =>
                    {
                        for (var i = 0; i < 100; i++) mgr.DoWithWriteLock(WriteAbc, "a", "b", "c");
                    },
                    () =>
                    {
                        for (var i = 0; i < 100; i++) mgr.DoWithWriteLock(WriteA, "a");
                    });
            });

            watch.Stop();

            Console.WriteLine($"{iterations} iterations took {watch.ElapsedMilliseconds} ms");
            Console.WriteLine($"retries = {mgr.Retries}");
            Console.WriteLine($"a = {_resourceA.Count} b = {_resourceB.Count} c = {_resourceC.Count}");


            var locks = mgr.GetCurrentlyHoldLocks();

            Assert.AreEqual(0, locks);
        }

        [Test]
        public void Managing_pending_locks()
        {
            var log = A.Fake<IEventsLog>();


            ILockManager lockManager = new LockManager(log);

            var sessionId = Guid.NewGuid();

            lockManager.AcquireLock(sessionId, false, "x", "y", "z");

            Assert.AreEqual(3, lockManager.GetCurrentlyHoldLocks());

            Assert.IsTrue(lockManager.CheckLock(sessionId, false, "x", "y", "z"));

            // false because it is a read-only lock
            Assert.False(lockManager.CheckLock(sessionId, true, "x", "y", "z"));

            lockManager.CloseSession(sessionId);

            Assert.AreEqual(0, lockManager.GetCurrentlyHoldLocks());

            // session no longer active
            Assert.IsFalse(lockManager.CheckLock(sessionId, false, "x", "y", "z"));


            lockManager.AcquireLock(sessionId, true, "tony", "tara");
            Assert.AreEqual(2, lockManager.GetCurrentlyHoldLocks());


            Thread.Sleep(500);

            Assert.AreEqual(2, lockManager.GetCurrentlyHoldLocks(100));

            var locks = lockManager.ForceRemoveAllLocks(100);

            A.CallTo(() => log.LogEvent(EventType.LockRemoved, null, 0)).MustHaveHappened();

            Assert.AreEqual(2, locks);

            Assert.AreEqual(0, lockManager.GetCurrentlyHoldLocks());
        }

        [Test]
        public void Consistent_read_sessions()
        {
            ILockManager lockManager = new LockManager();

            var session = Guid.NewGuid();

            lockManager.AcquireLock(session, false, "x", "y", "z");


            var success = lockManager.CheckLock(session, false, "x");
            Assert.IsTrue(success);


            // try with a new session (should not work)
            success = lockManager.CheckLock(Guid.NewGuid(), false, "x");
            Assert.IsFalse(success);

            // read-only lock so it should not work
            success = lockManager.CheckLock(session, true, "x");
            Assert.IsFalse(success);

            // different resource so it should not work
            success = lockManager.CheckLock(session, true, "nope");
            Assert.IsFalse(success);


            // trying to close an inactive session throws an exception
            Assert.Throws<NotSupportedException>(() => lockManager.CloseSession(Guid.NewGuid()));
        }
    }
}