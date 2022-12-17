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
                    new() {Text="beautiful view"},
                    new() {Text="close to the metro"},
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
                    new() {Text="ps4"},
                    new() {Text="close to the metro"},
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
                    new() {Text="wonderful sea view"},
                    new() {Text="close to beach"},
                }
            };

            result.Add(h1);
            result.Add(h2);
            result.Add(h3);

            return result;
        }

        private static int _objectCount = 100_000;
        
        private static string _connectionString = "localhost:48401";


        private static void Main(string[] args)
        {

            Title("Test application for Cachalot DB");
            
            Console.WriteLine("Command line options (optional):");
            Console.WriteLine();
            Console.WriteLine("1) connection string: host:port or host1:port1+host2;port2... or --internal to run an in-process server");
            Console.WriteLine($"    by default it will try to connect to {_connectionString}");

            Console.WriteLine("2) number of objects fed to the database for this test");
            Console.WriteLine($"    by default {_objectCount}");

            if (args.Length > 0)
            {
                try
                {
                    _connectionString = args[0];
                    if (_connectionString == "--internal")
                    {
                        _connectionString = null;
                    }

                    if (args.Length > 1)
                    {
                        _objectCount = int.Parse(args[1]);
                    }
                    
                }
                catch (Exception )
                {
                    Console.WriteLine("Invalid command line: using default values");
                }
            }

            try
            {
                
                using var connector = _connectionString != null ? 
                    new Connector(_connectionString): 
                    // ReSharper disable once RedundantArgumentDefaultValue
                    new Connector(false); // internal, non persistent server

                Title($"testing with {_connectionString} and {_objectCount} objects");
                
                PerfTest(connector);
            }
            catch (CacheException e)
            {
                Console.WriteLine(e.Message);
            }

            
        }
    }
}