using System;

namespace AdminConsole.ConsoleUtils
{
    public class LineState
    {
        public LineState(string line, int cursorPosition)
        {
            Line = line;
            if (line == null)
                return;
            CursorPosition = Math.Min(line.Length, Math.Max(0, cursorPosition));
            LineBeforeCursor = line.Substring(0, CursorPosition);
            LineAfterCursor = line.Substring(CursorPosition);
        }

        public string Line { get; }
        public int CursorPosition { get; }
        public string LineBeforeCursor { get; }
        public string LineAfterCursor { get; }
    }
}