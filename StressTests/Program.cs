using System;
using System.Collections.Generic;
using System.Linq;
using Cachalot.Linq;
using StressTests.Model;
using StressTests.TestData;

namespace StressTests
{
    internal class Program
    {
        private static void Main(string[] args)
        {
            if (args.Length == 0)
            {
                FeedDatabase();
                QueryTests();
            }
            else
            {
                FeedDatabase();
            }
        }


        static void CheckEqualAndNotVoid<T>(IList<T> collection1, IList<T> collection2)
        {
            if (collection1.Count == 0)
            {
                throw new Exception("empty collection");
            }

            if (collection1.Count != collection2.Count)
            {
                throw new Exception("Not the same number of elements");
            }

            for (int i = 0; i < collection1.Count; i++)
            {
                if (!collection1[i].Equals(collection2[i]))
                {
                    throw new Exception($"Different element at index {i}");
                }
            }
        }

        private static void QueryTests()
        {
            //using var connector = new Connector("localhost:48401+localhost:48402");
            using var connector = new Connector("localhost:48401");
            

            DeclareCollections(connector);

            var products = connector.DataSource<Product>("products");
            
            var salesDetails = connector.DataSource<SaleLine>("sales_detail");

            // = 

            {
                
                var withLinq = products.Where(p=>p.Brand == "REVLON").ToList();
                
                var withSql = products.SqlQuery("select from products where brand = REVLON").ToList();

                CheckEqualAndNotVoid(withLinq, withSql);
            }

            // <>
            {
                
                var withLinq = products.Where(p=>p.Brand != "REVLON").ToList();
                
                var withSql1 = products.SqlQuery("select from products where brand != REVLON").ToList();
                var withSql2 = products.SqlQuery("select from products where brand <> REVLON").ToList();

                CheckEqualAndNotVoid(withLinq, withSql1);
                CheckEqualAndNotVoid(withLinq, withSql2);
            }
            
            // < <= > >=
            {

                var withLinq = salesDetails.Where(s =>
                    s.Date > new DateTime(2020, 1, 1) && s.Date <= new DateTime(2020, 1, 15)).ToList();
                
                var withSql = salesDetails.SqlQuery("select from sales_detail where date > 2020-01-01 and date <= 2020-01-15").ToList();
                
                
                CheckEqualAndNotVoid(withLinq, withSql);
                
            }

            // with bool value
            {

                var withLinq = salesDetails.Where(s => s.IsDelivered).ToList();
                
                var withSql = salesDetails.SqlQuery("select from sales_detail where isdelivered=true").ToList();
                
                
                CheckEqualAndNotVoid(withLinq, withSql);
                
            }

            // with enum value
            {

                var withLinq = salesDetails.Where(s => s.Channel == Model.Channel.Facebook).ToList();
                
                var withSql = salesDetails.SqlQuery("select from sales_detail where channel = 1").ToList();
                
                
                CheckEqualAndNotVoid(withLinq, withSql);
                
            }
            
            // in
            {
                var brands = new[] {"REVLON", "Advanced Clinicals"};
                var withLinq = products.Where(p=>brands.Contains(p.Brand)).ToList();
                
                var withSql = products.SqlQuery("select from products where brand in (REVLON, Advanced Clinicals)").ToList();

                CheckEqualAndNotVoid(withLinq, withSql);
            }

            // not in 
            {
                var brands = new[] {"REVLON", "DOVE"};
                var withLinq = products.Where(p=>!brands.Contains(p.Brand)).ToList();
                
                var withSql = products.SqlQuery("select from products where brand not in (REVLON, DOVE)").ToList();

                CheckEqualAndNotVoid(withLinq, withSql);
            }


            // contains
            {
                

                var withLinq = products.Where(p=>p.Categories.Contains("lip stick")).ToList();
                var withSql = products.SqlQuery("select from products where categories contains lip stick").ToList();

                CheckEqualAndNotVoid(withLinq, withSql);
            }

            // not contains

            {
                
                var withLinq = products.Where(p=>!p.Categories.Contains("soap")).ToList();
                var withSql = products.SqlQuery("select from products where categories not contains soap").ToList();

                CheckEqualAndNotVoid(withLinq, withSql);
            }

            

            // like
            {
                var withLinq = products.Where(p=>p.Brand.Contains("clinical")).ToList();
                
                var withSql = products.SqlQuery("select from products where brand like %clinical%").ToList();

                CheckEqualAndNotVoid(withLinq, withSql);
            }

            // starts with
            {
                var withLinq = products.Where(p=>p.Brand.StartsWith("advanced")).ToList();
                
                var withSql = products.SqlQuery("select from products where brand like advanced%").ToList();

                CheckEqualAndNotVoid(withLinq, withSql);
            }

            // ends with
            {
                var withLinq = products.Where(p=>p.Brand.EndsWith("clinicals")).ToList();
                
                var withSql = products.SqlQuery("select from products where brand like %clinicals").ToList();

                CheckEqualAndNotVoid(withLinq, withSql);
            }

            // projection scalar 
            {
                var withLinq = products.Where(p=>p.Brand == "REVLON").Select(p=>p.Name).ToList();
                
                var withSql = products.SqlQuery("select Name from products where brand = REVLON").ToList();

            }

            // projection collection
            {
                var withLinq = products.Where(p=>p.Brand == "REVLON").Select(p=>new { p.Categories}).ToList();
                
                var withSql = products.SqlQuery("select categories from products where brand = REVLON").ToList();

            }

            // projection multiple properties
            {
                var withLinq = products.Where(p=>p.Brand == "REVLON").Select(p=>new {p.Name, p.ScanCode}).ToList();
                
                var withSql = products.SqlQuery("select name, scancode from products where brand = REVLON").ToList();

            }

            // distinct single property
            {
                var withLinq = products.Select(p=>p.Brand).Distinct().ToList();
                
                var withSql = products.SqlQuery("select distinct brand from products").ToList();
            }

            // distinct multiple properties
            {
                var withLinq = products.Select(p=>new {p.Brand, p.Name}).Distinct().ToList();
                
                var withSql = products.SqlQuery("select distinct brand, name from products").ToList();
            }

            // take
            {
                var withLinq = salesDetails.Where(s=>s.IsDelivered && s.Amount > 80).Take(10).ToList();
                
                var withSql = salesDetails.SqlQuery("select from sales_detail where isdelivered = true and amount > 80 take 10").ToList();

                CheckEqualAndNotVoid(withLinq, withSql);
            }

            // order by
            {
                var withLinq = salesDetails.Where(s=>s.IsDelivered && s.Amount > 80).OrderBy(s=> s.Amount).ToList();
                
                var withSql = salesDetails.SqlQuery("select from sales_detail where isdelivered = true and amount > 80 order by amount").ToList();

                CheckEqualAndNotVoid(withLinq, withSql);
            }

            // order by descending
            {
                var withLinq = salesDetails.Where(s=>s.IsDelivered && s.Amount > 80).OrderBy(s=> s.Amount).ToList();
                
                var withSql = salesDetails.SqlQuery("select from sales_detail where isdelivered = true and amount > 80 order by amount").ToList();

                CheckEqualAndNotVoid(withLinq, withSql);
            }


        }

        private static void DeclareCollections(Connector connector)
        {
            connector.DeclareCollection<Outlet>("outlets");
            connector.DeclareCollection<Product>("products");
            connector.DeclareCollection<Sale>("sales");
            connector.DeclareCollection<SaleLine>("sales_detail");
            connector.DeclareCollection<Stock>();
        }

        private static void FeedDatabase()
        {
            
            //using var connector = new Connector("localhost:48401+localhost:48402");
            using var connector = new Connector("localhost:48401");

            connector.AdminInterface().DropDatabase();

            DeclareCollections(connector);
            
            try
            {
                var outlets = connector.DataSource<Outlet>("outlets");
                var outlet = DataGenerator.GenerateOutlet();
                outlets.Put(outlet);

                var products = connector.DataSource<Product>("products");
                var prods = DataGenerator.GenerateProducts(connector);
                products.PutMany(prods);

                var sales = connector.DataSource<Sale>("sales");
                var salesDetails = connector.DataSource<SaleLine>("sales_detail");

                var data = DataGenerator.GenerateSales(100_000, outlet, prods).ToList();

                var sls = data.Select(t => t.Item1).ToList();
                var sld = data.Select(t => t.Item2).ToList();
                sales.PutMany(sls);
                salesDetails.PutMany(sld);



            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }
        }
    }
}