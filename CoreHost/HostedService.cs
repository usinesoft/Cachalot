using System;
using System.IO;
using System.Linq;
using System.Threading;
using Channel;
using Client.Core;
using Host;
using Newtonsoft.Json;
using Server;
using Server.Persistence;

namespace CoreHost
{
    public class HostedService
    {
        private readonly ManualResetEvent _stopEvent;
        private Server.Server _cacheServer;
        private TcpServerChannel _listener;
        private ILog Log { get; }

        public HostedService(ILog log, ManualResetEvent stopEvent)
        {
            _stopEvent = stopEvent;
            Log = log;

            ServerLog.ExternalLog = log;
        }


        public bool Start(string instance)
        {
            
            try
            {

                string configFile = Constants.NodeConfigFileName;

                if (instance != null)
                {
                    var baseName = configFile.Split('.').FirstOrDefault();
                    configFile = $"{baseName}_{instance}.json";
                }

                var port = Constants.DefaultPort;
                var persistent = true;
                string dataPath = null;

                if (File.Exists(configFile))
                    try
                    {
                        var nodeConfig = SerializationHelper.FormattedSerializer.Deserialize<NodeConfig>(
                            new JsonTextReader(new StringReader(File.ReadAllText(configFile))));
                        port = nodeConfig.TcpPort;
                        persistent = nodeConfig.IsPersistent;
                        dataPath = nodeConfig.DataPath;

                        HostServices.HostServices.Start(dataPath);

                        Log.LogInfo("----------------------------------------------------------");
                        Log.LogInfo($"Reading configuration file {configFile} ");

                    }
                    catch (Exception e)
                    {
                        Log.LogError($"Error reading configuration file {configFile} : {e.Message}");
                    }
                else
                {
                    HostServices.HostServices.Start(dataPath);
                    Log.LogWarning($"Configuration file {configFile} not found. Using defaults");
                }
                    

                _cacheServer = new Server.Server(new ServerConfig(), persistent, dataPath);

                _listener = new TcpServerChannel();
                _cacheServer.Channel = _listener;
                _listener.Init(port);
                _listener.Start();

                var fullDataPath =  Path.GetFullPath(dataPath ?? Constants.DataPath);

                var persistentDescription = persistent ? fullDataPath: " NO";

                Console.Title = $"Cachalot Core on port {port} persistent = {persistentDescription}";

                Log.LogInfo($"Starting hosted service on port {port} persistent = {persistentDescription}" );

                _cacheServer.StopRequired += (sender, args) =>
                {
                    HostServices.HostServices.Stop();
                    Stop();

                    _stopEvent.Set();
                };
                _cacheServer.Start();
            }
            catch (Exception e)
            {
                Log.LogError($"Failed to start host: {e}");

                return false;
            }

            Log.LogInfo("Host started successfully");

            return true;
        }

        public bool Stop()
        {
            Log.LogInfo("Stopping service");

            _listener.Stop();

            _cacheServer.Stop();

            Log.LogInfo("Service stopped successfully");

            // stop this after last log
            HostServices.HostServices.Stop();


            return true;
        }
    }
}