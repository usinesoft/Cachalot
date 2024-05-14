using System.Globalization;
using CsvHelper;
using DatasetGenerator.DataModel;

namespace NewIntegrationTests;

public class ProductLoader
{
    public IEnumerable<Product> Load(string csvPath)
    {
        using var reader = new StreamReader(csvPath);
        using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);
        csv.Context.RegisterClassMap<ProductClassMap>();

        HashSet<string> uniqueCodes = new HashSet<string>();

        foreach (var product in csv.GetRecords<Product>())
        {
            if (uniqueCodes.Add(product.Model))
            {
                product.Brand = product.Brand.Trim();
                yield return product;
            }
            
        }

    }
}