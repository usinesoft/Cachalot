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
      
        [ProtoMember(1)] private readonly DomainDescription _description;

        /// <summary>
        ///     For serialization only
        /// </summary>
        public DomainDeclarationRequest() : base(DataAccessType.Write, string.Empty)
        {
        }

        /// <summary>
        ///     Create a new request for the specified type. The domain description will be empty
        /// </summary>
        public DomainDeclarationRequest(DomainDescription description)
            : base(DataAccessType.Write, description.DescriptionAsQuery.TypeName)
        {            
            _description = description;
        }

        public DomainDescription Description => _description;

        public override string FullTypeName => Description.DescriptionAsQuery.TypeName;
    }
}