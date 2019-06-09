namespace AdminConsole.Commands
{
    /// <summary>
    ///     Type of cache command, used by the command line applcations
    /// </summary>
    public enum CommandType
    {
        /// <summary>
        ///     Can not parse the command line
        /// </summary>
        Unknown,

        /// <summary>
        ///     Display the result of the query as XML or STRING
        /// </summary>
        Display,

        /// <summary>
        ///     Select items and deserialize full objects on the client size
        /// </summary>
        Get,

        /// <summary>
        ///     Count the items matching the query
        /// </summary>
        Count,

        /// <summary>
        ///     Get the list of the colums(keys) from a table (data type)
        /// </summary>
        Desc,

        /// <summary>
        ///     Get log information from the server
        /// </summary>
        Log,

        /// <summary>
        ///     Exit the command interpreter
        /// </summary>
        Exit,

        /// <summary>
        ///     Dump output to a file
        /// </summary>
        Dump,

        Help,

        Delete,

        Truncate,

        Select,

        Connect,
        Restore,
        Recreate,
        Stop,
        ReadOnly,
        ReadWrite,
        Drop,
        Import,
        Search
    }
}