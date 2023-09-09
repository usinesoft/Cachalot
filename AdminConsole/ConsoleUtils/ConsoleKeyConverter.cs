using System;

namespace AdminConsole.ConsoleUtils;

public static class ConsoleKeyConverter
{
    public static bool TryParseChar(char keyChar, out ConsoleKey consoleKey)
    {
        if (!Enum.TryParse(keyChar.ToString().ToUpper(), out consoleKey))
            return false;
        return true;
    }

    public static char ConvertConsoleKey(ConsoleKey consoleKey)
    {
        return (char)consoleKey;
    }
}