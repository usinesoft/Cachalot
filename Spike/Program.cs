using System;
using Tests.IntegrationTests;

namespace Spike
{
    class Program
    {
        static void Main(string[] args)
        {


            try
            {

                //int servers = 2;
                //int threads = 20;


                //if (args.Length > 0)
                //{
                //    servers = int.Parse(args[0]);
                //}

                //if (args.Length > 1)
                //{
                //    threads = int.Parse(args[1]);
                //}

                //for (int i = 1; i < 10; i++)
                //{
                //    Console.WriteLine($"{servers} server(s) {threads} threads");
                //    Console.WriteLine("--------------------------------------------------------------");

                //    // test lock performance
                //    //new TestFixtureLockManager().No_deadlock_and_no_race_condition_on_resources();
                //    var tf = new TestFixtureTwoStageTransactionsOnMultiServerCluster{Servers = servers, Threads = threads};

                //    // test transaction performance

                //    tf.RunBeforeAnyTests();

                //    tf.Init();

                //    tf.Consistent_reads_and_transactions_run_in_parallel();

                //    tf.Exit();


                //    Console.WriteLine();

                //}

                var tf = new TestFixtureMultipleNodesWithPersistence();

                // test transaction performance

                tf.RunBeforeAnyTests();

                tf.Init();

                tf.Dump_and_import_with_server_side_values();

                tf.Exit();


            }
            catch (Exception e)
            {
                Console.WriteLine(e);

            }
        }
    }
}
