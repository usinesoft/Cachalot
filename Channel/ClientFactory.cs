using System;
using Client.Core;
using Client.Interface;

namespace Channel
{
    /// <summary>
    ///     Provide a functional client interface to the end user
    /// </summary>
    public static class ClientFactory
    {
        
        /// <summary>
        ///     Initialize from data structure
        /// </summary>
        /// <param name="config"></param>
        /// <returns></returns>
        internal static ICacheClient InitSingleNode(ClientConfig config)
        {
            var client = new CacheClient();


            if (config.Servers.Count < 1)
                throw new CacheException("no server specified in the client configuration");

            var srv = config.Servers[0];


            //create the tcp channel and connect to the server
            var channel = new TcpClientChannel(new TcpClientPool(config.ConnectionPoolCapacity,
                config.PreloadedConnections, srv.Host, srv.Port));

            client.Channel = channel;

            //register types
            foreach (var keyValuePair in config.TypeDescriptions)
            {
                var fullTypeName = keyValuePair.Key;
                var assemblyName = keyValuePair.Value.AssemblyName;
                var typeSearchString = fullTypeName + "," + assemblyName;

                var typeToRegister = Type.GetType(typeSearchString, true, true);
                client.RegisterTypeIfNeeded(typeToRegister, keyValuePair.Value);
            }


            return client;
        }


        
        /// <summary>
        ///     Use a configuration file but override the host and the port
        ///     Useful especially for testing as ports need to be chosen dynamically by the OS
        ///     This is meant to be used only for single-server configurations
        /// </summary>
        /// <param name="configurationFile"></param>
        /// <param name="serverHost"></param>
        /// <param name="serverPort"></param>
        /// <returns></returns>
        internal static ICacheClient InitSingleNode(string configurationFile, string serverHost, int serverPort)
        {
            var config = new ClientConfig();
            config.LoadFromFile(configurationFile);
            config.Servers[0].Host = serverHost;
            config.Servers[0].Port = serverPort;

            return InitSingleNode(config);
        }
    }
}