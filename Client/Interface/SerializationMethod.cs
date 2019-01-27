namespace Client.Interface
{
    /// <summary>
    ///     Specify the type of serialization used
    /// </summary>
    public enum SerializationMethod
    {
        /// <summary>
        ///     Use standard .NET serialization
        /// </summary>
        DotNet,

        /// <summary>
        ///     Use protocol buffers
        /// </summary>
        ProtocolBuffers
    }
}