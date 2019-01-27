namespace Client.Interface
{
    /// <summary>
    ///     Server config on the client
    /// </summary>
    public class ServerConfig
    {
        public int Port { get; set; } = 4488;

        public string Host { get; set; } = "localhost";
    }
}