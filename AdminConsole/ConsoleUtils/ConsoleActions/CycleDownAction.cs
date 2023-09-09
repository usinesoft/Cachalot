using System;

namespace AdminConsole.ConsoleUtils.ConsoleActions;

public class CycleDownAction : IConsoleAction
{
    public void Execute(IConsole console, ConsoleKeyInfo consoleKeyInfo)
    {
        if (!console.PreviousLineBuffer.CycleDown())
            return;
        console.CurrentLine = console.PreviousLineBuffer.LineAtIndex;
        console.CursorPosition = console.CurrentLine.Length;
    }
}