Running "accounts"
==============================================
This demo client implements a toy banking system where amounts of many are transfered between accounts and money transferes are recorded.
It proves the advanced transactional capabilities of Cachalot Db including the "optimistic synchronization".
About 15% of the transactions are supposed to be rolled back and the consistency of the rollback is chacked.
in order to run this example application three servers need to be started on ports:
4848
4851
4852
The port is specified in node_config.json
