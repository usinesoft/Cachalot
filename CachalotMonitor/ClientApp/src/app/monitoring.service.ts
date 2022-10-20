import { HttpClient } from '@angular/common/http';
import { Inject, Injectable } from '@angular/core';
import { BehaviorSubject, Observable } from 'rxjs';
import { ClusterInformation, ConnectionData, ConnectionResponse } from './model/connection-data';

@Injectable({
  providedIn: 'root'
})
export class MonitoringService {

  public connected: BehaviorSubject<boolean> = new BehaviorSubject(false);

  public connectionString: string | undefined;

  constructor(private http: HttpClient, @Inject('BASE_URL') private baseUrl: string) { }

  public connect(connection: ConnectionData) {
    this.http.post<ConnectionResponse>(this.baseUrl + 'Admin/connect', connection).subscribe(result => {

      if (result.connectionString) {
        this.connectionString = result.connectionString;
        this.connected.next(true);
      };
    });

  }

  public clusterInfo:ClusterInformation = new ClusterInformation;

  public updateClusterStatus():void{
    this.http.get<ClusterInformation>(this.baseUrl + 'Admin').subscribe(data=>  {
      this.clusterInfo = data;
    });
  }
}
