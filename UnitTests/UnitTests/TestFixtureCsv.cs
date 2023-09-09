using System;
using System.Linq;
using Client.Core;
using Client.Tools;
using NUnit.Framework;

namespace Tests.UnitTests
{
    [TestFixture]
    public class TestFixtureCsv
    {
        [Test]
        public void Test_schema_inference()
        {
            var csvHelper = new CsvSchemaBuilder("TestData/csv/20klines.csv");

            var schema = csvHelper.InferSchema();

            Console.Write(schema.AnalysisReport());

            var collectionSchema = schema.ToCollectionSchema();

            var dealId = collectionSchema.ServerSide.FirstOrDefault(x => x.Name == "DealId");
            Assert.IsNotNull(dealId);
            Assert.AreEqual(dealId.IndexType, IndexType.Dictionary);
            Assert.AreEqual(1, dealId.Order);

            var currentValue = collectionSchema.ServerSide.FirstOrDefault(x => x.Name == "CurrentValue");
            Assert.IsNotNull(currentValue);
            Assert.AreEqual(currentValue.IndexType, IndexType.None, "float values should not be indexed");
        }

        [Test]
        [TestCase("-618913.3333", KeyValue.OriginalType.SomeFloat)]
        [TestCase("3/20/2017", KeyValue.OriginalType.Date)]
        [TestCase("KB-143", KeyValue.OriginalType.String)]
        [TestCase("8/2/2022 11:03", KeyValue.OriginalType.Date)]
        [TestCase("0001-01-01", KeyValue.OriginalType.Date)]
        [TestCase("2005-01-01", KeyValue.OriginalType.Date)]
        public void Type_detection(string value, KeyValue.OriginalType expectedType)
        {
            var val = JExtensions.SmartParse(value);
            var type = new KeyValue(val).Type;

            Assert.AreEqual(expectedType, type);
        }


        [Test]
        public void Split_csv_lines()
        {
            var line =
                @"24228985,c68b5bbe-25d2-49dd-b3a1-5886c7ab3662,MTM,,TPT,MWH,436908.04800000,GEM,,2019-11-20,SettlementO,,,GSA,137252627,DTRZE,PF,,,,,,,EUR,69835.50925300,70095.89501700,2025-11-14,2023-02-15,2025-11-14,2022-07-31,,2022-07-31,2023-04-20,EUR,Unchanged,Modified,XP,0,EXFLEX,B,"" "",DE,DWSTR,Midstream,GASAM,2023-03-01,2023-03-31,2023,MAR-23,GSA,,True,,,EUR,,,0,,,False,IntraDivIntraOC,True,2022-08-02 11:03:59,Valo IFRS POP  M-1 intrinsic,To Be Mapped,Merchant,24228984,0,0,69835.50925300,70095.89501700,False,Engie Gas Trading,Engie Gas Trading,C,31920,,PHY,PHY,DSPZE,GAS,External Business - ABT,Supply,BE - Others MtM,""BE - Others MtM (Loc Spreads, DA Index, etc.)"",False,DTRZE,1,0.00000000,0.00000000,XX,,HBO-DSPZE-FXP-DTRZE,,,0,MRM,,ZTP VS ZGH,2019-11-20,BROLLY Xavier,PA,0.00000000,0.00000000,0,MUN,69835.50925300,70095.89501700,0.00000000,,,,,,,4554842,4559792,67316,Meteor";

            var values = CsvHelper.SplitCsvLine(line, ',');

            Assert.AreEqual(120, values.Count);
        }
    }
}