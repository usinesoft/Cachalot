using Cachalot.Linq;

namespace StressTests
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length == 0)
            {
                PerformReconnectionTests();
            }
        }

        private static void PerformReconnectionTests()
        {
            using var connector = new Connector("localhost:4848");
            
            while (true)
            {
                var entities = connector.DataSource<AbstractEntity>();



            }
        }
    }
}
