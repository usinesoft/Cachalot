using Client.Core;
using ProtoBuf;

namespace Client.Messages
{
    /// <summary>
    ///     Declare a domain as complete. Used by the GetMany operations
    /// </summary>
    [ProtoContract]
    public class DomainDeclarationRequest : DataRequest
    {
        /// <summary>
        ///     Add to, remove from or set the current domain description
        /// </summary>
        [ProtoMember(1)] private readonly DomainDeclarationAction _action = DomainDeclarationAction.Add;

        [ProtoMember(2)] private DomainDescription _description;

        /// <summary>
        ///     For serialization only
        /// </summary>
        public DomainDeclarationRequest() : base(DataAccessType.Write, string.Empty)
        {
        }

        /// <summary>
        ///     Create a new request for the specified type. The domain description will be empty
        /// </summary>
        public DomainDeclarationRequest(DomainDescription description, DomainDeclarationAction action)
            : base(DataAccessType.Write, description.FullTypeName)
        {
            _action = action;
            _description = description;
        }

        public DomainDescription Description
        {
            get => _description;
            set => _description = value;
        }

        /// <summary>
        ///     Add to, remove from or set the curret domain description
        /// </summary>
        public DomainDeclarationAction Action => _action;
    }
}