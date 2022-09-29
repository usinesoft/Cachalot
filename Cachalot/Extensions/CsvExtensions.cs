using Cachalot.Linq;
using Client.Core;
using Client.Interface;
using Client.Tools;
using System;
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

        public static void FeedCsv(this Connector connector, string csvFileName, string collectionName)
        {

            using var fileStream = File.OpenRead(csvFileName);
            
            using var reader = new StreamReader(fileStream, Encoding.UTF8, true, 50000);

            var header = reader.ReadLine();

            var separator = CsvHelper.DetectSeparator(header);

            connector.FeedWithCsvLines(collectionName, LinesGenerator(reader));

        }

        /// <summary>
        /// Pack a data line of a csv file. It also generates unique primary keys
        /// </summary>
        /// <param name="lines"></param>
        /// <param name="schema"></param>
        /// <param name="collectionName"></param>
        /// <returns></returns>
        public static IEnumerable<PackedObject> PackCsv(this Connector connector, IEnumerable<string> lines, string collectionName, char separator = ',')
        {
            if (connector is null)
            {
                throw new ArgumentNullException(nameof(connector));
            }

            int primaryKeyIndex = 0;

           
            // as we do not know how many lines there are, generate unique ids by pack of 1000
            var ids = connector.GenerateUniqueIds($"{collectionName}_id", 1000);

            foreach (var line in lines)
            {
                yield return PackedObject.PackCsv(ids[primaryKeyIndex], line, collectionName, separator);

                primaryKeyIndex++;

                if (primaryKeyIndex == ids.Length - 1) // a new pack of unique ids is required
                {
                    ids = connector.GenerateUniqueIds($"{collectionName}_id", 1000);

                    primaryKeyIndex = 0;
                }

            }
        }
    }
}
