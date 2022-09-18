using Cachalot.Linq;
using StressTests.Model;
using System;
using System.Collections.Generic;

namespace StressTests.TestData
{
    public static class DataGenerator
    {
        public static Outlet GenerateOutlet()
        {
            return new Outlet
            {
                Active = true,
                Address = "13 rue de la mort qui tue",
                ContactName = "Didier Dupont",
                Country = "FR",
                Currency = "EUR",
                Id = Guid.NewGuid(),
                Name = "Beauty Box",
                Town = "Ouilles"
            };
        }

        public static IList<Product> GenerateProducts(Connector connector)
        {
            const int count = 10;

            var ids = connector.GenerateUniqueIds("product_id", count);

            return new List<Product>
            {
                new Product
                {
                    ProductId = ids[0],
                    About = new List<string>{"A wonderful anti age cream"},
                    Brand = "LOREAL",
                    Categories = {"cream", "face", "anti age"},
                    Description = "you will look like Shrek",
                    ImageId = "img0005",
                    Ingredients = {"argan"},
                    Name = "Loreal Shrek X25",
                    ScanCode = "65465465",
                    Summary = "Put it on your face please",

                },
                new Product
                {
                    ProductId = ids[1],
                    About = new List<string>{"A very effective anti-wrinkle cream"},
                    Brand = "LOREAL",
                    Categories = {"cream", "face", "wrinkle"},
                    Description = "you will look like Madonna",
                    ImageId = "img0006",
                    Ingredients = {"argan"},
                    Name = "Loreal neat face v12",
                    ScanCode = "65465478",
                    Summary = "Put it on your face too please",


                }
                ,
                new Product
                {
                    ProductId = ids[2],
                    About = new List<string>{"A very effective anti-wrinkle cream"},
                    Brand = "NIVEA",
                    Categories = {"cream", "face-cream", "anti-wrinkle"},
                    Description = "you will look like Madonna",
                    ImageId = "img0007",
                    Ingredients = {"argan"},
                    Name = "Nivea Hyaluronic Cellular Filler",
                    ScanCode = "654665478",
                    Summary = "Anti-Ageing Day Cream, Day Cream with SPF 15",

                },

                new Product
                {
                    ProductId = ids[3],
                    About = new List<string>{"Face Moisturizer with Vitamin B3"},
                    Brand = "OLAY",
                    Categories = {"face cream", "moisturizer"},
                    Description = "you will look like Madonna",
                    ImageId = "img0008",
                    Ingredients = {"Vitamin B3"},
                    Name = "Olay Regenerist Collagen Peptide 24",
                    ScanCode = "654665848",
                    Summary = "Face Moisturizer with Vitamin B3",

                }

                ,
                new Product
                {
                    ProductId = ids[4],
                    About = new List<string>{"Face Moisturizer with Vitamin B3"},
                    Brand = "Advanced Clinicals",
                    Categories = {"legs cream", "body cream"},
                    Description = "you will look like Madonna",
                    ImageId = "img0009",
                    Ingredients = {"Vitamin C"},
                    Name = "Advanced Clinicals Vitamin C Cream",
                    ScanCode = "645665848",
                    Summary = "Advanced Brightening Cream. Anti-aging cream for age spots, dark spots on face, hands, body",

                },

                new Product
                {
                    ProductId = ids[5],
                    About = new List<string>{"Face Moisturizer with Vitamin B3"},
                    Brand = "Advanced Clinicals",
                    Categories = {"legs cream", "body cream"},
                    Description = "you will look like Madonna",
                    ImageId = "img0009",
                    Ingredients = {"Vitamin C"},
                    Name = "Advanced Clinicals Vitamin C Cream",
                    ScanCode = "645665848",
                    Summary = "Advanced Brightening Cream. Anti-aging cream for age spots, dark spots on face, hands, body",

                }
                ,
                new Product
                {
                    ProductId = ids[6],
                    About = new List<string>{"Multi-finish Lipcolor Gift Set"},
                    Brand = "REVLON",
                    Categories = {"legs cream", "body cream"},
                    Description = "you will look like Madonna",
                    ImageId = "img0009",
                    Ingredients = {"Vitamin C"},
                    Name = "REVLON Super Lustrous Lipstick",
                    ScanCode = "645665846",
                    Summary = "5 Piece Multi-finish Lipcolor Gift Set, in Cream Pearl & Matte",

                }
                ,
                new Product
                {
                    ProductId = ids[7],
                    About = new List<string>{"Multi-finish Lipcolor Gift Set"},
                    Brand = "REVLON",
                    Categories = {"brush"},
                    Description = "you will look like Madonna",
                    ImageId = "img0010",
                    Ingredients = {"Vitamin C"},
                    Name = "REVLON One-Step",
                    ScanCode = "645665849",
                    Summary = "REVLON One-Step Hair Dryer And Volumizer Hot Air Brush",

                }
                ,
                new Product
                {
                    ProductId = ids[8],
                    About = new List<string>{"Full Size Lip Kit- Lip Liner, Lipstick, and Lip Gloss"},
                    Brand = "Charlotte Tilbury",
                    Categories = {"lip liner", "lip stick"},
                    Description = "you will look like Madonna",
                    ImageId = "img0010",
                    Ingredients = {"Vitamin C"},
                    Name = "Charlotte Tilbury The Pillow Talk",
                    ScanCode = "645665849",
                    Summary = "Charlotte Tilbury The Pillow Talk Full Size Lip Kit- Lip Liner, Lipstick, and Lip Gloss",

                }
                ,
                new Product
                {
                    ProductId = ids[9],
                    About = new List<string>{"Dove shea butter beauty Bar: This rich body soap is made with Shea butter to moisturize dry skin - and it comes in a 14-pack for your convenience"},
                    Brand = "DOVE",
                    Categories = {"soap"},
                    Description = "you will look like Madonna",
                    ImageId = "img0011",
                    Ingredients = {"Shea butter"},
                    Name = "Dove Purely Pampering",
                    ScanCode = "645665849",
                    Summary = "This rich body soap is made with Shea butter to moisturize dry skin",

                }
            };


        }

        static Random _rand = new Random();

        public static IEnumerable<Tuple<Sale, SaleLine>> GenerateSales(int count, Outlet outlet, IList<Product> products)
        {

            var date = new DateTime(2020, 1, 1);

            for (int i = 0; i < count; i++)
            {
                var sale = new Sale
                {
                    Id = Guid.NewGuid(),
                    ClientId = i % 100,
                    OutletId = outlet.Id,
                    Date = date.AddDays(_rand.Next(100)).AddHours(_rand.Next(9, 19)),
                    Amount = _rand.NextDouble() * 100,

                };


                var saleLine = new SaleLine
                {
                    Id = Guid.NewGuid(),
                    Amount = _rand.NextDouble() * 100,
                    ClientId = sale.ClientId,
                    Date = sale.Date,
                    IsDelivered = i % 2 == 0,
                    ProductId = products[i % products.Count].ProductId,
                    Quantity = _rand.Next(1, 5),
                    SaleId = sale.Id,
                    Channel = (Model.Channel)(i % 3),
                };

                yield return new Tuple<Sale, SaleLine>(sale, saleLine);

            }

        }
    }
}
