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
    workingSet: number = 0;
    nonFragmentedMemory: number = 0;
    port: number | undefined;
    transactionLag: number | undefined;
    connectedClients: number | undefined;
    isPersistent: boolean = false;
    isReadOnly: boolean = false;
    threads: number = 0;
    waitingThreads: number = 0;
    runningThreads: number = 0;
    memoryLimitInGigabytes: number = 0;

}

export class ServerHistory {
    
    totalMemory:number[] = [];
    nonFragmentedMemory:number[] = [];
    runningThreads:number[] = [];

    public add(totalMemory:number, nonFragmentedMemory:number, runningThreads:number):void{
        const max = 60;
        
        this.totalMemory.push(totalMemory)
        this.nonFragmentedMemory.push(nonFragmentedMemory);
        this.runningThreads.push(runningThreads);


        if(this.totalMemory.length > max){
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
    fullTextSearch: boolean = false;

}


export class ClusterInformation {
    status:string = 'Ok';
    connectionString:string|undefined;
    statusReason:string|undefined;
    serversStatus: ServerInfo[] = [];
    collectionsSummary:CollectionSummary[] = [];
    schema:Schema[] = [];
}