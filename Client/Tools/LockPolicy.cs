using System;
using System.Threading;

namespace Client.Tools
{
    public static class LockPolicy
    {
        public static int SmartRetry(Func<bool> action, int maxRetry = 0)
        {
            int iteration = 0;

            var wait = new SemaphoreSlim(1, 1);

            while (true)
            {
                if (action())
                    break;

                iteration++;

                
                if (maxRetry > 0 && iteration >= maxRetry)
                    break;

                // this heuristic took lots of tests to nail down; it is a compromise between 
                // wait time for one client and average time for all clients
                var delay = ThreadLocalRandom.Instance.Next(10 * (iteration % 5));

                wait.Wait(delay);
                
            }

            return iteration;
        }
    }
}