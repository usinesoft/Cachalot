﻿using System;
using System.IO;
using System.Linq;
using System.Threading;
using Channel;
using Client.Core;
using Newtonsoft.Json;
using Constants = Server.Persistence.Constants;

namespace Server;

public class HostedService
{
    private readonly ManualResetEvent _stopEvent;
    private Server _cacheServer;
    private TcpServerChannel _listener;

    public HostedService(ILog log, ManualResetEvent stopEvent)
    {
        _stopEvent = stopEvent;
        Log = log;

        ServerLog.ExternalLog = log;
    }

    private ILog Log { get; }


    public bool Start(string instance)
    {
        try
        {
            var configFile = Constants.NodeConfigFileName;

            if (instance != null)
            {
                var baseName = configFile.Split('.').FirstOrDefault();
                configFile = $"{baseName}_{instance}.json";
            }


            var nodeConfig = new NodeConfig { TcpPort = Client.Core.Constants.DefaultPort, IsPersistent = true };

            if (File.Exists(configFile))
            {
                try
                {
                    var configFromFile = SerializationHelper.FormattedSerializer.Deserialize<NodeConfig>(
                        new JsonTextReader(new StringReader(File.ReadAllText(configFile))));

                    nodeConfig = configFromFile;

                    HostServices.HostServices.Start(configFromFile.DataPath);

                    Log.LogInfo("----------------------------------------------------------");
                    Log.LogInfo($"Reading configuration file {configFile} ");
                }
                catch (Exception e)
                {
                    Log.LogError($"Error reading configuration file {configFile} : {e.Message}");
                }
            }
            else
            {
                HostServices.HostServices.Start(nodeConfig.DataPath);
                Log.LogWarning($"Configuration file {configFile} not found. Using defaults");
            }


            _cacheServer = new(nodeConfig, HostServices.HostServices.Log);

            _listener = new();
            _cacheServer.Channel = _listener;
            _listener.Init(nodeConfig.TcpPort);


            var fullDataPath = Path.GetFullPath(nodeConfig.DataPath ?? Constants.DataPath);

            var persistentDescription = nodeConfig.IsPersistent ? fullDataPath : " NO";

            try
            {
                Console.Title = $"Cachalot Core on port {nodeConfig.TcpPort} persistent = {persistentDescription}";
            }
            catch (Exception)
            {
                //ignore this may throw an exception when run in service mode
            }

            Log.LogInfo(
                $"Starting hosted service on port {nodeConfig.TcpPort} persistent = {persistentDescription}");

            _cacheServer.StopRequired += (sender, args) =>
            {
                Stop();

                _stopEvent.Set();
            };
            _cacheServer.Start();
            // this is where we effectively start accepting requests
            _listener.Start();
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