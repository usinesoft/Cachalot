Running "accounts"
==============================================
This demo client implements a toy banking system where amounts of money are transferred between accounts and **money transfers** are recorded.

It proves the advanced transactional capabilities of Cachalot Db including the **optimistic synchronization**.
About 15% of the transactions are supposed to be rolled back and the consistency of the rollback is checked.

The application tests transactional performance in 3 cases:

* single server
* a cluster of two servers
* an in-process server

In order to run this example application, two servers need to be started on ports: **48401,48402**
Command files are provided to start these servers: **start01.cmd** and **start02.cmd**

The port is specified in **node_config.json**

