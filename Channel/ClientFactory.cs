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
        ///     Create an aggregator client from a multi-server configuration
        /// </summary>
        /// <param name="config"></param>
        /// <returns></returns>
        internal static ICacheClient InitMultiNode(ClientConfig config)
        {
            if (config.Servers.Count < 1)
                throw new CacheException("no server specified in the client configuration");

            var aggregator = new Aggregator();

            foreach (var srv in config.Servers)
            {
                //create the tcp channel and connect to the server
                var channel =
                    new TcpClientChannel(new TcpClientPool(config.ConnectionPoolCapacity, config.PreloadedConnections,
                        srv.Host, srv.Port));

                var client = new CacheClient {Channel = channel};

                //register types
                foreach (var keyValuePair in config.TypeDescriptions)
                {
                    var fullTypeName = keyValuePair.Key;
                    var assemblyName = keyValuePair.Value.AssemblyName;
                    var typeSearchString = fullTypeName + "," + assemblyName;

                    var typeToRegister = Type.GetType(typeSearchString, true, true);
                    client.RegisterTypeIfNeeded(typeToRegister, keyValuePair.Value);
                }


                aggregator.CacheClients.Add(client);
            }


            return aggregator;
        }

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
        ///     Initialize from configuration file
        /// </summary>
        /// <param name="configurationFile"></param>
        /// <returns></returns>
        internal static ICacheClient InitSingleNode(string configurationFile)
        {
            var config = new ClientConfig();
            config.LoadFromFile(configurationFile);

            return InitSingleNode(config);
        }


        internal static ICacheClient InitMultiNode(string configurationFile)
        {
            var config = new ClientConfig();
            config.LoadFromFile(configurationFile);

            return InitMultiNode(config);
        }

        /// <summary>
        ///     Use a configuration file but override the host and the port
        ///     Useful especially for testing as ports neeed to be chosen dynamically by the OS
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