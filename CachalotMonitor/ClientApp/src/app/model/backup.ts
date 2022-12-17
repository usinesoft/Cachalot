export class BackupConfig{
    public backupDirectory:string|undefined;
}

export class Process{
    
    processId: string|undefined;
    processName: string|undefined;
    clusterName: string|undefined;
    status: string|undefined;
    startTime: string|undefined;
    endTime: string|undefined;
    errorMessage: string|undefined;
    durationInSeconds: number = 0;
      
}