using Client.Messages;
using System.Collections.Generic;

namespace Client.Interface
{
    public class ServerLog
    {
        public ServerLog(LogResponse[] logs)
        {
            var index = 0;
            Entries = new List<string>();
            foreach (var log in logs)
            {
                foreach (var logEntry in log.Entries) Entries.Add($"{index:D4} {logEntry}");

                index++;
            }
        }

        public ServerLog(ServerLog[] logs)
        {
            var index = 0;
            Entries = new List<string>();
            foreach (var log in logs)
            {
                foreach (var logEntry in log.Entries) Entries.Add($"{index:D4} {logEntry}");

                index++;
            }
        }

        public List<string> Entries { get; }
    }
}