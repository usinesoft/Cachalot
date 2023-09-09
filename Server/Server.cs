using System;
using System.IO;
using System.Threading.Tasks;
using Client;
using Client.ChannelInterface;
using Client.Core;
using Client.Messages;
using Client.Tools;
using ProtoBuf.Meta;
using Server.Persistence;
using Constants = Server.Persistence.Constants;

namespace Server;

public class Server
{
    private readonly NodeConfig _config;

    private readonly Services _serviceContainer;


    private IServerChannel _channel;

    private DataContainer _dataContainer;
    private PersistenceEngine _persistenceEngine;


    private DateTime _startTime;

    static Server()
    {
        // initialize protobuf metadata
        RuntimeTypeModel.Default.CompileInPlace();
    }

    public Server(NodeConfig config, ILog log = null)
    {
        _serviceContainer = new(log, config);
        _dataContainer = new(_serviceContainer, config);

        _config = config;
    }


    private ServerMode Mode { get; set; }

    public IServerChannel Channel
    {
        private get => _channel;
        set
        {
            if (_channel != null)
                _channel.RequestReceived -= HandleRequestReceived;

            _channel = value;

            _channel.RequestReceived += HandleRequestReceived;
        }
    }

    /// <summary>
    ///     This call comes from different threads
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void HandleRequestReceived(object sender, RequestEventArgs e)
    {
        Dbg.Trace("request received ");

        switch (e.Request)
        {
            case ImportDumpRequest importRequest:
                ManageImportRequest(importRequest, e.Client);
                break;
            case StopRequest:
                ServerLog.LogWarning("stop request received");
                OnStopRequired();
                break;
            case SwitchModeRequest switchModeRequest:
                Mode = switchModeRequest.NewMode == 1 ? ServerMode.ReadOnly : ServerMode.Normal;
                _dataContainer.IsReadOnly = Mode == ServerMode.ReadOnly;
                e.Client.SendResponse(new NullResponse());
                break;
            case DropRequest:
                try
                {
                    ServerLog.LogWarning("drop request received");
                    ManageDropRequest();
                    ServerLog.LogWarning("all data was deleted");
                    e.Client.SendResponse(new NullResponse());
                }
                catch (Exception exception)
                {
                    e.Client.SendResponse(new ExceptionResponse(exception));
                }

                break;
            default:
            {
                if (Mode == ServerMode.DuringDumpImport)
                {
                    e.Client.SendResponse(new ExceptionResponse(
                        new NotSupportedException("Database is not available while restoring data from dump")));
                    return;
                }

                if (Mode == ServerMode.ReadOnly && e.Request is DataRequest { AccessType: DataAccessType.Write })
                {
                    e.Client.SendResponse(new ExceptionResponse(
                        new NotSupportedException("Database is in read-only mode")));
                    return;
                }

                //as the data container does not have access to the channel
                //let it know how many connections are active 
                var activeConnections = Channel.Connections;
                _dataContainer.ActiveConnections = activeConnections;
                _dataContainer.StartTime = _startTime;


                _dataContainer.DispatchRequest(e.Request, e.Client);
                break;
            }
        }
    }


    private void ManageDropRequest()
    {
        Mode = ServerMode.ReadOnly;
        _persistenceEngine?.Stop();

        // delete persistent data
        _persistenceEngine?.StoreDataForRollback();

        KeyValuePool.Reset();

        // delete data from memory
        _dataContainer = new(_serviceContainer, _config);

        KeyValuePool.Reset();

        GC.Collect();

        if (_persistenceEngine != null) _persistenceEngine.Container = _dataContainer;


        _dataContainer.PersistenceEngine = _persistenceEngine;

        _persistenceEngine?.LightStart(true);


        _persistenceEngine?.DeleteRollbackData();
        Mode = ServerMode.Normal;
    }

    private void ManageImportRequest(ImportDumpRequest importRequest, IClient client)
    {
        try
        {
            ServerLog.LogInfo($"begin import dump stage {importRequest.Stage} on shard {importRequest.ShardIndex}");

            switch (importRequest.Stage)
            {
                case 0:

                    Mode = ServerMode.DuringDumpImport;
                    _persistenceEngine?.Stop();

                    _persistenceEngine?.StoreDataForRollback();
                    break;

                case 1:

                    using (var storage = new ReliableStorage(new NullObjectProcessor(), _config.DataPath))
                    {
                        // first copy the schema and reinitialize data stores
                        var path = DumpHelper.NormalizeDumpPath(importRequest.Path);

                        var dataPath = _config.DataPath != null
                            ? Path.Combine(_config.DataPath, Constants.DataPath)
                            : Constants.DataPath;
                        File.Copy(Path.Combine(path, Constants.SchemaFileName),
                            Path.Combine(dataPath, Constants.SchemaFileName), true);

                        _dataContainer = new(_serviceContainer, _config);

                        if (_persistenceEngine != null)
                        {
                            _persistenceEngine.Container = _dataContainer;
                            _dataContainer.PersistenceEngine = _persistenceEngine;
                        }

                        // reinitialize data container                          
                        _dataContainer.LoadSchema(Path.Combine(dataPath, Constants.SchemaFileName));


                        // schema needs to be updated because the dump contains a single instance (the one for shard 0) so the 
                        // ShardIndex may be incorrect
                        _dataContainer.ShardIndex = importRequest.ShardIndex;
                        var schema = _dataContainer.GenerateSchema();
                        _serviceContainer.SchemaPersistence.SaveSchema(schema);

                        // fill the in-memory data stores
                        Parallel.ForEach(_dataContainer.Stores(),
                            store => store.LoadFromDump(path, importRequest.ShardIndex));

                        if (_persistenceEngine != null)
                            // write to the persistent storage (this is the only case where we write directly in the storage, not in the transaction log)
                            foreach (var dataStore in _dataContainer.Stores())
                            foreach (var item in dataStore.DataByPrimaryKey)
                            {
                                var itemData =
                                    SerializationHelper.ObjectToBytes(item.Value, SerializationMode.ProtocolBuffers,
                                        dataStore.CollectionSchema.StorageLayout == Layout.Compressed);

                                storage.StoreBlock(itemData, item.Value.GlobalKey, 0);
                            }

                        // import the sequences

                        var sequenceFile = $"sequence_{importRequest.ShardIndex:D3}.json";

                        _dataContainer.LoadSequence(Path.Combine(path, sequenceFile));
                        File.Copy(Path.Combine(path, sequenceFile),
                            Path.Combine(dataPath, Constants.SequenceFileName), true);
                    }

                    break;
                case 2: // all good
                    _persistenceEngine?.LightStart();


                    _persistenceEngine?.DeleteRollbackData();
                    Mode = ServerMode.Normal;
                    break;

                case 3: // something bad happened. Rollback
                    _persistenceEngine?.RollbackData();

                    _dataContainer = new(_serviceContainer, _config);

                    if (_persistenceEngine != null)
                    {
                        _persistenceEngine.Container = _dataContainer;
                        _dataContainer.PersistenceEngine = _persistenceEngine;
                    }

                    _persistenceEngine?.Start();

                    Mode = ServerMode.Normal;


                    break;

                default:
                    client.SendResponse(new ExceptionResponse(
                        new NotSupportedException(
                            $"Illegal value {importRequest.Stage} for parameter stage in import request")));
                    return;
            }

            ServerLog.LogInfo(
                $"end successful import dump stage {importRequest.Stage} on shard {importRequest.ShardIndex}");
            client.SendResponse(new NullResponse());
        }
        catch (AggregateException e)
        {
            ServerLog.LogError(
                $"end  import dump stage {importRequest.Stage} on shard {importRequest.ShardIndex} with exception {e.Message}");
            client.SendResponse(new ExceptionResponse(e.InnerException));
        }
        catch (Exception e)
        {
            client.SendResponse(new ExceptionResponse(e));
        }
    }

    public event EventHandler<EventArgs> StopRequired;

    public void Start()
    {
        Dbg.Trace("starting server");


        _startTime = DateTime.Now;


        if (_config.IsPersistent)
        {
            Dbg.Trace("starting persistence engine");

            _persistenceEngine = new(_dataContainer, _config.DataPath, _serviceContainer);
            _dataContainer.PersistenceEngine = _persistenceEngine;

            _persistenceEngine.Start();
        }
    }

    public void Stop()
    {
        Dbg.Trace("begin persistence engine stop ");
        _persistenceEngine?.Stop();
        Dbg.Trace("end persistence engine stop ");

        Dbg.Trace("SERVER STOPPED");
    }

    private void OnStopRequired()
    {
        StopRequired?.Invoke(this, EventArgs.Empty);
    }

    private enum ServerMode
    {
        Normal,
        DuringDumpImport,
        ReadOnly
    }
}