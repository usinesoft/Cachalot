using System;
using ProtoBuf;

namespace Client.Messages;

/// <summary>
///     Information about the server process
/// </summary>
[ProtoContract]
[Serializable]
public class ServerInfo
{
    /// <summary>
    ///     32 or 64 bits server process
    /// </summary>
    [ProtoMember(1)] private int _bits;

    [ProtoMember(14)] private string _clusterName;

    /// <summary>
    ///     Number of connected clients
    /// </summary>
    [ProtoMember(2)] private int _connectedClients;

    /// <summary>
    ///     Name of the machine on which the server is executed
    /// </summary>
    [ProtoMember(3)] private string _host;

    [ProtoMember(11)] private bool _isPersistent;

    [ProtoMember(13)] private bool _isReadOnly;

    [ProtoMember(12)] private int _memoryLimitInGigabytes;

    [ProtoMember(15)] private long _nonFragmentedMemory;

    /// <summary>
    ///     Main TCP port
    /// </summary>
    [ProtoMember(4)] private int _port;

    [ProtoMember(16)] private int _runningThreads;

    [ProtoMember(5)] private string _softwareVersion;


    /// <summary>
    ///     Time when the server process was started
    /// </summary>
    [ProtoMember(6)] private DateTime _startTime;

    /// <summary>
    ///     Number of threads
    /// </summary>
    [ProtoMember(7)] private int _threads;


    /// <summary>
    ///     Transactions present in the transaction log but not yet applied to the persistent storage
    /// </summary>
    [ProtoMember(10)] private int _transactionLag;

    /// <summary>
    ///     Virtual memory (physical + swap) allocated for the server process
    /// </summary>
    [ProtoMember(8)] private long _virtualMemory;

    [ProtoMember(17)] private int _waitingThreads;

    /// <summary>
    ///     Physical memory allocated for the server process
    /// </summary>
    [ProtoMember(9)] private long _workingSet;

    /// <summary>
    ///     32 or 64 bits server process
    /// </summary>
    public int Bits
    {
        get => _bits;
        set => _bits = value;
    }

    /// <summary>
    ///     Number of connected clients
    /// </summary>
    public int ConnectedClients
    {
        get => _connectedClients;
        set => _connectedClients = value;
    }

    /// <summary>
    ///     Name of the machine on which the server is executed
    /// </summary>
    public string Host
    {
        get => _host;
        set => _host = value;
    }

    /// <summary>
    ///     Main TCP port
    /// </summary>
    public int Port
    {
        get => _port;
        set => _port = value;
    }

    /// <summary>
    ///     Physical memory allocated for the server process
    /// </summary>
    public long WorkingSet
    {
        get => _workingSet;
        set => _workingSet = value;
    }

    /// <summary>
    ///     Virtual memory (physical + swap) allocated for the server process
    /// </summary>
    public long VirtualMemory
    {
        get => _virtualMemory;
        set => _virtualMemory = value;
    }

    /// <summary>
    ///     Time when the server process was started
    /// </summary>
    public DateTime StartTime
    {
        get => _startTime;
        set => _startTime = value;
    }

    /// <summary>
    ///     Number of threads
    /// </summary>
    public int Threads
    {
        get => _threads;
        set => _threads = value;
    }

    public int RunningThreads
    {
        get => _runningThreads;
        set => _runningThreads = value;
    }

    public int WaitingThreads
    {
        get => _waitingThreads;
        set => _waitingThreads = value;
    }

    public string SoftwareVersion
    {
        get => _softwareVersion;
        set => _softwareVersion = value;
    }

    public int TransactionLag
    {
        get => _transactionLag;
        set => _transactionLag = value;
    }

    public bool IsPersistent
    {
        get => _isPersistent;
        set => _isPersistent = value;
    }

    public int MemoryLimitInGigabytes
    {
        get => _memoryLimitInGigabytes;
        set => _memoryLimitInGigabytes = value;
    }

    public bool IsReadOnly
    {
        get => _isReadOnly;
        set => _isReadOnly = value;
    }

    public bool ConnectionError => _connectedClients == 0;

    public string ClusterName
    {
        get => _clusterName;
        set => _clusterName = value;
    }

    public long NonFragmentedMemory
    {
        get => _nonFragmentedMemory;
        set => _nonFragmentedMemory = value;
    }
}