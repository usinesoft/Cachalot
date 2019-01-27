using System;
using Client.ChannelInterface;
using Client.Core;
using ProtoBuf;

namespace Client.Messages
{
    /// <summary>
    ///     Base class for the Get/Put/Remove requests. These request need to declare an <see cref="AccessType" />
    ///     and are atached to an unique data type used for serialization
    /// </summary>
    [ProtoContract]
    [ProtoInclude(604, typeof(DomainDeclarationRequest))]
    [ProtoInclude(605, typeof(EvalRequest))]
    [ProtoInclude(606, typeof(GetAvailableRequest))]
    [ProtoInclude(607, typeof(GetDescriptionRequest))]
    [ProtoInclude(608, typeof(GetRequest))]
    [ProtoInclude(609, typeof(PutRequest))]
    [ProtoInclude(610, typeof(RemoveManyRequest))]
    [ProtoInclude(611, typeof(RemoveRequest))]
    public abstract class DataRequest : Request
    {
        [ProtoMember(1)] private readonly DataAccessType _accessType;
        [ProtoMember(2)] private readonly string _fullTypeName;

        /// <summary>
        ///     Create an abstract data request (allways attached to a data type and has a <see cref="DataAccessType" />)
        /// </summary>
        /// <param name="accessType">read-only or read-write access</param>
        /// <param name="fullTypeName"></param>
        protected DataRequest(DataAccessType accessType, string fullTypeName)
        {
            _accessType = accessType;
            _fullTypeName = fullTypeName;
        }

        public override RequestClass RequestClass => RequestClass.DataAccess;

        /// <summary>
        ///     read-only or read-write
        /// </summary>
        public DataAccessType AccessType => _accessType;

        /// <summary>
        ///     Full name as specified in the class <see cref="Type" />
        /// </summary>
        public string FullTypeName => _fullTypeName;
    }


    ///// <summary>
    ///// Response to a GetOne() request
    ///// </summary>
    //[ProtoContract]
    //public class GetOneResponse : Response
    //{
    //    [ProtoMember(1)] private readonly object _data;

    //    public override ResponseType ResponseType
    //    {
    //        get { return ResponseType.Data; }
    //    }

    //    public object Data
    //    {
    //        get { return _data; }
    //    }


    //    public GetOneResponse(object data)
    //    {
    //        if (data == null) throw new ArgumentNullException("data");
    //        _data = data;
    //    }


    //    public override string ToString()
    //    {
    //        return Data.ToString();
    //    }
    //}
}