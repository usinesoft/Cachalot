using System;

namespace AdminConsole.ConsoleUtils.ConsoleActions;

public class RemovePrecedingAction : IConsoleAction
{
    public void Execute(IConsole console, ConsoleKeyInfo consoleKeyInfo)
    {
        if (console.CursorPosition > 0)
        {
            console.CurrentLine = console.CurrentLine.Remove(0, console.CursorPosition);
            console.CursorPosition = 0;
        }
    }
}