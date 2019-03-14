Running "accounts"
==============================================
This demo client implements a toy banking system where amounts of money are transferred between accounts and **money transfers** are recorded.

It proves the advanced transactional capabilities of Cachalot Db including the **optimistic synchronization**.
About 15% of the transactions are supposed to be rolled back and the consistency of the rollback is checked.

The application tests transactional performance in 3 cases:

* single server
* a cluster of two servers
* an in-process server

In order to run this example application, three servers need to be started on ports: **4848,4851,4852**

The port is specified in **node_config.json**

If you are using the dotnet-core server, command scripts are available **start4848.cmd ...** 
