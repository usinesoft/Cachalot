#region

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Client.ChannelInterface;
using Client.Core;
using Newtonsoft.Json.Linq;

#endregion

namespace Channel;

/// <summary>
///     In process channel (for tests only)
/// </summary>
public class InProcessChannel : IClientChannel, IServerChannel
{
    #region IDisposable Members

    public void Dispose()
    {
        //nothing to dispose
    }

    #endregion


    public void ReserveConnection(Guid sessionId)
    {
        // nothing to do in this case
    }

    public void ReleaseConnection(Guid sessionId)
    {
        // nothing to do in this case
    }

    private class ClientData : IClient
    {
        private readonly ManualResetEvent _dataReceived;
        private readonly MemoryStream _stream;

        public ClientData()
        {
            _dataReceived = new(false);

            _stream = new();
        }

        public void SendResponse(Response response)
        {
            _stream.SetLength(0);
            Streamer.ToStream(_stream, response);
            _stream.Seek(0, SeekOrigin.Begin);
            _dataReceived.Set();
        }

        public void SendMany(ICollection<PackedObject> items, int[] selectedIndexes, string[] aliases)
        {
            Streamer.ToStreamMany(_stream, items, selectedIndexes, aliases);
            _stream.Seek(0, SeekOrigin.Begin);
            _dataReceived.Set();
        }

        public void SendMany(ICollection<JObject> items)
        {
            Streamer.ToStreamMany(_stream, items);
            _stream.Seek(0, SeekOrigin.Begin);
            _dataReceived.Set();
        }


        public bool? ShouldContinue()
        {
            throw new NotImplementedException("Should never be called for this class");
        }


        #region methods called on the client side

        public Stream WaitForData()
        {
            _dataReceived.WaitOne();
            return _stream;
        }

        #endregion
    }

    #region IServerChannel Members

    public long Connections => 1;


    public event EventHandler<RequestEventArgs> RequestReceived;

    #endregion


    #region IClientChannel Members

    public Response SendRequest(Request request)
    {
        var rcvRequest = request;

        Response response = null;


        if (RequestReceived != null)
        {
            var client = new ClientData();
            RequestReceived(this, new(rcvRequest, client));
            var stream = client.WaitForData();

            response = Streamer.FromStream<Response>(stream);
        }

        if (response == null)
            return new NullResponse();

        return response;
    }


    public IEnumerable<RankedItem> SendStreamRequest(Request request)
    {
        if (RequestReceived != null)
        {
            var client = new ClientData();
            RequestReceived(this, new(request, client));
            var stream = client.WaitForData();
            return Streamer.EnumerableFromStream(stream);
        }

        // otherwise return empty collection
        return new List<RankedItem>();
    }

    public IEnumerable<RankedItem2> SendStreamRequest2(Request request)
    {
        if (RequestReceived != null)
        {
            var client = new ClientData();
            RequestReceived(this, new(request, client));
            var stream = client.WaitForData();
            return Streamer.EnumerableFromStream2(stream);
        }

        // otherwise return empty collection
        return new List<RankedItem2>();
    }


    public Session BeginSession()
    {
        throw new NotImplementedException("Should never be called for this class");
    }

    public void EndSession(Session session)
    {
        throw new NotImplementedException("Should never be called for this class");
    }

    public void PushRequest(Session session, Request request)
    {
        throw new NotImplementedException("Should never be called for this class");
    }


    public Response GetResponse(Session session)
    {
        throw new NotImplementedException("Should never be called for thi class");
    }


    public bool Continue(Session session, bool ok)
    {
        throw new NotImplementedException("Should never be called for this class");
    }

    #endregion
}