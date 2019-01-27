using System;

namespace AdminConsole.ConsoleUtils.ConsoleActions
{
    public class InsertStringAction : IConsoleAction
    {
        private readonly string _string;

        public InsertStringAction(string str)
        {
            _string = str;
        }

        public void Execute(IConsole console, ConsoleKeyInfo consoleKeyInfo)
        {
            if (string.IsNullOrEmpty(_string))
                return;
            if (console.CurrentLine.Length >= byte.MaxValue - _string.Length)
                return;
            console.CurrentLine = console.CurrentLine.Insert(console.CursorPosition, _string);
            console.CursorPosition += _string.Length;
        }
    }
}