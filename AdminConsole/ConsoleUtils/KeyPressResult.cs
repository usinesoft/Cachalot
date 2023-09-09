using System;

namespace AdminConsole.ConsoleUtils;

public class KeyPressResult
{
    public KeyPressResult(ConsoleKeyInfo consoleKeyInfo, LineState lineBeforeKeyPress, LineState lineAfterKeyPress)
    {
        ConsoleKeyInfo = consoleKeyInfo;
        LineBeforeKeyPress = lineBeforeKeyPress;
        LineAfterKeyPress = lineAfterKeyPress;
    }

    public ConsoleKeyInfo ConsoleKeyInfo { get; }
    public ConsoleKey Key => ConsoleKeyInfo.Key;
    public char KeyChar => ConsoleKeyInfo.KeyChar;
    public ConsoleModifiers Modifiers => ConsoleKeyInfo.Modifiers;
    public LineState LineBeforeKeyPress { get; }
    public LineState LineAfterKeyPress { get; }
}