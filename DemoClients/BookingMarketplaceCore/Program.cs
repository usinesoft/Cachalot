using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Cachalot.Linq;
using Client.Core.Linq;
using Client.Interface;


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

            var rand = new Random();


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
            var config = new ClientConfig
            {
                Servers = {new ServerConfig {Host = "localhost", Port = 48401}, new ServerConfig {Host = "localhost", Port = 48402}}
            };

            try
            {
                // quick test with external server
                using var connector = new Connector("localhost:48401+localhost:48402");
                
                connector.AdminInterface().DropDatabase();
                
                Console.WriteLine();
                Console.WriteLine("test with external server");
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

        private static void PerfTest(Connector connector)
        {
            
            // first delete all data to start with a clean database
            connector.AdminInterface().DropDatabase();

            connector.DeclareCollection<Home>();

            var homes = connector.DataSource<Home>();

            var ids = connector.GenerateUniqueIds("home_id", 1);

            var property = new Home
            {
                Id = ids[0],
                Adress = "14 rue du chien qui fume",
                Bathrooms = 1,
                CountryCode = "FR",
                PriceInEuros = 125,
                Rooms = 2,
                Town = "Paris"
            };


            // warm up

            // this is an insert
            homes.Put(property);

            var reloaded = homes.First(p => p.Id == property.Id);

            // this is an update
            homes.Put(property);

            // performance test

            var watch = new Stopwatch();

            //watch.Start();
            //for (var i = 0; i < TestIterations; i++) homes.Put(property);

            //watch.Stop();

            //Console.WriteLine($"Updating {TestIterations} items one by one took {watch.ElapsedMilliseconds} ms");

            //watch.Reset();
            //watch.Start();
            //for (var i = 0; i < TestIterations; i++) reloaded = homes.First(p => p.Id == property.Id);

            //watch.Stop();

            //Console.WriteLine($"Loading {TestIterations} items one by one (linq) took {watch.ElapsedMilliseconds} ms");

            watch.Reset();
            watch.Start();
            for (var i = 0; i < TestIterations; i++) reloaded = homes[property.Id];

            watch.Stop();

            Console.WriteLine(
                $"Loading {TestIterations} items one by one (primary key) took {watch.ElapsedMilliseconds} ms");


            //const int feedObjects = 100000;
            //var pids = connector.GenerateUniqueIds("property_id", feedObjects);

            //var items = GenerateMany(pids);

            //var hashset = new HashSet<int>(pids);
            //// check they are all identical
            //if (hashset.Count != pids.Length)
            //{
            //    throw new NotSupportedException("ids are not unique");
            //}


            //// feed many
            //watch.Reset();
            //watch.Start();

            //homes.PutMany(items);

            //watch.Stop();

            //Console.WriteLine($"Inserting {feedObjects} items at once took {watch.ElapsedMilliseconds} ms");

            //// get many

            //watch.Reset();
            //watch.Start();

            //IList<Home> inParis = new List<Home>();
            //for (var i = 0; i < 10; i++) inParis = homes.Where(p => p.Town == "Paris").ToList();

            //watch.Stop();

            //Console.WriteLine($"Select {inParis.Count} items at once took {watch.ElapsedMilliseconds / 10} ms");

            //// get many with complex query
            //watch.Reset();
            //watch.Start();

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