using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
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
        }
    }
}