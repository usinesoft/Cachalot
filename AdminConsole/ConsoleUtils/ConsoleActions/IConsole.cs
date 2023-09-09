namespace AdminConsole.ConsoleUtils.ConsoleActions;

public interface IConsole
{
    PreviousLineBuffer PreviousLineBuffer { get; }
    string CurrentLine { get; set; }
    int CursorPosition { get; set; }
}