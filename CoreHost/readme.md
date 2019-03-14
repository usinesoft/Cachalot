![logo](https://github.com/usinesoft/Cachalot/blob/master/Media/cachalot_64.png?raw=true)  Cachalot DB  
===========================================================================================================
Full documentation can be found [here>>](https://github.com/usinesoft/Cachalot/blob/master/Doc/CachalotUserGuide.pdf)

Running CoreHost
===========================================
CoreHost is a dotnet core 2.1 executable, hosting a cachalot server with logging facility
It can be run with the command line "dotnet CoreHost.dll"
By default it uses the configuration file **node_config.json**

Example of configuration file
```java script
{
  "IsPersistent": true,  
  "ClusterName": "test",   
  "TcpPort": 6666,    
  "DataPath": "root" 
}
```
* **IsPersistent** = true means it works as a detabase, otherwise it is only a cache
* **ClusterName**, for monitoring only
* **TcpPort** on the same machine each node should use a different one
* **DataPath** is used only in database mode: the directory containg persistent data and logs. Multiple instances on the same machine should use different values

If run with an "instance name" like **dotnet CoreHost.dll cache_only** it will use **node_config_cache_only.json**
Using this command allows *multiple instances to be run with the same binaries*.

It is tested at each release on Windows and Ubuntu 