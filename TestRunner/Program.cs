using System;
using Tests.IntegrationTests;

namespace TestRunner;

internal class Program
{
    private static void Main(string[] args)
    {
        var fixture = new TestFixtureAdvancedCacheWithMultipleNodes();

        fixture.RunBeforeAnyTests();

        Run(() => fixture.Domain_declaration_example(), () => fixture.Init(), () => fixture.Exit());
    }

    private static void Run(Action toRun, Action before, Action after)
    {
        try
        {
            before();
            toRun();

            Console.WriteLine("Success");
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
        finally
        {
            after();
        }
    }
}