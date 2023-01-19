export interface QueryExecutionPlan {
    query: string;
    simpleQueryStrategy: boolean;
    fullScan: boolean;
    planningTimeInMicroseconds: number;
    indexTimeInMicroseconds: number;
    scanTimeInMicroseconds: number;
    usedIndexes: string[];
}


export interface ExecutionPlan {
    totalTimeInMicroseconds: number;
    queryPlans: QueryExecutionPlan[];
    distinctTimeInMicroseconds: number;
    mergeTimeInMicroseconds: number;
    orderTimeInMicroseconds: number;
}