using System;

namespace AdminConsole.ConsoleUtils.ConsoleActions;

public class BackspaceAction : IConsoleAction
{
    public void Execute(IConsole console, ConsoleKeyInfo consoleKeyInfo)
    {
        if (console.CursorPosition > 0)
        {
            console.CurrentLine = console.CurrentLine.Remove(console.CursorPosition - 1, 1);
            console.CursorPosition--;
        }
    }
}