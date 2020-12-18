using Tests.UnitTests;

namespace Spike
{
    class Program
    {
        static void Main(string[] args)
        {
            for (int i = 1; i < 100; i++)
            {
                new TestFixtureLockManager().No_deadlock_and_no_race_condition_on_resources();
            }
        }
    }
}
