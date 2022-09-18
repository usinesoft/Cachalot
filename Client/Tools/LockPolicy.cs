//#define DEBUG_VERBOSE
using System;
using System.Threading;

namespace Client.Tools
{
    public static class LockPolicy
    {
        public static int SmartRetry(Func<bool> action, int maxRetry = 0)
        {
            int iteration = 0;

            // its ok to be local, just meant to always block temporarily
            //var wait = new SemaphoreSlim(0, 1);

            while (true)
            {
                if (action())
                    break;

                iteration++;


                if (maxRetry > 0 && iteration >= maxRetry)
                    break;

                // this heuristic took lots of tests to nail down; it is a compromise between 
                // wait time for one client and average time for all clients
                var delay = ThreadLocalRandom.Instance.Next(10 * (iteration % 15 + 1));

                //wait.Wait(delay);
                Thread.Sleep(delay);

                Dbg.Trace($"Thread {Thread.CurrentThread.ManagedThreadId} smart retry delay {delay} for iteration {iteration}");
            }


            Dbg.Trace($"Thread {Thread.CurrentThread.ManagedThreadId} Smart retry finished in {iteration} iterations");

            return iteration;
        }
    }
}