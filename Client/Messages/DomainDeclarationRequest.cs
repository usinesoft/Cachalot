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
        ///     For serialization only
        /// </summary>
        public DomainDeclarationRequest() : base(DataAccessType.Write, string.Empty)
        {
        }

        /// <summary>
        ///     Create a new request for the specified type. The domain description will be empty
        /// </summary>
        public DomainDeclarationRequest(DomainDescription description)
            : base(DataAccessType.Write, description.DescriptionAsQuery.CollectionName)
        {
            Description = description;
        }

        [field: ProtoMember(1)] public DomainDescription Description { get; }

        public override string FullTypeName => Description.DescriptionAsQuery.CollectionName;
    }
}