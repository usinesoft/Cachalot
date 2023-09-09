using System;

namespace AdminConsole.ConsoleUtils.ConsoleActions;

public class MoveCursorToEndAction : IConsoleAction
{
    public void Execute(IConsole console, ConsoleKeyInfo consoleKeyInfo)
    {
        console.CursorPosition = console.CurrentLine.Length;
    }
}