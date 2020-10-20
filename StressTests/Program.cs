using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Cachalot.Linq;

namespace StressTests
{
    internal class Program
    {
        private static void Main(string[] args)
        {
            if (args.Length == 0) PerformReconnectionTests();
        }

        private static IList<AbstractEntity> GenerateRandomEntities(int count)
        {
            var result = new List<AbstractEntity>(count);

            var gen = new Random(Environment.TickCount);

            var collectionId = Guid.NewGuid();

            for (var i = 0; i < count; i++)
            {
                var entity = new AbstractEntity
                {
                    CollectionId = collectionId,
                    X = gen.Next(10),
                    Y = gen.Next(20),
                    Z = gen.Next(30)
                };

                result.Add(entity);
            }


            return result;
        }

        private static void PerformReconnectionTests()
        {
            using var connector = new Connector("localhost:48481");


            while (true)
                try
                {
                    var entities = connector.DataSource<AbstractEntity>();

                    var data = GenerateRandomEntities(1000);
                    var collection = data.First().CollectionId;

                    // count with objects 
                    var count = data.Count(x => x.CollectionId == collection && x.X == 3 && x.Y == 15);

                    var watch = new Stopwatch();

                    watch.Start();
                    // send data into the cache
                    entities.PutMany(data);

                    var t1 = watch.ElapsedMilliseconds;

                    // count data into the cache

                    var count1 = entities.Count(x => x.CollectionId == collection && x.X == 3 && x.Y == 15);

                    var t2 = watch.ElapsedMilliseconds;

                    // get data from the cache
                    var reloaded = entities.Where(x => x.CollectionId == collection && x.X == 3 && x.Y == 15).ToList();

                    var t3 = watch.ElapsedMilliseconds;

                    var count2 = reloaded.Count;

                    watch.Stop();

                    if (count1 == count2 && count == count2)
                        Console.WriteLine($"{count2} objects found w={t1} c={t2 - t1} r={t3 - t2}");
                    else
                        Console.WriteLine($"ERROR {count} {count1} {count2} ");
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.Message);
                }
        }
    }
}