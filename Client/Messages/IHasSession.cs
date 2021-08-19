using System;

namespace Client.Messages
{
    public interface IHasSession
    {
        Guid SessionId { get; }
    }
}