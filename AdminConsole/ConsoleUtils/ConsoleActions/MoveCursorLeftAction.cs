using System;

namespace AdminConsole.ConsoleUtils.ConsoleActions;

public class MoveCursorLeftAction : IConsoleAction
{
    public void Execute(IConsole console, ConsoleKeyInfo consoleKeyInfo)
    {
        console.CursorPosition = Math.Max(0, console.CursorPosition - 1);
    }
}