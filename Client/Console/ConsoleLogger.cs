using System;
using Client.Core;

namespace Client.Console
{
    public class ConsoleLogger : ICommandLogger
    {
        #region ICommandLogger Members

        public void Write(string message)
        {
            var colorBefore = System.Console.ForegroundColor;
            System.Console.ForegroundColor = ConsoleColor.DarkGreen;
            System.Console.WriteLine(message);
            System.Console.ForegroundColor = colorBefore;
        }

        public void Write(string format, params object[] parameters)
        {
            var colorBefore = System.Console.ForegroundColor;
            System.Console.ForegroundColor = ConsoleColor.DarkGreen;
            System.Console.WriteLine(format, parameters);
            System.Console.ForegroundColor = colorBefore;
        }

        public void WriteError(string message)
        {
            var colorBefore = System.Console.ForegroundColor;
            System.Console.ForegroundColor = ConsoleColor.Red;
            System.Console.WriteLine(message);
            System.Console.ForegroundColor = colorBefore;
        }

        public void WriteError(string format, params object[] parameters)
        {
            var colorBefore = System.Console.ForegroundColor;
            System.Console.ForegroundColor = ConsoleColor.Red;
            System.Console.WriteLine(format, parameters);
            System.Console.ForegroundColor = colorBefore;
        }

        #endregion
    }
}