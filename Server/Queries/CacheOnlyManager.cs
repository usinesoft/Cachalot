using System;
using Client.ChannelInterface;
using Client.Interface;
using Client.Messages;

namespace Server.Queries
{
    public class CacheOnlyManager : IRequestManager
    {
        private readonly DataStore _dataStore;

        public CacheOnlyManager(DataStore dataStore)
        {
            _dataStore = dataStore;
        }

        public void ProcessRequest(Request request, IClient client)
        {
            try
            {
                if (request is DomainDeclarationRequest domainDeclaration)
                {
                    DomainDeclaration(domainDeclaration, client);
                }            
                else if (request is EvictionSetupRequest evictionSetup)
                {
                    EvictionSetup(evictionSetup, client);
                }
            }
            catch (Exception e)
            {
                client.SendResponse(new ExceptionResponse(e));
            }
        }

        private void DomainDeclaration(DomainDeclarationRequest domainDeclaration, IClient client)
        {
            if (_dataStore.EvictionPolicy.Type != EvictionType.None)
                throw new NotSupportedException(
                    "Can not make a domain declaration for a type if eviction is active");

            _dataStore.DomainDescription = domainDeclaration.Description;

            client.SendResponse(new NullResponse());
        }

        private void EvictionSetup(EvictionSetupRequest evictionSetup, IClient client)
        {
            if (_dataStore.DomainDescription != null && !_dataStore.DomainDescription.IsEmpty)
                throw new NotSupportedException(
                    "Can not activate eviction on a type with a domain declaration");

            _dataStore.EvictionPolicy = evictionSetup.Type == EvictionType.LessRecentlyUsed
                ? new LruEvictionPolicy(evictionSetup.Limit, evictionSetup.ItemsToEvict)
                : evictionSetup.Type == EvictionType.TimeToLive
                    ? new TtlEvictionPolicy(
                        TimeSpan.FromMilliseconds(evictionSetup.TimeToLiveInMilliseconds))
                    : (EvictionPolicy) new NullEvictionPolicy();

            client.SendResponse(new NullResponse());
        }
    }
}