using System;

namespace AdminConsole.ConsoleUtils.ConsoleActions
{
    public interface IConsoleAction
    {
        void Execute(IConsole console, ConsoleKeyInfo consoleKeyInfo);
    }
}