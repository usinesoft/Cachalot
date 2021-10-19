using System;
using System.Diagnostics;

namespace BookingMarketplace
{
    internal partial class Program
    {
        private static void Title(string message)
        {
            var colorBefore = Console.ForegroundColor;

            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine(message.ToUpper());
            Console.WriteLine();

            Console.ForegroundColor = colorBefore;
        }

        private static void Header(string message)
        {
            var colorBefore = Console.ForegroundColor;

            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Blue;
            Console.WriteLine(message);
            Console.ForegroundColor = colorBefore;
        }

        private static void ResultHeader(string message = "result:")
        {
            var colorBefore = Console.ForegroundColor;

            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine(message);
            Console.ForegroundColor = colorBefore;
        }

        private static void RunOnce(Action action, string message)
        {
            try
            {
                var watch = new Stopwatch();

                watch.Start();

                action();

                watch.Stop();

                Console.WriteLine($" {message} took {watch.ElapsedMilliseconds} ms");
            }
            catch (Exception e)
            {
                Console.WriteLine($" {message} failed: {e.Message}");
            }
        }

        private static void Benchmark(Action action, string message)
        {
            try
            {
                // warm-up do not count
                action();

                var watch = new Stopwatch();

                watch.Start();

                const int iterations = 10;
                for (int i = 0; i < iterations; i++)
                {
                    action();
                }

                watch.Stop();

                Console.WriteLine(
                    $" {iterations} times {message} took {watch.ElapsedMilliseconds} ms average={watch.ElapsedMilliseconds / iterations} ms");
            }
            catch (Exception e)
            {
                Console.WriteLine($" {message} failed: {e.Message}");
            }
        }

        private static void CheckThat<T>(Predicate<T> check, string messageIfFails, T toCheck)
        {
            if (!check(toCheck))
            {
                throw new NotSupportedException(messageIfFails);
            }
        }
    }
}