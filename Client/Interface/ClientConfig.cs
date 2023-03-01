#region

using Client.Core;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Xml;

#endregion

namespace Client.Interface
{
    /// <summary>
    ///     Cache client configuration
    /// </summary>
    public class ClientConfig
    {
        private readonly List<ServerConfig> _servers = new List<ServerConfig>();

        public ClientConfig()
        {
            PreloadedConnections = 1;
            ConnectionPoolCapacity = 3;
        }

        public IList<ServerConfig> Servers => _servers;


        public int ConnectionPoolCapacity { get; set; }

        public int PreloadedConnections { get; set; }

        public bool IsPersistent { get; set; } = true;

        /// <summary>
        ///     Load from external XML file
        /// </summary>
        /// <param name="fileName"> </param>
        public void LoadFromFile(string fileName)
        {
            var doc = new XmlDocument();
            doc.Load(fileName);

            LoadFromElement(doc.DocumentElement);
        }

        /// <summary>
        /// Read from a connection string in the form host1:port1+host2:port2;max_connections_in_pool, preloaded_connections_in_pool
        /// </summary>
        /// <param name="connectionString"></param>
        public ClientConfig(string connectionString)
        {

            // if none is specified create an empty ClientConfig that is used to instantiate an in-process server
            if (string.IsNullOrWhiteSpace(connectionString))
                return;

            // the part after ; contains the connection pool parameters
            if (connectionString.Contains(';'))
            {
                var parts = connectionString.Split(';');
                connectionString = parts[0];

                if (parts.Length > 0)
                {
                    var pool = parts[1].Split(',', StringSplitOptions.RemoveEmptyEntries);
                    ConnectionPoolCapacity = int.Parse(pool[0]);
                    if (pool.Length > 1)
                    {
                        PreloadedConnections = int.Parse(pool[1]);
                    }
                }
            }
            else
            {
                ConnectionPoolCapacity = Constants.DefaultPoolCapacity;
                PreloadedConnections = Constants.DefaultPreloadedConnections;
            }

            var servers = connectionString.Split('+');

            foreach (var server in servers)
            {
                if (!server.Contains(":"))
                {
                    throw new FormatException("A server should be specified as hostname:port");

                }

                var parts = server.Split(':').Select(p => p.Trim()).ToList();

                var host = parts[0].Trim();
                var port = int.Parse(parts[1].Trim());

                Servers.Add(new ServerConfig { Host = host, Port = port });
            }

            // sort the servers by hostname then by port; shards should not depend of the order in the connection string
            if (Servers.Count > 1)
            {
                var orderedServers =  Servers.OrderBy(s => s.Host).ThenBy(s => s.Port).ToList();

                // check for duplicates
                for (int i = 0; i < orderedServers.Count-1; i++)
                {
                    if (orderedServers[i].Host == orderedServers[i + 1].Host &&
                        orderedServers[i].Port == orderedServers[i + 1].Port)
                    {
                        throw new CacheException($"Duplicate host:port in the connection string {orderedServers[i].Host}:{orderedServers[i].Port}");
                    }
                }


                Servers.Clear();
                foreach (var server in orderedServers)
                {
                    Servers.Add(server);
                }
            }
        }


        /// <summary>
        ///     Interpret a string as a boolean value (accept true/t/yes/y/1 with all casing variants)
        /// </summary>
        /// <param name="value"> </param>
        private static bool IsYes(string value)
        {
            if (string.IsNullOrEmpty(value))
                return false;

            value = value.Trim();
            if (string.IsNullOrEmpty(value))
                return false;

            value = value.ToLower(CultureInfo.InvariantCulture);

            var firstChar = value[0];

            return firstChar is 't' or 'y' or '1';
        }


        /// <summary>
        ///     Initialize from <see cref="XmlElement" />. Can be used to embed cache configuration in larger configuration files
        /// </summary>
        /// <param name="doc"> </param>
        private void LoadFromElement(XmlElement doc)
        {
            var persistent = StringFromXpath(doc, "@isPersistent");
            IsPersistent = IsYes(persistent);

            var nodeList = doc.SelectNodes("//connectionPool");

            if (nodeList != null)
                foreach (XmlNode node in nodeList)
                {
                    var capacity = StringFromXpath(node, "capacity");
                    ConnectionPoolCapacity = int.Parse(capacity);

                    var preloaded = StringFromXpath(node, "preloaded");
                    PreloadedConnections = int.Parse(preloaded);
                }


            //read servers
            nodeList = doc.SelectNodes("//servers/server");
            if (nodeList != null)
                foreach (XmlNode node in nodeList)
                {
                    var cfg = new ServerConfig();

                    var port = StringFromXpath(node, "port");
                    cfg.Port = int.Parse(port);

                    var host = StringFromXpath(node, "host");
                    cfg.Host = host;


                    Servers.Add(cfg);
                }


        }

        private static string StringFromXpath(XmlNode element, string xpath)
        {
            var node = element.SelectSingleNode(xpath);
            if (node != null) return node.InnerText;
            return string.Empty;
        }
    }
}