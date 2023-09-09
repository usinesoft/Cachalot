using System;

namespace AdminConsole.ConsoleUtils.ConsoleActions;

public class MoveCursorRightAction : IConsoleAction
{
    public void Execute(IConsole console, ConsoleKeyInfo consoleKeyInfo)
    {
        console.CursorPosition = Math.Min(console.CurrentLine.Length, console.CursorPosition + 1);
    }
}