using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Cachalot.Linq;
using Client.Core.Linq;
using Client.Interface;
using Remotion.Linq.Clauses;
// ReSharper disable AccessToModifiedClosure


namespace BookingMarketplace
{
    internal class Program
    {
        private static readonly int TestIterations = 10000;


        private static IList<Home> GenerateMany(int[] ids)
        {

            var countries = new[] {"FR", "US", "SP", "CA"};

            var towns = new[]
            {
                new[] {"Paris", "Nice", "Marseille", "Toulouse"},
                new[] {"New York", "Santa Clara", "Chicago", "Seattle"},
                new[] {"Madrid", "Barcelona", "Sevilia", "Cordoba"},
                new[] {"Ottawa", "Toronto", "Quebec", "Vancouver"},
            };

            var rand = new Random(Environment.TickCount);


            var result = new List<Home>(ids.Length);

            var today = DateTime.Today;
            var tomorrow = today.AddDays(1);

            // one in ten is available today, one in 100 is available tomorrow

            
            for (var i = 0; i < ids.Length - 3; i++)
            {
                var availableDates = new List<DateTime>();
                if (i % 10 == 0)
                {
                    availableDates.Add(today);
                }

                if (i % 100 == 0)
                {
                    availableDates.Add(tomorrow);
                }

                var property = new Home
                {
                    Id = ids[i],
                    Adress = "14 rue du chien qui fume",
                    Bathrooms = rand.Next(1,4),
                    CountryCode = countries[i % 4],
                    PriceInEuros = rand.Next(50, 300),
                    Rooms = rand.Next(1,5),
                    Town = towns[i%4][rand.Next(4)],
                    AvailableDates = availableDates
                };

                result.Add(property);
            }

            // manually add some items for full-text search testing
            var h1 = new Home
            {
                Id = ids[ids.Length - 3],
                Adress = "14 rue de la mort qui tue",
                Bathrooms = rand.Next(1,4),
                CountryCode = "FR",
                PriceInEuros = rand.Next(50, 300),
                Rooms = rand.Next(1,5),
                Town = "Paris",
                Comments = new List<Comment>
                {
                    new Comment{Text="beautiful view"},
                    new Comment{Text="close to the metro"},
                }
            };

            var h2 = new Home
            {
                Id = ids[ids.Length - 2],
                Adress = "15 allée de l'amour",
                Bathrooms = rand.Next(1,4),
                CountryCode = "FR",
                PriceInEuros = rand.Next(50, 300),
                Rooms = rand.Next(1,5),
                Town = "Paris",
                Comments = new List<Comment>
                {
                    new Comment{Text="ps4"},
                    new Comment{Text="close to the metro"},
                }
            };

            var h3 = new Home
            {
                Id = ids[ids.Length - 1],
                Adress = "156 db du gral Le Clerc",
                Bathrooms = rand.Next(1,4),
                CountryCode = "FR",
                PriceInEuros = rand.Next(50, 300),
                Rooms = rand.Next(1,5),
                Town = "Nice",
                Comments = new List<Comment>
                {
                    new Comment{Text="wonderful sea view"},
                    new Comment{Text="close to beach"},
                }
            };

            result.Add(h1);
            result.Add(h2);
            result.Add(h3);

            return result;
        }

        private static void Main()
        {
           

            try
            {
                // test with a cluster of two nodes
                using var connector = new Connector("localhost:48401+localhost:48402");
                
                Console.WriteLine();
                Console.WriteLine("test with a cluster of two nodes");
                Console.WriteLine("---------------------------");
                PerfTest(connector);
            }
            catch (CacheException e)
            {
                Console.WriteLine(e.Message);
            }

            try
            {
                // test with one external server
                using var connector = new Connector("localhost:48401");
                
                
                Console.WriteLine();
                Console.WriteLine("test with one external server");
                Console.WriteLine("---------------------------");
                PerfTest(connector);
            }
            catch (CacheException e)
            {
                Console.WriteLine(e.Message);
            }

            try
            {
                // quick test with in-process server
                using var connector = new Connector(new ClientConfig { IsPersistent = true });
                
                
                Console.WriteLine();
                Console.WriteLine("test with in-process server");
                Console.WriteLine("---------------------------");
                PerfTest(connector);
            }
            catch (CacheException e)
            {
                Console.WriteLine(e.Message);
            }
        }


        public static void RunOnce(Action action, string message)
        {
            try
            {
                var watch = new Stopwatch();

                watch.Start();

                action();

                watch.Stop();

                Console.WriteLine($" {message} took {watch.ElapsedMilliseconds} ms");
            }
            catch (Exception e)
            {
                Console.WriteLine($" {message} failed: {e.Message}");
            }

        }

        public static void Benchmark(Action action, string message)
        {
            try
            {
                // warm-up do not count
                action();

                var watch = new Stopwatch();

                watch.Start();

                const int iterations = 10;
                for (int i = 0; i < iterations; i++)
                {
                    action();
                }

                watch.Stop();

                Console.WriteLine($" {iterations} times {message} took {watch.ElapsedMilliseconds} ms average={watch.ElapsedMilliseconds/iterations} ms");
            }
            catch (Exception e)
            {
                Console.WriteLine($" {message} failed: {e.Message}");
            }

        }

        public static void CheckThat<T>(Predicate<T> check, string messageIfFails, T toCheck)
        {
            if (!check(toCheck))
            {
                throw new NotSupportedException(messageIfFails);
            }
        }

        private static void PerfTest(Connector connector)
        {
            connector.AdminInterface().DropDatabase();
            
            connector.DeclareCollection<Home>();

            var homes = connector.DataSource<Home>();

            const int feedObjects = 100_000;
            var pids = connector.GenerateUniqueIds("property_id", feedObjects);

            var items = GenerateMany(pids);
            var distinctIds = items.Select(i => i.Id).Distinct().Count();

            // 1 feed with many objects
            RunOnce(() => homes.PutMany(items), $"feeding the collection with {feedObjects} objects");

            int c1 = homes.Count();
            homes.PutMany(items);

            int c2 = homes.Count();

            // 2 read one object at a time
            const int objectsRead = 1000;
            
            Benchmark(() =>
            {
                for (int i = 0; i < objectsRead; i++)
                {
                    var _ = homes[pids[i]];
                }
            }, $"reading {objectsRead} objects one by one by primary key");

            Benchmark(() =>
            {
                for (int i = 0; i < objectsRead; i++)
                {
                    var _ = homes.First(h=>h.Id == pids[i]);
                }
            }, $"reading {objectsRead} objects one by one with linq");

            Benchmark(() =>
            {
                for (int i = 0; i < objectsRead; i++)
                {
                    var _ = homes.SqlQuery($"select from home where id={pids[i]}").First();
                }
            }, $"reading {objectsRead} objects one by one with sql");


            // 3 get many with simple query

            var inParis = items.Where(p => p.Town == "Paris").ToList();
            var resultCount = inParis.Count;
            inParis = homes.Where(p => p.Town == "Paris").ToList();
            inParis = homes.SqlQuery("select from home where town = Paris").ToList();

            Benchmark(() =>
            {
                inParis = homes.Where(p => p.Town == "Paris").ToList();
                CheckThat(r=>r.Count == resultCount, "wrong number of objects in result", inParis);

            }, $"reading {resultCount} objects with simple linq query");

            Benchmark(() =>
            {
                inParis = homes.SqlQuery("select from home where town = Paris").ToList();
                CheckThat(r=>r.Count == resultCount, "wrong number of objects in result", inParis);

            }, $"reading {resultCount} objects with simple sql query");

            // 4 get many with multiple predicate query

            var inParisNotExpensiveWithManyRooms = items
                .Where(p => p.Town == "Paris" && p.PriceInEuros >= 150 && p.PriceInEuros <= 200 && p.Rooms > 2)
                .ToList();
            resultCount = inParisNotExpensiveWithManyRooms.Count;

            Benchmark(() =>
            {
                inParisNotExpensiveWithManyRooms = homes
                    .Where(p => p.Town == "Paris" && p.PriceInEuros >= 150 && p.PriceInEuros <= 200 && p.Rooms > 2)
                    .ToList();

                CheckThat(r=>r.Count == resultCount, "wrong number of objects in result", inParisNotExpensiveWithManyRooms);

            }, $"reading {resultCount} objects with 3 predicate linq query");

            Benchmark(() =>
            {
                inParisNotExpensiveWithManyRooms = homes.SqlQuery("select from home where town=Paris and priceineuros > 150 and PriceInEuros < 200 and rooms > 2")
                    .ToList();

                CheckThat(r=>r.Count == resultCount, "wrong number of objects in result", inParisNotExpensiveWithManyRooms);

            }, $"reading {resultCount} objects with 3 predicate sql query");


            

            //IList<Home> inParisNotExpensiveWithManyRooms = new List<Home>();
            //for (var i = 0; i < 10; i++)
            //    inParisNotExpensiveWithManyRooms = homes
            //        .Where(p => p.Town == "Paris" && p.PriceInEuros >= 150 && p.PriceInEuros <= 200 && p.Rooms > 2)
            //        .ToList();

            //watch.Stop();

            //Console.WriteLine(
            //    $"Select {inParisNotExpensiveWithManyRooms.Count} items at once took {watch.ElapsedMilliseconds / 10} ms");

            //// get many with contains operator
            //watch.Reset();
            //watch.Start();

            //IList<Home> inParisAvailableToday = new List<Home>();
            //for (var i = 0; i < 10; i++)
            //    inParisAvailableToday = homes
            //        .Where(p => p.Town == "Paris" && p.AvailableDates.Contains(DateTime.Today))
            //        .ToList();

            //watch.Stop();

            //Console.WriteLine(
            //    $"Select with list index {inParisAvailableToday.Count} items at once took {watch.ElapsedMilliseconds / 10} ms");

            //watch.Reset();
            //watch.Start();

            //IList<Home> inParisAvailableTomorrow = new List<Home>();
            //for (var i = 0; i < 10; i++)
            //    inParisAvailableTomorrow = homes
            //        .Where(p => p.Town == "Paris" && p.AvailableDates.Contains(DateTime.Today.AddDays(1)))
            //        .ToList();

            //watch.Stop();

            //Console.WriteLine(
            //    $"Select with list index {inParisAvailableTomorrow.Count} items at once took {watch.ElapsedMilliseconds / 10} ms");

            //// update many
            //watch.Reset();
            //watch.Start();

            //homes.PutMany(inParisNotExpensiveWithManyRooms);

            //watch.Stop();


            //Console.WriteLine(
            //    $"Update  {inParisAvailableTomorrow.Count} items at once took {watch.ElapsedMilliseconds } ms");

            //Console.WriteLine("Full text search:");

            
            //watch.Reset();
            //watch.Start();

            //IList<Home> result1 = new List<Home>();
            //for (var i = 0; i < 10; i++)
            //    result1 = homes.FullTextSearch("Nice beach").ToList();

            //watch.Stop();

            //Console.WriteLine(
            //    $"Select {result1.Count} items at once with full-text search took {watch.ElapsedMilliseconds / 10} ms");

            //Console.WriteLine(
            //    $"bast match:  {result1.First().Id}");


            //watch.Reset();
            //watch.Start();

            //IList<Home> result2 = new List<Home>();
            //for (var i = 0; i < 10; i++)
            //    result2 = homes.FullTextSearch("close metro").ToList();

            //watch.Stop();

            //Console.WriteLine(
            //    $"Select {result2.Count} items at once with full-text search took {watch.ElapsedMilliseconds / 10} ms");

            //Console.WriteLine(
            //    $"bast match:  {result2.First().Id}");

            //watch.Reset();
            //watch.Start();

            //IList<Home> result3 = new List<Home>();
            //for (var i = 0; i < 10; i++)
            //    result3 = homes.FullTextSearch("rue de la mort").Take(10).ToList();

            //watch.Stop();

            //Console.WriteLine(
            //    $"Select {result3.Count} items at once with full-text search took {watch.ElapsedMilliseconds / 10} ms");

            //Console.WriteLine(
            //    $"bast match:  {result3.First().Id}");

            //// delete many with complex query
            //watch.Reset();
            //watch.Start();

            //homes.DeleteMany(p =>
            //    p.Town == "Paris" && p.PriceInEuros >= 150 && p.PriceInEuros <= 200 && p.Rooms > 2);

            //watch.Stop();


            //Console.WriteLine(
            //    $"Delete  {inParisAvailableTomorrow.Count} items at once took {watch.ElapsedMilliseconds } ms");

        }

       
    }
}