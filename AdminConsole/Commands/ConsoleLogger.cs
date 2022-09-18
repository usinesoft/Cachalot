using Client.Core;
using System;

namespace AdminConsole.Commands
{
    public class ConsoleLogger : ICommandLogger
    {
        #region ICommandLogger Members

        public void Write(string message)
        {

            var colorBefore = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.DarkGreen;
            //ConsoleExt.CurrentLine = message;
            //ConsoleExt.StartNewLine();
            Console.WriteLine(message);

            Console.ForegroundColor = colorBefore;
        }

        public void Write(string format, params object[] parameters)
        {
            var colorBefore = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.DarkGreen;
            Console.WriteLine(format, parameters);
            //var message = string.Format(format, parameters);
            //ConsoleExt.CurrentLine = message;
            //ConsoleExt.StartNewLine();

            Console.ForegroundColor = colorBefore;
        }

        public void WriteError(string message)
        {
            var colorBefore = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.Red;
            //ConsoleExt.CurrentLine = message;
            //ConsoleExt.StartNewLine();
            Console.WriteLine(message);

            Console.ForegroundColor = colorBefore;
        }

        public void WriteError(string format, params object[] parameters)
        {
            var colorBefore = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.Red;
            var message = string.Format(format, parameters);
            //ConsoleExt.CurrentLine = message;
            //ConsoleExt.StartNewLine();
            Console.WriteLine(message);

            Console.ForegroundColor = colorBefore;
        }

        #endregion
    }
}