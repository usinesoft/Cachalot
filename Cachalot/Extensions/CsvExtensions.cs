using Cachalot.Linq;
using Client.Tools;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Cachalot.Extensions
{
    public static class CsvExtensions
    {

        private static IEnumerable<string> LinesGenerator(TextReader reader)
        {
            try
            {

                var line = reader.ReadLine();
                while (line != null)
                {

                    yield return line;

                    line = reader.ReadLine();
                }
            }
            finally
            {
                reader.Close();
            }
            
        }

        public static void FeedCsvWithAutomaticSchema(this Connector connector, string csvFileName, string collectionName)
        {
            var csvSchema = new CsvSchemaBuilder(csvFileName).InfereSchema();

            using var fileStream = File.OpenRead(csvFileName);
            using var reader = new StreamReader(fileStream, Encoding.UTF8, true, 50000);
            
            var _ = reader.ReadLine(); // first one is the header => ignore it

            var schema = csvSchema.ToCollectionSchema();

            connector.DeclareCollection(collectionName, schema);

            connector.FeedWithCsvLines(collectionName, LinesGenerator(reader), csvSchema.Separator);

        }
    }
}
