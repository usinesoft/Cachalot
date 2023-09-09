using System.Collections.Generic;

namespace AdminConsole.ConsoleUtils;

public class PreviousLineBuffer
{
    private bool _cyclingStarted;

    public bool HasLines => PreviousLines.Count > 0;
    public string LastLine => PreviousLines.Count == 0 ? null : PreviousLines[PreviousLines.Count - 1];
    public string LineAtIndex => PreviousLines.Count == 0 ? null : PreviousLines[Index];

    public int Index { get; set; }

    public List<string> PreviousLines { get; } = new();

    public void AddLine(string line)
    {
        if (!string.IsNullOrEmpty(line))
            PreviousLines.Add(line);
        if (PreviousLines.Count > 0 && PreviousLines[Index] != line)
            Index = PreviousLines.Count - 1;
        _cyclingStarted = false;
    }

    public bool CycleUp()
    {
        if (!HasLines)
            return false;
        if (!_cyclingStarted)
        {
            _cyclingStarted = true;
            return true;
        }

        if (Index > 0)
        {
            Index--;
            return true;
        }

        return false;
    }

    public void CycleUpAndAround()
    {
        if (!HasLines)
            return;
        if (!_cyclingStarted)
        {
            _cyclingStarted = true;
            return;
        }

        Index--;
        if (Index < 0)
            Index = PreviousLines.Count - 1;
    }

    public bool CycleDown()
    {
        if (!HasLines)
            return false;
        if (Index >= PreviousLines.Count - 1)
            return false;
        Index++;
        return true;
    }

    public bool CycleTop()
    {
        if (!HasLines || Index == 0)
            return false;
        Index = 0;
        return true;
    }

    public bool CycleBottom()
    {
        if (!HasLines || Index >= PreviousLines.Count - 1)
            return false;
        Index = PreviousLines.Count - 1;
        return true;
    }
}