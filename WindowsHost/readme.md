![logo](https://github.com/usinesoft/Cachalot/blob/master/Media/cachalot_64.png?raw=true) 
# Cachalot DB  
Full documentation can be found [here>>](https://github.com/usinesoft/Cachalot/blob/master/Doc/CachalotUserGuide.pdf)

Running Cachalot server
===========================================
**Cachalot.exe** is a classic dotnet (4.6.1) executable, hosting a cachalot server with logging facility
It can be run as a console application or a windows service
Type **cachalot --help** to display all available options

It uses the configuration file **node_config.json**
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