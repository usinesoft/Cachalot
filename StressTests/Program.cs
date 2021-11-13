using System;
using System.Collections.Generic;
using System.Diagnostics;
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
            if (args.Length == 0) FeedDatabase();
        }

       

        private static void FeedDatabase()
        {
            
            //using var connector = new Connector("localhost:48401+localhost:48402");
            using var connector = new Connector("localhost:48401");

            connector.AdminInterface().DropDatabase();

            connector.DeclareCollection<Outlet>("outlets");
            connector.DeclareCollection<Product>("products");
            connector.DeclareCollection<Sale>("sales");
            connector.DeclareCollection<SaleLine>("sales_detail");
            connector.DeclareCollection<Stock>();
            
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