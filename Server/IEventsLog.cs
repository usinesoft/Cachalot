namespace Server
{
    public interface IEventsLog
    {
        void LogEvent(EventType eventType, string detail = null, int milliseconds = 0);
    }
}