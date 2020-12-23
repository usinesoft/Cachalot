using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FakeItEasy;
using Microsoft.VisualBasic;
using NUnit.Framework;
using Server;

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

            int iterations = 10_000;
            Parallel.For(0, iterations, i =>
            {
                Parallel.Invoke(
                    () => mgr.DoIfReadLock(ReadAbc, 10, "a", "b", "c"),
                    () => mgr.DoWithReadLock(ReadA, "a"),
                    () => mgr.DoIfWriteLock(WriteAbc, 10, "a", "b", "c"),
                    () => mgr.DoWithWriteLock(WriteA, "a")
                );
            });

            watch.Stop();

            Console.WriteLine($"{iterations} iterations took {watch.ElapsedMilliseconds} ms");
            Console.WriteLine($"retries = {mgr.Retries} successful reads {mgr.SuccessfulReads} successful writes {mgr.SuccessfulWrites}");
            Console.WriteLine($"a = {_resourceA.Count} b = {_resourceB.Count} c = {_resourceC.Count}");

            


            // each iteration add extra 3 elements to a (no failure accepted)
            Assert.AreEqual(3*mgr.SuccessfulWrites + iterations * 3, _resourceA.Count);
            
            // each successful write adds 3 elements to a, b and c
            Assert.AreEqual(3*mgr.SuccessfulWrites, _resourceB.Count);
            
            Assert.AreEqual(3*mgr.SuccessfulWrites, _resourceC.Count);

            int locks = mgr.GetCurrentlyHoldLocks();

            Assert.AreEqual(0, locks);
        }

        [Test]
        public void Managing_pending_locks()
        {
            var log = A.Fake<IEventsLog>();


            ILockManager lockManager = new LockManager(log);

            lockManager.TryAcquireReadLock(default, 10, "x", "y", "z");

            Assert.AreEqual(3, lockManager.GetCurrentlyHoldLocks());

            lockManager.RemoveReadLock( "x", "y", "z");

            Assert.AreEqual(0, lockManager.GetCurrentlyHoldLocks());

            lockManager.TryAcquireReadLock(default, 10, "tahra", "tony");
            
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
            ILockManager lockManager = new LockManager(null);

            var session = Guid.NewGuid();

            var success = lockManager.TryAcquireReadLock(session, 10, "x", "y", "z");
            Assert.IsTrue(success);

            success = lockManager.CheckReadLockIsActive(session);
            Assert.IsTrue(success);

            // locking the same resources should not work
            success = lockManager.TryAcquireReadLock(session, 10, "x", "y", "z");
            Assert.IsFalse(success);

            // from another thread should not work either
            Task.Run(()=>
            {
                success = lockManager.TryAcquireReadLock(session, 10, "x", "y", "z");
                Assert.IsFalse(success);
            });

            // try with a new session (should not work)
            success = lockManager.CheckReadLockIsActive(Guid.NewGuid());
            Assert.IsFalse(success);

            lockManager.CloseSession(session);
            // no more active lock
            success = lockManager.CheckReadLockIsActive(session);
            Assert.False(success);

            // trying to close an inactive session throws an exception

            Assert.Throws<NotSupportedException>(() => lockManager.CloseSession(Guid.NewGuid()));
            
        }
    }
}