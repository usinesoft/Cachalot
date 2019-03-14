![logo](https://github.com/usinesoft/Cachalot/blob/master/Media/cachalot_64.png?raw=true)  Cachalot DB  
===========================================================================================================

Full documentation can be found [here>>](https://github.com/usinesoft/Cachalot/blob/master/Doc/CachalotUserGuide.pdf)

Cachalot Administration Console
===========================================


**AdminConsole.exe** is an advanced administration console whith powerfull autocompletion features.

Type **help** for all available commands, **help command** for detailed explanation. And do not hesitate to use TAB for autocompletion

## Available commands

|  Command| Description                                                      |
----------|------------------------------------------------------------------
COUNT     | count the objects matching a specified query|
SELECT    | get the objects matching a specified query as JSON
DESC      | display information about the server process and data tables
CONNECT   | connect to a server or a cluster of servers
EXIT      | guess what?
READONLY  | switch on the readonly mode
READWRITE | switch off the readonly mode
STOP      | stop all the nodes in the cluster
DROP      | delete ALL DATA
DELETE    | remove all the objects matching a query
TRUNCATE  | remove all the objects of a given type
DUMP      | save all the database in a directory
RESTORE   | restore data saved with the DUMP command (same number of nodes)
RECREATE  | restore data saved with the DUMP command (number of nodes changed)
IMPORT    | import data from an external json file
LAST      | display information on the last actions executed by the server
