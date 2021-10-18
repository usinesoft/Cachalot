using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Cachalot.Linq;
using Client.Core.Linq;
using Client.Interface;

// ReSharper disable AccessToModifiedClosure


namespace BookingMarketplace
{
    internal class Program
    {
        
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
                Id = ids[^3],
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
                Id = ids[^2],
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
                Id = ids[^1],
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

        static void Title(string message)
        {
            var colorBefore = Console.ForegroundColor;
            
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine(message.ToUpper());
            Console.WriteLine();

            Console.ForegroundColor = colorBefore;
        }

        static void Header(string message)
        {
            var colorBefore = Console.ForegroundColor;
            
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Blue;
            Console.WriteLine(message);
            Console.ForegroundColor = colorBefore;
        }

        private static void Main()
        {


            try
            {
                // test with a cluster of two nodes
                using var connector = new Connector("localhost:48401+localhost:48402");

                Title("test with a cluster of two nodes");
                
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
                
                Title("test with one external server");
                
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
                
                Title("test with in-process server");
                
                PerfTest(connector);
            }
            catch (CacheException e)
            {
                Console.WriteLine(e.Message);
            }
        }


        private static void RunOnce(Action action, string message)
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

        private static void Benchmark(Action action, string message)
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

        private static void CheckThat<T>(Predicate<T> check, string messageIfFails, T toCheck)
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
            
            //////////////////////////////////////////////////////
            // 1 feed with many objects

            Header("Feeding data");

            RunOnce(() => homes.PutMany(items), $"feeding the collection with {feedObjects} objects");

            var count = homes.Count();
            if ( count != items.Count)
            {
                throw new NotSupportedException($"fed {items.Count} but count returned {count}");
            }
            

            //////////////////////////////////////////////////////
            // 2 read one object at a time

            Header("Reading objects one by one");

            const int objectsRead = 1000;
            
            Benchmark(() =>
            {
                for (int i = 0; i < objectsRead; i++)
                {
                    var _ = homes[pids[i]];
                }
            }, $"reading {objectsRead} objects using primary key");

            Benchmark(() =>
            {
                for (int i = 0; i < objectsRead; i++)
                {
                    var _ = homes.First(h=>h.Id == pids[i]);
                }
            }, $"reading {objectsRead} objects using linq");

            Benchmark(() =>
            {
                for (int i = 0; i < objectsRead; i++)
                {
                    var _ = homes.SqlQuery($"select from home where id={pids[i]}").First();
                }
            }, $"reading {objectsRead} objects using sql");

            //////////////////////////////////////////////////////
            // 3 get many with simple query

            var inParis = items.Where(p => p.Town == "Paris").ToList();
            var resultCount = inParis.Count;
            
            Header("select from home where town = Paris");
            

            Benchmark(() =>
            {
                inParis = homes.Where(p => p.Town == "Paris").ToList();
                CheckThat(r=>r.Count == resultCount, "wrong number of objects in result", inParis);

            }, $"reading {resultCount} objects with linq");

            Benchmark(() =>
            {
                inParis = homes.SqlQuery("select from home where town = Paris").ToList();
                CheckThat(r=>r.Count == resultCount, "wrong number of objects in result", inParis);

            }, $"reading {resultCount} objects with sql ");


            //////////////////////////////////////////////////////
            // 4 get many with multiple predicate query

            var inParisNotExpensiveWithManyRooms = items
                .Where(p => p.Town == "Paris" && p.PriceInEuros >= 150 && p.PriceInEuros <= 200 && p.Rooms > 2)
                .ToList();
            resultCount = inParisNotExpensiveWithManyRooms.Count;

            Header("select from home where town=Paris and PriceInEuros >= 150 and PriceInEuros <= 200 and rooms > 2");
            

            Benchmark(() =>
            {
                inParisNotExpensiveWithManyRooms = homes
                    .Where(p => p.Town == "Paris" && p.PriceInEuros >= 150 && p.PriceInEuros <= 200 && p.Rooms > 2)
                    .ToList();

                CheckThat(r=>r.Count == resultCount, "wrong number of objects in result", inParisNotExpensiveWithManyRooms);

            }, $"reading {resultCount} objects with linq");

            Benchmark(() =>
            {
                inParisNotExpensiveWithManyRooms = homes.SqlQuery("select from home where town=Paris and priceineuros >= 150 and PriceInEuros <= 200 and rooms > 2")
                    .ToList();

                CheckThat(r=>r.Count == resultCount, "wrong number of objects in result", inParisNotExpensiveWithManyRooms);

            }, $"reading {resultCount} objects with sql");


            //////////////////////////////////////////////////////
            // 5 query with contains operator 

            var inParisAvailableToday = items
                .Where(p => p.Town == "Paris" && p.AvailableDates.Contains(DateTime.Today))
                .ToList();

            resultCount = inParisAvailableToday.Count;

            Header($"select from home where town=Paris and AvailableDates contains {DateTime.Today:d}");
            
            Benchmark(() =>
            {
                inParisAvailableToday = homes
                    .Where(p => p.Town == "Paris" && p.AvailableDates.Contains(DateTime.Today))
                    .ToList();

                CheckThat(r=>r.Count == resultCount, "wrong number of objects in result", inParisAvailableToday);

            }, $"reading {resultCount} objects with linq");

            Benchmark(() =>
            {
                inParisAvailableToday = homes.SqlQuery($"select from home where town=Paris and AvailableDates contains {DateTime.Today:d}").ToList();
                
                CheckThat(r=>r.Count == resultCount, "wrong number of objects in result", inParisAvailableToday);

            }, $"reading {resultCount} objects with sql");

            Header($"select from home where town=Paris and AvailableDates contains {DateTime.Today:d} order by PriceInEuros descending take 10");
            
            Benchmark(() =>
            {
                inParisAvailableToday = homes
                    .Where(p => p.Town == "Paris" && p.AvailableDates.Contains(DateTime.Today)).OrderByDescending(h=>h.PriceInEuros).Take(10)
                    .ToList();

                CheckThat(r=>r.Count == 10, "wrong number of objects in result", inParisAvailableToday);

            }, "reading 10 objects with linq");

            Benchmark(() =>
            {
                inParisAvailableToday = homes.SqlQuery($"select from home where town=Paris and AvailableDates contains {DateTime.Today:d} order by PriceInEuros descending take 10").ToList();
                
                CheckThat(r=>r.Count == 10, "wrong number of objects in result", inParisAvailableToday);

            }, "reading 10 objects with sql");

            Header($"select from home where town=Paris and AvailableDates contains {DateTime.Today:d} order by PriceInEuros");
            
            Benchmark(() =>
            {
                inParisAvailableToday = homes
                    .Where(p => p.Town == "Paris" && p.AvailableDates.Contains(DateTime.Today)).OrderBy(h=>h.PriceInEuros)
                    .ToList();

                CheckThat(r=>r.Count == resultCount, "wrong number of objects in result", inParisAvailableToday);

            }, $"reading {resultCount} objects with linq");

            Benchmark(() =>
            {
                inParisAvailableToday = homes.SqlQuery($"select from home where town=Paris and AvailableDates contains {DateTime.Today:d} order by PriceInEuros").ToList();
                
                CheckThat(r=>r.Count == resultCount, "wrong number of objects in result", inParisAvailableToday);

            }, $"reading {resultCount} objects with sql");


            //////////////////////////////////////////////////////
            // 6 full text search

            Header("full-text search");

            int ftCount = 0;
            Benchmark(() =>
            {
                var result = homes.FullTextSearch("Nice beach").ToList();
                ftCount = result.Count;

            }, "searching for 'Nice beach'");
            Console.WriteLine($" -> found {ftCount} objects ");
            

            Benchmark(() =>
            {
                var result = homes.FullTextSearch("close metro").ToList();
                ftCount = result.Count;
                
            }, "searching for 'close metro'");
            Console.WriteLine($" -> found {ftCount} objects ");

            Benchmark(() =>
            {
                var result = homes.FullTextSearch("close metro").ToList();
                ftCount = result.Count;
                
            }, "searching for 'ps4'");
            Console.WriteLine($" -> found {ftCount} objects ");

            Benchmark(() =>
            {
                var result = homes.FullTextSearch("rue de la mort").ToList();
                ftCount = result.Count;
                
            }, "searching for 'rue de la mort'");
            Console.WriteLine($" -> found {ftCount} objects ");


        }

       
    }
}