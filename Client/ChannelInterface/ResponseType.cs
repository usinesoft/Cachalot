namespace Client.ChannelInterface;

/// <summary>
///     Generic class of the response
/// </summary>
public enum ResponseType
{
    /// <summary>
    ///     Contains useful data
    /// </summary>
    Data,

    /// <summary>
    ///     An exception has occured
    /// </summary>
    Exception,

    /// <summary>
    ///     Void response
    /// </summary>
    Null,

    /// <summary>
    ///     Ready co commit (for two stage transactions)
    /// </summary>
    Ready
}