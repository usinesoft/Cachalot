using System;

namespace AdminConsole.ConsoleUtils.ConsoleActions;

public class CycleBottomAction : IConsoleAction
{
    public void Execute(IConsole console, ConsoleKeyInfo consoleKeyInfo)
    {
        if (!console.PreviousLineBuffer.CycleBottom())
            return;
        console.CurrentLine = console.PreviousLineBuffer.LineAtIndex;
        console.CursorPosition = console.CurrentLine.Length;
    }
}