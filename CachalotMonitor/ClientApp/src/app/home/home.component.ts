
import { Component, Inject, OnDestroy, OnInit } from '@angular/core';
import { interval, Subscription } from 'rxjs';
import { ClusterInformation, ClusterNode, CollectionSummary, ConnectionData, ConnectionResponse } from '../model/connection-data';
import { MatSnackBar } from '@angular/material/snack-bar';
import { MonitoringService } from '../monitoring.service';
import { ScreenStateService } from '../screen-state.service';
//import { ToastrService } from 'ngx-toastr';

@Component({
  selector: 'app-home',
  templateUrl: './home.component.html',
  styleUrls: ['./home.component.css']
})
export class HomeComponent implements OnInit, OnDestroy {

  public connection: ConnectionData = new ConnectionData;

  public canConnect: boolean = false;

  private timerSubscription: Subscription | undefined;


  constructor(private service:MonitoringService, private snackBar: MatSnackBar, private stateService:ScreenStateService) {
    var defaultNode = new ClusterNode();
    defaultNode.host = 'localhost';
    defaultNode.port = 48401;
    this.connection.nodes.push(defaultNode);
  }


  public get connected():boolean {

    if(!this.service.clusterInfo){
      return false;
    }

    return  this.service.clusterInfo?.status != 'NotConnected';
  }

  public get working():boolean {
    return this.service.working;
  }

  public get disconnecting():boolean {
    return this.service.disconnecting;
  }

  


  public get connectionString():string|undefined{
    return this.service.connectionString;
  }

  public get clusterInformation():ClusterInformation|undefined{
    return this.service.clusterInfo;
  }

  public get history():string[]{
    return this.service.history;
  }

  public identifyCollection(index:Number, collection:CollectionSummary) {
    return collection.name;
 }

  ngOnInit() {


    // init the timer
    let tick:number = 0;
    this.timerSubscription = interval(400)
      .subscribe(x => {  
        this.onFastTimer();

        if(tick%5 == 0){
          this.onSlowTimer();
        }

        tick++;
      });

    
    this.service.getConnectionHistory();

    
  }


  //every 400 ms
  private onFastTimer():void{
    this.checkCanConnect();
  }

  // every 2 seconds
  private onSlowTimer():void{
    if(this.connected && !this.disconnecting){
      this.service.updateClusterStatus('slow timer');
    }
  }



  ngOnDestroy(): void {
    this.timerSubscription?.unsubscribe();
  }


  public connect() {
    this.service.connect(this.connection);
    this.stateService.clearScreenState();
    
  }

  public connectWithHistory(entry:string) {
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
