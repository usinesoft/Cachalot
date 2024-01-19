using System;
using System.Linq;
using Cachalot.Linq;
using Client.Core.Linq;
using Client.Messages.Pivot;

namespace BookingMarketplace
{
    internal partial class Program
    {
        private static void PerfTest(Connector connector)
        {

            connector.DeclareCollection<Home>();

            var homes = connector.DataSource<Home>();
            homes.Truncate();

            int feedObjects = _objectCount;
            var ids = connector.GenerateUniqueIds("property_id", feedObjects);

            var items = GenerateTestData(ids);

            //////////////////////////////////////////////////////
            // 1 feed with many objects

            Header("Feeding data");

            RunOnce(() => homes.PutMany(items), $"feeding the collection with {feedObjects} objects");

            var count = homes.Count();
            if (count != items.Count)
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
                    var _ = homes[ids[i]];
                }
            }, $"reading {objectsRead} objects using primary key");

            Benchmark(() =>
            {
                for (int i = 0; i < objectsRead; i++)
                {
                    var _ = homes.First(h => h.Id == ids[i]);
                }
            }, $"reading {objectsRead} objects using linq");

            Benchmark(() =>
            {
                for (int i = 0; i < objectsRead; i++)
                {
                    var _ = homes.SqlQuery($"select from home where id={ids[i]}").First();
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
                CheckThat(r => r.Count == resultCount, "wrong number of objects in result", inParis);
            }, $"reading {resultCount} objects with linq");

            Benchmark(() =>
            {
                inParis = homes.SqlQuery("select from home where town = Paris").ToList();
                CheckThat(r => r.Count == resultCount, "wrong number of objects in result", inParis);
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

                CheckThat(r => r.Count == resultCount, "wrong number of objects in result",
                    inParisNotExpensiveWithManyRooms);
            }, $"reading {resultCount} objects with linq");

            Benchmark(() =>
            {
                inParisNotExpensiveWithManyRooms = homes.SqlQuery(
                        "select from home where town=Paris and priceineuros >= 150 and PriceInEuros <= 200 and rooms > 2")
                    .ToList();

                CheckThat(r => r.Count == resultCount, "wrong number of objects in result",
                    inParisNotExpensiveWithManyRooms);
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

                CheckThat(r => r.Count == resultCount, "wrong number of objects in result", inParisAvailableToday);
            }, $"reading {resultCount} objects with linq");

            Benchmark(() =>
            {
                inParisAvailableToday =
                    homes.SqlQuery($"select from home where town=Paris and AvailableDates contains {DateTime.Today:d}")
                        .ToList();

                CheckThat(r => r.Count == resultCount, "wrong number of objects in result", inParisAvailableToday);
            }, $"reading {resultCount} objects with sql");

            Header(
                $"select from home where town=Paris and AvailableDates contains {DateTime.Today:d} order by PriceInEuros descending take 10");

            Benchmark(() =>
            {
                inParisAvailableToday = homes
                    .Where(p => p.Town == "Paris" && p.AvailableDates.Contains(DateTime.Today))
                    .OrderByDescending(h => h.PriceInEuros).Take(10)
                    .ToList();

                CheckThat(r => r.Count == 10, "wrong number of objects in result", inParisAvailableToday);
            }, "reading 10 objects with linq");

            Benchmark(() =>
            {
                inParisAvailableToday =
                    homes.SqlQuery(
                            $"select from home where town=Paris and AvailableDates contains {DateTime.Today:d} order by PriceInEuros descending take 10")
                        .ToList();

                CheckThat(r => r.Count == 10, "wrong number of objects in result", inParisAvailableToday);
            }, "reading 10 objects with sql");

            Header(
                $"select from home where town=Paris and AvailableDates contains {DateTime.Today:d} order by PriceInEuros");

            Benchmark(() =>
            {
                inParisAvailableToday = homes
                    .Where(p => p.Town == "Paris" && p.AvailableDates.Contains(DateTime.Today))
                    .OrderBy(h => h.PriceInEuros)
                    .ToList();

                CheckThat(r => r.Count == resultCount, "wrong number of objects in result", inParisAvailableToday);
            }, $"reading {resultCount} objects with linq");

            Benchmark(() =>
            {
                inParisAvailableToday =
                    homes.SqlQuery(
                            $"select from home where town=Paris and AvailableDates contains {DateTime.Today:d} order by PriceInEuros")
                        .ToList();

                CheckThat(r => r.Count == resultCount, "wrong number of objects in result", inParisAvailableToday);
            }, $"reading {resultCount} objects with sql");

            //////////////////////////////////////////////////////
            // 8 query with projection and DISTINCT clause

            var distinctResult = items.Select(h => new {h.CountryCode, h.Town}).Distinct().ToList();
            resultCount = distinctResult.Count;

            Header($"select distinct CountryCode, Town from home");

            Benchmark(() =>
            {
                distinctResult = homes.Select(h => new {h.CountryCode, h.Town}).Distinct().ToList();

                CheckThat(r => r.Count == resultCount, "wrong number of objects in result", distinctResult);
            }, $"reading {resultCount} objects with linq");

            ResultHeader();
            foreach (var result in distinctResult)
            {
                Console.WriteLine(result.ToString());
            }
            Console.WriteLine();

            Header($"select distinct Town from home");

            var distinctResult1 = items.Select(h => new {h.Town}).Distinct().ToList();
            resultCount = distinctResult.Count;

            Benchmark(() =>
            {
                var distinctResult1 = homes.Select(h => new {h.Town}).Distinct().ToList();

                CheckThat(r => r.Count == resultCount, "wrong number of objects in result", distinctResult1);
            }, $"reading {resultCount} objects with linq");

            ResultHeader();
            foreach (var result in distinctResult1)
            {
                Console.WriteLine(result.ToString());
            }
            Console.WriteLine();
            
            

            //////////////////////////////////////////////////////
            // 7 full text search

            Header("full-text search");

            int ftCount = 0;
            Benchmark(() =>
            {
                var result = homes.FullTextSearch("beautiful view").ToList();
                ftCount = result.Count;
            }, "searching for 'beautiful view'");
            Console.WriteLine($" -> found {ftCount} objects ");


            Benchmark(() =>
            {
                var result = homes.FullTextSearch("close metro").ToList();
                ftCount = result.Count;
            }, "searching for 'close metro'");
            Console.WriteLine($" -> found {ftCount} objects ");

            Benchmark(() =>
            {
                var result = homes.FullTextSearch("ps4").ToList();
                ftCount = result.Count;
            }, "searching for 'ps4'");
            Console.WriteLine($" -> found {ftCount} objects ");

            Benchmark(() =>
            {
                var result = connector.SqlQueryAsJson("select from home take 10", "rue de la mort").ToList();
                ftCount = result.Count;
            }, "mixed search 'rue de la mort'");
            Console.WriteLine($" -> found {ftCount} objects ");


            //////////////////////////////////////////////////////
            // 8 pivot calculation

            PivotLevel pivot = null;

            Header("computing pivot without filter (full data)");

            Benchmark(() =>
            {
                pivot = homes.PreparePivotRequest()
                    .OnAxis(h => h.CountryCode, h => h.Town)
                    .AggregateValues(h => h.Rooms, h => h.PriceInEuros)
                    .Execute();
            }, "computing pivot without filter");

            ResultHeader();
            Console.WriteLine(pivot.ToString());

            Header("computing pivot with filter (CountryCode = FR)");

            Benchmark(() =>
            {
                pivot = homes.PreparePivotRequest(h => h.CountryCode == "FR")
                    .OnAxis(h => h.CountryCode, h => h.Town)
                    .AggregateValues(h => h.Rooms, h => h.PriceInEuros)
                    .Execute();
            }, "computing pivot with filter");

            ResultHeader();
            Console.WriteLine(pivot.ToString());
        }
    }
}