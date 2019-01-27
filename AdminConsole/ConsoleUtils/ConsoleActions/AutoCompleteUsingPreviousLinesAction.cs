using System;
using System.Globalization;

namespace AdminConsole.ConsoleUtils.ConsoleActions
{
    public class AutoCompleteUsingPreviousLinesAction : IConsoleAction
    {
        public void Execute(IConsole console, ConsoleKeyInfo consoleKeyInfo)
        {
            if (!console.PreviousLineBuffer.HasLines)
                return;
            var pattern = console.CurrentLine.Substring(0, console.CursorPosition);
            console.CurrentLine = pattern + AutoCompleteUsingPreviousLines(console, pattern);
        }

        private string AutoCompleteUsingPreviousLines(IConsole console, string pattern)
        {
            var previousLineBuffer = console.PreviousLineBuffer;
            previousLineBuffer.CycleUpAndAround();

            for (var i = previousLineBuffer.Index; i >= 0; i--)
            {
                var previousLine = previousLineBuffer.PreviousLines[i];
                if (previousLine.StartsWith(pattern, false, CultureInfo.InvariantCulture))
                {
                    previousLineBuffer.Index = i;
                    return previousLine.Remove(0, pattern.Length);
                }
            }

            for (var i = previousLineBuffer.PreviousLines.Count - 1; i > previousLineBuffer.Index; i--)
            {
                var previousLine = previousLineBuffer.PreviousLines[i];
                if (previousLine.StartsWith(pattern, false, CultureInfo.InvariantCulture))
                {
                    previousLineBuffer.Index = i;
                    return previousLine.Remove(0, pattern.Length);
                }
            }

            return string.Empty;
        }
    }
}