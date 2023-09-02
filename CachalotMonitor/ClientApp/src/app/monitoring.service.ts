import { HttpClient } from '@angular/common/http';
import { Inject, Injectable } from '@angular/core';
import { MatSnackBar } from '@angular/material/snack-bar';
import { BehaviorSubject, interval, Observable, Subscription } from 'rxjs';
import { ClusterInformation, ConnectionData, ConnectionResponse, ServerHistory } from './model/connection-data';
import { SchemaUpdateRequest } from './model/schema';

@Injectable({
  providedIn: 'root'
})
export class
  MonitoringService {


  ////////////////////////////////////
  // data


  public connectionString: string | undefined;

  public history: string[] = [];

  private timerSubscription: Subscription | undefined;



  public working: boolean = false;
  public disconnecting: boolean = false;
  public disconnected: boolean = true;


  // initialization
  constructor(private http: HttpClient, private snackBar: MatSnackBar, @Inject('BASE_URL') private baseUrl: string) {

    this.timerSubscription = interval(2000).subscribe(_x => {
      this.updateClusterStatus();
    });
  }


  displayError(message: string, detail?: string): void {
    let fullMessage = message;
    if (detail) {
      fullMessage += ':' + detail
    }
    this.snackBar.open(fullMessage, "", { duration: 3000, panelClass: 'red-snackbar' });
  }

  displaySuccess(message: string, detail?: string): void {
    let fullMessage = message;
    if (detail) {
      fullMessage += ':' + detail
    }
    this.snackBar.open(fullMessage, "", { duration: 2000, panelClass: 'green-snackbar' });
  }




  ///////////////////////////////////////////
  //public methods
  public connect(connection: ConnectionData) {

    this.working = true;
    this.http.post<ConnectionResponse>(this.baseUrl + 'Admin/connect', connection).subscribe(result => {

      this.working = false;
      if (result.connectionString) {
        this.connectionString = result.connectionString;

        this.disconnected = false;
        this.disconnecting = false;

        this.updateClusterStatus();
        this.displaySuccess('connected');
      }
      else {
        this.displayError(result.errorMessage ?? 'connection error');
      }
    }, err => {
      this.working = false;
      this.displayError(err ?? 'connection error');
    });

  }

  public get isConnected(): boolean {
    return this.clusterInformation.getValue()?.status != 'NotConnected';
  }

  public disconnect() {

    this.disconnecting = true;

    this.clusterInformation.next(null);

    this.currentCluster.next(this.getCLusterName());

    this.http.post<any>(this.baseUrl + 'Admin/disconnect', null).subscribe(_result => {
      console.log('disconnected');

      //this.updateClusterStatus();
      this.disconnected = true;
      this.disconnecting = false;
      this.clusterHistory = [];
      
    });

  }

  public connectWithHistory(connectionNameFromHistory: string) {
    this.working = true;
    this.http.post<ConnectionResponse>(this.baseUrl + 'Admin/connect/' + connectionNameFromHistory, null).subscribe(result => {
      if (result.connectionString) {
        this.connectionString = result.connectionString;
        this.disconnected = false;
        this.disconnecting = false;
        this.working = false;
        this.displaySuccess('connected');
        this.updateClusterStatus();
        

      } else {
        this.working = false;
        this.displayError(result.errorMessage ?? 'connection error');
      }
    }, _err => this.displayError('connection error'));

  }



  public currentCluster: BehaviorSubject<string | null> = new BehaviorSubject<string | null>(null);

  public clusterInformation: BehaviorSubject<ClusterInformation | null> = new BehaviorSubject<ClusterInformation | null>(null);

  public clusterHistory: ServerHistory[] = [];

  public updateClusterStatus(): void {


    if(this.disconnecting || this.disconnected)
      return;

    this.http.get<ClusterInformation>(this.baseUrl + 'Admin').subscribe((data: ClusterInformation) => {


      // accumulate data as history
      if (this.clusterHistory.length == 0) { // init once
        data?.serversStatus.forEach(element => {
          this.clusterHistory.push(new ServerHistory);
        });

      }
      else { // update

        for (let i = 0; i < this.clusterHistory.length; i++) {
          let status = data?.serversStatus[i];
          this.clusterHistory[i].add(status?.workingSet! / (1024 * 1024 * 1024), status?.nonFragmentedMemory! / (1024 * 1024 * 1024)!, status?.runningThreads!);
          console.log('history length ' + this.clusterHistory[i].totalMemory.length);
        }
      }

      if (!this.disconnecting) {
        this.clusterInformation.next(data);
        this.currentCluster.next(this.getCLusterName());        
      }

      this.working = false;
      this.disconnecting = false;
      this.disconnected = false;


    }, _err => {

      this.clusterInformation.next(null);
      this.currentCluster.next(this.getCLusterName());
      this.working = false;
      this.disconnecting = false;
      this.clusterInformation.next(null);

    });
  }

  public updateOnce(): Observable<ClusterInformation> {
    return this.http.get<ClusterInformation>(this.baseUrl + 'Admin');
  }

  public getConnectionHistory(): void {
    this.http.get<string[]>(this.baseUrl + 'Admin/history').subscribe(data => {
      this.history = data;
    });
  }

  public updateSchema(request: SchemaUpdateRequest): Observable<any> {
    return this.http.post<any>(this.baseUrl + 'Admin/update/schema', request);
  }

  private getCLusterName(): string | null {
    var info = this.clusterInformation.getValue();
    if (info && info.serversStatus && info.serversStatus.length > 0) {
      return info.serversStatus[0]?.clusterName ?? null;
    }

    return null;
  }



}
