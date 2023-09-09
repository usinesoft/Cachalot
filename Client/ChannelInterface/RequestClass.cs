namespace Client.ChannelInterface;

/// <summary>
///     Type of request
/// </summary>
public enum RequestClass
{
    /// <summary>
    ///     All kind of access to data in the cache ( read or write)
    ///     This type of request is always related to a data type
    /// </summary>
    DataAccess,

    /// <summary>
    ///     Administrative request
    /// </summary>
    Admin,

    /// <summary>
    ///     Different from data types as unique id generators are not necessarily linked to a type in the cache
    /// </summary>
    UniqueIdGeneration,

    /// <summary>
    ///     Continue two stage transaction
    /// </summary>
    Continue
}