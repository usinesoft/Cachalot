﻿using System;
using AdminConsole.ConsoleUtils.ConsoleActions;

namespace AdminConsole.ConsoleUtils;

internal class ConsoleExtInstance : IConsole
{
    public PreviousLineBuffer PreviousLineBuffer => ConsoleExt.PreviousLineBuffer;

    public string CurrentLine
    {
        get => ConsoleExt.CurrentLine;
        set => ConsoleExt.CurrentLine = value;
    }

    public int CursorPosition
    {
        get => Console.CursorLeft;
        set => Console.CursorLeft = value;
    }
}