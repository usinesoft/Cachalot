using System;

namespace AdminConsole.ConsoleUtils.ConsoleActions
{
    public class AutoCompleteSingleCharacterAction : IConsoleAction
    {
        public void Execute(IConsole console, ConsoleKeyInfo consoleKeyInfo)
        {
            if (!console.PreviousLineBuffer.HasLines ||
                console.CurrentLine.Length >= console.PreviousLineBuffer.LastLine.Length)
                return;
            if (console.CursorPosition == console.CurrentLine.Length)
                console.CurrentLine =
                    console.CurrentLine + console.PreviousLineBuffer.LastLine[console.CurrentLine.Length];
            console.CursorPosition++;
        }
    }
}