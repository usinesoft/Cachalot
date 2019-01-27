using System;

namespace AdminConsole.ConsoleUtils.ConsoleActions
{
    public class AutoCompleteRestOfLineAction : IConsoleAction
    {
        public void Execute(IConsole console, ConsoleKeyInfo consoleKeyInfo)
        {
            if (!console.PreviousLineBuffer.HasLines)
                return;
            var previous = console.PreviousLineBuffer.LineAtIndex;
            if (previous.Length <= console.CursorPosition)
                return;
            var partToUse = previous.Remove(0, console.CursorPosition);
            var newLine = console.CurrentLine.Remove(console.CursorPosition,
                Math.Min(console.CurrentLine.Length - console.CursorPosition, partToUse.Length));
            console.CurrentLine = newLine.Insert(console.CursorPosition, partToUse);
            console.CursorPosition = console.CursorPosition + partToUse.Length;
        }
    }
}