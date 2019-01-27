using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Cachalot.Linq;
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

            
            for (var i = 0; i < ids.Length; i++)
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

            return result;
        }

        private static void Main(string[] args)
        {
            var config = new ClientConfig
            {
                Servers = {new ServerConfig {Host = "localhost", Port = 4848}}
            };

            try
            {
                // quick test with external server
                using (var connector = new Connector(config))
                {
                    Console.WriteLine();
                    Console.WriteLine("test with external server");
                    Console.WriteLine("---------------------------");
                    PerfTest(connector);
                }
            }
            catch (CacheException e)
            {
                Console.WriteLine(e.Message);
            }

            try
            {
                // quick test with in-process server
                using (var connector = new Connector(new ClientConfig { IsPersistent = true }))
                {
                    Console.WriteLine();
                    Console.WriteLine("test with in-process server");
                    Console.WriteLine("---------------------------");
                    PerfTest(connector);
                }
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


            var properties = connector.DataSource<Home>();

            var ids = connector.GenerateUniqueIds("property_id", 1);

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
            properties.Put(property);

            var reloaded = properties.First(p => p.Id == property.Id);

            // this is an update
            properties.Put(property);

            // performance test

            var watch = new Stopwatch();

            watch.Start();
            for (var i = 0; i < TestIterations; i++) properties.Put(property);

            watch.Stop();

            Console.WriteLine($"Updating {TestIterations} items one by one took {watch.ElapsedMilliseconds} ms");

            watch.Reset();
            watch.Start();
            for (var i = 0; i < TestIterations; i++) reloaded = properties.First(p => p.Id == property.Id);

            watch.Stop();

            Console.WriteLine($"Loading {TestIterations} items one by one (linq) took {watch.ElapsedMilliseconds} ms");

            watch.Reset();
            watch.Start();
            for (var i = 0; i < TestIterations; i++) reloaded = properties[property.Id];

            watch.Stop();

            Console.WriteLine(
                $"Loading {TestIterations} items one by one (primary key) took {watch.ElapsedMilliseconds} ms");


            const int feedObjects = 100000;
            var pids = connector.GenerateUniqueIds("property_id", feedObjects);

            var items = GenerateMany(pids);

            var hashset = new HashSet<int>(pids);
            // check they are all identical
            if (hashset.Count != pids.Length)
            {
                throw  new NotSupportedException("ids are not unique");
            }


            // feed many
            watch.Reset();
            watch.Start();

            properties.PutMany(items);

            watch.Stop();

            Console.WriteLine($"Inserting {feedObjects} items at once took {watch.ElapsedMilliseconds} ms");

            // get many

            watch.Reset();
            watch.Start();

            IList<Home> inParis = new List<Home>();
            for (var i = 0; i < 10; i++) inParis = properties.Where(p => p.Town == "Paris").ToList();

            watch.Stop();

            Console.WriteLine($"Select {inParis.Count} items at once took {watch.ElapsedMilliseconds / 10} ms");

            // get many with complex query
            watch.Reset();
            watch.Start();

            IList<Home> inParisNotExpensiveWithManyRooms = new List<Home>();
            for (var i = 0; i < 10; i++)
                inParisNotExpensiveWithManyRooms = properties
                    .Where(p => p.Town == "Paris" && p.PriceInEuros >= 150 && p.PriceInEuros <= 200 && p.Rooms > 2)
                    .ToList();

            watch.Stop();

            Console.WriteLine(
                $"Select {inParisNotExpensiveWithManyRooms.Count} items at once took {watch.ElapsedMilliseconds / 10} ms");

            // get many with contains operator
            watch.Reset();
            watch.Start();

            IList<Home> inParisAvailableToday = new List<Home>();
            for (var i = 0; i < 10; i++)
                inParisAvailableToday = properties
                    .Where(p => p.Town == "Paris" && p.AvailableDates.Contains(DateTime.Today))
                    .ToList();

            watch.Stop();

            Console.WriteLine(
                $"Select with list index {inParisAvailableToday.Count} items at once took {watch.ElapsedMilliseconds / 10} ms");

            watch.Reset();
            watch.Start();

            IList<Home> inParisAvailableTomorrow = new List<Home>();
            for (var i = 0; i < 10; i++)
                inParisAvailableTomorrow = properties
                    .Where(p => p.Town == "Paris" && p.AvailableDates.Contains(DateTime.Today.AddDays(1)))
                    .ToList();

            watch.Stop();

            Console.WriteLine(
                $"Select with list index {inParisAvailableTomorrow.Count} items at once took {watch.ElapsedMilliseconds / 10} ms");

            // update many
            watch.Reset();
            watch.Start();

            properties.PutMany(inParisNotExpensiveWithManyRooms);

            watch.Stop();


            Console.WriteLine(
                $"Update  {inParisAvailableTomorrow.Count} items at once took {watch.ElapsedMilliseconds } ms");


            // delete many with complex query
            watch.Reset();
            watch.Start();

            properties.DeleteMany(p =>
                        p.Town == "Paris" && p.PriceInEuros >= 150 && p.PriceInEuros <= 200 && p.Rooms > 2);
                    
            watch.Stop();


            Console.WriteLine(
                $"Delete  {inParisAvailableTomorrow.Count} items at once took {watch.ElapsedMilliseconds } ms");


        }

        //private static void MoneyTransfer(Connector connector, Account sourceAccount, Account targetAccount, decimal amount)
        //{

        //    sourceAccount.Balance -= amount;
        //    targetAccount.Balance += amount;

        //    var tids = connector.GenerateUniqueIds("transaction_id", 1);
        //    var transfer = new Transaction
        //    {
        //        Id = tids[0],
        //        SourceAccount = sourceAccount.Id,
        //        TargetAccount = targetAccount.Id,
        //        TransferedAmount = amount
        //    };

        //    var transaction = connector.BeginTransaction();
        //    transaction.Put(sourceAccount);
        //    transaction.Put(targetAccount);
        //    transaction.Put(transfer);

        //    // this is where the two stage transaction happens
        //    transaction.Commit();
        //}
    }
}