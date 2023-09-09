using Client.ChannelInterface;

namespace Server.Queries;

public interface IRequestManager
{
    void ProcessRequest(Request request, IClient client);
}