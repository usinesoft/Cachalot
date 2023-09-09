import { Schema } from "./schema";

export class ConnectionData {

  clusterName: string | undefined;

  nodes: ClusterNode[] = [];
}

export class ClusterNode {

  host: string | undefined;

  port: number | undefined;

}


export class ConnectionResponse {

  connectionString: string | undefined;

  errorMessage: string | undefined;

}

export class ServerInfo {
  connectionError: Boolean = false;
  startTime: string | undefined;
  softwareVersion: string | undefined;
  host: string | undefined;
  clusterName: string | undefined;
  workingSet = 0;
  nonFragmentedMemory = 0;
  port: number | undefined;
  transactionLag: number | undefined;
  connectedClients: number | undefined;
  isPersistent = false;
  isReadOnly = false;
  threads = 0;
  waitingThreads = 0;
  runningThreads = 0;
  memoryLimitInGigabytes = 0;

}

export class ServerHistory {

  totalMemory: number[] = [];
  nonFragmentedMemory: number[] = [];
  runningThreads: number[] = [];

  add(totalMemory: number, nonFragmentedMemory: number, runningThreads: number): void {
    const max = 60;

    this.totalMemory.push(totalMemory);
    this.nonFragmentedMemory.push(nonFragmentedMemory);
    this.runningThreads.push(runningThreads);


    if (this.totalMemory.length > max) {
      this.totalMemory.shift();
      this.nonFragmentedMemory.shift();
      this.runningThreads.shift();
    }
  }
}

export class CollectionSummary {

  name: string | undefined;
  itemsCount: Number = 0;
  storageLayout: string | undefined;
  evictionType: string | undefined;
  fullTextSearch = false;

}


export class ClusterInformation {
  status = "Ok";
  connectionString: string | undefined;
  statusReason: string | undefined;
  serversStatus: ServerInfo[] = [];
  collectionsSummary: CollectionSummary[] = [];
  schema: Schema[] = [];
}
