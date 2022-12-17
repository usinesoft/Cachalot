import { HttpClient } from '@angular/common/http';
import { Inject, Injectable } from '@angular/core';
import { BehaviorSubject, Observable } from 'rxjs';
import { ClusterInformation, ConnectionData, ConnectionResponse } from './model/connection-data';
import { SchemaUpdateRequest } from './model/schema';

@Injectable({
  providedIn: 'root'
})
export class MonitoringService {


  ////////////////////////////////////
  // data


  public connectionString: string | undefined;

  public history: string[] = [];

  public clusterInfo: ClusterInformation | undefined;

  public working: boolean = false;
  public disconnecting: boolean = false;


  // initialization
  constructor(private http: HttpClient, @Inject('BASE_URL') private baseUrl: string) { }


  ///////////////////////////////////////////
  //public methods
  public connect(connection: ConnectionData) {

    this.working = true;
    this.http.post<ConnectionResponse>(this.baseUrl + 'Admin/connect', connection).subscribe(result => {

      if (result.connectionString) {
        this.connectionString = result.connectionString;

        this.updateClusterStatus('connect');
      };
    }, err => this.working = false);

  }

  public disconnect() {

    this.disconnecting = true;

    delete this.clusterInfo;
    this.currentCluster.next(this.getCLusterName());

    this.http.post<any>(this.baseUrl + 'Admin/disconnect', null).subscribe(result => {
      console.log('disconnected');

      this.updateClusterStatus('disconnect');
      
    });

  }

  public connectWithHistory(connectionNameFromHistory: string) {
    this.http.post<ConnectionResponse>(this.baseUrl + 'Admin/connect/' + connectionNameFromHistory, null).subscribe(result => {
      if (result.connectionString) {
        this.connectionString = result.connectionString;

        this.updateClusterStatus('connect with history');
      };
    });

  }


  public currentCluster:BehaviorSubject<string|null>  = new BehaviorSubject<string|null>(null);

  public updateClusterStatus(caller:string): void {
    

    console.log(`update cluster status called by ${caller}`);

    console.log(`CALL disconnecting = ${this.disconnecting} clusterInfo = ${this.clusterInfo}`);

    this.http.get<ClusterInformation>(this.baseUrl + 'Admin').subscribe((data:ClusterInformation) => {
      
      console.log(`RESULT disconnecting = ${this.disconnecting} clusterInfo = ${this.clusterInfo}`);

      if(!this.disconnecting){
        this.clusterInfo = data;
        this.currentCluster.next(this.getCLusterName());
      }
      
      this.working = false;
      this.disconnecting = false;

    }, err => {
      console.log(`ERROR disconnecting = ${this.disconnecting} clusterInfo = ${this.clusterInfo}`);
      delete this.clusterInfo;
      this.currentCluster.next(this.getCLusterName());
      this.working = false;
      this.disconnecting = false;

    });
  }

  public updateOnce():Observable<ClusterInformation>{
    return this.http.get<ClusterInformation>(this.baseUrl + 'Admin');
  }

  public getConnectionHistory(): void {
    this.http.get<string[]>(this.baseUrl + 'Admin/history').subscribe(data => {
      this.history = data;
    });
  }

  public updateSchema(request:SchemaUpdateRequest): Observable<any> {
    return this.http.post<any>(this.baseUrl + 'Admin/update/schema', request);
  }

  private getCLusterName():string|null{
    if(this.clusterInfo){
      return this.clusterInfo.serversStatus[0]?.clusterName ?? null;
    }

    return null;
  }



}
