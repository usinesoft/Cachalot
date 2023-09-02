
import { Component, Inject, OnDestroy, OnInit } from '@angular/core';
import { interval, Subscription } from 'rxjs';
import { ClusterInformation, ClusterNode, CollectionSummary, ConnectionData, ConnectionResponse, ServerHistory, ServerInfo } from '../model/connection-data';
import { MatSnackBar } from '@angular/material/snack-bar';
import { MonitoringService } from '../monitoring.service';
import { ScreenStateService } from '../screen-state.service';
//import { ToastrService } from 'ngx-toastr';

@Component({
  selector: 'app-home',
  templateUrl: './home.component.html',
  styleUrls: ['./home.component.scss']
})
export class HomeComponent implements OnInit, OnDestroy {

  public connection: ConnectionData = new ConnectionData;

  public canConnect: boolean = false;

  private timerSubscription: Subscription | undefined;
  private dataSubscription: Subscription | undefined;

  public get clusterHistory(){
    return this.service.clusterHistory;
  };

  public clusterStatus:ServerInfo[] = [];

  constructor(private service: MonitoringService, private snackBar: MatSnackBar, private stateService: ScreenStateService) {
    var defaultNode = new ClusterNode();
    defaultNode.host = 'localhost';
    defaultNode.port = 48401;
    this.connection.nodes.push(defaultNode);
  }


  public get connected(): boolean {

    if (!this.service.clusterInformation.getValue()) {
      return false;
    }

    return this.service.clusterInformation.getValue()?.status != 'NotConnected';
  }

  public get working(): boolean {
    return this.service.working;
  }

  public get disconnecting(): boolean {
    return this.service.disconnecting;
  }


  public get connectionString(): string | undefined {
    return this.service.connectionString;
  }

  public get clusterInformation(): ClusterInformation | null {
    return this.service.clusterInformation.getValue();
  }

  public get history(): string[] {
    return this.service.history;
  }

  public identifyServer(index: Number, server: ServerInfo) {
    if(!server){
      return '';
    }
    return `${server.host}:${server.port}`;
  }


  ngOnInit() {

    this.timerSubscription = interval(400)
      .subscribe(x => {
        this.onFastTimer();

      });


    this.service.getConnectionHistory();

    this.dataSubscription = this.service.clusterInformation.subscribe(data=>{
      
      if(!data)
        return;

      if(data?.status == 'NotConnected'){
        
        this.clusterStatus = [];
        return;
      }
        
      this.service.connectionString = data?.connectionString;

      if(this.clusterStatus.length == 0){ // init once
        data?.serversStatus.forEach(element => {                    
          this.clusterStatus.push(element);
        });

      }
      else{ // update
        
        for(let i = 0; i < this.clusterHistory.length; i++){
          let status = data?.serversStatus[i];
          this.clusterStatus[i] = status!;                    
        }
      }

      console.log('resize event');
      console.log(this.clusterHistory[0].nonFragmentedMemory);
      window.dispatchEvent(new Event('resize'));
      
    });


  }


  //every 400 ms
  private onFastTimer(): void {
    this.checkCanConnect();
  }


  ngOnDestroy(): void {
    this.timerSubscription?.unsubscribe();
    this.dataSubscription?.unsubscribe();
  }


  public connect() {
    this.clusterStatus = []
    this.service.connect(this.connection);
    this.stateService.clearScreenState();

  }

  public connectWithHistory(entry: string) {
    this.clusterStatus = []
    this.service.connectWithHistory(entry);
  }

  public disconnect() {

    this.service.disconnect();

  }

  public addServer() {
    var node = new ClusterNode;
    node.port = 48401;
    this.connection.nodes.push(node);

    this.checkCanConnect();
  }

  public removeServer(toRemove: ClusterNode) {
    var index = this.connection.nodes.indexOf(toRemove);
    this.connection.nodes.splice(index, 1);
    this.checkCanConnect();
  }


  public checkCanConnect() {

    let result = true;
    this.connection.nodes.forEach(element => {
      if (!element.host || !element.port)
        result = false;
    });

    this.canConnect = result;
  }

}
