using Client.ChannelInterface;

namespace Server
{
    internal class TaskInput
    {
        public TaskInput(IClient client, Request request)
        {
            Client = client;
            Request = request;
        }

        public IClient Client { get; }

        public Request Request { get; }
    }
}