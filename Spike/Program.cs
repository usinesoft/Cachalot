using System;
using Tests.IntegrationTests;
using Tests.UnitTests;

namespace Spike
{
    class Program
    {
        static void Main(string[] args)
        {
            for (int i = 1; i < 100; i++)
            {
                Console.WriteLine("--------------------------------------------------------------");
                new TestFixtureLockManager().No_deadlock_and_no_race_condition_on_resources();
                var tf = new TestFixtureTwoStageTransactionsOnMultiServerCluster();

                tf.RunBeforeAnyTests();
                tf.Init();
                tf.Consistent_reads_and_transactions_run_in_parallel();
                tf.Exit();

            }
        }
    }
}
