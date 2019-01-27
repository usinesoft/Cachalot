using Client.Core;

namespace Client.Messages
{
    /// <summary>
    ///     When a domain is declared as complete in the cache, the new domain definition may be added to the
    ///     existent one, subtracted from it or it may completely replace the current definition.
    ///     <seealso cref="DomainDescription" />
    /// </summary>
    public enum DomainDeclarationAction
    {
        /// <summary>
        ///     Value added to the current domain definition
        /// </summary>
        Add,

        /// <summary>
        ///     Value subtracted from the current domain definition
        /// </summary>
        Remove,

        /// <summary>
        ///     The new value replaces the current definition
        /// </summary>
        Set
    }
}