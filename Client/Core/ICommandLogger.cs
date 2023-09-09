namespace Client.Core;

public interface ICommandLogger
{
    void Write(string message);
    void Write(string format, params object[] parameters);
    void WriteError(string message);
    void WriteError(string format, params object[] parameters);
}