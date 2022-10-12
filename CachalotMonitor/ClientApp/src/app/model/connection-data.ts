export class ConnectionData {

    clusterName:string|undefined;

    nodes:ClusterNode[] = [];
}

export class ClusterNode{
    
    host:string | undefined;
    
    port:number|undefined;
    
}
