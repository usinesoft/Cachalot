using System;
using System.Collections.Generic;
using Cachalot.Linq;
using Client.Interface;

// ReSharper disable AccessToModifiedClosure


namespace BookingMarketplace
{
    // ReSharper disable once ClassNeverInstantiated.Global
    internal partial class Program
    {
        
        private static IList<Home> GenerateTestData(int[] ids)
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

        private static int ObjectCount = 100_000;

        private static void Main(string[] args)
        {
            if (args.Length > 0)
            {
                try
                {
                    ObjectCount = int.Parse(args[0]);
                }
                catch (Exception )
                {
                    Console.WriteLine("Argument ignored. Only one integer argument can be specified");
                }
            }

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
    }
}